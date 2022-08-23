﻿using UnityEngine;
using System.Collections;

/// <summary>
/// sends recenter hmd transaction
/// </summary>

namespace Cognitive3D.Components
{
    [AddComponentMenu("Cognitive3D/Components/Recenter Event")]
    public class RecenterEvent : Cognitive3DAnalyticsComponent
    {
#if C3D_OCULUS
        public override void Cognitive3D_Init(Error initError)
        {
            base.Cognitive3D_Init(initError);
            if (OVRManager.display != null)
                OVRManager.display.RecenteredPose += RecenterEventTracker_RecenteredPose;
        }

        private void RecenterEventTracker_RecenteredPose()
        {
            new CustomEvent("cvr.recenter").Send();
        }
#endif

        public override bool GetWarning()
        {
#if C3D_OCULUS
            return false;
#else
            return true;
#endif
        }

        public override string GetDescription()
        {
#if C3D_OCULUS
            return "Sends transaction when the HMD recenters";
#else
            return "Current platform does not support this component\nRequires Oculus Utilities";
#endif

        }

        void OnDestroy()
        {
#if C3D_OCULUS
            OVRManager.display.RecenteredPose -= RecenterEventTracker_RecenteredPose;
#endif
        }
    }
}