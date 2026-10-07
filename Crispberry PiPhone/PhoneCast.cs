using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Draws a live copy of this player's phone onto a flight board or a screen
    /// a mod registered. The phone itself stays put. Only me keeps the copy on
    /// this PC. Let others watch sends a short description, and their phone
    /// builds the same screen from the apps they already have.
    /// Either way the screen is reserved so nobody else can cast to it.
    /// </summary>
    internal sealed class PhoneCast : MonoBehaviour
    {
        private const float LookRange = 12f;
        private const float NearRange = 5f;
        private const int Chunk = 6000;
        private const int MaxJpg = 180000;
        private const float CaptureInterval = 1f / 30f;

        private static PhoneCast _instance;
        private static readonly List<PiPhoneCastDevice> Devices = new List<PiPhoneCastDevice>();
        private static readonly Dictionary<string, int> Owners = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Display> Shows = new Dictionary<string, Display>(StringComparer.Ordinal);
        private static readonly Dictionary<string, FrameBuf> Frames = new Dictionary<string, FrameBuf>(StringComparer.Ordinal);
        private static bool _builtins;
        private static bool _casting;
        private static string _deviceId = string.Empty;
        private static bool _waiting;
        private static string _waitId = string.Empty;
        private static float _waitUntil;
        private static int _seq;
        private static float _nextAnnounce;
        private static float _netWait;
        private static int _netHash;
        private static Coroutine _routine;
        private static float _nextCapture;
        private static bool _sharePick;
        private static bool _share;
        private static float _nextState;
        private static float _nextPose;
        private static string _stateSig = string.Empty;
        private static string _statePose = string.Empty;
        private static string _watchId = string.Empty;
        private static readonly HashSet<int> _watcherIds = new HashSet<int>();
        private static readonly Dictionary<string, bool> Shared = new Dictionary<string, bool>(StringComparer.Ordinal);
        private static Shader _shader;
        private static bool _shaderLogged;

        private static float _aspectW;
        private static float _aspectH;
        private static Mesh _quad;
        private static GameObject _picker;
        private static bool _copyFailed;
        private static Camera _grabCam;
        private static RenderTexture _grabRt;
        private static readonly List<Transform> _layerNodes = new List<Transform>();
        private static readonly List<int> _layerSaved = new List<int>();

        private sealed class Display
        {
            public string Id;
            public GameObject Host;
            public Renderer Quad;
            public Texture2D Tex;
            public Texture2D Picture;
            public Renderer Source;
            public Material[] OriginalMats;
            public Material ScreenMat;
            public RenderTexture Screen;
            public float FaceW;
            public float FaceH;
            public float ScreenAspect;
        }

        private sealed class FrameBuf
        {
            public int Seq;
            public int Count;
            public byte[][] Parts;
            public int Got;
        }

        public static bool IsOn
        {
            get { return _casting; }
        }

        public static void Ensure()
        {
            if (_instance == null)
            {
                var go = new GameObject("PiP_Cast");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<PhoneCast>();
            }
            if (_builtins)
                return;
            _builtins = true;
            Register(Board("peak.airport.flightboard", "Flight Board"));
            Register(Board("peak.airport.flightboard.1", "Flight Board (1)"));
            Register(Board("peak.airport.flightboard.2", "Flight Board (2)"));
            Register(Board("peak.airport.flightboard.3", "Flight Board (3)"));
        }

        public static void Register(PiPhoneCastDevice device)
        {
            if (device == null || string.IsNullOrEmpty(device.Id))
                return;
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i] != null && Devices[i].Id == device.Id)
                {
                    Devices[i] = device;
                    return;
                }
            }
            Devices.Add(device);
        }

        public static void Unregister(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (_casting && _deviceId == id)
                Release();
            for (int i = Devices.Count - 1; i >= 0; i--)
            {
                if (Devices[i] != null && Devices[i].Id == id)
                    Devices.RemoveAt(i);
            }
        }

        public static void OnSceneLoaded()
        {
            ClosePicker();
            _discoverScene = int.MinValue;
            _waiting = false;
            if (_casting)
                Release();
            EndWatch();
            ClearShows();
            Owners.Clear();
            Shared.Clear();
            Frames.Clear();
            _watcherIds.Clear();
        }

        public static void Toggle()
        {
            Ensure();
            if (_picker != null)
            {
                ClosePicker();
                return;
            }
            OpenPicker();
        }

        public static bool ClosePicker()
        {
            if (_picker == null)
                return false;
            UnityEngine.Object.Destroy(_picker);
            _picker = null;
            return true;
        }

        private static void OpenPicker()
        {
            ClosePicker();
            List<PiPhoneCastDevice> list = Available();
            if (list.Count == 0)
            {
                PhoneMenu.Toast(PhoneLang.T("no_cast_screen", "No screen nearby"));
                return;
            }
            RectTransform bezel = PhoneMenu.BezelRt;
            if (bezel == null)
                return;
            Transform screen = bezel.Find("Screen");
            if (screen == null)
                return;
            PhoneMenu.CloseShade();
            var root = PhoneUi.CreateImage(screen, "CastList", PhoneUi.White(), new Color(0.06f, 0.07f, 0.09f, 0.98f));
            root.GetComponent<Image>().raycastTarget = true;
            PhoneUi.Stretch(root, 0f, 0f);
            root.SetAsLastSibling();
            _picker = root.gameObject;
            PhoneUi.AddVertical(_picker, 8f, new RectOffset(16, 16, 36, 16));
            var head = new GameObject("Head", typeof(RectTransform));
            head.transform.SetParent(root, false);
            PhoneUi.Size(head, 40f);
            var headRow = PhoneUi.AddHorizontal(head, 8f);
            headRow.childAlignment = TextAnchor.MiddleLeft;
            headRow.childForceExpandWidth = false;
            headRow.childForceExpandHeight = false;
            PhoneUi.MaterialChip(head.transform, "arrow_back", "Back", () => ClosePicker(), new Vector2(36f, 32f));
            var title = PhoneUi.CreateLabel(head.transform, "Title", PhoneLang.T("screen_cast", "Screen cast"), 20f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            var titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;
            titleLe.preferredHeight = 36f;
            titleLe.minHeight = 36f;
            var modeGo = new GameObject("Who", typeof(RectTransform));
            modeGo.transform.SetParent(root, false);
            PhoneUi.AddHorizontal(modeGo, 8f);
            var modeRow = modeGo.GetComponent<HorizontalLayoutGroup>();
            modeRow.childAlignment = TextAnchor.MiddleCenter;
            modeRow.childForceExpandWidth = false;
            modeRow.childForceExpandHeight = false;
            var modeLe = modeGo.AddComponent<LayoutElement>();
            modeLe.preferredHeight = 44f;
            modeLe.minHeight = 44f;
            Button onlyMe = PhoneUi.CreateButton(modeGo.transform, PhoneLang.T("cast_only_me", "Only me"), () =>
            {
                _sharePick = false;
                OpenPicker();
            }, new Vector2(130f, 36f));
            Button letWatch = PhoneUi.CreateButton(modeGo.transform, PhoneLang.T("cast_let_watch", "Let others watch"), () =>
            {
                _sharePick = true;
                OpenPicker();
            }, new Vector2(160f, 36f));
            Image onlyImg = onlyMe.GetComponent<Image>();
            Image watchImg = letWatch.GetComponent<Image>();
            if (onlyImg != null)
                onlyImg.color = _sharePick ? PhoneUi.SurfaceAlt : new Color(0.16f, 0.42f, 0.30f, 1f);
            if (watchImg != null)
                watchImg.color = _sharePick ? new Color(0.16f, 0.42f, 0.30f, 1f) : PhoneUi.SurfaceAlt;
            RectTransform rows;
            ScrollRect scroll = PhoneUi.CreateScrollView(root, out rows);
            var scrollLe = scroll.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
            scrollLe.minHeight = 80f;
            var fit = rows.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            PhoneUi.AddVertical(rows.gameObject, 8f, new RectOffset(0, 0, 0, 8));
            for (int i = 0; i < list.Count; i++)
            {
                PiPhoneCastDevice device = list[i];
                string id = device.Id;
                string label = string.IsNullOrEmpty(device.Name) ? id : device.Name;
                bool mine = _casting && _deviceId == id;
                bool taken = OwnedByOther(id);
                bool shared = IsShared(id);
                bool watching = _watchId == id;
                if (mine)
                    label = label + "  ·  " + PhoneLang.T("cast_on", "On");
                else if (watching)
                    label = label + "  ·  " + PhoneLang.T("cast_watching", "Watching");
                else if (taken && shared)
                    label = label + "  ·  " + PhoneLang.T("cast_watch", "Watch");
                else if (taken)
                    label = label + "  ·  " + PhoneLang.T("cast_in_use", "In use");
                Button btn = PhoneUi.CreateButton(rows, label, () =>
                {
                    if (!mine && taken && shared)
                        ToggleWatch(id);
                    else
                        Choose(id);
                }, new Vector2(280f, 48f));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 48f;
                le.minHeight = 48f;
                if (mine || watching)
                {
                    Image img = btn.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0.16f, 0.42f, 0.30f, 1f);
                }
            }
        }

        private static List<PiPhoneCastDevice> Available()
        {
            Discover(true);
            var list = new List<PiPhoneCastDevice>();
            for (int i = 0; i < Devices.Count; i++)
            {
                PiPhoneCastDevice device = Devices[i];
                if (device == null || string.IsNullOrEmpty(device.Id))
                    continue;
                if (SafeFind(device) == null)
                    continue;
                list.Add(device);
            }
            return list;
        }

        private static void Choose(string id)
        {
            ClosePicker();
            if (string.IsNullOrEmpty(id))
                return;
            if (OwnedByOther(id))
            {
                PhoneMenu.Toast(PhoneLang.T("cast_in_use", "That screen is in use"));
                return;
            }
            if (_casting && _deviceId == id)
            {
                Release();
                return;
            }
            if (_casting)
                Release();
            StartCast(id);
        }

        private static void StartCast(string id)
        {
            bool share = _sharePick;
            bool room = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
            if (!room)
            {
                _share = false;
                BeginLocal(id);
                return;
            }
            if (PhotonNetwork.IsMasterClient)
            {
                if (!MasterClaim(id, Actor()))
                {
                    PhoneMenu.Toast(PhoneLang.T("cast_in_use", "That screen is in use"));
                    return;
                }
                PhoneNet.SendCastGrant(id, Actor(), share);
                _share = share;
                _stateSig = string.Empty;
                _statePose = string.Empty;
                _nextState = 0f;
                _nextPose = 0f;
                BeginLocal(id);
                return;
            }
            _waiting = true;
            _waitId = id;
            _waitUntil = Time.unscaledTime + 2.5f;
            PhoneNet.SendCastAsk(id, share);
        }

        public static void Release()
        {
            _waiting = false;
            string id = _deviceId;
            bool was = _casting;
            if (_parked)
            {
                _parked = false;
                PhoneMenu.ReturnFromCast();
            }
            StopRoutine();
            _casting = false;
            _deviceId = string.Empty;
            if (was && !string.IsNullOrEmpty(id))
            {
                DropShow(id);
                int owner;
                if (Owners.TryGetValue(id, out owner) && owner == Actor())
                {
                    Owners.Remove(id);
                    if (PhotonNetwork.InRoom)
                        PhoneNet.SendCastStop(id, Actor());
                }
            }
            _share = false;
            _stateSig = string.Empty;
            _statePose = string.Empty;
            _watcherIds.Clear();
            if (!string.IsNullOrEmpty(id))
                Shared.Remove(id);
            PhoneMenu.RefreshShade();
        }

        public static void OnAsk(int actor, string id, bool share)
        {
            if (!PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(id) || actor <= 0)
                return;
            if (!MasterClaim(id, actor))
            {
                PhoneNet.SendCastDeny(actor, id);
                return;
            }
            ApplyGrant(id, actor, share);
            PhoneNet.SendCastGrant(id, actor, share);
        }

        public static void OnGrant(string id, int actor, bool share)
        {
            if (string.IsNullOrEmpty(id) || actor <= 0)
                return;
            ApplyGrant(id, actor, share);
        }

        public static void OnDeny(string id)
        {
            if (_waiting && (string.IsNullOrEmpty(id) || id == _waitId))
            {
                _waiting = false;
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
            }
        }

        public static void OnStop(string id, int actor)
        {
            if (string.IsNullOrEmpty(id))
                return;
            int owner;
            if (Owners.TryGetValue(id, out owner) && owner != actor && actor > 0)
                return;
            Owners.Remove(id);
            Frames.Remove(id);
            if (_casting && _deviceId == id && actor == Actor())
            {
                StopRoutine();
                _casting = false;
                _deviceId = string.Empty;
                PhoneMenu.RefreshShade();
            }
            DropShow(id);
            Shared.Remove(id);
            if (_watchId == id)
                EndWatch();
        }

        public static void OnFrame(string id, int actor, int seq, int index, int count, byte[] chunk)
        {
            if (string.IsNullOrEmpty(id) || chunk == null || count <= 0 || index < 0 || index >= count)
                return;
            if (actor == Actor())
                return;
            int owner;
            if (!Owners.TryGetValue(id, out owner) || owner != actor)
                return;
            FrameBuf buf;
            if (!Frames.TryGetValue(id, out buf) || buf == null || buf.Seq != seq || buf.Count != count || buf.Parts == null)
            {
                buf = new FrameBuf();
                buf.Seq = seq;
                buf.Count = count;
                buf.Parts = new byte[count][];
                buf.Got = 0;
                Frames[id] = buf;
            }
            if (buf.Parts[index] != null)
                return;
            buf.Parts[index] = chunk;
            buf.Got++;
            if (buf.Got < count)
                return;
            int len = 0;
            for (int i = 0; i < count; i++)
            {
                if (buf.Parts[i] == null)
                    return;
                len += buf.Parts[i].Length;
            }
            var jpg = new byte[len];
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                Buffer.BlockCopy(buf.Parts[i], 0, jpg, at, buf.Parts[i].Length);
                at += buf.Parts[i].Length;
            }
            Frames.Remove(id);
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!PhoneImages.LoadImage(tex, jpg))
            {
                UnityEngine.Object.Destroy(tex);
                return;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Show(id, tex);
        }

        private void Update()
        {
            if (_waiting && Time.unscaledTime >= _waitUntil)
            {
                _waiting = false;
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
            }
            if (_casting && !string.IsNullOrEmpty(_deviceId) && PhotonNetwork.InRoom && Time.unscaledTime >= _nextAnnounce)
            {
                _nextAnnounce = Time.unscaledTime + 3f;
                if (PhotonNetwork.IsMasterClient)
                    PhoneNet.SendCastGrant(_deviceId, Actor(), _share);
                else if (PhotonNetwork.MasterClient != null)
                    PhoneNet.SendCastAsk(_deviceId, _share);
            }
        }

        private void LateUpdate()
        {
            PublishWatch();
        }

        private static int _pictureHides;

        internal static void SuspendPicture()
        {
            _pictureHides++;
            if (_pictureHides == 1)
                SetPictureShown(false);
        }

        internal static void ResumePicture()
        {
            if (_pictureHides <= 0)
                return;
            _pictureHides--;
            if (_pictureHides == 0)
                SetPictureShown(true);
        }

        private static void SetPictureShown(bool shown)
        {
            foreach (var kv in Shows)
            {
                Display show = kv.Value;
                if (show != null && show.Quad != null)
                    show.Quad.enabled = shown;
            }
            PhoneMenu.SetCastCanvasDrawn(shown);
        }

        private static void ApplyGrant(string id, int actor, bool share)
        {
            Owners[id] = actor;
            int me = Actor();
            Shared[id] = share;
            if (actor != me)
            {
                if (!share && _watchId == id)
                    EndWatch();
                return;
            }
            _waiting = false;
            _share = share;
            if (_casting)
                return;
            _stateSig = string.Empty;
            _statePose = string.Empty;
            _nextState = 0f;
            _nextPose = 0f;
            BeginLocal(id);
        }

        private static bool MasterClaim(string id, int actor)
        {
            ForgetMissingOwners();
            int owner;
            if (Owners.TryGetValue(id, out owner) && owner != actor && owner > 0 && PlayerHere(owner))
                return false;
            Owners[id] = actor;
            return true;
        }

        public static void SetAspect(float width, float height)
        {
            if (width <= 0.01f || height <= 0.01f)
            {
                ClearAspect();
                return;
            }
            _aspectW = width;
            _aspectH = height;
        }

        public static void ClearAspect()
        {
            _aspectW = 0f;
            _aspectH = 0f;
        }

        private static void ContentRatio(Texture phone, out float width, out float height)
        {
            if (_aspectW > 0.01f && _aspectH > 0.01f)
            {
                width = _aspectW;
                height = _aspectH;
                return;
            }
            PiPhoneApp app = PhoneMenu.OpenApp();
            if (app != null && app.CastAspectWidth > 0.01f && app.CastAspectHeight > 0.01f)
            {
                width = app.CastAspectWidth;
                height = app.CastAspectHeight;
                return;
            }
            width = phone != null ? Mathf.Max(1, phone.width) : 1f;
            height = phone != null ? Mathf.Max(1, phone.height) : 1f;
        }

        private static void BeginLocal(string id)
        {
            if (!EnsureShow(id))
            {
                Owners.Remove(id);
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
                return;
            }
            _casting = true;
            _deviceId = id;
            _seq = 0;
            _netWait = 0f;
            _netHash = 0;
            _nextAnnounce = Time.unscaledTime + 2f;
            StopRoutine();
            _nextCapture = 0f;
            if (_instance != null)
                _routine = _instance.StartCoroutine(_instance.CaptureLoop());
            PhoneMenu.RefreshShade();
            Plugin.LogInfo("Casting to " + id);
        }

        private IEnumerator CaptureLoop()
        {
            while (_casting)
            {
                float wait = _nextCapture - Time.unscaledTime;
                if (wait > 0.01f)
                    yield return new WaitForSecondsRealtime(wait);
                yield return new WaitForEndOfFrame();
                if (!_casting)
                    yield break;
                if (Time.unscaledTime < _nextCapture)
                    continue;
                _nextCapture = Time.unscaledTime + CaptureInterval;
                PaintLive(_deviceId);
            }
        }

        private static int NextSeq()
        {
            _seq++;
            if (_seq > 1000000)
                _seq = 1;
            return _seq;
        }

        private static bool _parked;

        public static bool PhoneIsOnBoard
        {
            get { return _parked; }
        }

        public static bool TrySpeakerPoint(out Vector3 point)
        {
            Display show;
            if (_casting && Shows.TryGetValue(_deviceId, out show) && show != null && show.Host != null)
            {
                point = show.Host.transform.position;
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        public static bool UseBoard(bool on)
        {
            if (!on)
            {
                if (!_parked)
                    return true;
                _parked = false;
                PhoneMenu.ReturnFromCast();
                _nextCapture = 0f;
                if (_casting && _instance != null && _routine == null)
                    _routine = _instance.StartCoroutine(_instance.CaptureLoop());
                return true;
            }
            if (!_casting)
                return false;
            if (PhoneMenu.IsPhoneMounted)
                return false;
            Display show;
            if (!Shows.TryGetValue(_deviceId, out show) || show == null || show.Host == null)
                return false;
            StopRoutine();
            _parked = true;
            if (show.Quad != null)
                show.Quad.enabled = true;
            if (show.ScreenMat != null)
            {
                show.ScreenMat.mainTexture = null;
                show.ScreenMat.color = Color.black;
            }
            PhoneMenu.PlaceOnCast(show.Host.transform, show.FaceW, show.FaceH);
            Plugin.LogInfo("Cast is the phone on " + _deviceId);
            return true;
        }

        private static void PaintLive(string id)
        {
            if (_parked)
                return;
            Display show;
            if (!Shows.TryGetValue(id, out show) || show == null || show.ScreenMat == null)
                return;
            EnsureScreenTarget(show);
            RenderTexture phone = RenderLive();
            if (phone == null)
            {
                ClearTarget(show.Screen);
                show.ScreenMat.mainTexture = show.Screen;
                return;
            }
            float phoneAspect = phone.height > 0 ? phone.width / (float)phone.height : 1f;
            Letterbox(phone, show.Screen, phoneAspect, show.ScreenAspect);
            show.ScreenMat.mainTexture = show.Screen;
            show.ScreenMat.color = Color.white;
        }

        private static bool CastIsPrivate()
        {
            if (CallService.IsBusy)
                return true;
            PiPhoneApp app = PhoneMenu.OpenApp();
            if (app == null || string.IsNullOrEmpty(app.Id))
                return false;
            string id = app.Id;
            return id == BuiltinApps.MessagesId
                || id == BuiltinApps.PhoneId
                || id == BuiltinApps.VoicemailId
                || id == BuiltinApps.NotesId
                || id == BuiltinApps.VoiceMemosId;
        }

        private static void EnsureScreenTarget(Display show)
        {
            int w;
            int h;
            FitPixels(show.ScreenAspect, 960, out w, out h);
            if (show.Screen != null && show.Screen.width == w && show.Screen.height == h)
                return;
            if (show.Screen != null)
            {
                show.Screen.Release();
                UnityEngine.Object.Destroy(show.Screen);
            }
            show.Screen = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            show.Screen.wrapMode = TextureWrapMode.Clamp;
            show.Screen.filterMode = FilterMode.Bilinear;
            show.Screen.Create();
        }

        private static void FitPixels(float aspect, int longSide, out int w, out int h)
        {
            if (aspect < 0.05f)
                aspect = 1f;
            if (aspect >= 1f)
            {
                w = longSide;
                h = Mathf.Max(8, Mathf.RoundToInt(longSide / aspect));
            }
            else
            {
                h = longSide;
                w = Mathf.Max(8, Mathf.RoundToInt(longSide * aspect));
            }
        }

        private static void ClearTarget(RenderTexture rt)
        {
            if (rt == null)
                return;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
        }

        private static void Letterbox(RenderTexture phone, RenderTexture screen, float phoneAspect, float screenAspect)
        {
            if (phone == null || screen == null)
                return;
            float sx = 1f;
            float sy = 1f;
            if (phoneAspect > 0.05f && screenAspect > 0.05f)
            {
                if (phoneAspect < screenAspect)
                    sx = phoneAspect / screenAspect;
                else
                    sy = screenAspect / phoneAspect;
            }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = screen;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, screen.width, 0f, screen.height);
            GL.Clear(true, true, Color.black);
            float dw = screen.width * sx;
            float dh = screen.height * sy;
            Graphics.DrawTexture(new Rect((screen.width - dw) * 0.5f, (screen.height - dh) * 0.5f, dw, dh), phone);
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        private static RenderTexture RenderLive()
        {
            Canvas canvas = PhoneMenu.RootCanvas;
            RectTransform bezel = PhoneMenu.BezelRt;
            if (canvas == null || bezel == null || !canvas.gameObject.activeInHierarchy)
                return null;
            if (canvas.renderMode == RenderMode.WorldSpace)
                return RenderWorld(canvas, bezel);
            return RenderOverlay(canvas, bezel);
        }

        private static RenderTexture RenderOverlay(Canvas canvas, RectTransform bezel)
        {
            float bw = Mathf.Abs(bezel.rect.width);
            float bh = Mathf.Abs(bezel.rect.height);
            if (bw < 8f)
                bw = Mathf.Abs(bezel.sizeDelta.x);
            if (bh < 8f)
                bh = Mathf.Abs(bezel.sizeDelta.y);
            if (bw < 8f || bh < 8f)
                return null;
            int w;
            int h;
            FitPixels(bw / bh, 960, out w, out h);
            RenderTexture rt = EnsureGrabRt(w, h);
            Camera cam = EnsureGrabCam();
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = h * 0.5f;
            cam.aspect = w / (float)h;
            cam.pixelRect = new Rect(0f, 0f, w, h);
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << 31;

            RenderMode prevMode = canvas.renderMode;
            Camera prevCam = canvas.worldCamera;
            float prevDist = canvas.planeDistance;
            var scaler = canvas.GetComponent<CanvasScaler>();
            bool scalerOn = scaler != null && scaler.enabled;
            Vector2 anchorMin = bezel.anchorMin;
            Vector2 anchorMax = bezel.anchorMax;
            Vector2 pivot = bezel.pivot;
            Vector2 anchored = bezel.anchoredPosition;
            Vector2 size = bezel.sizeDelta;
            Vector3 scale = bezel.localScale;
            PushLayers(canvas.transform, 31);
            PhoneMenu.ShowHardware(false);
            try
            {
                if (scaler != null)
                    scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                bezel.anchorMin = Vector2.zero;
                bezel.anchorMax = Vector2.one;
                bezel.pivot = new Vector2(0.5f, 0.5f);
                bezel.anchoredPosition = Vector2.zero;
                bezel.sizeDelta = Vector2.zero;
                bezel.localScale = Vector3.one;
                Canvas.ForceUpdateCanvases();
                cam.enabled = true;
                cam.Render();
            }
            finally
            {
                cam.enabled = false;
                bezel.anchorMin = anchorMin;
                bezel.anchorMax = anchorMax;
                bezel.pivot = pivot;
                bezel.anchoredPosition = anchored;
                bezel.sizeDelta = size;
                bezel.localScale = scale;
                canvas.renderMode = prevMode;
                canvas.worldCamera = prevCam;
                canvas.planeDistance = prevDist;
                if (scaler != null)
                    scaler.enabled = scalerOn;
                PopLayers();
                if (!PhoneMenu.OnCast)
                    PhoneMenu.ShowHardware(true);
                Canvas.ForceUpdateCanvases();
            }
            return rt;
        }

        private static RenderTexture RenderWorld(Canvas canvas, RectTransform bezel)
        {
            var corners = new Vector3[4];
            bezel.GetWorldCorners(corners);
            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);
            if (width < 0.01f || height < 0.01f)
                return null;
            int w;
            int h;
            FitPixels(width / height, 960, out w, out h);
            RenderTexture rt = EnsureGrabRt(w, h);
            Camera cam = EnsureGrabCam();
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = height * 0.5f;
            cam.aspect = width / height;
            cam.pixelRect = new Rect(0f, 0f, w, h);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]);
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.forward;
            normal.Normalize();
            Camera view = Camera.main;
            if (view != null && Vector3.Dot(normal, view.transform.position - center) < 0f)
                normal = -normal;
            cam.transform.position = center + normal * 0.25f;
            cam.transform.rotation = Quaternion.LookRotation(-normal, (corners[1] - corners[0]).normalized);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            PushLayers(canvas.transform, 31);
            cam.cullingMask = 1 << 31;
            PhoneMenu.ShowHardware(false);
            try
            {
                cam.enabled = true;
                cam.Render();
            }
            finally
            {
                cam.enabled = false;
                PopLayers();
                if (!PhoneMenu.OnCast)
                    PhoneMenu.ShowHardware(true);
            }
            return rt;
        }

        private static Texture2D Grab()
        {
            RectTransform bezel = PhoneMenu.BezelRt;
            Canvas canvas = PhoneMenu.RootCanvas;
            if (bezel == null || canvas == null || !bezel.gameObject.activeInHierarchy)
                return null;
            if (canvas.renderMode == RenderMode.WorldSpace)
                return GrabFramed(canvas, bezel);
            var corners = new Vector3[4];
            bezel.GetWorldCorners(corners);
            return ReadBezel(null, corners);
        }

        private static Texture2D GrabOverlay(Canvas canvas, RectTransform bezel)
        {
            int sw = Mathf.Max(8, Screen.width);
            int sh = Mathf.Max(8, Screen.height);
            RenderTexture rt = EnsureGrabRt(sw, sh);
            Camera cam = EnsureGrabCam();
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = sh * 0.5f;
            cam.aspect = sw / (float)sh;
            cam.pixelRect = new Rect(0f, 0f, sw, sh);
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;

            RenderMode prevMode = canvas.renderMode;
            Camera prevCam = canvas.worldCamera;
            float prevDist = canvas.planeDistance;
            bool switched = prevMode == RenderMode.ScreenSpaceOverlay;
            PushLayers(canvas.transform, 31);
            cam.cullingMask = 1 << 31;
            var corners = new Vector3[4];
            try
            {
                if (switched)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = cam;
                    canvas.planeDistance = 10f;
                }
                Canvas.ForceUpdateCanvases();
                bezel.GetWorldCorners(corners);
                cam.enabled = true;
                cam.Render();
                for (int i = 0; i < 4; i++)
                    corners[i] = cam.WorldToScreenPoint(corners[i]);
            }
            finally
            {
                cam.enabled = false;
                if (switched)
                {
                    canvas.renderMode = prevMode;
                    canvas.worldCamera = prevCam;
                    canvas.planeDistance = prevDist;
                }
                PopLayers();
            }
            return ReadBezel(rt, corners);
        }

        private static Texture2D GrabFramed(Canvas canvas, RectTransform bezel)
        {
            var corners = new Vector3[4];
            bezel.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);
            if (width < 0.01f || height < 0.01f)
                return null;
            int w = Mathf.Clamp(Mathf.RoundToInt(width * 200f), 64, 1280);
            int h = Mathf.Clamp(Mathf.RoundToInt(height * 200f), 64, 1280);
            RenderTexture rt = EnsureGrabRt(w, h);
            Camera cam = EnsureGrabCam();
            cam.targetTexture = rt;
            cam.orthographic = true;
            cam.orthographicSize = height * 0.5f;
            cam.aspect = width / height;
            cam.pixelRect = new Rect(0f, 0f, w, h);
            Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]);
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.forward;
            normal.Normalize();
            Camera view = Camera.main;
            if (view != null && Vector3.Dot(normal, view.transform.position - center) < 0f)
                normal = -normal;
            cam.transform.position = center + normal * 2f;
            cam.transform.rotation = Quaternion.LookRotation(-normal, (corners[1] - corners[0]).normalized);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 20f;
            PushLayers(canvas.transform, 31);
            cam.cullingMask = 1 << 31;
            try
            {
                cam.enabled = true;
                cam.Render();
            }
            finally
            {
                cam.enabled = false;
                PopLayers();
            }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            tex.Apply(false);
            RenderTexture.active = prev;
            return tex;
        }

        private static Texture2D ReadBezel(RenderTexture rt, Vector3[] corners)
        {
            float xMin = Mathf.Min(corners[0].x, corners[2].x);
            float xMax = Mathf.Max(corners[0].x, corners[2].x);
            float yMin = Mathf.Min(corners[0].y, corners[2].y);
            float yMax = Mathf.Max(corners[0].y, corners[2].y);
            int w = Mathf.RoundToInt(xMax - xMin);
            int h = Mathf.RoundToInt(yMax - yMin);
            if (w < 8 || h < 8)
                return null;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            int rx = Mathf.RoundToInt(xMin);
            int ry = Mathf.RoundToInt(yMin);
            int srcX = Mathf.Max(0, rx);
            int srcY = Mathf.Max(0, ry);
            int dstX = srcX - rx;
            int dstY = srcY - ry;
            int rtW = rt != null ? rt.width : Screen.width;
            int rtH = rt != null ? rt.height : Screen.height;
            int copyW = Mathf.Min(rtW, rx + w) - srcX;
            int copyH = Mathf.Min(rtH, ry + h) - srcY;
            var black = new Color32[w * h];
            var solid = new Color32(0, 0, 0, 255);
            for (int i = 0; i < black.Length; i++)
                black[i] = solid;
            tex.SetPixels32(black);
            if (copyW > 1 && copyH > 1)
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var part = new Texture2D(copyW, copyH, TextureFormat.RGB24, false);
                part.ReadPixels(new Rect(srcX, srcY, copyW, copyH), 0, 0);
                part.Apply(false);
                RenderTexture.active = prev;
                tex.SetPixels(dstX, dstY, copyW, copyH, part.GetPixels());
                UnityEngine.Object.Destroy(part);
            }
            tex.Apply(false);
            return tex;
        }

        private static RenderTexture EnsureGrabRt(int width, int height)
        {
            if (_grabRt != null && (_grabRt.width != width || _grabRt.height != height))
            {
                _grabRt.Release();
                UnityEngine.Object.Destroy(_grabRt);
                _grabRt = null;
            }
            if (_grabRt == null)
            {
                _grabRt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                _grabRt.wrapMode = TextureWrapMode.Clamp;
                _grabRt.filterMode = FilterMode.Bilinear;
                _grabRt.Create();
            }
            return _grabRt;
        }

        private static Camera EnsureGrabCam()
        {
            if (_grabCam != null)
                return _grabCam;
            var go = new GameObject("PiP_CastCam");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            _grabCam = go.AddComponent<Camera>();
            _grabCam.enabled = false;
            _grabCam.clearFlags = CameraClearFlags.SolidColor;
            _grabCam.backgroundColor = new Color(0f, 0f, 0f, 1f);
            _grabCam.cullingMask = 1 << 31;
            _grabCam.allowHDR = false;
            _grabCam.allowMSAA = false;
            return _grabCam;
        }

        private static void PushLayers(Transform root, int layer)
        {
            _layerNodes.Clear();
            _layerSaved.Clear();
            PushLayer(root, layer);
        }

        private static void PushLayer(Transform t, int layer)
        {
            if (t == null)
                return;
            _layerNodes.Add(t);
            _layerSaved.Add(t.gameObject.layer);
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                PushLayer(t.GetChild(i), layer);
        }

        private static void PopLayers()
        {
            int count = Mathf.Min(_layerNodes.Count, _layerSaved.Count);
            for (int i = 0; i < count; i++)
            {
                if (_layerNodes[i] != null)
                    _layerNodes[i].gameObject.layer = _layerSaved[i];
            }
            _layerNodes.Clear();
            _layerSaved.Clear();
        }

        private static bool EnsureShow(string id)
        {
            Display have;
            if (Shows.TryGetValue(id, out have) && have != null && (have.ScreenMat != null || (have.Host != null)))
                return true;
            PiPhoneCastDevice device = FindDevice(id);
            Transform board = device != null ? SafeFind(device) : null;
            if (board == null)
                return false;
            Renderer source;
            int slot;
            if (TryScreen(board, out source, out slot))
            {
                Display show = MakeQuad(id, source, slot);
                if (show == null)
                    return false;
                Shows[id] = show;
                Plugin.LogInfo("Cast local on " + board.name + " aspect " + show.ScreenAspect.ToString("0.00"));
                return true;
            }
            Renderer fallback = Largest(board);
            if (fallback == null)
                return false;
            Display placed = MakeQuad(id, fallback, -1);
            if (placed == null)
                return false;
            Shows[id] = placed;
            Plugin.LogInfo("Cast has no screen material on " + board.name + ". Using the board face. aspect " + placed.ScreenAspect.ToString("0.00"));
            return true;
        }

        private static Display MakeQuad(string id, Renderer source, int slot)
        {
            Shader shader = CastShader();
            if (shader == null || source == null)
                return null;
            var host = new GameObject("PiPhoneCast");
            var quadGo = new GameObject("Quad", typeof(MeshFilter), typeof(MeshRenderer));
            var filter = quadGo.GetComponent<MeshFilter>();
            filter.sharedMesh = CastQuad();
            quadGo.transform.SetParent(host.transform, false);
            Bounds local = ScreenBounds(source, slot);
            float faceW;
            float faceH;
            FitBounds(source.transform, local, host, quadGo.transform, out faceW, out faceH);
            var show = new Display
            {
                Id = id,
                Host = host,
                Quad = quadGo.GetComponent<Renderer>(),
                FaceW = faceW,
                FaceH = faceH,
                ScreenAspect = faceH > 0.0001f ? faceW / faceH : 1.6f
            };
            show.Quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            show.Quad.receiveShadows = false;
            show.ScreenMat = new Material(shader);
            MakeOpaque(show.ScreenMat);
            show.ScreenMat.renderQueue = 4000;
            show.Quad.sharedMaterial = show.ScreenMat;
            int layer = source.gameObject.layer;
            host.layer = layer;
            quadGo.layer = layer;
            return show;
        }

        private static void Show(string id, Texture2D tex)
        {
            if (tex == null)
                return;
            if (!EnsureShow(id))
            {
                UnityEngine.Object.Destroy(tex);
                return;
            }
            Display show = Shows[id];
            if (show.Tex != null && show.Tex != tex)
                UnityEngine.Object.Destroy(show.Tex);
            show.Tex = tex;
            if (show.ScreenMat != null)
                PaintScreen(show, tex);
        }

        private static void PaintScreen(Display show, Texture phone)
        {
            int cw;
            int ch;
            ScreenCanvas(show, phone, out cw, out ch);
            if (show.Screen == null || show.Screen.width != cw || show.Screen.height != ch)
            {
                if (show.Screen != null)
                {
                    show.Screen.Release();
                    UnityEngine.Object.Destroy(show.Screen);
                }
                show.Screen = new RenderTexture(cw, ch, 0, RenderTextureFormat.ARGB32);
                show.Screen.wrapMode = TextureWrapMode.Clamp;
                show.Screen.filterMode = FilterMode.Bilinear;
                show.Screen.Create();
            }
            PaintFit(show.Screen, phone);
            BakePicture(show);
            AssignPicture(show.ScreenMat, show.Picture);
        }

        private static void AssignPicture(Material mat, Texture tex)
        {
            if (mat == null || tex == null)
                return;
            mat.mainTexture = tex;
            SetTex(mat, "_BaseTexture", tex);
            SetTex(mat, "_MainTex", tex);
            SetTex(mat, "_BaseMap", tex);
            SetTex(mat, "_BaseColorMap", tex);
            SetTex(mat, "_EmissionMap", tex);
            Shader shader = mat.shader;
            if (shader == null)
                return;
            int count = shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                    continue;
                string prop = shader.GetPropertyName(i);
                if (string.IsNullOrEmpty(prop))
                    continue;
                string n = prop.ToLowerInvariant();
                if (n.Contains("normal") || n.Contains("bump") || n.Contains("metal") || n.Contains("occlus") || n.Contains("mask"))
                    continue;
                if (mat.GetTexture(prop) == null && n.IndexOf("tex", StringComparison.Ordinal) < 0 && n.IndexOf("base", StringComparison.Ordinal) < 0)
                    continue;
                mat.SetTexture(prop, tex);
            }
        }

        private static void SetTex(Material mat, string prop, Texture tex)
        {
            if (mat.HasProperty(prop))
                mat.SetTexture(prop, tex);
        }

        private static void BakePicture(Display show)
        {
            int w = show.Screen.width;
            int h = show.Screen.height;
            if (show.Picture == null || show.Picture.width != w || show.Picture.height != h)
            {
                if (show.Picture != null)
                    UnityEngine.Object.Destroy(show.Picture);
                show.Picture = new Texture2D(w, h, TextureFormat.RGB24, false);
                show.Picture.wrapMode = TextureWrapMode.Clamp;
                show.Picture.filterMode = FilterMode.Bilinear;
            }
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = show.Screen;
            show.Picture.ReadPixels(new Rect(0f, 0f, w, h), 0, 0);
            show.Picture.Apply(false);
            RenderTexture.active = prev;
        }

        private static void ScreenCanvas(Display show, Texture phone, out int cw, out int ch)
        {
            float screenAspect = show.ScreenAspect > 0.05f ? show.ScreenAspect : 1.6f;
            float rw;
            float rh;
            ContentRatio(phone, out rw, out rh);
            int frameW;
            int frameH;
            FramePixels(phone, rw, rh, out frameW, out frameH);
            bool portrait = rh >= rw;
            if (portrait)
            {
                ch = frameH;
                cw = Mathf.Max(frameW, Mathf.RoundToInt(ch * screenAspect));
            }
            else
            {
                cw = frameW;
                ch = Mathf.Max(frameH, Mathf.RoundToInt(cw / Mathf.Max(0.01f, screenAspect)));
            }
            cw = Mathf.Max(8, cw);
            ch = Mathf.Max(8, ch);
        }

        private static void FramePixels(Texture phone, float rw, float rh, out int frameW, out int frameH)
        {
            int longSide = phone != null ? Mathf.Max(phone.width, phone.height) : 8;
            longSide = Mathf.Max(8, longSide);
            if (rh >= rw)
            {
                frameH = longSide;
                frameW = Mathf.Max(8, Mathf.RoundToInt(longSide * (rw / Mathf.Max(0.01f, rh))));
            }
            else
            {
                frameW = longSide;
                frameH = Mathf.Max(8, Mathf.RoundToInt(longSide * (rh / Mathf.Max(0.01f, rw))));
            }
        }

        private static void PaintFit(RenderTexture dest, Texture phone)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = dest;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = prev;
            float rw;
            float rh;
            ContentRatio(phone, out rw, out rh);
            bool portrait = rh >= rw;
            float frameAspect = rw / Mathf.Max(0.01f, rh);
            int frameW;
            int frameH;
            if (portrait)
            {
                frameH = dest.height;
                frameW = Mathf.RoundToInt(dest.height * frameAspect);
                if (frameW > dest.width)
                {
                    frameW = dest.width;
                    frameH = Mathf.Max(1, Mathf.RoundToInt(dest.width / frameAspect));
                }
            }
            else
            {
                frameW = dest.width;
                frameH = Mathf.Max(1, Mathf.RoundToInt(dest.width / frameAspect));
                if (frameH > dest.height)
                {
                    frameH = dest.height;
                    frameW = Mathf.Max(1, Mathf.RoundToInt(dest.height * frameAspect));
                }
            }
            float contain = Mathf.Min(frameW / (float)Mathf.Max(1, phone.width), frameH / (float)Mathf.Max(1, phone.height));
            int dw = Mathf.Max(1, Mathf.RoundToInt(phone.width * contain));
            int dh = Mathf.Max(1, Mathf.RoundToInt(phone.height * contain));
            int frameX = (dest.width - frameW) / 2;
            int frameY = (dest.height - frameH) / 2;
            int x = frameX + (frameW - dw) / 2;
            int y = frameY + (frameH - dh) / 2;
            BlitAt(dest, phone, dw, dh, x, y);
        }

        private static void PaintContain(RenderTexture dest, Texture phone)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = dest;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = prev;
            float contain = Mathf.Min(dest.width / (float)Mathf.Max(1, phone.width), dest.height / (float)Mathf.Max(1, phone.height));
            int dw = Mathf.Max(1, Mathf.RoundToInt(phone.width * contain));
            int dh = Mathf.Max(1, Mathf.RoundToInt(phone.height * contain));
            int x = (dest.width - dw) / 2;
            int y = (dest.height - dh) / 2;
            BlitAt(dest, phone, dw, dh, x, y);
        }

        private static void BlitAt(RenderTexture dest, Texture phone, int dw, int dh, int x, int y)
        {
            int srcX = 0;
            int srcY = 0;
            int copyW = dw;
            int copyH = dh;
            if (x < 0)
            {
                srcX = -x;
                copyW += x;
                x = 0;
            }
            if (y < 0)
            {
                srcY = -y;
                copyH += y;
                y = 0;
            }
            if (x + copyW > dest.width)
                copyW = dest.width - x;
            if (y + copyH > dest.height)
                copyH = dest.height - y;
            if (copyW < 1 || copyH < 1)
                return;
            if (!_copyFailed)
            {
                RenderTexture temp = RenderTexture.GetTemporary(dw, dh, 0, RenderTextureFormat.ARGB32);
                try
                {
                    Graphics.Blit(phone, temp);
                    Graphics.CopyTexture(temp, 0, 0, srcX, srcY, copyW, copyH, dest, 0, 0, x, y);
                    RenderTexture.ReleaseTemporary(temp);
                    return;
                }
                catch (Exception ex)
                {
                    RenderTexture.ReleaseTemporary(temp);
                    _copyFailed = true;
                    Plugin.LogError("Cast copy failed: " + ex.Message);
                }
            }
            Graphics.Blit(phone, dest);
        }

        private static bool TryScreen(Transform board, out Renderer source, out int slot)
        {
            source = null;
            slot = -1;
            Renderer[] all = board.GetComponentsInChildren<Renderer>(true);
            Renderer fallback = null;
            int fallbackSlot = -1;
            for (int i = 0; i < all.Length; i++)
            {
                Renderer rend = all[i];
                if (rend == null)
                    continue;
                string typeName = rend.GetType().Name;
                if (typeName == "ParticleSystemRenderer" || typeName == "TrailRenderer" || typeName == "LineRenderer")
                    continue;
                Material[] mats = rend.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (!IsScreen(mats[m]))
                        continue;
                    if (IsDeparture(mats[m]))
                    {
                        source = rend;
                        slot = m;
                        return true;
                    }
                    if (fallback == null)
                    {
                        fallback = rend;
                        fallbackSlot = m;
                    }
                }
            }
            if (fallback == null)
                return false;
            source = fallback;
            slot = fallbackSlot;
            return true;
        }

        private static bool IsDeparture(Material mat)
        {
            return IsNamedScreen(mat);
        }

        private static bool IsScreen(Material mat)
        {
            return IsNamedScreen(mat);
        }

        private static bool IsNamedScreen(Material mat)
        {
            if (mat == null || string.IsNullOrEmpty(mat.name))
                return false;
            string n = mat.name.ToLowerInvariant();
            if (n.Contains("kiosk"))
                return false;
            if (n.Contains("departure"))
                return true;
            return n.Contains("screen logo") || n.Contains("screenlogo");
        }

        private static void FitBounds(Transform t, Bounds local, GameObject host, Transform quad, out float faceW, out float faceH)
        {
            int thin = Thin(local.size);
            int ax = (thin + 1) % 3;
            int ay = (thin + 2) % 3;
            var thinAxis = Vector3.zero;
            thinAxis[thin] = 1f;
            var rightAxis = Vector3.zero;
            rightAxis[ax] = 1f;
            var upAxis = Vector3.zero;
            upAxis[ay] = 1f;
            Vector3 center = t.TransformPoint(local.center);
            Vector3 worldN = t.TransformDirection(thinAxis);
            if (worldN.sqrMagnitude < 0.0001f)
                worldN = Vector3.forward;
            worldN.Normalize();
            Camera cam = Camera.main;
            if (cam != null && Vector3.Dot(worldN, cam.transform.position - center) < 0f)
                worldN = -worldN;
            Vector3 worldUp = t.TransformDirection(upAxis);
            if (worldUp.sqrMagnitude < 0.0001f)
                worldUp = Vector3.up;
            if (Vector3.Dot(worldUp, Vector3.up) < 0f)
                worldUp = -worldUp;
            worldUp.Normalize();
            if (Mathf.Abs(Vector3.Dot(worldUp, worldN)) > 0.95f)
                worldUp = Vector3.up;
            Vector3 worldRight = Vector3.Cross(worldUp, worldN);
            if (worldRight.sqrMagnitude < 0.0001f)
                worldRight = t.TransformDirection(rightAxis);
            worldRight.Normalize();
            worldUp = Vector3.Cross(worldN, worldRight).normalized;
            faceW = t.TransformVector(rightAxis * local.size[ax]).magnitude;
            faceH = t.TransformVector(upAxis * local.size[ay]).magnitude;
            if (faceW < 0.05f)
                faceW = 0.8f;
            if (faceH < 0.05f)
                faceH = 0.45f;
            host.transform.SetParent(null, false);
            host.transform.SetPositionAndRotation(center + worldN * 0.02f, Quaternion.LookRotation(worldN, worldUp));
            host.transform.localScale = new Vector3(faceW, faceH, 1f);
            host.transform.SetParent(t, true);
            quad.localPosition = Vector3.zero;
            quad.localRotation = Quaternion.identity;
            quad.localScale = Vector3.one;
        }

        private static void SendStill(Texture2D shot)
        {
            if (shot == null || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount < 2)
                return;
            if (Time.unscaledTime < _netWait)
                return;
            _netWait = Time.unscaledTime + 1.25f;
            int hash = SampleHash(shot);
            if (hash == _netHash)
                return;
            _netHash = hash;
            Texture2D small = Downscale(shot, 320);
            Texture2D src = small != null ? small : shot;
            byte[] jpg = PhoneImages.EncodeJpg(src, 32);
            if (jpg != null && jpg.Length > 40000)
            {
                Texture2D tinier = Downscale(src, 200);
                if (tinier != null)
                {
                    if (small != null)
                        UnityEngine.Object.Destroy(small);
                    small = tinier;
                    jpg = PhoneImages.EncodeJpg(small, 28);
                }
            }
            if (small != null && small != shot)
                UnityEngine.Object.Destroy(small);
            if (jpg != null && jpg.Length > 32 && jpg.Length <= 48000)
                PhoneNet.SendCastFrame(_deviceId, NextSeq(), jpg);
        }

        private static int SampleHash(Texture2D tex)
        {
            Color32[] px = tex.GetPixels32();
            int h = 17;
            int step = px.Length / 48;
            if (step < 1)
                step = 1;
            for (int i = 0; i < px.Length; i += step)
            {
                Color32 c = px[i];
                h = unchecked(h * 31 + (c.r >> 4));
                h = unchecked(h * 31 + (c.g >> 4));
                h = unchecked(h * 31 + (c.b >> 4));
            }
            return h;
        }

        private static Bounds ScreenBounds(Renderer rend, int slot)
        {
            Mesh mesh = null;
            var filter = rend.GetComponent<MeshFilter>();
            if (filter != null)
                mesh = filter.sharedMesh;
            if (mesh == null)
            {
                var skin = rend as SkinnedMeshRenderer;
                if (skin != null)
                    mesh = skin.sharedMesh;
            }
            if (mesh != null && slot >= 0 && slot < mesh.subMeshCount)
            {
                try
                {
                    Bounds sub = mesh.GetSubMesh(slot).bounds;
                    if (sub.size.sqrMagnitude > 0.000001f)
                        return sub;
                }
                catch (Exception ex)
                {
                    Plugin.LogInfo("Cast submesh bounds unavailable: " + ex.Message);
                }
            }
            return MeshBounds(rend);
        }

        private static Bounds SlotBounds(Renderer rend, int slot)
        {
            Mesh mesh = null;
            var filter = rend.GetComponent<MeshFilter>();
            if (filter != null)
                mesh = filter.sharedMesh;
            if (mesh == null)
            {
                var skin = rend as SkinnedMeshRenderer;
                if (skin != null)
                    mesh = skin.sharedMesh;
            }
            if (mesh == null || slot < 0 || slot >= mesh.subMeshCount)
                return MeshBounds(rend);
            int[] tris = mesh.GetTriangles(slot);
            if (tris == null || tris.Length == 0)
                return MeshBounds(rend);
            Vector3[] verts = mesh.vertices;
            Vector3 min = verts[tris[0]];
            Vector3 max = min;
            for (int i = 1; i < tris.Length; i++)
            {
                Vector3 v = verts[tris[i]];
                if (v.x < min.x) min.x = v.x;
                if (v.y < min.y) min.y = v.y;
                if (v.z < min.z) min.z = v.z;
                if (v.x > max.x) max.x = v.x;
                if (v.y > max.y) max.y = v.y;
                if (v.z > max.z) max.z = v.z;
            }
            Vector3 size = max - min;
            if (size.sqrMagnitude < 0.000001f)
                return MeshBounds(rend);
            return new Bounds((min + max) * 0.5f, size);
        }

        private static Bounds MeshBounds(Renderer rend)
        {
            var filter = rend.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                return filter.sharedMesh.bounds;
            var skin = rend as SkinnedMeshRenderer;
            if (skin != null && skin.sharedMesh != null)
                return skin.sharedMesh.bounds;
            return new Bounds(Vector3.zero, new Vector3(1.6f, 0.9f, 0.05f));
        }

        private static int Thin(Vector3 size)
        {
            int thin = 0;
            if (Mathf.Abs(size.y) < Mathf.Abs(size[thin]))
                thin = 1;
            if (Mathf.Abs(size.z) < Mathf.Abs(size[thin]))
                thin = 2;
            return thin;
        }

        private static Renderer Largest(Transform board)
        {
            Renderer[] all = board.GetComponentsInChildren<Renderer>(true);
            Renderer best = null;
            float bestArea = 0f;
            for (int i = 0; i < all.Length; i++)
            {
                Renderer rend = all[i];
                if (rend == null)
                    continue;
                string typeName = rend.GetType().Name;
                if (typeName == "ParticleSystemRenderer" || typeName == "TrailRenderer" || typeName == "LineRenderer")
                    continue;
                if (rend.transform.parent != null && rend.transform.parent.name == "PiPhoneCast")
                    continue;
                Vector3 s = rend.bounds.size;
                float area = Mathf.Abs(s.x * s.y) + Mathf.Abs(s.x * s.z) + Mathf.Abs(s.y * s.z);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = rend;
                }
            }
            return best;
        }

        private static Shader CastShader()
        {
            if (_shader != null)
                return _shader;
            _shader = Shader.Find("Unlit/Texture");
            if (_shader == null)
                _shader = Shader.Find("Mobile/Unlit (Supports Lightmap)");
            if (_shader == null)
                _shader = Shader.Find("Sprites/Default");
            if (_shader == null)
                _shader = Shader.Find("UI/Default");
            if (_shader == null && !_shaderLogged)
            {
                _shaderLogged = true;
                Plugin.LogError("Cast shader missing.");
            }
            return _shader;
        }

        private static void MakeOpaque(Material mat)
        {
            if (mat == null)
                return;
            mat.color = Color.white;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
        }

        private static float MeshAspect(Renderer rend, int slot)
        {
            Mesh mesh = null;
            var filter = rend.GetComponent<MeshFilter>();
            if (filter != null)
                mesh = filter.sharedMesh;
            if (mesh == null)
            {
                var skin = rend as SkinnedMeshRenderer;
                if (skin != null)
                    mesh = skin.sharedMesh;
            }
            if (mesh == null || slot < 0 || slot >= mesh.subMeshCount)
                return RendererAspect(rend);
            int[] tris = mesh.GetTriangles(slot);
            if (tris == null || tris.Length == 0)
                return RendererAspect(rend);
            Vector3[] verts = mesh.vertices;
            Vector3 min = verts[tris[0]];
            Vector3 max = min;
            for (int i = 1; i < tris.Length; i++)
            {
                Vector3 v = verts[tris[i]];
                if (v.x < min.x) min.x = v.x;
                if (v.y < min.y) min.y = v.y;
                if (v.z < min.z) min.z = v.z;
                if (v.x > max.x) max.x = v.x;
                if (v.y > max.y) max.y = v.y;
                if (v.z > max.z) max.z = v.z;
            }
            return AxesAspect(rend.transform, max - min);
        }

        private static float RendererAspect(Renderer rend)
        {
            return AxesAspect(rend.transform, rend.localBounds.size);
        }

        private static float AxesAspect(Transform t, Vector3 size)
        {
            Vector3 wx = t.TransformVector(new Vector3(size.x, 0f, 0f));
            Vector3 wy = t.TransformVector(new Vector3(0f, size.y, 0f));
            Vector3 wz = t.TransformVector(new Vector3(0f, 0f, size.z));
            float mx = wx.magnitude;
            float my = wy.magnitude;
            float mz = wz.magnitude;
            Vector3 a;
            Vector3 b;
            float ma;
            float mb;
            if (mx <= my && mx <= mz)
            {
                a = wy;
                ma = my;
                b = wz;
                mb = mz;
            }
            else if (my <= mx && my <= mz)
            {
                a = wx;
                ma = mx;
                b = wz;
                mb = mz;
            }
            else
            {
                a = wx;
                ma = mx;
                b = wy;
                mb = my;
            }
            float upA = a.sqrMagnitude > 0.0001f ? Mathf.Abs(Vector3.Dot(a.normalized, Vector3.up)) : 0f;
            float upB = b.sqrMagnitude > 0.0001f ? Mathf.Abs(Vector3.Dot(b.normalized, Vector3.up)) : 0f;
            float height = upA >= upB ? ma : mb;
            float width = upA >= upB ? mb : ma;
            if (height < 0.0001f)
                return 1.6f;
            return Mathf.Max(0.05f, width / height);
        }

        private static PiPhoneCastDevice Pick()
        {
            Camera cam = Camera.main;
            if (cam == null)
                return null;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            PiPhoneCastDevice looked = null;
            float lookDist = LookRange;
            PiPhoneCastDevice near = null;
            float nearDist = NearRange;
            for (int i = 0; i < Devices.Count; i++)
            {
                PiPhoneCastDevice device = Devices[i];
                Transform tr = SafeFind(device);
                if (tr == null)
                    continue;
                Renderer rend = Largest(tr);
                if (rend == null)
                    continue;
                float hit;
                if (rend.bounds.IntersectRay(ray, out hit) && hit > 0.05f && hit < lookDist)
                {
                    lookDist = hit;
                    looked = device;
                }
                float dist = Vector3.Distance(cam.transform.position, rend.bounds.center);
                if (dist < nearDist)
                {
                    nearDist = dist;
                    near = device;
                }
            }
            return looked != null ? looked : near;
        }

        private static bool OwnedByOther(string id)
        {
            ForgetMissingOwners();
            int owner;
            if (!Owners.TryGetValue(id, out owner) || owner <= 0)
                return false;
            if (owner == Actor())
                return false;
            if (!PlayerHere(owner))
            {
                Owners.Remove(id);
                return false;
            }
            return true;
        }

        private static void ForgetMissingOwners()
        {
            if (!PhotonNetwork.InRoom)
                return;
            var stale = new List<string>();
            foreach (KeyValuePair<string, int> kv in Owners)
            {
                if (kv.Value > 0 && kv.Value != Actor() && !PlayerHere(kv.Value))
                    stale.Add(kv.Key);
            }
            for (int i = 0; i < stale.Count; i++)
                Owners.Remove(stale[i]);
        }

        private static bool PlayerHere(int actor)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
                return false;
            return PhotonNetwork.CurrentRoom.GetPlayer(actor) != null;
        }

        private static PiPhoneCastDevice FindDevice(string id)
        {
            PiPhoneCastDevice hit = FindListed(id);
            if (hit != null)
                return hit;
            Discover(false);
            return FindListed(id);
        }

        private static PiPhoneCastDevice FindListed(string id)
        {
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i] != null && Devices[i].Id == id)
                    return Devices[i];
            }
            return null;
        }

        private static int _discoverScene = int.MinValue;
        private static float _discoverAt = -999f;

        private static void Discover(bool force)
        {
            Scene scene = SceneManager.GetActiveScene();
            int handle = scene.handle;
            if (!force && handle == _discoverScene && Time.unscaledTime - _discoverAt < 1.5f)
                return;
            _discoverScene = handle;
            _discoverAt = Time.unscaledTime;
            var claimed = new List<int>();
            for (int i = 0; i < Devices.Count; i++)
            {
                Transform have = SafeFind(Devices[i]);
                if (have != null)
                    claimed.Add(have.GetInstanceID());
            }
            var hits = new List<Transform>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null)
                    CollectBoards(roots[i].transform, hits);
            }
            for (int i = 0; i < hits.Count; i++)
            {
                Transform board = hits[i];
                if (board == null)
                    continue;
                int key = board.GetInstanceID();
                if (claimed.Contains(key))
                    continue;
                string path = PathOf(board);
                string label = board.name;
                PiPhoneCastDevice device = PathBoard(path, label);
                if (FindListed(device.Id) == null)
                    Plugin.LogInfo("Cast screen " + label);
                Register(device);
                claimed.Add(key);
            }
        }

        private static void CollectBoards(Transform t, List<Transform> hits)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return;
            if (InFlight(t) && HasScreen(t))
                hits.Add(t);
            for (int i = 0; i < t.childCount; i++)
                CollectBoards(t.GetChild(i), hits);
        }

        private static bool InFlight(Transform t)
        {
            while (t != null)
            {
                if (NameHasFlight(t.name))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private static bool NameHasFlight(string name)
        {
            return !string.IsNullOrEmpty(name) && name.IndexOf("flight", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasScreen(Transform t)
        {
            Renderer[] rends = t.GetComponents<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer rend = rends[i];
                if (rend == null)
                    continue;
                string typeName = rend.GetType().Name;
                if (typeName == "ParticleSystemRenderer" || typeName == "TrailRenderer" || typeName == "LineRenderer")
                    continue;
                Material[] mats = rend.sharedMaterials;
                if (mats == null)
                    continue;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (IsNamedScreen(mats[m]))
                        return true;
                }
            }
            return false;
        }

        private static PiPhoneCastDevice PathBoard(string path, string label)
        {
            string captured = path;
            return new PiPhoneCastDevice
            {
                Id = "peak.cast.path:" + path,
                Name = label,
                Find = () => FindPath(captured)
            };
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static Transform FindPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            string[] parts = path.Split('/');
            if (parts.Length == 0)
                return null;
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null || roots[i].name != parts[0])
                    continue;
                Transform at = roots[i].transform;
                for (int p = 1; p < parts.Length && at != null; p++)
                    at = at.Find(parts[p]);
                if (at != null)
                    return at;
            }
            return null;
        }

        private static Transform SafeFind(PiPhoneCastDevice device)
        {
            if (device == null || device.Find == null)
                return null;
            try
            {
                return device.Find();
            }
            catch (Exception ex)
            {
                Plugin.LogError("Cast find failed: " + ex.Message);
                return null;
            }
        }

        private static PiPhoneCastDevice Board(string id, string childName)
        {
            string captured = childName;
            return new PiPhoneCastDevice
            {
                Id = id,
                Name = childName,
                Find = () => FindBoard(captured)
            };
        }

        private static Transform FindBoard(string childName)
        {
            GameObject go = GameObject.Find("Map/" + childName);
            if (go != null)
                return go.transform;
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform hit = Walk(roots[i].transform, childName);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        private static Transform Walk(Transform t, string childName)
        {
            if (t.name == childName && t.parent != null && t.parent.name == "Map")
                return t;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform hit = Walk(t.GetChild(i), childName);
                if (hit != null)
                    return hit;
            }
            return null;
        }

        private static void DropShow(string id)
        {
            Display show;
            if (!Shows.TryGetValue(id, out show))
                return;
            Shows.Remove(id);
            if (show == null)
                return;
            if (show.Source != null && show.OriginalMats != null)
                show.Source.sharedMaterials = show.OriginalMats;
            if (show.Screen != null)
            {
                show.Screen.Release();
                UnityEngine.Object.Destroy(show.Screen);
            }
            if (show.ScreenMat != null)
                UnityEngine.Object.Destroy(show.ScreenMat);
            if (show.Tex != null)
                UnityEngine.Object.Destroy(show.Tex);
            if (show.Picture != null)
                UnityEngine.Object.Destroy(show.Picture);
            if (show.Host != null)
                UnityEngine.Object.Destroy(show.Host);
        }

        private static Texture2D Downscale(Texture2D src, int longMax)
        {
            if (src == null)
                return null;
            int longSide = Mathf.Max(src.width, src.height);
            if (longSide <= longMax)
                return null;
            float s = longMax / (float)longSide;
            int nw = Mathf.Max(8, Mathf.RoundToInt(src.width * s));
            int nh = Mathf.Max(8, Mathf.RoundToInt(src.height * s));
            RenderTexture rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(nw, nh, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0f, 0f, nw, nh), 0, 0);
            small.Apply(false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return small;
        }

        private static void ClearShows()
        {
            var ids = new List<string>(Shows.Keys);
            for (int i = 0; i < ids.Count; i++)
                DropShow(ids[i]);
        }

        private static void StopRoutine()
        {
            if (_routine != null && _instance != null)
                _instance.StopCoroutine(_routine);
            _routine = null;
        }

        private static Mesh CastQuad()
        {
            if (_quad != null)
                return _quad;
            _quad = new Mesh();
            _quad.name = "PiPhoneCastQuad";
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            _quad.uv = new[]
            {
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                new Vector2(1f, 0f),
                new Vector2(0f, 0f)
            };
            _quad.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            _quad.RecalculateNormals();
            return _quad;
        }

        private static void PublishWatch()
        {
            if (!_casting || !_share || _watcherIds.Count == 0 || !PhotonNetwork.InRoom)
                return;
            PiPhoneApp app = PhoneMenu.OpenApp();
            string appId = app != null ? app.Id : "home";
            string title = app != null && !string.IsNullOrEmpty(app.DisplayName) ? app.DisplayName : "Home";
            bool land = PhoneMenu.IsLandscape;
            string home = HomeList();
            bool front = false;
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;
            float fov = 60f;
            bool cam = false;
            bool closet = false;
            if (app != null && appId != "#")
            {
                if (app.Id == BuiltinApps.CameraId)
                    cam = CameraApp.TryWorldPose(out pos, out rot, out fov, out front);
                else if (app.Id == BuiltinApps.ClosetId)
                    closet = ClosetApp.TryWorldPose(out pos, out rot, out fov);
            }
            string pose = (cam || closet)
                ? pos.x.ToString("0.00") + "," + pos.y.ToString("0.00") + "," + pos.z.ToString("0.00") + "," + fov.ToString("0.00")
                : string.Empty;
            float now = Time.unscaledTime;
            bool pictureDue = now >= _nextState;
            bool poseDue = now >= _nextPose;
            if (!pictureDue && !poseDue)
                return;
            string blob = pictureDue ? PhoneCastMirror.Capture(CastIsPrivate()) : _stateSig;
            bool pictureChanged = pictureDue && blob != _stateSig;
            if (!pictureChanged && (!poseDue || pose == _statePose))
            {
                if (pictureDue)
                    _nextState = now + 0.28f;
                return;
            }
            if (pictureChanged)
            {
                _stateSig = blob;
                _nextState = now + 0.28f;
            }
            else if (pictureDue)
                _nextState = now + 0.28f;
            _statePose = pose;
            _nextPose = now + 0.1f;
            PhoneNet.SendCastState(new object[]
            {
                PhoneNet.Magic, PhoneNet.Protocol, PhoneNet.KindCastState,
                _deviceId, appId, land, title, home,
                cam, front, pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w, fov,
                closet, pictureChanged ? blob : string.Empty
            }, pictureChanged);
        }

        private static string HomeList()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < PhoneStore.HomeIds.Count; i++)
            {
                string id = PhoneStore.HomeIds[i];
                if (string.IsNullOrEmpty(id) || !PhoneStore.IsInstalled(id))
                    continue;
                if (sb.Length > 0)
                    sb.Append(',');
                sb.Append(id);
            }
            sb.Append(';');
            for (int i = 0; i < PhoneStore.DockIds.Count && i < 4; i++)
            {
                string id = PhoneStore.DockIds[i];
                if (string.IsNullOrEmpty(id) || !PhoneStore.IsInstalled(id))
                    continue;
                if (sb.Length > 0 && sb[sb.Length - 1] != ';')
                    sb.Append(',');
                sb.Append(id);
            }
            return sb.ToString();
        }

        public static void OnActorGone(int actor)
        {
            if (actor <= 0)
                return;
            _watcherIds.Remove(actor);
            var ids = new List<string>();
            foreach (KeyValuePair<string, int> pair in Owners)
            {
                if (pair.Value == actor)
                    ids.Add(pair.Key);
            }
            for (int i = 0; i < ids.Count; i++)
                OnStop(ids[i], actor);
        }

        public static void OnLeftRoom()
        {
            if (!_casting && string.IsNullOrEmpty(_watchId) && Owners.Count == 0)
                return;
            Release();
            EndWatch();
            Owners.Clear();
            Shared.Clear();
            Frames.Clear();
            _watcherIds.Clear();
        }

        public static void OnWatch(int actor, string id, bool on)
        {
            if (!_casting || actor <= 0 || id != _deviceId)
                return;
            if (on)
            {
                _watcherIds.Add(actor);
                _nextState = 0f;
                _nextPose = 0f;
                _stateSig = string.Empty;
                _statePose = string.Empty;
            }
            else
                _watcherIds.Remove(actor);
        }

        public static void OnState(int actor, object[] data)
        {
            if (data == null || data.Length < 19)
                return;
            string id = data[3] as string;
            if (string.IsNullOrEmpty(id) || id != _watchId)
                return;
            int owner;
            if (!Owners.TryGetValue(id, out owner) || owner != actor)
                return;
            var snap = new PhoneCastView.Snap();
            snap.App = data[4] as string ?? string.Empty;
            snap.Land = data[5] is bool && (bool)data[5];
            snap.Title = data[6] as string ?? string.Empty;
            snap.Home = data[7] as string ?? string.Empty;
            snap.Cam = data[8] is bool && (bool)data[8];
            snap.Front = data[9] is bool && (bool)data[9];
            snap.Pos = new Vector3(Num(data[10]), Num(data[11]), Num(data[12]));
            snap.Rot = new Quaternion(Num(data[13]), Num(data[14]), Num(data[15]), Num(data[16]));
            snap.Fov = Num(data[17]);
            snap.Closet = data[18] is bool && (bool)data[18];
            snap.Blob = data.Length > 19 ? data[19] as string ?? string.Empty : string.Empty;
            snap.Actor = actor;
            PhoneCastView.Apply(snap);
        }

        private static float Num(object value)
        {
            if (value is float)
                return (float)value;
            if (value is double)
                return (float)(double)value;
            if (value is int)
                return (int)value;
            return 0f;
        }

        private static void ToggleWatch(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (_watchId == id)
            {
                string was = _watchId;
                EndWatch();
                PhoneNet.SendCastWatch(was, false);
                return;
            }
            if (!string.IsNullOrEmpty(_watchId))
            {
                string was = _watchId;
                EndWatch();
                PhoneNet.SendCastWatch(was, false);
            }
            if (!EnsureShow(id))
            {
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
                return;
            }
            Display show;
            if (!Shows.TryGetValue(id, out show) || show == null || show.Host == null)
                return;
            _watchId = id;
            if (show.Quad != null)
                show.Quad.enabled = true;
            if (show.ScreenMat != null)
            {
                show.ScreenMat.mainTexture = null;
                show.ScreenMat.color = Color.black;
                show.ScreenMat.renderQueue = 2500;
            }
            PhoneCastView.Attach(show.Host.transform, show.FaceW, show.FaceH);
            PhoneNet.SendCastWatch(id, true);
            ClosePicker();
        }

        private static void EndWatch()
        {
            string id = _watchId;
            _watchId = string.Empty;
            PhoneCastView.Drop();
            if (!string.IsNullOrEmpty(id) && _deviceId != id)
                DropShow(id);
        }

        private static bool IsShared(string id)
        {
            bool on;
            return !string.IsNullOrEmpty(id) && Shared.TryGetValue(id, out on) && on;
        }

        private static int Actor()
        {
            return PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        }
    }
}
