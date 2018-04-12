using UnityEngine;
using System.Collections.Generic;
using System.IO;

//handles network requests at runtime
//also handles local storage of data. saving + uploading
//stack of line lengths, read/write through single filestream

namespace CognitiveVR
{
    public class NetworkManager : MonoBehaviour
    {
        static NetworkManager _sender;
        static NetworkManager Sender
        {
            get
            {
                if (_sender == null)
                {
                    var go = new GameObject("Cognitive Network");
                    Object.DontDestroyOnLoad(go);
                    _sender = go.AddComponent<NetworkManager>();
                }
                return _sender;
            }
        }

        static string localDataPath = Application.persistentDataPath + "/c3dlocal/data";
        static string localExitPollPath = Application.persistentDataPath + "/c3dlocal/exitpoll/";

        public delegate void FullResponse(string url, string content, int responsecode, string error, string text, bool uploadLocalData);

        public delegate void Response(int responsecode, string error, string text);

        static StreamReader sr;
        static StreamWriter sw;
        static FileStream fs;
        //line sizes of contents, ignoring line breaks. line breaks added automatically from StreamWriter.WriteLine
        static Stack<int> linesizes = new Stack<int>();
        static int totalBytes = 0;

        //TODO close sr, sw, fs on end session
        private void OnDestroy()
        {
            if (sr != null) sr.Close();
            if (sw != null) { sw.Flush(); sw.Close(); }
            if (fs != null) fs.Close();
        }

        System.Collections.IEnumerator WaitForResponse(WWW www, Response callback)
        {
            yield return www;
            if (callback != null)
            {
                callback.Invoke(Util.GetResponseCode(www.responseHeaders), www.error, www.text);
            }
        }

        System.Collections.IEnumerator WaitForExitpollResponse(WWW www, string hookname, Response callback, float timeout)
        {
            float time = 0;
            while (time < timeout)
            {
                yield return null;
                if (www.isDone) break;
                time += Time.deltaTime;
            }

            int responsecode = Util.GetResponseCode(www.responseHeaders);

            if (!www.isDone || responsecode != 200)
            {
                //try to read from file
                if (CognitiveVR_Preferences.Instance.LocalStorage && File.Exists(localExitPollPath+hookname))
                {
                    var text = File.ReadAllText(localExitPollPath + hookname);
                    if (callback != null)
                    {
                        callback.Invoke(responsecode, "", text);
                    }
                }
                else
                {
                    //do callback, even if no files saved
                    if (callback != null)
                    {
                        callback.Invoke(responsecode, "", "");
                    }
                }
            }
            else
            {
                if (!CognitiveVR_Preferences.Instance.LocalStorage) { yield break; }
                //write response to file
                File.WriteAllText(localExitPollPath + hookname, www.text);
                if (callback != null)
                {
                    callback.Invoke(responsecode, www.error, www.text);
                }
            }
        }

        System.Collections.IEnumerator WaitForFullResponse(WWW www, string contents, FullResponse callback, bool allowLocalUpload)
        {
            yield return www;
            if (callback != null)
            {
                callback.Invoke(www.url, contents, Util.GetResponseCode(www.responseHeaders), www.error, www.text, allowLocalUpload);
            }
        }

        void GenericPostFullResponse(string url, string content, int responsecode, string error, string text, bool allowLocalUpload)
        {
            if (responsecode == 200)
            {
                if (!allowLocalUpload) { return; }
                if (!CognitiveVR_Preferences.Instance.LocalStorage) { return; }
                //search through files and upload outstanding data + remove that file
                UploadLocalFile();
            }
            else
            {
                if (responsecode == 401) { return; } //ignore if invalid auth api key
                //write to file
                WriteRequestToFile(url, content);
            }
        }

        static int EOLByteCount = 2;
        static string EnvironmentEOL;
        static int ReadLocalCacheCount;

