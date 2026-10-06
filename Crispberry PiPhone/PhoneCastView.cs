using System;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// The phone a watcher builds on the board. It is drawn locally from a short
    /// description of the caster's phone. Camera and closet copy that camera's
    /// place in the world and render it here, the same idea as a video call.
    /// </summary>
    internal static class PhoneCastView
    {
        internal struct Snap
        {
            public string App;
            public bool Land;
            public string Title;
            public string Home;
            public bool Cam;
            public bool Front;
            public Vector3 Pos;
            public Quaternion Rot;
            public float Fov;
            public bool Closet;
            public string Blob;
            public int Actor;
        }

        private static RectTransform _root;
        private static Transform _host;
        private static float _faceW;
        private static float _faceH;
        private static string _key = string.Empty;
        internal static string OpenApp = string.Empty;
        private static Snap _snap;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static RawImage _live;
        private static int _hides;

        public static void Attach(Transform host, float faceW, float faceH)
        {
            Drop();
            if (host == null)
                return;
            _host = host;
            _faceW = faceW;
            _faceH = faceH;
            var go = new GameObject("PiP_WatchPhone");
            go.transform.SetParent(host, false);
            _root = go.AddComponent<RectTransform>();
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 4000;
            go.AddComponent<RectMask2D>();
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _root.localPosition = new Vector3(0f, 0f, 0.02f);
            Fit(false);
            _key = string.Empty;
        }

        public static void Apply(Snap snap)
        {
            _snap = snap;
            if (_root == null)
                return;
            Canvas canvas = _root.GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;
            OpenApp = snap.App ?? string.Empty;
            string blob = snap.Blob ?? string.Empty;
            bool mirror = blob.StartsWith("M1|");
            if (!mirror)
                return;
            if (blob == _key)
                return;
            _key = blob;
            float pw;
            float ph;
            PhoneCastMirror.ReadSize(blob, out pw, out ph);
            FitSize(pw, ph);
            for (int i = _root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(_root.GetChild(i).gameObject);
            _live = null;
            StopCam();
            PhoneCastMirror.Paint(_root, blob);
            if (PhoneCastMirror.Hole != null)
            {
                EnsureCam();
                FitRt(PhoneCastMirror.Hole.rectTransform.rect);
                _live = PhoneCastMirror.Hole;
                if (_live != null)
                    _live.texture = _rt;
                _snap.Cam = !PhoneCastMirror.HoleCloset;
                _snap.Closet = PhoneCastMirror.HoleCloset;
            }
        }

        public static void Drop()
        {
            StopCam();
            _key = string.Empty;
            _host = null;
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
                _root = null;
            }
        }

        public static void SetShown(bool shown)
        {
            if (shown)
            {
                if (_hides > 0)
                    _hides--;
            }
            else
                _hides++;
            if (_root != null)
                _root.gameObject.SetActive(_hides <= 0);
        }

        private static void Note(RectTransform screen, string text)
        {
            var label = PhoneUi.CreateLabel(screen, "Note", text, 20f, FontStyles.Normal, TextAlignmentOptions.Center);
            PhoneUi.Stretch(label.rectTransform, 24f, 48f);
            label.color = PhoneUi.TextDim;
        }

        private static void FillHome(RectTransform screen, string home, bool land)
        {
            var wall = PhoneUi.CreateImage(screen, "Wall", PhoneUi.Wallpaper(), Color.white);
            PhoneUi.Stretch(wall, 0f, 0f);
            Image wallImg = wall.GetComponent<Image>();
            wallImg.raycastTarget = false;
            wallImg.preserveAspect = false;
            string gridPart = home ?? string.Empty;
            string dockPart = string.Empty;
            int semi = gridPart.IndexOf(';');
            if (semi >= 0)
            {
                dockPart = gridPart.Substring(semi + 1);
                gridPart = gridPart.Substring(0, semi);
            }
            var dock = new GameObject("Dock", typeof(RectTransform));
            dock.transform.SetParent(screen, false);
            var dockRt = dock.GetComponent<RectTransform>();
            PhoneUi.StretchBottom(dockRt, 108f);
            var dockRow = dock.AddComponent<HorizontalLayoutGroup>();
            dockRow.childAlignment = TextAnchor.MiddleCenter;
            dockRow.spacing = 18f;
            dockRow.childForceExpandWidth = false;
            dockRow.childForceExpandHeight = false;
            string[] dockIds = string.IsNullOrEmpty(dockPart) ? new string[0] : dockPart.Split(',');
            for (int i = 0; i < dockIds.Length && i < 4; i++)
                AddIcon(dock.transform, dockIds[i], true);

            var body = new GameObject("Home", typeof(RectTransform));
            body.transform.SetParent(screen, false);
            var rt = body.GetComponent<RectTransform>();
            PhoneUi.Stretch(rt, 12f, 40f);
            rt.offsetMin = new Vector2(rt.offsetMin.x, 112f);
            var grid = body.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(84f, 96f);
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = land ? 6 : 4;
            grid.childAlignment = TextAnchor.UpperCenter;
            string[] ids = string.IsNullOrEmpty(gridPart) ? new string[0] : gridPart.Split(',');
            int shown = 0;
            for (int i = 0; i < ids.Length && shown < 16; i++)
            {
                if (AddIcon(body.transform, ids[i], false))
                    shown++;
            }
        }

        private static bool AddIcon(Transform parent, string id, bool dock)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            PiPhoneApp app;
            if (!PiPhoneApi.TryGetApp(id, out app) || app == null)
                return false;
            var go = new GameObject("App", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<VerticalLayoutGroup>();
            col.childAlignment = TextAnchor.UpperCenter;
            col.spacing = 4f;
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            col.childControlWidth = false;
            col.childControlHeight = false;
            PhoneIcons.CreateView(go.transform, app, dock ? 52f : 56f, false);
            if (!dock)
            {
                string name = string.IsNullOrEmpty(app.DisplayName) ? id : app.DisplayName;
                var label = PhoneUi.CreateLabel(go.transform, "Name", name, 12f, FontStyles.Normal, TextAlignmentOptions.Center);
                label.color = Color.white;
                PhoneUi.Size(label.gameObject, 18f, 76f);
                label.overflowMode = TextOverflowModes.Ellipsis;
            }
            return true;
        }

        private static void Fit(bool land)
        {
            if (_root == null)
                return;
            float w = land ? 860f : 420f;
            float h = land ? 420f : 860f;
            _root.sizeDelta = new Vector2(w, h);
            float hx = _host != null ? Mathf.Abs(_host.lossyScale.x) : 0f;
            float hy = _host != null ? Mathf.Abs(_host.lossyScale.y) : 0f;
            float hz = _host != null ? Mathf.Abs(_host.lossyScale.z) : 0f;
            if (hx < 0.0001f)
                hx = Mathf.Max(0.05f, _faceW);
            if (hy < 0.0001f)
                hy = Mathf.Max(0.05f, _faceH);
            if (hz < 0.0001f)
                hz = 1f;
            float s = Mathf.Min(_faceW / w, _faceH / h);
            if (s < 0.00001f)
                s = 0.001f;
            _root.localScale = new Vector3(s / hx, s / hy, s / hz);
        }

        private static void FitSize(float w, float h)
        {
            if (_root == null)
                return;
            if (w < 32f)
                w = 420f;
            if (h < 32f)
                h = 860f;
            _root.sizeDelta = new Vector2(w, h);
            float hx = _host != null ? Mathf.Abs(_host.lossyScale.x) : 0f;
            float hy = _host != null ? Mathf.Abs(_host.lossyScale.y) : 0f;
            float hz = _host != null ? Mathf.Abs(_host.lossyScale.z) : 0f;
            if (hx < 0.0001f)
                hx = Mathf.Max(0.05f, _faceW);
            if (hy < 0.0001f)
                hy = Mathf.Max(0.05f, _faceH);
            if (hz < 0.0001f)
                hz = 1f;
            float s = Mathf.Min(_faceW / w, _faceH / h);
            if (s < 0.00001f)
                s = 0.001f;
            _root.localScale = new Vector3(s / hx, s / hy, s / hz);
        }

        private static void EnsureCam()
        {
            if (_cam != null)
                return;
            _rt = new RenderTexture(480, 720, 16, RenderTextureFormat.ARGB32);
            _rt.Create();
            var go = new GameObject("PiP_WatchCam");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _cam = go.AddComponent<Camera>();
            _cam.enabled = true;
            _cam.depth = -80f;
            _cam.nearClipPlane = 0.08f;
            _cam.farClipPlane = 2000f;
            _cam.targetTexture = _rt;
            _cam.clearFlags = CameraClearFlags.Skybox;
            Camera main = Camera.main;
            if (main != null)
            {
                _cam.cullingMask = main.cullingMask;
                _cam.farClipPlane = main.farClipPlane;
            }
            go.AddComponent<WatchCam>();
        }

        private static void FitRt(Rect rect)
        {
            float aspect = rect.height > 2f ? rect.width / rect.height : 9f / 16f;
            if (aspect < 0.2f)
                aspect = 0.2f;
            if (aspect > 4f)
                aspect = 4f;
            int longSide = 720;
            int w;
            int h;
            if (aspect >= 1f)
            {
                w = longSide;
                h = Mathf.Max(32, Mathf.RoundToInt(longSide / aspect));
            }
            else
            {
                h = longSide;
                w = Mathf.Max(32, Mathf.RoundToInt(longSide * aspect));
            }
            if (_rt != null && _rt.width == w && _rt.height == h)
                return;
            if (_rt != null)
            {
                _rt.Release();
                UnityEngine.Object.Destroy(_rt);
            }
            _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
            _rt.Create();
            if (_cam != null)
                _cam.targetTexture = _rt;
        }

        private static void StopCam()
        {
            _hides = 0;
            if (_cam != null)
            {
                UnityEngine.Object.Destroy(_cam.gameObject);
                _cam = null;
            }
            if (_rt != null)
            {
                _rt.Release();
                UnityEngine.Object.Destroy(_rt);
                _rt = null;
            }
        }

        private static void Aim(Camera cam)
        {
            if (cam == null || (!_snap.Cam && !_snap.Closet))
                return;
            if (_snap.Pos.sqrMagnitude < 0.0001f && _snap.Rot == Quaternion.identity)
                return;
            cam.transform.SetPositionAndRotation(_snap.Pos, _snap.Rot);
            float fov = _snap.Fov;
            if (fov < 2f)
                fov = 2f;
            if (fov > 170f)
                fov = 170f;
            cam.fieldOfView = fov;
            if (_rt != null)
                cam.aspect = (float)_rt.width / _rt.height;
            cam.nearClipPlane = _snap.Front ? 0.12f : 0.05f;
        }

        private sealed class WatchCam : MonoBehaviour
        {
            private Renderer[] _body;
            private bool[] _bodyOn;
            private int _bodyActor;

            private void OnPreCull()
            {
                SetShown(false);
                HideCaster();
            }

            private void OnPostRender()
            {
                ShowCaster();
                SetShown(true);
            }

            private void OnDisable()
            {
                ShowCaster();
                SetShown(true);
            }

            private void LateUpdate()
            {
                Aim(GetComponent<Camera>());
                if (_snap.Actor > 0 && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null
                    && PhotonNetwork.CurrentRoom.GetPlayer(_snap.Actor) == null)
                    PhoneCast.OnActorGone(_snap.Actor);
            }

            private void HideCaster()
            {
                if (!_snap.Cam || _snap.Front || _snap.Actor <= 0)
                    return;
                if (_body == null || _bodyActor != _snap.Actor)
                    CacheBody(_snap.Actor);
                if (_body == null)
                    return;
                for (int i = 0; i < _body.Length; i++)
                {
                    Renderer rend = _body[i];
                    if (rend == null)
                        continue;
                    _bodyOn[i] = rend.enabled;
                    rend.enabled = false;
                }
            }

            private void ShowCaster()
            {
                if (_body == null || _bodyOn == null)
                    return;
                for (int i = 0; i < _body.Length; i++)
                {
                    Renderer rend = _body[i];
                    if (rend != null && _bodyOn[i])
                        rend.enabled = true;
                }
            }

            private void CacheBody(int actor)
            {
                _bodyActor = actor;
                _body = null;
                _bodyOn = null;
                List<Character> all = Character.AllCharacters;
                if (all == null)
                    return;
                for (int i = 0; i < all.Count; i++)
                {
                    Character character = all[i];
                    if (character == null || ScoutQuery.ActorOf(character) != actor)
                        continue;
                    _body = character.GetComponentsInChildren<Renderer>(true);
                    _bodyOn = new bool[_body.Length];
                    return;
                }
            }
        }
    }
}
