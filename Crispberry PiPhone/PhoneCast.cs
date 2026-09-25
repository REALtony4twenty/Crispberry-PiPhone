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
    /// Mirrors the phone onto allow-listed screens (airport flight boards, plus
    /// anything a mod registers). Other players in the room get a JPEG of that screen.
    /// </summary>
    internal sealed class PhoneCast : MonoBehaviour
    {
        private const float LookRange = 12f;
        private const float NearRange = 5f;
        private const int Chunk = 6000;
        private const int MaxJpg = 180000;

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
        private static Coroutine _routine;
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
            _waiting = false;
            if (_casting)
                Release();
            ClearShows();
            Owners.Clear();
            Frames.Clear();
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
            PhoneUi.Stretch(root, 0f, 0f);
            root.SetAsLastSibling();
            _picker = root.gameObject;
            PhoneUi.AddVertical(_picker, 8f, new RectOffset(16, 16, 36, 16));
            var title = PhoneUi.CreateLabel(root, "Title", PhoneLang.T("screen_cast", "Screen cast"), 20f, FontStyles.Bold, TextAlignmentOptions.Center);
            var titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredHeight = 36f;
            titleLe.minHeight = 36f;
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
                if (mine)
                    label = label + "  ·  " + PhoneLang.T("cast_on", "On");
                Button btn = PhoneUi.CreateButton(rows, label, () => Choose(id), new Vector2(280f, 48f));
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = 48f;
                le.minHeight = 48f;
                if (mine)
                {
                    Image img = btn.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0.16f, 0.42f, 0.30f, 1f);
                }
            }
        }

        private static List<PiPhoneCastDevice> Available()
        {
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
            if (_casting && _deviceId == id)
            {
                Release();
                return;
            }
            if (OwnedByOther(id))
            {
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
                return;
            }
            if (_casting)
                Release();
            StartCast(id);
        }

        private static void StartCast(string id)
        {
            int me = Actor();
            if (!PhotonNetwork.InRoom)
            {
                BeginLocal(id);
                return;
            }
            if (PhotonNetwork.IsMasterClient)
            {
                if (!MasterClaim(id, me))
                {
                    PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
                    return;
                }
                BeginLocal(id);
                PhoneNet.SendCastGrant(id, me);
                return;
            }
            _waiting = true;
            _waitId = id;
            _waitUntil = Time.unscaledTime + 2f;
            PhoneNet.SendCastAsk(id);
        }

        public static void Release()
        {
            _waiting = false;
            string id = _deviceId;
            bool was = _casting;
            StopRoutine();
            _casting = false;
            _deviceId = string.Empty;
            if (was && !string.IsNullOrEmpty(id))
            {
                DropShow(id);
                int owner;
                if (Owners.TryGetValue(id, out owner) && owner == Actor())
                    Owners.Remove(id);
                if (PhotonNetwork.InRoom)
                    PhoneNet.SendCastStop(id, Actor());
            }
            PhoneMenu.RefreshShade();
        }

        public static void OnAsk(int actor, string id)
        {
            if (!PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(id) || actor <= 0)
                return;
            if (!MasterClaim(id, actor))
            {
                PhoneNet.SendCastDeny(actor, id);
                return;
            }
            ApplyGrant(id, actor);
            PhoneNet.SendCastGrant(id, actor);
        }

        public static void OnGrant(string id, int actor)
        {
            if (string.IsNullOrEmpty(id) || actor <= 0)
                return;
            ApplyGrant(id, actor);
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
        }

        public static void OnFrame(string id, int actor, int seq, int index, int count, byte[] chunk)
        {
            if (string.IsNullOrEmpty(id) || chunk == null || chunk.Length == 0 || count < 1 || count > 40 || index < 0 || index >= count)
                return;
            if (actor == Actor())
                return;
            int owner;
            if (!Owners.TryGetValue(id, out owner))
                Owners[id] = actor;
            else if (owner != actor)
                return;
            FrameBuf buf;
            if (!Frames.TryGetValue(id, out buf) || buf.Seq != seq || buf.Count != count)
            {
                buf = new FrameBuf
                {
                    Seq = seq,
                    Count = count,
                    Parts = new byte[count][],
                    Got = 0
                };
                Frames[id] = buf;
            }
            if (buf.Parts[index] != null)
                return;
            buf.Parts[index] = chunk;
            buf.Got++;
            if (buf.Got < buf.Count)
                return;
            int total = 0;
            for (int i = 0; i < buf.Parts.Length; i++)
            {
                if (buf.Parts[i] == null)
                    return;
                total += buf.Parts[i].Length;
            }
            if (total <= 0 || total > MaxJpg)
                return;
            var jpg = new byte[total];
            int offset = 0;
            for (int i = 0; i < buf.Parts.Length; i++)
            {
                Buffer.BlockCopy(buf.Parts[i], 0, jpg, offset, buf.Parts[i].Length);
                offset += buf.Parts[i].Length;
            }
            Frames.Remove(id);
            Texture2D tex = PhoneImages.LoadTexture(jpg);
            if (tex == null)
                return;
            Show(id, tex);
        }

        private void Update()
        {
            if (_waiting && Time.unscaledTime >= _waitUntil)
            {
                _waiting = false;
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
            }
            if (!_casting || !PhotonNetwork.InRoom)
                return;
            if (Time.unscaledTime < _nextAnnounce)
                return;
            _nextAnnounce = Time.unscaledTime + 2f;
            PhoneNet.SendCastGrant(_deviceId, Actor());
        }

        private static void ApplyGrant(string id, int actor)
        {
            Owners[id] = actor;
            int me = Actor();
            if (actor == me)
            {
                _waiting = false;
                if (!_casting)
                    BeginLocal(id);
                return;
            }
            if (_waiting && _waitId == id)
            {
                _waiting = false;
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
            }
            if (_casting && _deviceId == id)
            {
                StopRoutine();
                _casting = false;
                _deviceId = string.Empty;
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
                PhoneMenu.RefreshShade();
            }
            EnsureShow(id);
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
                if (PhotonNetwork.InRoom)
                    PhoneNet.SendCastStop(id, Actor());
                PhoneMenu.Toast(PhoneLang.T("cannot_cast", "Cannot cast to this device"));
                return;
            }
            _casting = true;
            _deviceId = id;
            _seq = 0;
            _nextAnnounce = Time.unscaledTime + 2f;
            StopRoutine();
            if (_instance != null)
                _routine = _instance.StartCoroutine(_instance.CaptureLoop());
            PhoneMenu.RefreshShade();
            Plugin.LogInfo("Casting to " + id);
        }

        private IEnumerator CaptureLoop()
        {
            while (_casting)
            {
                yield return new WaitForEndOfFrame();
                if (!_casting)
                    yield break;
                if (PhoneMenu.IsOpen && PiPhoneApi.Powered)
                {
                    Texture2D shot = Grab();
                    if (shot != null)
                    {
                        Show(_deviceId, shot);
                        byte[] jpg = PhoneImages.EncodeJpg(shot, 45);
                        if (jpg != null && jpg.Length > MaxJpg)
                        {
                            Texture2D smaller = Downscale(shot, 960);
                            if (smaller != null)
                            {
                                jpg = PhoneImages.EncodeJpg(smaller, 40);
                                UnityEngine.Object.Destroy(smaller);
                            }
                        }
                        if (jpg != null && jpg.Length > 32 && jpg.Length <= MaxJpg && PhotonNetwork.InRoom)
                            PhoneNet.SendCastFrame(_deviceId, NextSeq(), jpg);
                    }
                }
                float t = 0f;
                while (t < 0.3f && _casting)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private static int NextSeq()
        {
            _seq++;
            if (_seq > 1000000)
                _seq = 1;
            return _seq;
        }

        private static Texture2D Grab()
        {
            RectTransform bezel = PhoneMenu.BezelRt;
            Canvas canvas = PhoneMenu.RootCanvas;
            if (bezel == null || canvas == null || !bezel.gameObject.activeInHierarchy)
                return null;
            if (canvas.renderMode == RenderMode.WorldSpace)
                return GrabFramed(canvas, bezel);
            return GrabOverlay(canvas, bezel);
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
            int copyW = Mathf.Min(rt.width, rx + w) - srcX;
            int copyH = Mathf.Min(rt.height, ry + h) - srcY;
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
                Plugin.LogInfo("Cast screen quad shader " + show.ScreenMat.shader.name + " aspect " + show.ScreenAspect.ToString("0.00"));
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
            Bounds local = slot >= 0 ? SlotBounds(source, slot) : MeshBounds(source);
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
            show.ScreenMat.color = Color.white;
            if (show.ScreenMat.HasProperty("_Color"))
                show.ScreenMat.SetColor("_Color", Color.white);
            show.Quad.sharedMaterial = show.ScreenMat;
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
            if (show.Quad != null && show.ScreenMat != null)
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
            show.ScreenMat.color = Color.white;
            show.ScreenMat.mainTexture = show.Picture;
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
            if (mat == null)
                return false;
            string blob = ((mat.name ?? string.Empty) + " " + (mat.shader != null ? mat.shader.name : string.Empty)).ToLowerInvariant();
            if (blob.Contains("departure"))
                return true;
            Texture tex = BaseTex(mat);
            return tex != null && tex.name != null && tex.name.ToLowerInvariant().Contains("departure");
        }

        private static bool IsScreen(Material mat)
        {
            if (mat == null)
                return false;
            if (IsDeparture(mat))
                return true;
            if (mat.HasProperty("_Columns") && mat.HasProperty("_TextureScroll"))
                return true;
            return mat.HasProperty("_BaseTexture");
        }

        private static Texture BaseTex(Material mat)
        {
            if (mat == null)
                return null;
            if (mat.HasProperty("_BaseTexture"))
            {
                Texture tex = mat.GetTexture("_BaseTexture");
                if (tex != null)
                    return tex;
            }
            if (mat.HasProperty("_MainTex"))
            {
                Texture tex = mat.GetTexture("_MainTex");
                if (tex != null)
                    return tex;
            }
            return mat.mainTexture;
        }

        private static void FitAspect(Display show, float ratioW, float ratioH)
        {
            if (show == null || show.Host == null || show.Quad == null || ratioW < 0.01f || ratioH < 0.01f)
                return;
            bool portrait = ratioH >= ratioW;
            float bw = Mathf.Max(0.05f, show.FaceW);
            float bh = Mathf.Max(0.05f, show.FaceH);
            float qw;
            float qh;
            if (portrait)
            {
                qh = bh;
                qw = bh * (ratioW / ratioH);
                if (qw > bw)
                {
                    qw = bw;
                    qh = bw * (ratioH / ratioW);
                }
            }
            else
            {
                qw = bw;
                qh = bw * (ratioH / ratioW);
                if (qh > bh)
                {
                    qh = bh;
                    qw = bh * (ratioW / ratioH);
                }
            }
            Transform quad = show.Quad.transform;
            Vector3 parentScale = show.Host.transform.lossyScale;
            float sx = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : Mathf.Abs(parentScale.x);
            float sy = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : Mathf.Abs(parentScale.y);
            quad.localScale = new Vector3(qw / sx, qh / sy, 1f);
        }

        private static void FaceSize(Renderer source, out float width, out float height)
        {
            Transform t = source.transform;
            Bounds local = MeshBounds(source);
            int thin = Thin(local.size);
            int ax = (thin + 1) % 3;
            int ay = (thin + 2) % 3;
            Vector3 lossy = t.lossyScale;
            width = Mathf.Abs(lossy[ax]) * Mathf.Abs(local.size[ax]);
            height = Mathf.Abs(lossy[ay]) * Mathf.Abs(local.size[ay]);
        }

        private static void FitBounds(Transform t, Bounds local, GameObject host, Transform quad, out float faceW, out float faceH)
        {
            int thin = Thin(local.size);
            int ax = (thin + 1) % 3;
            int ay = (thin + 2) % 3;
            var axis = Vector3.zero;
            axis[thin] = 1f;
            Vector3 worldN = t.TransformDirection(axis);
            if (worldN.sqrMagnitude < 0.0001f)
                worldN = Vector3.forward;
            worldN.Normalize();
            Vector3 center = t.TransformPoint(local.center);
            Camera cam = Camera.main;
            if (cam != null && Vector3.Dot(worldN, cam.transform.position - center) < 0f)
                worldN = -worldN;
            Vector3 lossy = t.lossyScale;
            float width = Mathf.Abs(lossy[ax]) * Mathf.Abs(local.size[ax]);
            float height = Mathf.Abs(lossy[ay]) * Mathf.Abs(local.size[ay]);
            if (width < 0.05f)
                width = 0.8f;
            if (height < 0.05f)
                height = 0.45f;
            float depth = Mathf.Abs(lossy[thin]) * Mathf.Abs(local.size[thin]) * 0.5f + 0.02f;
            var upAxis = Vector3.zero;
            upAxis[ay] = 1f;
            Vector3 up = t.TransformDirection(upAxis);
            if (up.sqrMagnitude < 0.0001f || Mathf.Abs(Vector3.Dot(up.normalized, worldN)) > 0.95f)
                up = Vector3.up;
            host.transform.SetParent(t, true);
            host.transform.position = center + worldN * depth;
            host.transform.rotation = Quaternion.LookRotation(worldN, up);
            Vector3 parentScale = host.transform.lossyScale;
            float sx = Mathf.Abs(parentScale.x) < 0.0001f ? 1f : Mathf.Abs(parentScale.x);
            float sy = Mathf.Abs(parentScale.y) < 0.0001f ? 1f : Mathf.Abs(parentScale.y);
            quad.localPosition = Vector3.zero;
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(width / sx, height / sy, 1f);
            faceW = width;
            faceH = height;
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
            for (int i = 0; i < Devices.Count; i++)
            {
                if (Devices[i] != null && Devices[i].Id == id)
                    return Devices[i];
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
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            _quad.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            _quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            _quad.RecalculateNormals();
            return _quad;
        }

        private static int Actor()
        {
            return PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0;
        }
    }
}