        //called on init to find all files not uploaded
        public static void InitLocalStorage(string environmentEOL)
        {
            ReadLocalCacheCount = CognitiveVR_Preferences.Instance.ReadLocalCacheCount;
            EnvironmentEOL = environmentEOL;
            EOLByteCount = System.Text.Encoding.UTF8.GetByteCount(environmentEOL);
            //EOLByteCount = eolByteCount;
            if (!CognitiveVR_Preferences.Instance.LocalStorage) { return; }
            if (!Directory.Exists(localExitPollPath))
                Directory.CreateDirectory(localExitPollPath);

            fs = File.Open(localDataPath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            sr = new StreamReader(fs);
            sw = new StreamWriter(fs);
            //read all line sizes from data
            while (sr.Peek() != -1)
            {
                int lineLength = System.Text.Encoding.UTF8.GetByteCount(sr.ReadLine());
                linesizes.Push(lineLength);
                totalBytes += lineLength;
            }
        }
        
        void WriteRequestToFile(string url, string contents)
        {
            if (!CognitiveVR_Preferences.Instance.LocalStorage) { return; }

            //Debug.Log("<<<<<<<<<write request to file");

            contents = contents.Replace('\n', ' ');

            //byte[] b64bytes = System.Text.Encoding.UTF8.GetBytes(contents);
            //string b64 = System.Convert.ToBase64String(b64bytes);

            int urlByteCount = System.Text.Encoding.UTF8.GetByteCount(url);
            int contentByteCount = System.Text.Encoding.UTF8.GetByteCount(contents);

            if (urlByteCount + contentByteCount + totalBytes > CognitiveVR.CognitiveVR_Preferences.Instance.LocalDataCacheSize)
            {
                //cache size reached! skip writing data
                //Debug.Log("!!!!!!!!!!!!!cache reached");
                return;
            }

            sw.Write(url);
            sw.Write(EnvironmentEOL);
            linesizes.Push(urlByteCount);
            totalBytes += urlByteCount;

            sw.Write(contents);
            sw.Write(EnvironmentEOL);
            linesizes.Push(contentByteCount);
            totalBytes += contentByteCount;

            sw.Flush();
        }

        public static void UploadAllLocalStorage()
        {
            //upload from local storage
        }

        //uploads a single local file from the queue. only called when a 200 is returned from a post request
        void UploadLocalFile()
        {
            if (linesizes.Count < 2) { return; }
            for (int i = 0; i < ReadLocalCacheCount; i++)
            {
                if (linesizes.Count < 2) { return; }
                int contentsize = linesizes.Pop();
                int urlsize = linesizes.Pop();

                int lastrequestsize = contentsize + urlsize + EOLByteCount + EOLByteCount;

                fs.Seek(-lastrequestsize, SeekOrigin.End);

                long originallength = fs.Length;

                string tempurl = null;
                string tempcontent = null;
                char[] buffer = new char[urlsize];
                while (sr.Peek() != -1)
                {
                    //TODO check performance on read vs readblock. read might be faster?                    
                    sr.ReadBlock(buffer, 0, urlsize);
                    
                    tempurl = new string(buffer);
                    //line return
                    for(int eolc = 0; eolc < EOLByteCount; eolc++)
                        sr.Read();
                    

                    buffer = new char[contentsize];
                    sr.ReadBlock(buffer, 0, contentsize);
                    tempcontent = new string(buffer);

                    //tempcontent = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64CharArray(buffer,0,buffer.Length));
                    //line return
                    for (int eolc2 = 0; eolc2 < EOLByteCount; eolc2++)
                        sr.Read();
                }
                //Debug.Log(">>>>>>>>>read request");
                //Debug.Log(tempurl);
                //Debug.Log(tempcontent);

                fs.SetLength(originallength - lastrequestsize);
                LocalCachePost(tempurl, tempcontent);
            }
        }

        static Dictionary<string, string> getHeaders;

        public static void GetExitPollQuestions(string url, string hookname, Response callback, float timeout = 3)
        {

            if (getHeaders == null)//AUTH
            {
                getHeaders = new Dictionary<string, string>() { { "Content-Type", "application/json" }, { "X-HTTP-Method-Override", "GET" }, { "Authorization", "APIKEY:DATA " + CognitiveVR_Preferences.Instance.APIKey } };
            }
            WWW www = new WWW(url, null, getHeaders);
            Sender.StartCoroutine(Sender.WaitForExitpollResponse(www, hookname, callback,timeout));
        }

        //currently unused. TODO exitpoll should use this
        /*public static void Get(string url, Response callback)
        {
            if (getHeaders == null)//AUTH
            {
                 getHeaders = new Dictionary<string, string>() { { "Content-Type", "application/json" }, { "X-HTTP-Method-Override", "GET" }, {"Authorization","APIKEY:DATA "+CognitiveVR_Preferences.Instance.APIKey } };
            }
            WWW www = new WWW(url,null, getHeaders);
            Sender.StartCoroutine(Sender.WaitForResponse(www, callback));
        }*/

        static Dictionary<string, string> postHeaders;

        public static void Post(string url, string stringcontent)
        {
            if (postHeaders == null)//AUTH
            {
                postHeaders = new Dictionary<string, string>() { { "Content-Type", "application/json" }, { "X-HTTP-Method-Override", "POST" }, { "Authorization", "APIKEY:DATA " + CognitiveVR_Preferences.Instance.APIKey } };
            }
            var bytes = System.Text.UTF8Encoding.UTF8.GetBytes(stringcontent);
            WWW www = new WWW(url, bytes,postHeaders);
            Sender.StartCoroutine(Sender.WaitForFullResponse(www, stringcontent, Sender.GenericPostFullResponse,true));
        }

        //used internally so uploading a file from cache doesn't trigger more files
        public static void LocalCachePost(string url, string stringcontent)
        {
            if (postHeaders == null)//AUTH
            {
                postHeaders = new Dictionary<string, string>() { { "Content-Type", "application/json" }, { "X-HTTP-Method-Override", "POST" }, { "Authorization", "APIKEY:DATA " + CognitiveVR_Preferences.Instance.APIKey } };
            }
            var bytes = System.Text.UTF8Encoding.UTF8.GetBytes(stringcontent);
            WWW www = new WWW(url, bytes, postHeaders);
            Sender.StartCoroutine(Sender.WaitForFullResponse(www, stringcontent, Sender.GenericPostFullResponse,false));
        }
    }
}
