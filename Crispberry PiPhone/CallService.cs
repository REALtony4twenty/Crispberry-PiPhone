using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    internal sealed class CallService : MonoBehaviour
    {
        public enum Phase
        {
            Idle,
            Dialing,
            Incoming,
            Active
        }

        public static CallService Instance;
        public static event Action Changed;

        public static Phase State = Phase.Idle;
        public static string CallId;
        public static byte VoiceGroup;
        public static bool Group;
        public static bool Muted;
        public static string RemoteName = string.Empty;
        public static string RemoteId = string.Empty;
        public static readonly List<int> Members = new List<int>();
        public static int CallerActor;
        public static bool LeavingVoicemail;
        public static bool WantVideo;

        private Coroutine _ring;
        private Coroutine _vm;
        private static readonly Dictionary<CharacterVoiceHandler, SavedVoice> Flattened = new Dictionary<CharacterVoiceHandler, SavedVoice>();
        private static readonly HashSet<int> MutedActors = new HashSet<int>();

        private struct SavedVoice
        {
            public float SpatialBlend;
            public float Doppler;
            public bool Spatialize;
            public float MinDistance;
            public float MaxDistance;
            public AudioRolloffMode Rolloff;
            public int PlayDelay;
            public bool HadSpeaker;
        }

        public static bool IsBusy
        {
            get { return State != Phase.Idle || CallVideo.Chasing; }
        }

        public static bool IsOnCall
        {
            get { return State == Phase.Active; }
        }

        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("PiP_Calls");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<CallService>();
        }

        public static void Dial(int[] actors, bool group)
        {
            Dial(actors, group, false);
        }

        public static void Dial(int[] actors, bool group, bool video)
        {
            if (!PhoneMenu.CanUsePhone())
            {
                PhoneMenu.Toast("You're unconscious.");
                return;
            }
            if (actors == null || actors.Length == 0)
            {
                PhoneMenu.Toast("Pick someone to call.");
                return;
            }
            if (!PhotonNetwork.InRoom)
            {
                PhoneMenu.Toast("No signal.");
                return;
            }
            if (IsBusy)
            {
                PhoneMenu.Toast("Already on a call.");
                return;
            }

            CallId = PhoneStore.NewId();
            VoiceGroup = (byte)UnityEngine.Random.Range(200, 250);
            Group = group || actors.Length > 1;
            Muted = false;
            WantVideo = video;
            CallerActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            Members.Clear();
            Members.Add(CallerActor);
            for (int i = 0; i < actors.Length; i++)
            {
                if (!Members.Contains(actors[i]))
                    Members.Add(actors[i]);
            }
            RemoteName = NamesFor(actors);
            RemoteId = FirstRemoteId();
            State = Phase.Dialing;
            PhoneNet.SendCallInvite(actors, CallId, VoiceGroup, Group, Members.ToArray());
            PhoneStore.AddCall(new CallLogItem
            {
                Id = PhoneStore.NewId(),
                OtherId = FirstRemoteId(),
                OtherName = RemoteName,
                Outgoing = true,
                Missed = false,
                Group = Group,
                UnixMs = PhoneStore.NowMs()
            });
            PhoneMenu.Open();
            Raise();
            if (Instance != null)
                Instance._ring = Instance.StartCoroutine(Instance.RingTimeout());
        }

        public static void Accept()
        {
            if (State != Phase.Incoming)
                return;
            if (!PhoneMenu.CanUsePhone())
                return;
            PhoneSounds.StopRing();
            PhoneStore.DismissNotices(BuiltinApps.PhoneId, "Incoming call");
            State = Phase.Active;
            PhoneNet.SendCallAccept(CallerActor, CallId);
            if (WantVideo)
                CallVideo.StartIfPending();
            Raise();
        }

        public static void Reject()
        {
            PhoneSounds.StopRing();
            PhoneStore.DismissNotices(BuiltinApps.PhoneId, "Incoming call");
            if (State == Phase.Incoming)
                PhoneNet.SendCallReject(CallerActor, CallId, false);
            Reset("Declined");
        }

        public static void HangUp()
        {
            bool chase = CallVideo.Chasing;
            if (chase)
                CallVideo.StopChase();
            if (chase)
                PhoneNumbers.RequestDismiss();
            if (State == Phase.Idle)
                return;
            int[] others = Others();
            string id = CallId;
            RestoreCallVoice();
            PhoneNet.SendCallHangup(others, id);
            CallVideo.HangUp();
            Reset("Call ended");
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            PhoneNet.SendCallMute(Others(), CallId, Muted);
            Raise();
        }

        public static void AddPerson(int actor)
        {
            if (!IsOnCall || actor <= 0 || Members.Contains(actor))
                return;
            Group = true;
            Members.Add(actor);
            PhoneNet.SendCallInvite(new[] { actor }, CallId, VoiceGroup, true, Members.ToArray());
            PhoneNet.SendCallAdd(Others(), CallId, VoiceGroup, actor);
            RemoteName = NamesFor(Others());
            Raise();
        }

        public static void LeaveVoicemailForCurrent()
        {
            GoToVoicemail();
        }

        public static void FinishVoicemail()
        {
            if (Instance != null && Instance._vm != null)
            {
                Instance.StopCoroutine(Instance._vm);
                Instance._vm = null;
            }
            object clip = VoiceIo.StopRecord();
            LeavingVoicemail = false;
            int target = FirstRemoteActor();
            PhotonPlayer player = BuiltinApps.FindByActor(target);
            Members.Clear();
            if (clip == null || player == null)
            {
                PhoneMenu.Toast("No voicemail recorded.");
                return;
            }
            byte[] wav = VoiceIo.ToWav(clip);
            string id = PhoneStore.NewId();
            string file = PhoneStore.SaveAudio(id, wav);
            var msg = new ChatMessage
            {
                Id = id,
                ThreadId = BuiltinApps.PlayerId(player),
                FromId = BuiltinApps.LocalId(),
                FromName = BuiltinApps.LocalName(),
                Text = "Voicemail",
                AudioFile = file,
                UnixMs = PhoneStore.NowMs(),
                VoiceMail = true
            };
            PhoneStore.AddMessage(msg);
            PhoneNet.SendVoice(target, msg, wav);
            PhoneMenu.Toast("Voicemail sent.");
        }

        public static void OnInvite(object[] data)
        {
            if (data.Length < 10)
                return;
            string callId = data[3] as string;
            byte group = ToByte(data[4]);
            int caller = ToInt(data[5]);
            string callerId = data[6] as string;
            string callerName = data[7] as string;
            bool groupCall = data[8] is bool && (bool)data[8];
            int[] members = data[9] as int[];
            if (PhoneTheme.DoNotDisturb || (!string.IsNullOrEmpty(callerId) && PhoneContacts.BlocksCalls(callerId)))
            {
                PhoneNet.SendCallReject(caller, callId, true);
                if (PhoneTheme.DoNotDisturb)
                {
                    PhoneStore.AddCall(new CallLogItem
                    {
                        Id = PhoneStore.NewId(),
                        OtherId = callerId,
                        OtherName = callerName,
                        Outgoing = false,
                        Missed = true,
                        Group = groupCall,
                        UnixMs = PhoneStore.NowMs()
                    });
                }
                return;
            }
            if (IsBusy)
            {
                PhoneNet.SendCallReject(caller, callId, true);
                return;
            }
            CallId = callId;
            VoiceGroup = group;
            Group = groupCall;
            CallerActor = caller;
            PhoneContacts.See(callerId, callerName);
            RemoteName = PhoneContacts.Display(callerId, callerName);
            RemoteId = callerId ?? string.Empty;
            Members.Clear();
            if (members != null)
            {
                for (int i = 0; i < members.Length; i++)
                    Members.Add(members[i]);
            }
            int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            if (!Members.Contains(me))
                Members.Add(me);
            State = Phase.Incoming;
            PhoneStore.AddCall(new CallLogItem
            {
                Id = PhoneStore.NewId(),
                OtherId = callerId,
                OtherName = RemoteName,
                Outgoing = false,
                Missed = true,
                Group = Group,
                UnixMs = PhoneStore.NowMs()
            });
            PhoneNotify.IncomingCall(RemoteId, RemoteName);
            Raise();
            if (PhoneTheme.AutoAnswer && PhoneMenu.CanUsePhone())
                Accept();
        }

        public static void OnAccept(object[] data)
        {
            if (State != Phase.Dialing)
                return;
            PhoneSounds.StopRing();
            State = Phase.Active;
            if (WantVideo)
                CallVideo.StartIfPending();
            Raise();
        }

        public static void OnReject(object[] data)
        {
            if (State != Phase.Dialing && State != Phase.Incoming)
                return;
            bool busy = data.Length > 4 && data[4] is bool && (bool)data[4];
            Reset(busy ? "Busy" : "Declined");
        }

        public static void OnHangup(object[] data)
        {
            if (State == Phase.Idle)
                return;
            RestoreCallVoice();
            Reset("Call ended");
        }

        public static void OnAdd(object[] data)
        {
            if (!IsOnCall || data.Length < 6)
                return;
            int actor = ToInt(data[5]);
            if (!Members.Contains(actor))
                Members.Add(actor);
            Group = true;
            RemoteName = NamesFor(Others());
            if (Muted)
                PhoneNet.SendCallMute(new[] { actor }, CallId, true);
            Raise();
        }

        public static void OnMute(object[] data)
        {
            if (!IsOnCall || data.Length < 6)
                return;
            string callId = data[3] as string;
            if (!string.IsNullOrEmpty(CallId) && !string.IsNullOrEmpty(callId) && callId != CallId)
                return;
            int actor = ToInt(data[4]);
            bool muted = data[5] is bool && (bool)data[5];
            if (actor <= 0)
                return;
            if (muted)
                MutedActors.Add(actor);
            else
                MutedActors.Remove(actor);
        }

        private IEnumerator RingTimeout()
        {
            yield return new WaitForSecondsRealtime(20f);
            if (State == Phase.Dialing)
                GoToVoicemail();
        }

        private IEnumerator FinishVoicemailSoon()
        {
            yield return new WaitForSecondsRealtime(20f);
            FinishVoicemail();
        }

        private static void GoToVoicemail()
        {
            if (State != Phase.Dialing)
                return;
            PhoneSounds.StopRing();
            int[] others = Others();
            string id = CallId;
            PhoneNet.SendCallHangup(others, id);
            LeavingVoicemail = true;
            HangUpSilent();
            VoiceIo.StartRecord(20);
            PhoneMenu.Toast("No answer. Recording voicemail (20s)...");
            if (Instance != null)
                Instance._vm = Instance.StartCoroutine(Instance.FinishVoicemailSoon());
        }

        private static void RestoreCallVoice()
        {
            RestoreFlattened();
            MutedActors.Clear();
        }

        private static void HangUpSilent()
        {
            RestoreCallVoice();
            State = Phase.Idle;
            Raise();
        }

        private static void Reset(string toast)
        {
            PhoneSounds.StopRing();
            if (Instance != null && Instance._ring != null)
            {
                Instance.StopCoroutine(Instance._ring);
                Instance._ring = null;
            }
            RestoreCallVoice();
            State = Phase.Idle;
            CallId = null;
            RemoteId = string.Empty;
            WantVideo = false;
            Members.Clear();
            Muted = false;
            CallVideo.HangUp();
            if (!string.IsNullOrEmpty(toast))
                PhoneMenu.Toast(toast);
            Raise();
        }

        private static void Raise()
        {
            Action handler = Changed;
            if (handler != null)
                handler();
            PhoneMenu.RefreshCallUi();
            AlertHud.Sync();
        }

        internal static int[] OtherActors()
        {
            return Others();
        }

        private static int[] Others()
        {
            int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            var list = new List<int>();
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i] != me)
                    list.Add(Members[i]);
            }
            return list.ToArray();
        }

        private static int FirstRemoteActor()
        {
            int[] others = Others();
            return others.Length > 0 ? others[0] : 0;
        }

        private static string FirstRemoteId()
        {
            PhotonPlayer p = BuiltinApps.FindByActor(FirstRemoteActor());
            return p != null ? BuiltinApps.PlayerId(p) : string.Empty;
        }

        private static string NamesFor(int[] actors)
        {
            if (actors == null || actors.Length == 0)
                return "Scout";
            var names = new List<string>();
            for (int i = 0; i < actors.Length; i++)
            {
                PhotonPlayer p = BuiltinApps.FindByActor(actors[i]);
                names.Add(p != null ? BuiltinApps.ContactName(p) : ("#" + actors[i]));
            }
            return string.Join(", ", names.ToArray());
        }

        public static bool IsCallMember(Character character)
        {
            if (!IsOnCall || character == null || character.IsLocal)
                return false;
            try
            {
                if (character.photonView == null || character.photonView.Owner == null)
                    return false;
                int actor = character.photonView.Owner.ActorNumber;
                return Members.Contains(actor) && !MutedActors.Contains(actor);
            }
            catch
            {
                return false;
            }
        }

        public static bool ShouldFlatten(Character character)
        {
            return IsCallMember(character) && FarFromLocal(character);
        }

        private static bool CrossPlane(Character remote)
        {
            bool localGhost = false;
            bool remoteGhost = false;
            try
            {
                Character local = Character.localCharacter;
                if (local != null)
                    localGhost = local.IsGhost;
            }
            catch
            {
            }
            try
            {
                if (remote != null)
                    remoteGhost = remote.IsGhost;
            }
            catch
            {
            }
            return localGhost != remoteGhost;
        }

        private const float FlattenBeyond = 16f;

        private static bool FarFromLocal(Character character)
        {
            Character local = Character.localCharacter;
            if (local == null || character == null)
                return false;
            return (VoicePos(character) - VoicePos(local)).sqrMagnitude > FlattenBeyond * FlattenBeyond;
        }

        private static Vector3 VoicePos(Character character)
        {
            if (character == null)
                return Vector3.zero;
            try
            {
                if (character.Ghost != null)
                    return character.Ghost.transform.position;
            }
            catch
            {
            }
            try
            {
                CharacterVoiceHandler handler = character.GetComponentInChildren<CharacterVoiceHandler>();
                if (handler != null && handler.m_transformProvider != null)
                    return handler.m_transformProvider.GetVoicePosition(false);
            }
            catch
            {
            }
            try
            {
                if (character.refs != null && character.refs.head != null)
                    return character.refs.head.transform.position;
            }
            catch
            {
            }
            return character.Center;
        }

        private static void FlattenSource(CharacterVoiceHandler handler)
        {
            AudioSource src = VoiceSource(handler);
            if (src == null)
                return;
            if (!Flattened.ContainsKey(handler))
            {
                SavedVoice saved = new SavedVoice
                {
                    SpatialBlend = src.spatialBlend,
                    Doppler = src.dopplerLevel,
                    Spatialize = src.spatialize,
                    MinDistance = src.minDistance,
                    MaxDistance = src.maxDistance,
                    Rolloff = src.rolloffMode
                };
                Speaker speaker = handler.GetComponentInChildren<Speaker>(true);
                if (speaker != null)
                {
                    saved.HadSpeaker = true;
                    saved.PlayDelay = speaker.PlayDelay;
                    if (speaker.PlayDelay > 120)
                        speaker.PlayDelay = 80;
                }
                Flattened.Add(handler, saved);
            }
            src.spatialBlend = 0f;
            src.dopplerLevel = 0f;
            src.spatialize = false;
            src.minDistance = 500f;
            src.maxDistance = 10000f;
            src.volume = handler.audioLevel;
        }

        private static void RestoreSpatial(CharacterVoiceHandler handler)
        {
            SavedVoice saved;
            if (handler == null || !Flattened.TryGetValue(handler, out saved))
                return;
            AudioSource src = VoiceSource(handler);
            if (src != null)
            {
                src.spatialBlend = saved.SpatialBlend;
                src.dopplerLevel = saved.Doppler;
                src.spatialize = saved.Spatialize;
                src.minDistance = saved.MinDistance;
                src.maxDistance = saved.MaxDistance;
                src.rolloffMode = saved.Rolloff;
            }
            if (saved.HadSpeaker)
            {
                Speaker speaker = handler.GetComponentInChildren<Speaker>(true);
                if (speaker != null)
                    speaker.PlayDelay = saved.PlayDelay;
            }
        }

        private static void RestoreFlattened()
        {
            foreach (CharacterVoiceHandler handler in Flattened.Keys)
                RestoreSpatial(handler);
            Flattened.Clear();
            VoiceFx.ClearAllCallLayers();
        }

        private static AudioSource VoiceSource(CharacterVoiceHandler handler)
        {
            if (handler == null)
                return null;
            if (handler.m_source != null)
                return handler.m_source;
            return handler.audioSource;
        }

        private static byte ToByte(object value)
        {
            if (value is byte b)
                return b;
            return Convert.ToByte(value);
        }

        private static int ToInt(object value)
        {
            if (value is int i)
                return i;
            return Convert.ToInt32(value);
        }

        private void Update()
        {
            CallVideo.Tick();
        }

        [HarmonyPatch(typeof(CharacterVoiceHandler), "LateUpdate")]
        private static class Patch_VoiceLateUpdate
        {
            private static bool _logged;

            private static void Postfix(CharacterVoiceHandler __instance)
            {
                try
                {
                    if (__instance == null || __instance.m_character == null)
                        return;
                    if (IsCallMember(__instance.m_character))
                    {
                        PiPhoneVoiceFilter tone = CrossPlane(__instance.m_character)
                            ? PiPhoneVoiceFilter.Realm()
                            : PiPhoneVoiceFilter.Dry();
                        VoiceFx.SetCallLayer(__instance, tone);
                        if (ShouldFlatten(__instance.m_character))
                            FlattenSource(__instance);
                        else if (Flattened.ContainsKey(__instance))
                        {
                            RestoreSpatial(__instance);
                            Flattened.Remove(__instance);
                        }
                    }
                    else if (Flattened.ContainsKey(__instance) || VoiceFx.HasCallLayer(__instance))
                    {
                        RestoreSpatial(__instance);
                        Flattened.Remove(__instance);
                        VoiceFx.ClearCallLayer(__instance);
                    }
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Call flatten patch failed: " + ex.Message);
                }
            }
        }
    }
}
