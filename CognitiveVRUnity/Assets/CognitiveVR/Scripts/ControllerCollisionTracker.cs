﻿using UnityEngine;
using System.Collections;

/// <summary>
/// sends transactions when either the controller collides with something in the game world
/// collision layers are set in CognitiveVR_Preferences
/// </summary>

namespace CognitiveVR
{
    public class ControllerCollisionTracker : CognitiveVRAnalyticsComponent
    {
        string controller0GUID;
        string controller1GUID;

        public override void CognitiveVR_Init(Error initError)
        {
            base.CognitiveVR_Init(initError);
            CognitiveVR_Manager.OnTick += CognitiveVR_Manager_OnTick;
        }

        private void CognitiveVR_Manager_OnTick()
        {
            bool hit;

#if CVR_STEAMVR
            if (CognitiveVR_Manager.GetController(false) != null)
#endif
            {
                Vector3 pos = CognitiveVR_Manager.GetControllerPosition(false);

                hit = Physics.CheckSphere(pos, 0.1f, CognitiveVR_Preferences.Instance.CollisionLayerMask);
                if (hit && string.IsNullOrEmpty(controller0GUID))
                {
                    Util.logDebug("controller collision");
                    controller0GUID = System.Guid.NewGuid().ToString();
                    Instrumentation.Transaction("cvr.collision", controller0GUID).setProperty("device", "left controller").begin();
                }
                else if (!hit && !string.IsNullOrEmpty(controller0GUID))
                {
                    Instrumentation.Transaction("cvr.collision", controller0GUID).setProperty("device", "left controller").end();
                    controller0GUID = string.Empty;
                }
            }


#if CVR_STEAMVR
            if (CognitiveVR_Manager.GetController(true) != null)
#endif
            {
                Vector3 pos = CognitiveVR_Manager.GetControllerPosition(true);

                hit = Physics.CheckSphere(pos, 0.1f, CognitiveVR_Preferences.Instance.CollisionLayerMask);
                if (hit && string.IsNullOrEmpty(controller1GUID))
                {
                    Util.logDebug("controller collision");
                    controller1GUID = System.Guid.NewGuid().ToString();
                    Instrumentation.Transaction("cvr.collision", controller1GUID).setProperty("device", "right controller").begin();
                }
                else if (!hit && !string.IsNullOrEmpty(controller1GUID))
                {
                    Instrumentation.Transaction("cvr.collision", controller1GUID).setProperty("device", "right controller").end();
                    controller1GUID = string.Empty;
                }
            }
        }

        public static string GetDescription()
        {
            return "Sends transactions when either controller collides in the game world\nCollision layers are set in CognitiveVR_Preferences\nRequires SteamVR controllers or Oculus Touch controllers";
        }
    }
}