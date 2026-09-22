using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Video calls render local spectator cameras in front of each scout.
    /// No JPEG stream — each client looks at the other character in-world.
    /// </summary>
    internal static class CallVideo
    {
        private const int Width = 480;
        private const int Height = 720;

        public static bool Sending;
        public static bool Chasing;
        public static Texture RemoteTexture;

        private static Camera _remoteCam;
        private static Camera _localCam;
        private static RenderTexture _remoteRt;
        private static RenderTexture _localRt;
        private static RawImage _remote;
        private static RawImage _local;
        private static Button _videoBtn;
        private static Character _chaseHunter;
        private static Character _chasePrey;

        public static void Bind(RawImage remote, RawImage local, Button videoBtn)
        {
            _remote = remote;
            _local = local;
            _videoBtn = videoBtn;
            ApplyUi();
        }

        public static void Toggle()
        {
            if (!CallService.IsOnCall)
            {
                PhoneMenu.Toast("Join the call first.");
                return;
            }
            if (Sending)
                StopSending();
            else
                StartSending();
            PhoneNet.SendVideoState(CallService.OtherActors(), CallService.CallId, Sending);
            ApplyUi();
        }

        public static void StartIfPending()
        {
            if (!CallService.IsOnCall || Sending)
                return;
            StartSending();
            PhoneNet.SendVideoState(CallService.OtherActors(), CallService.CallId, true);
            ApplyUi();
        }

        public static void HangUp()
        {
            StopChase();
            StopSending();
            ApplyUi();
        }

        internal static void DropToVoice()
        {
            if (!Sending)
                return;
            StopSending();
            PhoneNet.SendVideoState(CallService.OtherActors(), CallService.CallId, false);
            ApplyUi();
        }

        public static void StartChase(Character hunter, Character prey, float seconds)
        {
            _chaseHunter = hunter;
            _chasePrey = prey;
            Chasing = true;
            EnsureCams();
            PhoneMenu.Open();
            PhoneMenu.SetPlayThrough(PiPhonePlayThrough.Walk);
            PhoneMenu.RefreshCallUi();
        }

        public static void StopChase()
        {
            if (!Chasing)
                return;
            Chasing = false;
            _chaseHunter = null;
            _chasePrey = null;
            if (!Sending)
            {
                DestroyCam(ref _remoteCam, ref _remoteRt);
                DestroyCam(ref _localCam, ref _localRt);
                if (_remote != null)
                    _remote.texture = null;
                if (_local != null)
                    _local.texture = null;
            }
            PhoneMenu.RefreshCallUi();
        }

        public static void Tick()
        {
            if (Chasing && ScoutmasterGrabbedPrey())
            {
                StopChase();
                PhoneNumbers.RequestDismiss();
                return;
            }
            if ((!Sending && !Chasing) || (!CallService.IsOnCall && !Chasing))
                return;
            EnsureCams();
            Character local = VisibleLocal();
            Character remote = FindRemote();
            if (Chasing)
            {
                Character hunter = _chaseHunter != null ? _chaseHunter : FindScoutmasterBody();
                Character prey = _chasePrey != null ? _chasePrey : local;
                if (_remoteCam != null)
                {
                    PlaceChaseCam(_remoteCam, hunter, prey);
                    _remoteCam.Render();
                    if (_remote != null)
                        _remote.texture = _remoteRt;
                    RemoteTexture = _remoteRt;
                }
                if (_localCam != null)
                {
                    PlaceCam(_localCam, prey != null ? prey : local);
                    _localCam.Render();
                    if (_local != null)
                        _local.texture = _localRt;
                }
                return;
            }
            if (_localCam != null)
            {
                PlaceCam(_localCam, local);
                _localCam.Render();
                if (_local != null)
                    _local.texture = _localRt;
            }
            if (_remoteCam != null)
            {
                PlaceCam(_remoteCam, remote != null ? remote : local);
                _remoteCam.Render();
                if (_remote != null)
                    _remote.texture = _remoteRt;
                RemoteTexture = _remoteRt;
            }
        }

        public static void OnFrame(string callId, int actor, byte[] jpeg)
        {
        }

        public static void OnState(string callId, int actor, bool on)
        {
            if (callId != CallService.CallId)
                return;
            if (on && CallService.IsOnCall && !Sending)
            {
                StartSending();
                ApplyUi();
            }
            if (!on && Sending)
            {
                int[] others = CallService.OtherActors();
                bool anyone = false;
                for (int i = 0; i < others.Length; i++)
                {
                    if (others[i] == actor)
                        continue;
                    anyone = true;
                }
                if (!anyone && actor != (PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0))
                    StopSending();
                ApplyUi();
            }
        }

        private static void StartSending()
        {
            Sending = true;
            EnsureCams();
        }

        private static void StopSending()
        {
            Sending = false;
            RemoteTexture = null;
            DestroyCam(ref _remoteCam, ref _remoteRt);
            DestroyCam(ref _localCam, ref _localRt);
            if (_remote != null)
                _remote.texture = null;
            if (_local != null)
                _local.texture = null;
        }

        internal static void ApplyUi()
        {
            bool onCall = CallService.IsOnCall || Chasing;
            bool show = onCall && (Sending || Chasing);
            if (_remote != null)
            {
                _remote.enabled = show;
                _remote.gameObject.SetActive(onCall);
            }
            if (_local != null)
            {
                _local.enabled = show;
                _local.gameObject.SetActive(show);
            }
            if (_videoBtn != null)
                PhoneUi.SetChipIcon(_videoBtn, PhoneIcons.Material(Sending ? "stop" : "videocam"), Sending ? "Stop" : "Video");
        }

        private static void EnsureCams()
        {
            if (_remoteCam == null)
                MakeCam(out _remoteCam, out _remoteRt, "PiP_CallRemote");
            if (_localCam == null)
                MakeCam(out _localCam, out _localRt, "PiP_CallLocal");
        }

        private static void MakeCam(out Camera cam, out RenderTexture rt, string name)
        {
            rt = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 1;
            rt.Create();
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.allowMSAA = false;
            cam.allowHDR = false;
            cam.depth = -81;
            cam.nearClipPlane = 0.12f;
            cam.farClipPlane = 2000f;
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        private static void DestroyCam(ref Camera cam, ref RenderTexture rt)
        {
            if (cam != null)
            {
                Object.Destroy(cam.gameObject);
                cam = null;
            }
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
                rt = null;
            }
        }

        private static Character VisibleLocal()
        {
            Character hint = Character.localCharacter;
            if (hint == null)
                return null;
            int actor = ActorOf(hint);
            if (actor == 0)
                return hint;
            Character[] all = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            if (all == null)
                return hint;
            Character picked = PickPlayerBody(all, actor);
            return picked != null ? picked : hint;
        }

        private static Character FindRemote()
        {
            int[] others = CallService.OtherActors();
            if (others == null || others.Length == 0)
                return null;
            Character[] all = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            if (all == null)
                return null;
            for (int a = 0; a < others.Length; a++)
            {
                Character pick = PickPlayerBody(all, others[a]);
                if (pick != null)
                    return pick;
            }
            return null;
        }

        private static Character PickPlayerBody(Character[] all, int actor)
        {
            Character ghost = null;
            Character living = null;
            Character any = null;
            for (int i = 0; i < all.Length; i++)
            {
                Character c = all[i];
                if (c == null || !IsPlayerScout(c))
                    continue;
                if (ActorOf(c) != actor)
                    continue;
                if (c.IsGhost || c.Ghost != null)
                {
                    ghost = c;
                    continue;
                }
                if (c.data != null && !c.data.dead)
                    living = c;
                else
                    any = c;
            }
            if (ghost != null)
                return ghost;
            if (living != null)
                return living;
            return any;
        }

        private static bool IsPlayerScout(Character c)
        {
            try
            {
                if (c == null)
                    return false;
                if (c.isBot)
                    return false;
                if (c.data != null && c.data.isScoutmaster)
                    return false;
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static int ActorOf(Character c)
        {
            try
            {
                if (c != null && c.photonView != null && c.photonView.Owner != null)
                    return c.photonView.Owner.ActorNumber;
            }
            catch
            {
            }
            return 0;
        }

        internal static Character FindScoutmasterBody()
        {
            Scoutmaster sm;
            if (!Scoutmaster.GetPrimaryScoutmaster(out sm) || sm == null)
                return null;
            if (sm.character != null)
                return sm.character;
            return sm.GetComponent<Character>();
        }

        private static void PlaceCam(Camera cam, Character target)
        {
            Camera main = Camera.main;
            Transform look = LookTransform(target);
            if (look != null)
            {
                cam.transform.position = look.position + look.forward * 1.7f + Vector3.up * 0.12f;
                cam.transform.LookAt(look.position + Vector3.up * 0.05f);
                cam.fieldOfView = 48f;
                cam.cullingMask = MaskFor(main, target);
                return;
            }
            if (main == null)
                return;
            cam.transform.position = main.transform.position + main.transform.forward * 0.28f;
            cam.transform.rotation = main.transform.rotation;
            cam.fieldOfView = 48f;
            cam.cullingMask = MaskFor(main, target);
        }

        private static int MaskFor(Camera main, Character target)
        {
            int mask = main != null ? main.cullingMask : ~0;
            return mask | BodyLayers(target);
        }

        private static int BodyLayers(Character target)
        {
            int mask = 0;
            if (target == null)
                return mask;
            try
            {
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null)
                        mask |= 1 << renderers[i].gameObject.layer;
                }
                if (target.Ghost != null && target.Ghost.transform != null)
                {
                    Renderer[] ghost = target.Ghost.transform.GetComponentsInChildren<Renderer>(true);
                    for (int i = 0; i < ghost.Length; i++)
                    {
                        if (ghost[i] != null)
                            mask |= 1 << ghost[i].gameObject.layer;
                    }
                }
            }
            catch
            {
            }
            return mask;
        }

        private static void PlaceChaseCam(Camera cam, Character hunter, Character prey)
        {
            Transform from = LookTransform(hunter);
            Transform at = LookTransform(prey);
            Camera main = Camera.main;
            if (from != null && at != null)
            {
                Vector3 face = from.position + Vector3.up * 0.12f;
                Vector3 aim = at.position + Vector3.up * 0.2f;
                Vector3 toPrey = aim - face;
                if (toPrey.sqrMagnitude < 0.04f)
                    toPrey = from.forward;
                toPrey.Normalize();
                cam.transform.position = face + toPrey * 1.25f + Vector3.up * 0.15f;
                cam.transform.LookAt(aim);
                cam.fieldOfView = 52f;
                cam.cullingMask = MaskFor(main, hunter) | BodyLayers(prey);
                return;
            }
            PlaceCam(cam, prey != null ? prey : hunter);
        }

        private static bool ScoutmasterGrabbedPrey()
        {
            try
            {
                Scoutmaster sm;
                if (!Scoutmaster.GetPrimaryScoutmaster(out sm) || sm == null)
                    return false;
                if (sm.isThrowing)
                    return true;
                Character body = sm.character != null ? sm.character : sm.GetComponent<Character>();
                if (body == null || body.data == null)
                    return false;
                if (body.data.grabbedPlayer != null)
                    return true;
                Character prey = _chasePrey != null ? _chasePrey : Character.localCharacter;
                if (prey != null && body.data.carriedPlayer == prey)
                    return true;
            }
            catch
            {
            }
            return false;
        }

        private static Transform LookTransform(Character target)
        {
            if (target == null)
                return null;
            try
            {
                if (target.Ghost != null && target.Ghost.transform != null)
                    return target.Ghost.transform;
            }
            catch
            {
            }
            try
            {
                if (target.refs != null && target.refs.head != null)
                    return target.refs.head.transform;
            }
            catch
            {
            }
            return target.transform;
        }
    }
}
