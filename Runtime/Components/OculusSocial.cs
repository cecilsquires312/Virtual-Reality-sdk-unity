﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Cognitive3D.Components
{
    public class OculusSocial : AnalyticsComponentBase
    {
        [Tooltip("Used to automatically associate a profile to a participant. Allows tracking between different sessions")]
        public bool AssignOculusProfileToParticipant = true;

        [Tooltip("Sets a session property with the size of the user's party (skipped if playing alone)")]
        public bool RecordPartySize = true;

        public override void Cognitive3D_Init()
        {
            base.Cognitive3D_Init();

            if (!Oculus.Platform.Core.IsInitialized())
            {
                //Initialize will throw error if appid is invalid/missing
                try
                {
                    Oculus.Platform.Core.Initialize();
                    if (AssignOculusProfileToParticipant)
                    {
                        AssignParticipant();
                    }
                    if (RecordPartySize)
                    {
                        CheckPartySize();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        void AssignParticipant()
        {
            Oculus.Platform.Users.GetLoggedInUser().OnComplete(delegate (Oculus.Platform.Message<Oculus.Platform.Models.User> message)
            {
                if (message.IsError)
                {
                    Util.logDebug(message.GetError().Message);
                }
                else
                {
                    Oculus.Platform.Users.GetOrgScopedID(message.Data.ID).OnComplete(delegate (Oculus.Platform.Message<Oculus.Platform.Models.OrgScopedID> message2)
                    {
                        Cognitive3D_Manager.SetParticipantId(message2.Data.ID.ToString());
                    });
                }
            });
        }

        void CheckPartySize()
        {
            Oculus.Platform.Parties.GetCurrent().OnComplete(delegate (Oculus.Platform.Message<Oculus.Platform.Models.Party> message)
            {
                if (message.IsError)
                {
                    Util.logDebug(message.GetError().Message);
                }
                else if (message.Data != null)
                {
                    if (message.Data.UsersOptional != null)
                    {
                        Cognitive3D_Manager.SetSessionProperty("Party Size", message.Data.UsersOptional.Count);
                    }
                }
                else
                {
                    //no party
                }
            });
        }

        public override string GetDescription()
        {
            return "Set a property for the user's party size and an Id to associate the user across your organization";
        }
    }
}