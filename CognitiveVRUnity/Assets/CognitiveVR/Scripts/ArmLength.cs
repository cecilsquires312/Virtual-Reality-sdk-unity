﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// samples distances the the HMD to the player's arm. max is assumed to be roughly player arm length
/// this only starts tracking when the player has pressed the Steam Controller Trigger
/// </summary>

namespace CognitiveVR.Components
{
    public class ArmLength : CognitiveVRAnalyticsComponent
    {
        /*[DisplaySetting]
        public float someFloat = 3.14f;
        
        public int someInt = 3;

        [DisplaySetting]
        public string someString = "pi";

        [DisplaySetting]
        [Tooltip("some non-static thing")]
        public int nonStaticInt = 13;

        [DisplaySetting]
        public bool someOtherBool = true;*/

        [DisplaySetting]
        [Tooltip("Number of samples taken. The max is assumed to be maximum arm length")]
        public int SampleCount = 50;

#if CVR_STEAMVR || CVR_OCULUS
        float maxSqrDistance;
        int samples = 0;
#endif
#if CVR_STEAMVR
        public override void CognitiveVR_Init(Error initError)
        {
            base.CognitiveVR_Init(initError);

            CognitiveVR_Manager.OnUpdate += CognitiveVR_Manager_OnUpdate;
        }

        public Valve.VR.VRControllerState_t controllerState;
        private void CognitiveVR_Manager_OnUpdate()
        {
            var system = Valve.VR.OpenVR.System;
            if (system != null && system.GetControllerState(0, ref controllerState))
            {
                ulong trigger = controllerState.ulButtonPressed & (1UL << ((int)Valve.VR.EVRButtonId.k_EButton_SteamVR_Trigger));
                if (trigger > 0L)
                {
                    CognitiveVR_Manager.OnTick += CognitiveVR_Manager_OnTick;
                    CognitiveVR_Manager.OnUpdate -= CognitiveVR_Manager_OnUpdate;
                }
            }
        }

        private void CognitiveVR_Manager_OnTick()
        {
            if (CognitiveVR_Manager.GetController(0) == null){return;}

            if (samples < SampleCount)
            {
                maxSqrDistance = Mathf.Max(Vector3.SqrMagnitude(CognitiveVR_Manager.GetController(0).position - CognitiveVR_Manager.HMD.position));

                samples++;
                if (samples >= SampleCount)
                {
                    Util.logDebug("arm length " + maxSqrDistance);
                    Instrumentation.updateUserState(new Dictionary<string, object> { { "armlength", Mathf.Sqrt(maxSqrDistance) } });
                    CognitiveVR_Manager.OnTick -= CognitiveVR_Manager_OnTick;
                }
            }
        }
#endif

#if CVR_OCULUS
        public override void CognitiveVR_Init(Error initError)
        {
            base.CognitiveVR_Init(initError);
            CognitiveVR_Manager.OnUpdate += CognitiveVR_Manager_OnUpdate;
        }

        private void CognitiveVR_Manager_OnUpdate()
        {
            if (OVRInput.GetDown(OVRInput.Button.Any))
            {
                CognitiveVR_Manager.OnTick += CognitiveVR_Manager_OnTick;
                CognitiveVR_Manager.OnUpdate -= CognitiveVR_Manager_OnUpdate;
            }
        }

        private void CognitiveVR_Manager_OnTick()
        {
            if (samples < SampleCount)
            {
                maxSqrDistance = Mathf.Max(maxSqrDistance, OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch).sqrMagnitude, OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch).sqrMagnitude);
                //maxSqrDistance = Mathf.Max(Vector3.SqrMagnitude(CognitiveVR_Manager.GetController(0).position - CognitiveVR_Manager.HMD.position));

                samples++;
                if (samples >= SampleCount)
                {
                    Util.logDebug("arm length " + maxSqrDistance);
                    Instrumentation.updateUserState(new Dictionary<string, object> { { "armlength", Mathf.Sqrt(maxSqrDistance) } });
                    CognitiveVR_Manager.OnTick -= CognitiveVR_Manager_OnTick;
                }
            }
        }
#endif
        public static string GetDescription()
        {
            return "Samples distances from the HMD to the player's controller. Max is assumed to be roughly player arm length. This only starts tracking when the player has pressed the Steam Controller Trigger\nRequires SteamVR or Oculus Touch controllers";
        }

        void OnDestroy()
        {
#if CVR_STEAMVR || CVR_OCULUS
            CognitiveVR_Manager.OnUpdate -= CognitiveVR_Manager_OnUpdate;
            CognitiveVR_Manager.OnTick -= CognitiveVR_Manager_OnTick;
#endif
        }


    }
}