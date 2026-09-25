using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    internal sealed class PhoneNet : MonoBehaviour, IOnEventCallback
    {
        public const byte EventCode = Plugin.PhotonEventCode;
        public const string Magic = Plugin.EventMagic;
        public const int Protocol = 1;

        internal const byte KindText = 1;
        internal const byte KindCallInvite = 2;
        internal const byte KindCallAccept = 3;
        internal const byte KindCallReject = 4;
        internal const byte KindCallHangup = 5;
        internal const byte KindCallAdd = 6;
        internal const byte KindVoiceMeta = 7;
        internal const byte KindVoiceChunk = 8;
        internal const byte KindVoiceEnd = 9;
        internal const byte KindVideoFrame = 10;
        internal const byte KindVideoState = 11;
        internal const byte KindMediaMeta = 12;
        internal const byte KindMediaChunk = 13;
        internal const byte KindMediaEnd = 14;
        internal const byte KindGame = 15;
        internal const byte KindCallMute = 16;
        internal const byte KindRescue = 17;
        internal const byte KindRescueResult = 18;
        internal const byte KindRescueChase = 19;
        internal const byte KindRescueEnd = 20;
        internal const byte KindCastAsk = 21;
        internal const byte KindCastGrant = 22;
        internal const byte KindCastDeny = 23;
        internal const byte KindCastStop = 24;
        internal const byte KindCastFrame = 25;
        public const int MaxMediaBytes = 8388608;

        public static PhoneNet Instance;

        private bool _subscribed;
        private readonly Dictionary<string, PendingVoice> _incoming = new Dictionary<string, PendingVoice>();
        private readonly Dictionary<string, PendingMedia> _incomingMedia = new Dictionary<string, PendingMedia>();

        private sealed class PendingVoice
        {
            public string Id;
            public string ThreadId;
            public string FromId;
            public string FromName;
            public bool Voicemail;
            public int Total;
            public readonly List<byte[]> Chunks = new List<byte[]>();
        }

        private sealed class PendingMedia
        {
            public string Id;
            public string FromId;
            public string FromName;
            public string Text;
            public string Kind;
            public int Total;
            public readonly List<byte[]> Chunks = new List<byte[]>();
        }

        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("PiP_Net");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<PhoneNet>();
        }

        private void OnEnable()
        {
            if (_subscribed)
                return;
            PhotonNetwork.AddCallbackTarget(this);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (!_subscribed)
                return;
            PhotonNetwork.RemoveCallbackTarget(this);
            _subscribed = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void SendText(int actor, ChatMessage msg)
        {
            SendTo(actor, new object[]
            {
                Magic, Protocol, KindText,
                msg.Id, msg.ThreadId, msg.FromId, msg.FromName, msg.Text ?? string.Empty, msg.UnixMs
            });
        }

        public static void SendCallInvite(int[] actors, string callId, byte voiceGroup, bool group, int[] members)
        {
            PhotonPlayer me = PhotonNetwork.LocalPlayer;
            object[] payload = new object[]
            {
                Magic, Protocol, KindCallInvite,
                callId, voiceGroup, me != null ? me.ActorNumber : 0,
                BuiltinApps.LocalId(), BuiltinApps.LocalName(), group, members
            };
            SendToMany(actors, payload);
        }

        public static void SendCallAccept(int actor, string callId)
        {
            SendTo(actor, new object[] { Magic, Protocol, KindCallAccept, callId, PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0 });
        }

        public static void SendCallReject(int actor, string callId, bool busy)
        {
            SendTo(actor, new object[] { Magic, Protocol, KindCallReject, callId, busy });
        }

        public static void SendCallHangup(int[] actors, string callId)
        {
            SendToMany(actors, new object[] { Magic, Protocol, KindCallHangup, callId });
        }

        public static void SendCallAdd(int[] actors, string callId, byte voiceGroup, int newActor)
        {
            SendToMany(actors, new object[] { Magic, Protocol, KindCallAdd, callId, voiceGroup, newActor });
        }

        public static void SendCallMute(int[] actors, string callId, bool muted)
        {
            int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            SendToMany(actors, new object[] { Magic, Protocol, KindCallMute, callId ?? string.Empty, me, muted });
        }

        public static void SendRescue(int masterActor, int targetViewId, float seconds)
        {
            SendTo(masterActor, new object[] { Magic, Protocol, KindRescue, targetViewId, seconds });
        }

        public static void SendRescueResult(int actor, bool ok, string message)
        {
            SendTo(actor, new object[] { Magic, Protocol, KindRescueResult, ok, message ?? string.Empty });
        }

        public static void SendRescueChase(int actor, float seconds)
        {
            SendTo(actor, new object[] { Magic, Protocol, KindRescueChase, seconds });
        }

        public static void SendRescueEnd(int masterActor)
        {
            SendTo(masterActor, new object[] { Magic, Protocol, KindRescueEnd });
        }

        public static void SendVoice(int actor, ChatMessage msg, byte[] wav)
        {
            if (Instance != null)
                Instance.StartCoroutine(SendVoiceRoutine(actor, msg, wav));
            else
                SendVoiceImmediate(actor, msg, wav);
        }

        private static IEnumerator SendVoiceRoutine(int actor, ChatMessage msg, byte[] wav)
        {
            List<byte[]> chunks = VoiceIo.Chunk(wav, 7000);
            SendTo(actor, new object[]
            {
                Magic, Protocol, KindVoiceMeta,
                msg.Id, msg.ThreadId, msg.FromId, msg.FromName, msg.VoiceMail, chunks.Count
            });
            for (int i = 0; i < chunks.Count; i++)
            {
                SendTo(actor, new object[] { Magic, Protocol, KindVoiceChunk, msg.Id, i, chunks[i] });
                if ((i & 1) == 1)
                    yield return new WaitForSecondsRealtime(0.03f);
            }
            SendTo(actor, new object[] { Magic, Protocol, KindVoiceEnd, msg.Id });
        }

        private static void SendVoiceImmediate(int actor, ChatMessage msg, byte[] wav)
        {
            List<byte[]> chunks = VoiceIo.Chunk(wav, 7000);
            SendTo(actor, new object[]
            {
                Magic, Protocol, KindVoiceMeta,
                msg.Id, msg.ThreadId, msg.FromId, msg.FromName, msg.VoiceMail, chunks.Count
            });
            for (int i = 0; i < chunks.Count; i++)
                SendTo(actor, new object[] { Magic, Protocol, KindVoiceChunk, msg.Id, i, chunks[i] });
            SendTo(actor, new object[] { Magic, Protocol, KindVoiceEnd, msg.Id });
        }

        public static void SendMedia(int actor, ChatMessage msg, byte[] data, string kind)
        {
            if (data != null && data.Length > MaxMediaBytes)
            {
                PhoneMenu.Toast("Too large to send (" + SizeLabel(data.Length) + ", max " + SizeLabel(MaxMediaBytes) + ").");
                return;
            }
            PhoneMenu.Toast("Sending…");
            if (Instance != null)
                Instance.StartCoroutine(SendMediaRoutine(actor, msg, data, kind));
            else
                SendMediaImmediate(actor, msg, data, kind);
        }

        private static IEnumerator SendMediaRoutine(int actor, ChatMessage msg, byte[] data, string kind)
        {
            List<byte[]> chunks = VoiceIo.Chunk(data, 20000);
            SendTo(actor, new object[]
            {
                Magic, Protocol, KindMediaMeta,
                msg.Id, msg.FromId, msg.FromName, msg.Text ?? string.Empty, kind ?? "img", chunks.Count
            });
            for (int i = 0; i < chunks.Count; i++)
            {
                SendTo(actor, new object[] { Magic, Protocol, KindMediaChunk, msg.Id, i, chunks[i] });
                yield return new WaitForSecondsRealtime(0.04f);
            }
            SendTo(actor, new object[] { Magic, Protocol, KindMediaEnd, msg.Id });
        }

        private static void SendMediaImmediate(int actor, ChatMessage msg, byte[] data, string kind)
        {
            List<byte[]> chunks = VoiceIo.Chunk(data, 20000);
            SendTo(actor, new object[]
            {
                Magic, Protocol, KindMediaMeta,
                msg.Id, msg.FromId, msg.FromName, msg.Text ?? string.Empty, kind ?? "img", chunks.Count
            });
            for (int i = 0; i < chunks.Count; i++)
                SendTo(actor, new object[] { Magic, Protocol, KindMediaChunk, msg.Id, i, chunks[i] });
            SendTo(actor, new object[] { Magic, Protocol, KindMediaEnd, msg.Id });
        }

        public static void SendVideoFrame(int[] actors, string callId, int actor, byte[] jpeg)
        {
            SendToMany(actors, new object[] { Magic, Protocol, KindVideoFrame, callId ?? string.Empty, actor, jpeg }, false);
        }

        public static void SendVideoState(int[] actors, string callId, bool on)
        {
            int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
            SendToMany(actors, new object[] { Magic, Protocol, KindVideoState, callId ?? string.Empty, me, on }, true);
        }

        public static void SendGame(int actor, params object[] extra)
        {
            if (extra == null)
                return;
            var payload = new object[3 + extra.Length];
            payload[0] = Magic;
            payload[1] = Protocol;
            payload[2] = KindGame;
            for (int i = 0; i < extra.Length; i++)
                payload[3 + i] = extra[i];
            SendTo(actor, payload);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != EventCode)
                return;
            object[] data = photonEvent.CustomData as object[];
            if (data == null || data.Length < 3)
                return;
            string magic = data[0] as string;
            if (magic != Magic)
                return;
            if (!(data[1] is int) || (int)data[1] != Protocol)
                return;
            byte kind = ToByte(data[2]);
            try
            {
                switch (kind)
                {
                    case KindText:
                        OnText(data);
                        break;
                    case KindCallInvite:
                        CallService.OnInvite(data);
                        break;
                    case KindCallAccept:
                        CallService.OnAccept(data);
                        break;
                    case KindCallReject:
                        CallService.OnReject(data);
                        break;
                    case KindCallHangup:
                        CallService.OnHangup(data);
                        break;
                    case KindCallAdd:
                        CallService.OnAdd(data);
                        break;
                    case KindCallMute:
                        CallService.OnMute(data);
                        break;
                    case KindRescue:
                        PhoneNumbers.OnRescueRequest(photonEvent, data);
                        break;
                    case KindRescueResult:
                        PhoneNumbers.OnRescueResult(data);
                        break;
                    case KindRescueChase:
                        PhoneNumbers.OnRescueChase(data);
                        break;
                    case KindRescueEnd:
                        PhoneNumbers.OnRescueEnd();
                        break;
                    case KindVoiceMeta:
                        OnVoiceMeta(data);
                        break;
                    case KindVoiceChunk:
                        OnVoiceChunk(data);
                        break;
                    case KindVoiceEnd:
                        OnVoiceEnd(data);
                        break;
                    case KindVideoFrame:
                        if (data.Length >= 6)
                            CallVideo.OnFrame(data[3] as string, ToInt(data[4]), data[5] as byte[]);
                        break;
                    case KindVideoState:
                        if (data.Length >= 6)
                            CallVideo.OnState(data[3] as string, ToInt(data[4]), data[5] is bool && (bool)data[5]);
                        break;
                    case KindMediaMeta:
                        OnMediaMeta(data);
                        break;
                    case KindMediaChunk:
                        OnMediaChunk(data);
                        break;
                    case KindMediaEnd:
                        OnMediaEnd(data);
                        break;
                    case KindGame:
                        FourAcrossApp.OnNet(data);
                        break;
                    case KindCastAsk:
                        if (data.Length >= 4)
                            PhoneCast.OnAsk(photonEvent.Sender, data[3] as string);
                        break;
                    case KindCastGrant:
                        if (data.Length >= 5)
                            PhoneCast.OnGrant(data[3] as string, ToInt(data[4]));
                        break;
                    case KindCastDeny:
                        if (data.Length >= 4)
                            PhoneCast.OnDeny(data[3] as string);
                        break;
                    case KindCastStop:
                        if (data.Length >= 5)
                            PhoneCast.OnStop(data[3] as string, ToInt(data[4]));
                        break;
                    case KindCastFrame:
                        if (data.Length >= 8)
                            PhoneCast.OnFrame(data[3] as string, photonEvent.Sender, ToInt(data[4]), ToInt(data[5]), ToInt(data[6]), data[7] as byte[]);
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Phone net event failed: " + ex.Message);
            }
        }

        private static void OnText(object[] data)
        {
            if (data.Length < 9)
                return;
            var msg = new ChatMessage
            {
                Id = data[3] as string,
                FromId = data[5] as string,
                FromName = data[6] as string,
                Text = data[7] as string,
                UnixMs = ToLong(data[8])
            };
            if (string.IsNullOrEmpty(msg.Id) || string.IsNullOrEmpty(msg.FromId))
                return;
            if (PhoneTheme.DoNotDisturb || PhoneContacts.BlocksTexts(msg.FromId))
                return;
            msg.ThreadId = msg.FromId;
            PhoneStore.AddMessage(msg);
            PhoneNotify.IncomingText(msg.FromId, msg.FromName, msg.Text);
            MessagesApp.RefreshIfOpen();
        }

        private void OnVoiceMeta(object[] data)
        {
            if (data.Length < 9)
                return;
            string id = data[3] as string;
            if (string.IsNullOrEmpty(id) || PhoneTheme.DoNotDisturb || PhoneContacts.BlocksTexts(data[5] as string))
                return;
            _incoming[id] = new PendingVoice
            {
                Id = id,
                ThreadId = data[4] as string,
                FromId = data[5] as string,
                FromName = data[6] as string,
                Voicemail = data[7] is bool && (bool)data[7],
                Total = ToInt(data[8])
            };
        }

        private void OnVoiceChunk(object[] data)
        {
            if (data.Length < 6)
                return;
            string id = data[3] as string;
            PendingVoice pending;
            if (string.IsNullOrEmpty(id) || !_incoming.TryGetValue(id, out pending))
                return;
            byte[] chunk = data[5] as byte[];
            if (chunk != null)
                pending.Chunks.Add(chunk);
        }

        private void OnVoiceEnd(object[] data)
        {
            if (data.Length < 4)
                return;
            string id = data[3] as string;
            PendingVoice pending;
            if (string.IsNullOrEmpty(id) || !_incoming.TryGetValue(id, out pending))
                return;
            _incoming.Remove(id);
            int total = 0;
            for (int i = 0; i < pending.Chunks.Count; i++)
                total += pending.Chunks[i] != null ? pending.Chunks[i].Length : 0;
            var wav = new byte[total];
            int offset = 0;
            for (int i = 0; i < pending.Chunks.Count; i++)
            {
                byte[] part = pending.Chunks[i];
                if (part == null || part.Length == 0)
                    continue;
                Buffer.BlockCopy(part, 0, wav, offset, part.Length);
                offset += part.Length;
            }
            string file = PhoneStore.SaveAudio(id, wav);
            var msg = new ChatMessage
            {
                Id = id,
                ThreadId = pending.FromId,
                FromId = pending.FromId,
                FromName = pending.FromName,
                Text = pending.Voicemail ? "Voicemail" : "Voice message",
                AudioFile = file,
                UnixMs = PhoneStore.NowMs(),
                VoiceMail = pending.Voicemail
            };
            PhoneStore.AddMessage(msg);
            PhoneNotify.IncomingText(pending.FromId, pending.FromName, pending.Voicemail ? "Voicemail" : "Voice message");
            MessagesApp.RefreshIfOpen();
            VoicemailApp.RefreshIfOpen();
        }

        private void OnMediaMeta(object[] data)
        {
            if (data.Length < 9)
                return;
            string id = data[3] as string;
            if (string.IsNullOrEmpty(id) || PhoneContacts.BlocksTexts(data[4] as string))
                return;
            _incomingMedia[id] = new PendingMedia
            {
                Id = id,
                FromId = data[4] as string,
                FromName = data[5] as string,
                Text = data[6] as string,
                Kind = data[7] as string,
                Total = ToInt(data[8])
            };
            PhoneNotify.Quiet("Downloading", (data[5] as string) ?? "File");
        }

        private void OnMediaChunk(object[] data)
        {
            if (data.Length < 6)
                return;
            string id = data[3] as string;
            PendingMedia pending;
            if (string.IsNullOrEmpty(id) || !_incomingMedia.TryGetValue(id, out pending))
                return;
            byte[] chunk = data[5] as byte[];
            if (chunk != null)
                pending.Chunks.Add(chunk);
        }

        private void OnMediaEnd(object[] data)
        {
            if (data.Length < 4)
                return;
            string id = data[3] as string;
            PendingMedia pending;
            if (string.IsNullOrEmpty(id) || !_incomingMedia.TryGetValue(id, out pending))
                return;
            _incomingMedia.Remove(id);
            int total = 0;
            for (int i = 0; i < pending.Chunks.Count; i++)
            {
                if (pending.Chunks[i] != null)
                    total += pending.Chunks[i].Length;
            }
            var bytes = new byte[total];
            int o = 0;
            for (int i = 0; i < pending.Chunks.Count; i++)
            {
                byte[] c = pending.Chunks[i];
                if (c == null)
                    continue;
                System.Buffer.BlockCopy(c, 0, bytes, o, c.Length);
                o += c.Length;
            }
            string ext = PhoneImages.GuessExtFromBytes(bytes, pending.Kind);
            string mediaFile;
            if (pending.Kind == "gif" || pending.Kind == "aud")
            {
                mediaFile = PhoneStore.SaveChatMedia(bytes, ext);
                if (pending.Kind == "aud")
                {
                    string src = PhoneStore.MediaPath(mediaFile);
                    if (!string.IsNullOrEmpty(src))
                        PhoneStore.AddSound(src, false, 0f);
                }
            }
            else
            {
                DownloadItem saved = PhoneStore.AddDownload(bytes, ext, string.Empty);
                mediaFile = saved != null ? "downloads/" + saved.File : string.Empty;
            }
            var msg = new ChatMessage
            {
                Id = pending.Id,
                ThreadId = pending.FromId,
                FromId = pending.FromId,
                FromName = pending.FromName,
                Text = string.IsNullOrEmpty(pending.Text) ? "Photo" : pending.Text,
                MediaFile = mediaFile,
                MediaKind = pending.Kind,
                UnixMs = PhoneStore.NowMs()
            };
            PhoneStore.AddMessage(msg);
            PhoneNotify.IncomingText(pending.FromId, pending.FromName, msg.Text);
            MessagesApp.RefreshIfOpen();
        }

        public static void SendCastAsk(string deviceId)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.MasterClient == null)
                return;
            SendTo(PhotonNetwork.MasterClient.ActorNumber, new object[] { Magic, Protocol, KindCastAsk, deviceId ?? string.Empty });
        }

        public static void SendCastGrant(string deviceId, int actor)
        {
            SendOthers(new object[] { Magic, Protocol, KindCastGrant, deviceId ?? string.Empty, actor }, true);
        }

        public static void SendCastDeny(int actor, string deviceId)
        {
            SendTo(actor, new object[] { Magic, Protocol, KindCastDeny, deviceId ?? string.Empty });
        }

        public static void SendCastStop(string deviceId, int actor)
        {
            SendOthers(new object[] { Magic, Protocol, KindCastStop, deviceId ?? string.Empty, actor }, true);
        }

        public static void SendCastFrame(string deviceId, int seq, byte[] jpg)
        {
            if (jpg == null || jpg.Length == 0 || !PhotonNetwork.InRoom)
                return;
            int count = (jpg.Length + 5999) / 6000;
            for (int i = 0; i < count; i++)
            {
                int offset = i * 6000;
                int len = Math.Min(6000, jpg.Length - offset);
                var chunk = new byte[len];
                Buffer.BlockCopy(jpg, offset, chunk, 0, len);
                SendOthers(new object[] { Magic, Protocol, KindCastFrame, deviceId ?? string.Empty, seq, i, count, chunk }, false);
            }
        }

        private static void SendOthers(object[] payload, bool reliable)
        {
            if (!PhotonNetwork.InRoom)
                return;
            try
            {
                SendOptions opts = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
                PhotonNetwork.RaiseEvent(EventCode, payload, new RaiseEventOptions { Receivers = ReceiverGroup.Others }, opts);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Phone send failed: " + ex.Message);
            }
        }

        private static void SendToMany(int[] actors, object[] payload)
        {
            SendToMany(actors, payload, true);
        }

        private static void SendToMany(int[] actors, object[] payload, bool reliable)
        {
            if (actors == null)
                return;
            for (int i = 0; i < actors.Length; i++)
                SendTo(actors[i], payload, reliable);
        }

        private static void SendTo(int actor, object[] payload)
        {
            SendTo(actor, payload, true);
        }

        private static void SendTo(int actor, object[] payload, bool reliable)
        {
            if (actor <= 0 || !PhotonNetwork.InRoom)
                return;
            try
            {
                SendOptions opts = reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable;
                PhotonNetwork.RaiseEvent(EventCode, payload, new RaiseEventOptions { TargetActors = new[] { actor } }, opts);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Phone send failed: " + ex.Message);
            }
        }

        internal static string SizeLabel(int bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024 * 1024)
                return (bytes / 1024) + " KB";
            return ((bytes + 512 * 1024) / (1024 * 1024)) + " MB";
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

        private static long ToLong(object value)
        {
            if (value is long l)
                return l;
            return Convert.ToInt64(value);
        }
    }
}
