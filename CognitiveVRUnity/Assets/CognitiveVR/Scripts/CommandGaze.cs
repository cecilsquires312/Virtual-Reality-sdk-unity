﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CognitiveVR;
using UnityEngine.Rendering;

//use command buffer
namespace CognitiveVR
{
    [AddComponentMenu("Cognitive3D/Internal/Command Gaze")]
    public class CommandGaze : GazeBase
    {

        public RenderTexture rt;
        public BuiltinRenderTextureType blitTo = BuiltinRenderTextureType.CurrentActive;
        public CameraEvent camevent = CameraEvent.BeforeForwardOpaque;

        CommandBufferHelper helper;

        public override void Initialize()
        {
            base.Initialize();
            Core.InitEvent += CognitiveVR_Manager_InitEvent;
#if CVR_TOBIIVR || CVR_FOVE || CVR_NEURABLE || CVR_PUPIL || CVR_AH || CVR_VIVEPROEYE
            Debug.LogError("Cognitive3D does not support eye tracking using Command Gaze. From 'Advanced Options' in the cognitive3D menu, please change 'Gaze Type' to 'Physics'");
#endif
        }

        private void CognitiveVR_Manager_InitEvent(Error initError)
        {
            if (initError == Error.None)
            {
                var buf = new CommandBuffer();
                buf.name = "cognitive depth";

                if (GameplayReferences.HMD == null) { CognitiveVR.Util.logWarning("HMD is null! Command Gaze will not function"); return; }

                GameplayReferences.HMDCameraComponent.depthTextureMode = DepthTextureMode.Depth;
                GameplayReferences.HMDCameraComponent.AddCommandBuffer(camevent, buf);
                var material = new Material(Shader.Find("Hidden/Cognitive/CommandDepth"));

                //buf.SetGlobalFloat(Shader.PropertyToID("_DepthScale"), 1f / 1);
                //buf.Blit((Texture)null, BuiltinRenderTextureType.CameraTarget, material, (int)0);

#if SRP_LW3_0_0
            rt = new RenderTexture(Screen.width, Screen.height,0);
#else
                //rt = new RenderTexture(256, 256, 0);
                rt = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGBFloat);
#endif
                buf.Blit(BuiltinRenderTextureType.CurrentActive, rt, material, (int)0);

                //buf.Blit(blitTo, rt);

                Core.TickEvent += CognitiveVR_Manager_TickEvent;

                helper = GameplayReferences.HMD.gameObject.AddComponent<CommandBufferHelper>();
                helper.Initialize(rt, GameplayReferences.HMDCameraComponent, OnHelperPostRender, this);
            }
        }

        private void CognitiveVR_Manager_TickEvent()
        {
            if (GameplayReferences.HMD == null) { return; }

            if (helper == null) //if there's a scene change and camera is destroyed, replace helper
            {
                helper = GameplayReferences.HMD.gameObject.AddComponent<CommandBufferHelper>();
                helper.Initialize(rt, GameplayReferences.HMDCameraComponent, OnHelperPostRender, this);
            }

            Vector3 viewport = GetViewportGazePoint();
            viewport.z = 100;
            var viewportray = GameplayReferences.HMDCameraComponent.ViewportPointToRay(viewport);

            helper.Begin(GetViewportGazePoint(), viewportray);
        }

        void OnHelperPostRender(Ray ray, Vector3 gazeVector, Vector3 worldpos)
        {
            Vector3 gpsloc = new Vector3();
            float compass = 0;
            Vector3 floorPos = new Vector3();

            GetOptionalSnapshotData(ref gpsloc, ref compass, ref floorPos);

            float hitDistance;
            DynamicObject hitDynamic;
            Vector3 hitWorld;
            Vector3 hitLocal;
            Vector2 hitcoord;
            string ObjectId = "";

            if (DynamicRaycast(ray.origin, ray.direction, GameplayReferences.HMDCameraComponent.farClipPlane, 0.05f, out hitDistance, out hitDynamic, out hitWorld, out hitLocal, out hitcoord)) //hit dynamic
            {
                ObjectId = hitDynamic.Data.Id;
            }

            float depthDistance = Vector3.Distance(GameplayReferences.HMD.position, worldpos);

            if (hitDistance > 0 && hitDistance < depthDistance)
            {
                var mediacomponent = hitDynamic.GetComponent<MediaComponent>();
                if (mediacomponent != null)
                {
                    var mediatime = mediacomponent.IsVideo ? (int)((mediacomponent.VideoPlayer.frame / mediacomponent.VideoPlayer.frameRate) * 1000) : 0;
                    var mediauvs = hitcoord;
                    GazeCore.RecordGazePoint(Util.Timestamp(Time.frameCount), ObjectId, hitLocal, GameplayReferences.HMD.position, GameplayReferences.HMD.rotation, gpsloc, compass, mediacomponent.MediaSource, mediatime, mediauvs, floorPos);
                }
                else
                {
                    GazeCore.RecordGazePoint(Util.Timestamp(Time.frameCount), ObjectId, hitLocal, GameplayReferences.HMD.position, GameplayReferences.HMD.rotation, gpsloc, compass, floorPos);
                }
                Debug.DrawLine(GameplayReferences.HMD.position, hitDynamic.transform.position + hitLocal, Color.magenta, 1);
                Debug.DrawRay(worldpos, Vector3.right, Color.red, 1);
                Debug.DrawRay(worldpos, Vector3.forward, Color.blue, 1);
                Debug.DrawRay(worldpos, Vector3.up, Color.green, 1);
                return;
            }

            if (gazeVector.magnitude > GameplayReferences.HMDCameraComponent.farClipPlane * 0.99f) //compare to farplane. skybox
            {
                Vector3 pos = GameplayReferences.HMD.position;
                Quaternion rot = GameplayReferences.HMD.rotation;
                GazeCore.RecordGazePoint(Util.Timestamp(Time.frameCount), pos, rot, gpsloc, compass, floorPos);
                Debug.DrawRay(pos, GameplayReferences.HMD.forward * GameplayReferences.HMDCameraComponent.farClipPlane, Color.cyan, 1);
            }
            else
            {
                Vector3 pos = GameplayReferences.HMD.position;
                Quaternion rot = GameplayReferences.HMD.rotation;

                //hit world
                GazeCore.RecordGazePoint(Util.Timestamp(Time.frameCount), worldpos, pos, rot, gpsloc, compass, floorPos);
                //Debug.DrawLine(pos, pos + gazepoint, Color.red, 1);


                Debug.DrawLine(ray.origin, worldpos, Color.yellow, 1);
                Debug.DrawRay(worldpos, Vector3.right, Color.red, 1);
                Debug.DrawRay(worldpos, Vector3.forward, Color.blue, 1);
                Debug.DrawRay(worldpos, Vector3.up, Color.green, 1);

                //Debug.Log("hit world");
                LastGazePoint = worldpos;
            }
        }

        /*private void OnDrawGizmos()
        {
            UnityEditor.Handles.BeginGUI();
            GUI.Label(new Rect(0, 0, 128, 128), rt);
            UnityEditor.Handles.EndGUI();
        }*/

        private void OnDestroy()
        {
            Destroy(helper);
            Core.InitEvent -= CognitiveVR_Manager_InitEvent;
            Core.TickEvent -= CognitiveVR_Manager_TickEvent;
        }
    }
}