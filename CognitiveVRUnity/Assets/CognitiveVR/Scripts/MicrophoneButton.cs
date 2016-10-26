﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using CognitiveVR;
using System.IO;

//used in cognitivevr exit poll to call actions on the main exit poll panel

namespace CognitiveVR
{
    public class MicrophoneButton : MonoBehaviour
    {
        public Image Button;
        public Image Fill;

        public float LookTime = 1.5f;

        float _currentLookTime;
        float _currentRecordTime;

        //this is used to increase the dot product threshold as distance increases - basically a very cheap raycast
        public float Radius = 0.25f;
        float _distanceToTarget;
        float _angle;
        float _theta;

        //call this after the recording sent response, or _maxUploadWaitTime seconds after sent request
        public UnityEngine.EventSystems.EventTrigger.TriggerEvent OnFinishedRecording;

        private int bufferSize;
        private int numBuffers;
        private int outputRate = 16000;// 44100;
        //private int headerSize = 44; //default for uncompressed wav

        private bool recOutput;
        AudioClip clip;

        private FileStream fileStream;

        bool _recording;
        bool _finishedRecording;
        float _maxUploadWaitTime = 2;

        public int RecordTime = 10;

        Transform _t;
        Transform _transform
        {
            get
            {
                if (_t == null)
                {
                    _t = transform;
                }
                return _t;
            }
        }

        void OnEnable()
        {
            _currentLookTime = 0;
            UpdateFillAmount();
            _distanceToTarget = Vector3.Distance(CognitiveVR_Manager.HMD.position, _transform.position);
            _angle = Mathf.Atan(Radius / _distanceToTarget);
            _theta = Mathf.Cos(_angle);
        }

        //if the player is looking at the button, updates the fill image and calls ActivateAction if filled
        void Update()
        {
            if (CognitiveVR_Manager.HMD == null) { return; }
            if (ExitPollPanel.NextResponseTime > Time.time) { return; }
            if (_finishedRecording) { return; }

            if (_recording)
            {
                _currentRecordTime -= Time.deltaTime;
                UpdateFillAmount();
                if (_currentRecordTime <= 0)
                {
                    Microphone.End(null);
                    StartCoroutine(UploadAudio());
                    _finishedRecording = true;
                }
            }
            else
            {
                if (Vector3.Dot(CognitiveVR_Manager.HMD.forward, (_transform.position - CognitiveVR_Manager.HMD.position).normalized) > _theta)
                {
                    _currentLookTime += Time.deltaTime;
                    UpdateFillAmount();

                    //maybe also scale button slightly if it has focus

                    if (_currentLookTime >= LookTime)
                    {
                        Debug.Log("recording");
                        // Call this to start recording. 'null' in the first argument selects the default microphone. Add some mic checking later
                        clip = Microphone.Start(null, false, RecordTime, outputRate);

                        GetComponentInParent<ExitPollPanel>().DisableTimeout();

                        _currentRecordTime = RecordTime;
                        _finishedRecording = false;
                        _recording = true;
                    }
                }
                else if (_currentLookTime > 0)
                {
                    _currentLookTime = 0;
                    UpdateFillAmount();
                }
            }
        }

        IEnumerator UploadAudio()
        {
            //customer id or something

            string url = "http://someurl/poll";
            url = "";

            byte[] bytes;
            string filepath = CognitiveVR.MicrophoneUtility.Save(clip, out bytes);
            //TODO upload to some server byte by byte

            var headers = new Dictionary<string, string>();
            //headers.Add("Content-Type", "application/json");
            headers.Add("X-HTTP-Method-Override", "POST");

            WWW www = new UnityEngine.WWW(url, bytes, headers);

            float startTime = 0;
            while (startTime < _maxUploadWaitTime)
            {
                startTime += Time.deltaTime;
                if (www.isDone) { break; }
                yield return null;
            }

            Debug.Log("upload complete! delete clip");

            //clip = null;
            //File.Delete(filepath);
            ActivateAction();
        }

        void UpdateFillAmount()
        {
            if (_recording)
            {
                Fill.fillAmount = _currentRecordTime / RecordTime;
            }
            else
            {
                Fill.fillAmount = _currentLookTime / LookTime;
            }
        }

        public void ActivateAction()
        {
            OnFinishedRecording.Invoke(null);
        }

        public void ClearAction()
        {
            //_action = null;
            _currentLookTime = 0;
            UpdateFillAmount();
        }

        void OnDrawGizmos()
        {
            Gizmos.DrawWireSphere(transform.position, Radius);
        }
    }
}