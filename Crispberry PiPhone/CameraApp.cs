using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class CameraApp
    {
        internal static bool IsOpen;
        internal static bool Landscape;
        internal static bool UiLocked;

        internal static bool WantsPlayThrough
        {
            get { return IsOpen && !UiLocked; }
        }

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.CameraId,
                DisplayName = "Camera",
                IconGlyph = "C",
                IconBackground = PhoneUi.CameraIcon,
                SortOrder = 25,
                ShowOnHome = true,
                AllowPlayThrough = true,
                OnOpen = Build,
                OnClose = Cleanup,
                OnOrientation = OnOrient
            });
        }

        private static void OnOrient()
        {
            if (!IsOpen || _recording || _host == null)
                return;
            Landscape = PhoneMenu.IsLandscape;
            CreateCam();
            RefreshBar();
            PlaceEmoteUi();
        }

        private static IPiPhoneHost _host;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static RawImage _preview;
        private static TextMeshProUGUI _videoLabel;
        private static bool _front;
        private static bool _videoMode;
        private static bool _recording;
        private static Button _orientBtn;
        private static Button _modeBtn;
        private static Button _shutterBtn;
        private static Button _squareBtn;
        private static Button _flipBtn;
        private static bool _square;
        private static Button _emoteBtn;
        private static GameObject _emotePanel;
        private static bool _emotesOpen;
        private static string _videoId;
        private static int _frames;
        private static float _accum;
        private static PhoneVideo.Recorder _recorder;
        private static bool _posterSaved;
        private const float ZoomMin = 0.5f;
        private const float ZoomMax = 16f;
        private static float _zoom = 1f;
        private static float _camOrbit;
        private static float _camLift;
        private static float _pinch;
        private static Slider _zoomSlider;
        private static TextMeshProUGUI _zoomLabel;
        private static Button[] _zoomChips;

        private static void Build(IPiPhoneHost host)
        {
            _host = host;
            IsOpen = true;
            UiLocked = false;
            _front = false;
            PhoneMenu.SetPlayThrough(true);
            host.SetTitle("Camera");
            PhoneMenu.SetCameraFill(true);

            var root = new GameObject("CamRoot", typeof(RectTransform));
            root.transform.SetParent(host.Content, false);
            PhoneUi.IgnoreLayout(root);
            PhoneUi.Stretch(root.GetComponent<RectTransform>(), 0f, 0f);

            var previewGo = new GameObject("Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            previewGo.transform.SetParent(root.transform, false);
            var previewRt = previewGo.GetComponent<RectTransform>();
            PhoneUi.Stretch(previewRt, 0f, 0f);
            previewRt.offsetMin = new Vector2(0f, 64f);
            _preview = previewGo.GetComponent<RawImage>();
            _preview.color = Color.white;
            _preview.raycastTarget = true;
            previewGo.AddComponent<ZoomGesture>();

            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(root.transform, false);
            PhoneUi.StretchBottom(bar.GetComponent<RectTransform>(), 56f);
            PhoneUi.AddHorizontal(bar, 6f);
            var barLayout = bar.GetComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(6, 6, 8, 8);
            barLayout.childForceExpandWidth = false;
            _flipBtn = PhoneUi.CreateIconChip(bar.transform, "Face", PhoneIcons.Material("face_up"), Flip, false, new Vector2(40f, 40f));
            _orientBtn = PhoneUi.CreateIconChip(bar.transform, "Wide", PhoneIcons.Material("screen_rotation"), ToggleLandscape, false, new Vector2(40f, 40f));
            _modeBtn = PhoneUi.CreateIconChip(bar.transform, "Photo", PhoneIcons.Material("photo_camera"), ToggleMode, false, new Vector2(40f, 40f));
            _squareBtn = PhoneUi.CreateIconChip(bar.transform, "Square", PhoneIcons.Material("aspect_ratio"), ToggleSquare, false, new Vector2(40f, 40f));
            _shutterBtn = PhoneUi.CreateIconChip(bar.transform, "Snap", PhoneIcons.Material("shutter"), PressShutter, false, new Vector2(40f, 40f));

            _videoLabel = PhoneUi.CreateLabel(root.transform, "Rec", string.Empty, 14f, FontStyles.Normal, TextAlignmentOptions.Top);
            _videoLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _videoLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _videoLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            _videoLabel.rectTransform.sizeDelta = new Vector2(0f, 24f);
            _videoLabel.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _videoLabel.color = PhoneUi.HangRed;

            var hint = PhoneUi.CreateLabel(root.transform, "Keys", Plugin.FormatCameraKeys(), 11f, FontStyles.Normal, TextAlignmentOptions.Bottom);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(-8f, 18f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, 58f);
            hint.color = new Color(1f, 1f, 1f, 0.72f);

            BuildZoomUi(root.transform);
            BuildOrbitHud(root.transform);
            BuildEmoteUi(root.transform);
            CreateCam();
            RefreshBar();
            SetZoom(_zoom);
            PhoneMenu.SyncPlayThrough();
        }

        internal static void TickHotkeys()
        {
            if (!IsOpen || Plugin.CapturingHotkey || Plugin.Instance == null)
                return;
            TickZoomInput();
            if (PhoneKeys.Down(PhoneKeys.CamFlip))
                Flip();
            if (PhoneKeys.Down(PhoneKeys.CamRotate))
                ToggleLandscape();
            if (PhoneKeys.Down(PhoneKeys.CamShutter))
                PressShutter();
        }

        private static void Flip()
        {
            _front = !_front;
            if (_host != null)
                _host.ShowToast(_front ? "Front camera." : "Back camera.");
            RefreshBar();
        }

        private static void ToggleMode()
        {
            if (_recording)
            {
                if (_host != null)
                    _host.ShowToast("Stop the video first.");
                return;
            }
            _videoMode = !_videoMode;
            if (!_recording)
                CreateCam();
            RefreshBar();
        }

        private static void ToggleSquare()
        {
            if (_recording)
            {
                if (_host != null)
                    _host.ShowToast("Stop the video first.");
                return;
            }
            _square = !_square;
            CreateCam();
            RefreshBar();
            if (_host != null)
                _host.ShowToast(_square ? "Square photos, for profile pictures." : "Full frame photos.");
        }

        private static void PressShutter()
        {
            if (_videoMode)
                ToggleVideo();
            else
                Snap();
        }

        private static void ToggleLandscape()
        {
            Landscape = !Landscape;
            PhoneMenu.SetLandscape(Landscape);
            if (!_recording)
                CreateCam();
            RefreshBar();
        }

        private static void RefreshBar()
        {
            PhoneUi.SetChipIcon(_flipBtn, PhoneIcons.Material(_front ? "face_down" : "face_up"), _front ? "Back cam" : "Face cam");
            PhoneUi.SetChipIcon(_modeBtn, PhoneIcons.Material(_videoMode ? "videocam" : "photo_camera"), _videoMode ? "Video" : "Photo");
            if (_recording)
                PhoneUi.SetChipIcon(_shutterBtn, PhoneIcons.Material("stop"), "Stop");
            else if (_videoMode)
                PhoneUi.SetChipIcon(_shutterBtn, PhoneIcons.Material("record"), "Rec");
            else
                PhoneUi.SetChipIcon(_shutterBtn, PhoneIcons.Material("shutter"), "Snap");
            ApplyEmoteIcon();
            PlaceEmoteUi();
            PlaceOrbitHud();
        }

        private static void BuildZoomUi(Transform root)
        {
            _zoomLabel = PhoneUi.CreateLabel(root, "ZoomReadout", "1×", 16f, FontStyles.Normal, TextAlignmentOptions.TopRight);
            _zoomLabel.rectTransform.anchorMin = new Vector2(0.55f, 1f);
            _zoomLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _zoomLabel.rectTransform.pivot = new Vector2(1f, 1f);
            _zoomLabel.rectTransform.sizeDelta = new Vector2(-16f, 24f);
            _zoomLabel.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            _zoomLabel.color = Color.white;

            _zoomSlider = PhoneUi.CreateSlider(root, Mathf.Log(ZoomMin), Mathf.Log(ZoomMax), Mathf.Log(_zoom), false, v => SetZoom(Mathf.Exp(v)));
            _zoomSlider.direction = Slider.Direction.BottomToTop;
            PhoneUi.IgnoreLayout(_zoomSlider.gameObject);
            var srt = _zoomSlider.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(1f, 0.5f);
            srt.sizeDelta = new Vector2(22f, -168f);
            srt.anchoredPosition = new Vector2(-10f, 18f);
            var sle = _zoomSlider.GetComponent<LayoutElement>();
            if (sle != null)
                sle.ignoreLayout = true;

            var chips = new GameObject("ZoomChips", typeof(RectTransform));
            chips.transform.SetParent(root, false);
            PhoneUi.IgnoreLayout(chips);
            var crt = chips.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(220f, 32f);
            crt.anchoredPosition = new Vector2(0f, 78f);
            PhoneUi.AddHorizontal(chips, 6f);
            var layout = chips.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            _zoomChips = new[]
            {
                PhoneUi.CreateButton(chips.transform, "0.5", () => SetZoom(0.5f), new Vector2(44f, 28f)),
                PhoneUi.CreateButton(chips.transform, "1×", () => SetZoom(1f), new Vector2(44f, 28f)),
                PhoneUi.CreateButton(chips.transform, "2×", () => SetZoom(2f), new Vector2(44f, 28f)),
                PhoneUi.CreateButton(chips.transform, "8×", () => SetZoom(8f), new Vector2(44f, 28f))
            };
        }

        private static void TickZoomInput()
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
                SetZoom(_zoom * Mathf.Pow(1.14f, wheel));

            if (PhoneKeys.Held(PhoneKeys.CamZoomIn) || Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus))
                SetZoom(_zoom * (1f + Time.unscaledDeltaTime * 2.2f));
            if (PhoneKeys.Held(PhoneKeys.CamZoomOut) || Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.Underscore) || Input.GetKey(KeyCode.KeypadMinus))
                SetZoom(_zoom * Mathf.Exp(-Time.unscaledDeltaTime * 2.2f));

            if (Input.touchCount >= 2)
            {
                float dist = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
                if (_pinch > 8f)
                    SetZoom(_zoom * (dist / _pinch));
                _pinch = dist;
            }
            else
                _pinch = 0f;
        }

        private static void SetZoom(float value)
        {
            _zoom = Mathf.Clamp(value, ZoomMin, ZoomMax);
            if (_zoomLabel != null)
                _zoomLabel.text = FormatZoom(_zoom);
            if (_zoomSlider != null)
                _zoomSlider.SetValueWithoutNotify(Mathf.Log(_zoom));
            if (_preview != null)
                _preview.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (_zoomChips == null)
                return;
            float[] stops = { 0.5f, 1f, 2f, 8f };
            for (int i = 0; i < _zoomChips.Length; i++)
            {
                Button chip = _zoomChips[i];
                if (chip == null)
                    continue;
                var img = chip.GetComponent<Image>();
                bool on = Mathf.Abs(Mathf.Log(_zoom) - Mathf.Log(stops[i])) < 0.12f;
                if (img != null)
                    img.color = on ? new Color(1f, 1f, 1f, 0.88f) : new Color(0.12f, 0.13f, 0.16f, 0.55f);
                var tmp = chip.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.color = on ? Color.black : Color.white;
            }
        }

        private static string FormatZoom(float zoom)
        {
            if (Mathf.Abs(zoom - 0.5f) < 0.04f)
                return "0.5×";
            if (Mathf.Abs(zoom - 1f) < 0.04f)
                return "1×";
            if (Mathf.Abs(zoom - 2f) < 0.06f)
                return "2×";
            if (Mathf.Abs(zoom - 8f) < 0.2f)
                return "8×";
            if (Mathf.Abs(zoom - 16f) < 0.3f)
                return "16×";
            return zoom.ToString("0.0") + "×";
        }

        private static float _binocMin = 14f;
        private static float _binocMax = 60f;
        private static float _binocChecked;

        private static void RefreshBinocularRange()
        {
            if (Time.unscaledTime - _binocChecked < 2f)
                return;
            _binocChecked = Time.unscaledTime;
            CameraOverride_Binoculars binoc = UnityEngine.Object.FindAnyObjectByType<CameraOverride_Binoculars>();
            if (binoc == null)
                return;
            if (binoc.minFov > 3f)
                _binocMin = binoc.minFov;
            if (binoc.maxFov > _binocMin)
                _binocMax = binoc.maxFov;
        }

        private static void ApplyZoom(Camera cam, float baseFov)
        {
            if (cam == null)
                return;
            RefreshBinocularRange();
            float maxFov = _binocMax > 8f ? _binocMax : Mathf.Max(50f, baseFov);
            float minFov = _binocMin > 3f ? _binocMin : 14f;
            if (minFov >= maxFov)
                minFov = maxFov * 0.22f;
            minFov *= 0.5f;
            if (minFov < 4f)
                minFov = 4f;
            if (_zoom <= 1f)
                cam.fieldOfView = Mathf.Lerp(maxFov * 1.6f, maxFov, Mathf.InverseLerp(ZoomMin, 1f, _zoom));
            else
                cam.fieldOfView = Mathf.Lerp(maxFov, minFov, Mathf.InverseLerp(1f, ZoomMax, _zoom));
        }

        private static void Snap()
        {
            if (_cam == null || _rt == null)
            {
                _host.ShowToast("No camera.");
                return;
            }
            try
            {
                _cam.Render();
                byte[] png = GrabPng();
                if (png == null || png.Length == 0)
                {
                    _host.ShowToast("Couldn't encode photo.");
                    return;
                }
                PhoneStore.AddPhoto(png, _front);
                _host.ShowToast(_front ? "Selfie saved to Photos." : "Photo saved to Photos.");
            }
            catch (Exception ex)
            {
                Plugin.LogError("Photo failed: " + ex.Message);
                _host.ShowToast("Couldn't save photo.");
            }
        }

        private static void ToggleVideo()
        {
            if (_recording)
            {
                StopVideo();
                return;
            }
            if (_cam == null || _rt == null)
            {
                _host.ShowToast("No camera.");
                return;
            }
            _videoId = PhoneStore.NewId();
            _frames = 0;
            _accum = 0f;
            _posterSaved = false;
            if (_recorder != null)
                _recorder.Dispose();
            _recorder = new PhoneVideo.Recorder();
            if (!_recorder.Start(PhoneStore.VideoPath(_videoId), _rt.width, _rt.height))
            {
                _recorder.Dispose();
                _recorder = null;
                _host.ShowToast("Couldn't start video.");
                return;
            }
            _recording = true;
            RefreshBar();
            if (_videoLabel != null)
                _videoLabel.text = "REC";
            if (_host != null)
                _host.ShowToast("Recording. " + Plugin.Pretty(Plugin.Instance.CameraShutterKey.Value) + " stops it.");
        }

        private static void StopVideo()
        {
            _recording = false;
            RefreshBar();
            if (_videoLabel != null)
                _videoLabel.text = string.Empty;
            string output = _recorder != null ? _recorder.OutputFile : null;
            if (_recorder != null)
            {
                _recorder.Dispose();
                _recorder = null;
            }
            if (string.IsNullOrEmpty(output))
                output = PhoneStore.VideoPath(_videoId);
            if (_frames < 2 || string.IsNullOrEmpty(_videoId) || !File.Exists(output) || new FileInfo(output).Length < 64)
            {
                try
                {
                    if (!string.IsNullOrEmpty(output) && File.Exists(output))
                        File.Delete(output);
                    string poster = PhoneStore.VideoPosterPath(_videoId);
                    if (File.Exists(poster))
                        File.Delete(poster);
                }
                catch
                {
                }
                if (_host != null)
                    _host.ShowToast("Video was too short.");
                return;
            }
            PhoneStore.AddRecordedVideo(_videoId, _front, _frames, output);
            if (_host != null)
                _host.ShowToast("Video saved to Photos.");
        }

        private static Texture2D GrabView()
        {
            return PhoneVideo.GrabRgb(_rt);
        }

        private static byte[] GrabPng()
        {
            Texture2D tex = GrabView();
            if (tex == null)
                return new byte[0];
            byte[] png = PhoneImages.EncodePng(tex);
            UnityEngine.Object.Destroy(tex);
            return png;
        }

        private static void CreateCam()
        {
            CleanupCamOnly();
            int w;
            int h;
            if (_square && !_videoMode)
            {
                w = 1024;
                h = 1024;
            }
            else if (Landscape)
            {
                w = 1280;
                h = 720;
            }
            else
            {
                w = 720;
                h = 1280;
            }
            _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32);
            _rt.antiAliasing = 1;
            _rt.Create();

            var go = new GameObject("PiP_Cam");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _cam = go.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.allowMSAA = false;
            _cam.allowHDR = false;
            _cam.depth = -80;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 2000f;
            _cam.targetTexture = _rt;
            _cam.enabled = true;
            go.AddComponent<CamFollow>();

            if (_preview != null)
            {
                _preview.texture = _rt;
                var fitter = _preview.GetComponent<AspectRatioFitter>();
                if (fitter == null)
                    fitter = _preview.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fitter.aspectRatio = (float)w / h;
                _preview.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private static RectTransform _orbitBar;

        private static void PlaceOrbitHud()
        {
            if (_orbitBar == null)
                return;
            float gap = 6f;
            float s = Landscape ? 32f : 26f;
            var grid = _orbitBar.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(s, s);
                grid.spacing = new Vector2(4f, 4f);
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Landscape ? 1 : Mathf.Max(1, _orbitBar.childCount);
            }
            int n = Mathf.Max(1, _orbitBar.childCount);
            float span = n * s + (n - 1) * 4f;
            for (int i = 0; i < _orbitBar.childCount; i++)
            {
                var le = _orbitBar.GetChild(i).GetComponent<LayoutElement>();
                if (le == null)
                    continue;
                le.minWidth = s;
                le.preferredWidth = s;
                le.minHeight = s;
                le.preferredHeight = s;
            }
            EmoteSpot(out Vector2 anchor, out Vector2 pivot, out Vector2 emoteSize, out Vector2 emotePos);
            _orbitBar.anchorMin = anchor;
            _orbitBar.anchorMax = anchor;
            if (Landscape)
            {
                float stackH = span;
                _orbitBar.pivot = new Vector2(0f, 1f);
                _orbitBar.sizeDelta = new Vector2(s, stackH);
                float emoteBottom = emotePos.y - emoteSize.y * pivot.y;
                _orbitBar.anchoredPosition = new Vector2(emotePos.x, emoteBottom - gap);
            }
            else
            {
                float rowW = span;
                _orbitBar.pivot = new Vector2(0f, 1f);
                _orbitBar.sizeDelta = new Vector2(rowW, s);
                _orbitBar.anchoredPosition = new Vector2(emotePos.x + emoteSize.x + gap, emotePos.y);
            }
        }

        private static void BuildOrbitHud(Transform root)
        {
            var bar = new GameObject("Orbit", typeof(RectTransform));
            bar.transform.SetParent(root, false);
            PhoneUi.IgnoreLayout(bar);
            _orbitBar = bar.GetComponent<RectTransform>();
            var grid = bar.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            float s = 26f;
            OrbitBtn(bar.transform, PhoneIcons.Material("chevron_left"), "<", () => NudgeView(0f, -18f), s);
            OrbitBtn(bar.transform, PhoneIcons.Material("chevron_right"), ">", () => NudgeView(0f, 18f), s);
            OrbitBtn(bar.transform, PhoneIcons.Material("expand_less"), "^", () => NudgeView(0.14f, 0f), s);
            OrbitBtn(bar.transform, PhoneIcons.Material("expand_more"), "v", () => NudgeView(-0.14f, 0f), s);
            OrbitBtn(bar.transform, PhoneIcons.Material("filter_center_focus"), "o", RecenterView, s);
            PlaceOrbitHud();
        }

        private static void OrbitBtn(Transform parent, Sprite icon, string fallback, UnityEngine.Events.UnityAction click, float size)
        {
            Button btn = PhoneUi.CreateIconChip(parent, fallback, icon, click, false, new Vector2(size, size));
            var le = btn.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minWidth = size;
                le.preferredWidth = size;
                le.flexibleWidth = 0f;
                le.minHeight = size;
                le.preferredHeight = size;
                le.flexibleHeight = 0f;
            }
        }

        private static void NudgeView(float lift, float orbit)
        {
            _camLift = Mathf.Clamp(_camLift + lift, -0.85f, 1.6f);
            _camOrbit += orbit;
        }

        private static void RecenterView()
        {
            _camLift = 0f;
            _camOrbit = 0f;
        }

        private static void BuildEmoteUi(Transform root)
        {
            _emotesOpen = false;
            _emoteBtn = PhoneUi.CreateIconChip(root, "Emote", DanceSprite(), ToggleEmotes, false, new Vector2(36f, 36f), false);
            PhoneUi.IgnoreLayout(_emoteBtn.gameObject);
            _emotePanel = new GameObject("Emotes", typeof(RectTransform), typeof(Image));
            _emotePanel.transform.SetParent(root, false);
            PhoneUi.IgnoreLayout(_emotePanel);
            _emotePanel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            _emotePanel.SetActive(false);
            PlaceEmoteUi();
        }

        private static void EmoteSpot(out Vector2 anchor, out Vector2 pivot, out Vector2 size, out Vector2 pos)
        {
            if (Landscape)
            {
                anchor = new Vector2(0f, 1f);
                pivot = new Vector2(0f, 1f);
                size = new Vector2(36f, 36f);
                pos = new Vector2(8f, -8f);
            }
            else
            {
                anchor = new Vector2(0f, 1f);
                pivot = new Vector2(0f, 1f);
                size = new Vector2(32f, 32f);
                pos = new Vector2(8f, -8f);
            }
        }

        private static void ApplyEmoteIcon()
        {
            Sprite dance = DanceSprite();
            if (dance != null)
                PhoneUi.SetChipIcon(_emoteBtn, dance, "Emote", false);
        }

        private static Sprite DanceSprite()
        {
            EmoteWheelData[] data = EmoteList();
            if (data == null)
                return null;
            for (int i = 0; i < data.Length; i++)
            {
                EmoteWheelData item = data[i];
                if (item == null || item.emoteSprite == null)
                    continue;
                string name = (item.emoteName ?? string.Empty) + " " + (item.anim ?? string.Empty);
                if (name.IndexOf("dance", StringComparison.OrdinalIgnoreCase) >= 0)
                    return item.emoteSprite;
            }
            return null;
        }

        private static void PlaceEmoteUi()
        {
            if (_emoteBtn != null)
            {
                var rt = _emoteBtn.GetComponent<RectTransform>();
                EmoteSpot(out Vector2 anchor, out Vector2 pivot, out Vector2 size, out Vector2 pos);
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.sizeDelta = size;
                rt.anchoredPosition = pos;
            }
            if (_emotePanel == null)
                return;
            var panel = _emotePanel.GetComponent<RectTransform>();
            if (Landscape)
            {
                panel.anchorMin = new Vector2(0f, 1f);
                panel.anchorMax = new Vector2(0f, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.sizeDelta = new Vector2(210f, 200f);
                panel.anchoredPosition = new Vector2(50f, -8f);
            }
            else
            {
                panel.anchorMin = new Vector2(0f, 1f);
                panel.anchorMax = new Vector2(0f, 1f);
                panel.pivot = new Vector2(0f, 1f);
                panel.sizeDelta = new Vector2(168f, 180f);
                panel.anchoredPosition = new Vector2(8f, -44f);
            }
        }

        private static void ToggleEmotes()
        {
            _emotesOpen = !_emotesOpen;
            if (_emotePanel != null)
                _emotePanel.SetActive(_emotesOpen);
            if (_emotesOpen)
                FillEmotes();
        }

        private static void FillEmotes()
        {
            if (_emotePanel == null)
                return;
            for (int i = _emotePanel.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(_emotePanel.transform.GetChild(i).gameObject);
            EmoteWheelData[] data = EmoteList();
            if (data == null || data.Length == 0)
            {
                if (_host != null)
                    _host.ShowToast("No emotes right now.");
                return;
            }
            ScrollRect scroll = PhoneUi.CreateScrollView(_emotePanel.transform, out RectTransform content);
            PhoneUi.Stretch(scroll.GetComponent<RectTransform>(), 4f, 4f);
            PhoneUi.AddVertical(content.gameObject, 4f, new RectOffset(4, 4, 4, 4));
            PhoneUi.FitVertical(content.gameObject);
            float w = Landscape ? 180f : 140f;
            float h = Landscape ? 32f : 26f;
            for (int i = 0; i < data.Length; i++)
            {
                EmoteWheelData item = data[i];
                if (item == null || string.IsNullOrEmpty(item.anim))
                    continue;
                string anim = item.anim;
                var row = new GameObject("EmoteRow", typeof(RectTransform));
                row.transform.SetParent(content, false);
                PhoneUi.Size(row, h);
                PhoneUi.AddHorizontal(row, 4f);
                if (item.emoteSprite != null)
                    PhoneUi.CreateIconChip(row.transform, string.Empty, item.emoteSprite, () => PerformEmote(anim), false, new Vector2(h, h), false);
                PhoneUi.CreateButton(row.transform, EmoteLabel(item), () => PerformEmote(anim), new Vector2(w, h));
            }
        }

        private static EmoteWheelData[] EmoteList()
        {
            try
            {
                GUIManager gui = GUIManager.instance;
                if (gui == null || gui.emoteWheel == null)
                    return null;
                EmoteWheel wheel = gui.emoteWheel.GetComponent<EmoteWheel>();
                return wheel != null ? wheel.data : null;
            }
            catch (Exception ex)
            {
                Plugin.LogError("Emotes failed: " + ex.Message);
                return null;
            }
        }

        private static string EmoteLabel(EmoteWheelData item)
        {
            if (item == null || string.IsNullOrEmpty(item.emoteName))
                return "Emote";
            try
            {
                string text = LocalizedText.GetText(item.emoteName);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }
            catch
            {
            }
            return item.emoteName;
        }

        private static void PerformEmote(string anim)
        {
            _emotesOpen = false;
            if (_emotePanel != null)
                _emotePanel.SetActive(false);
            try
            {
                Character local = Character.localCharacter;
                if (local == null || local.refs == null || local.refs.animations == null)
                {
                    if (_host != null)
                        _host.ShowToast("Can't emote right now.");
                    return;
                }
                local.refs.animations.PlayEmote(anim);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Emote failed: " + ex.Message);
                if (_host != null)
                    _host.ShowToast("Couldn't play that emote.");
            }
        }

        private static void Cleanup()
        {
            if (_recording)
                StopVideo();
            if (_recorder != null)
            {
                _recorder.Dispose();
                _recorder = null;
            }
            CleanupCamOnly();
            IsOpen = false;
            UiLocked = false;
            Landscape = false;
            PhoneMenu.SetCameraFill(false);
            PhoneMenu.SetLandscape(false);
            _preview = null;
            _videoLabel = null;
            _orientBtn = null;
            _modeBtn = null;
            _shutterBtn = null;
            _squareBtn = null;
            _flipBtn = null;
            _emoteBtn = null;
            _emotePanel = null;
            _emotesOpen = false;
            _orbitBar = null;
            _zoomSlider = null;
            _zoomLabel = null;
            _zoomChips = null;
            _pinch = 0f;
            _zoom = 1f;
            _camOrbit = 0f;
            _camLift = 0f;
            _host = null;
            PhoneMenu.SyncPlayThrough();
        }

        private static void CleanupCamOnly()
        {
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

        private sealed class ZoomGesture : MonoBehaviour, IBeginDragHandler, IDragHandler, IScrollHandler
        {
            private float _startY;
            private float _startZoom;

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (eventData == null)
                    return;
                _startY = eventData.position.y;
                _startZoom = _zoom;
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (eventData == null)
                    return;
                float dy = eventData.position.y - _startY;
                SetZoom(_startZoom * Mathf.Pow(1.0065f, dy));
            }

            public void OnScroll(PointerEventData eventData)
            {
                if (eventData == null)
                    return;
                SetZoom(_zoom * Mathf.Pow(1.14f, eventData.scrollDelta.y));
            }
        }

        private sealed class CamFollow : MonoBehaviour
        {
            private bool _logged;

            private void LateUpdate()
            {
                try
                {
                    Camera cam = GetComponent<Camera>();
                    if (cam == null)
                        return;
                    if (_front)
                        PlaceFront(cam);
                    else
                        PlaceBack(cam);
                    TickVideo();
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Camera follow failed: " + ex.Message);
                }
            }

            private static void PlaceBack(Camera cam)
            {
                Camera main = Camera.main;
                if (main == null)
                    return;
                Quaternion rot = Quaternion.AngleAxis(_camOrbit, Vector3.up)
                    * main.transform.rotation
                    * Quaternion.Euler(-_camLift * 40f, 0f, 0f);
                Vector3 pos = main.transform.position + Vector3.up * (_camLift * 0.35f) + rot * Vector3.forward * 0.28f;
                cam.transform.SetPositionAndRotation(pos, rot);
                ApplyZoom(cam, main.fieldOfView);
                cam.nearClipPlane = 0.08f;
                cam.farClipPlane = main.farClipPlane;
                cam.cullingMask = main.cullingMask;
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
                cam.targetTexture = _rt;
                if (_rt != null)
                    cam.aspect = (float)_rt.width / _rt.height;
            }

            private static void PlaceFront(Camera cam)
            {
                Character local = Character.localCharacter;
                if (local == null || local.refs == null || local.refs.head == null)
                {
                    PlaceBack(cam);
                    return;
                }
                PlaceOrbit(cam, local.refs.head.transform, local.refs.head.transform.forward, 1.55f);
                ApplyZoom(cam, 50f);
                cam.nearClipPlane = 0.12f;
                cam.targetTexture = _rt;
                if (_rt != null)
                    cam.aspect = (float)_rt.width / _rt.height;
            }

            private static void PlaceOrbit(Camera cam, Transform head, Vector3 forward, float dist)
            {
                Vector3 look = head.position + Vector3.up * _camLift;
                Vector3 flat = forward;
                flat.y = 0f;
                if (flat.sqrMagnitude < 0.0001f)
                    flat = Vector3.forward;
                flat.Normalize();
                Vector3 orbitFwd = Quaternion.AngleAxis(_camOrbit, Vector3.up) * flat;
                cam.transform.position = look + orbitFwd * dist;
                cam.transform.LookAt(look);
            }

            private static void TickVideo()
            {
                if (!_recording || _rt == null || _recorder == null || !_recorder.IsOpen)
                    return;
                _accum += Time.unscaledDeltaTime;
                float step = 1f / PhoneVideo.Fps;
                if (_accum < step)
                    return;
                _accum -= step;
                if (_frames >= PhoneVideo.Fps * PhoneVideo.MaxSeconds)
                {
                    StopVideo();
                    return;
                }
                try
                {
                    Texture2D tex = GrabView();
                    if (tex == null)
                        return;
                    if (!_posterSaved)
                    {
                        byte[] jpg = PhoneImages.EncodeJpg(tex, 86);
                        if (jpg != null && jpg.Length > 0)
                            File.WriteAllBytes(PhoneStore.VideoPosterPath(_videoId), jpg);
                        _posterSaved = true;
                    }
                    _recorder.WriteFrame(tex);
                    UnityEngine.Object.Destroy(tex);
                    _frames++;
                    if (_videoLabel != null)
                        _videoLabel.text = "REC  " + (_frames / PhoneVideo.Fps) + "s";
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Video frame failed: " + ex.Message);
                }
            }
        }
    }
}
