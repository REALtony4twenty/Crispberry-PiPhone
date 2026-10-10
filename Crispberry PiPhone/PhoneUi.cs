using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Android-styled uGUI helpers. Other mods can use these so third-party apps
    /// match the phone chrome instead of cloning Peak pause-menu buttons.
    /// </summary>
    public static class PhoneUi
    {
        public static Color Bezel = new Color(0.07f, 0.07f, 0.08f, 1f);
        public static Color Screen = new Color(0.08f, 0.10f, 0.13f, 1f);
        public static Color Surface = new Color(0.16f, 0.18f, 0.21f, 0.96f);
        public static Color SurfaceAlt = new Color(0.20f, 0.22f, 0.26f, 1f);
        public static Color Text = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color ClockText = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color BatteryText = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color SignalText = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color ButtonText = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color TextDim = new Color(0.70f, 0.74f, 0.78f, 1f);
        public static Color Accent = new Color(0.24f, 0.86f, 0.52f, 1f);
        public static Color CallGreen = new Color(0.18f, 0.72f, 0.38f, 1f);
        public static Color HangRed = new Color(0.86f, 0.24f, 0.24f, 1f);
        public static Color Nav = new Color(0.10f, 0.11f, 0.13f, 1f);

        public static Color PhoneIcon = new Color(0.18f, 0.72f, 0.38f, 1f);
        public static Color MessagesIcon = new Color(0.15f, 0.55f, 0.92f, 1f);
        public static Color VoicemailIcon = new Color(0.95f, 0.55f, 0.16f, 1f);
        public static Color SettingsIcon = new Color(0.45f, 0.48f, 0.52f, 1f);
        public static Color NotesIcon = new Color(0.96f, 0.78f, 0.20f, 1f);
        public static Color CameraIcon = new Color(0.35f, 0.72f, 0.82f, 1f);
        public static Color PhotosIcon = new Color(0.72f, 0.42f, 0.82f, 1f);
        public static Color MemosIcon = new Color(0.92f, 0.32f, 0.42f, 1f);

        private static Sprite _white;
        private static Sprite _circle;
        private static Sprite _wallpaper;
        private static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        private static TMP_FontAsset _cachedFont;
        private static int _cachedSceneHandle = -1;
        private static RectTransform _tipRoot;
        private static TextMeshProUGUI _tipLabel;

        public static Canvas CreateOverlayCanvas(GameObject root, int sortingOrder)
        {
            var canvas = root.GetComponent<Canvas>();
            bool created = canvas == null;
            if (created)
                canvas = root.AddComponent<Canvas>();
            if (created)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
            }
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            var scaler = root.GetComponent<CanvasScaler>() ?? root.AddComponent<CanvasScaler>();
            if (created)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
            }

            if (root.GetComponent<GraphicRaycaster>() == null)
                root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>
        /// A picture. It does not take clicks. Call <see cref="SetClickable"/> when this picture is a button.
        /// </summary>
        public static RectTransform CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// Turn click-testing on or off for a picture you built yourself.
        /// Buttons need this on. Backgrounds, icon art, and card faces should leave it off.
        /// </summary>
        public static void SetClickable(GameObject go, bool clickable)
        {
            if (go == null)
                return;
            Graphic graphic = go.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = clickable;
        }

        public static TextMeshProUGUI CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles style = FontStyles.Normal,
            TextAlignmentOptions align = TextAlignmentOptions.Left)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text ?? string.Empty;
            tmp.fontSize = fontSize * PhoneTheme.FontScale;
            var basis = go.AddComponent<FontBasis>();
            basis.Size = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.color = Text;
            tmp.raycastTarget = false;
#pragma warning disable CS0618
            tmp.enableWordWrapping = false;
#pragma warning restore CS0618
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.maskable = true;
            ApplyFont(tmp);
            return tmp;
        }

        public static Button CreateButton(Transform parent, string label, UnityAction onClick, Vector2 preferredSize)
        {
            var rt = CreateImage(parent, "Btn_" + Sanitize(label), ButtonShape(), SurfaceAlt);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.colors = TintColors();
            var tmp = CreateLabel(rt, "Text", label, 18f, FontStyles.Normal, TextAlignmentOptions.Center);
            tmp.color = ButtonText;
            Stretch(tmp.rectTransform, 8f, 4f);
            ApplySize(rt, preferredSize);
            PhoneSfx.BindPress(button, onClick, false, false);
            return button;
        }

        /// <summary>Compact shade/toolbar chip. Prefer a sprite icon; <paramref name="glyph"/> is the fallback.</summary>
        public static Button CreateIconChip(Transform parent, string glyph, Sprite icon, UnityAction onClick, bool lit, Vector2 size, bool tintIcon = true, bool backSound = false, bool trashSound = false, string pressCue = null)
        {
            Color fill = lit ? Accent : SurfaceAlt;
            var rt = CreateImage(parent, "Chip", ChipShape(), fill);
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.colors = TintColors();
            Color fg = !tintIcon ? Color.white : (lit ? new Color(0.10f, 0.11f, 0.12f, 1f) : ButtonText);
            if (icon != null)
            {
                var art = CreateImage(rt, "I", icon, fg);
                Stretch(art, 6f, 6f);
                var artImg = art.GetComponent<Image>();
                artImg.raycastTarget = false;
                artImg.preserveAspect = true;
                artImg.type = Image.Type.Simple;
            }
            else
            {
                var tmp = CreateLabel(rt, "G", glyph ?? string.Empty, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                tmp.color = fg;
                Stretch(tmp.rectTransform, 2f, 2f);
            }
            ApplySize(rt, size);
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = size.x;
            le.minHeight = size.y;
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
            bool trash = trashSound || glyph == "Delete" || glyph == "Trash" || glyph == "Uninstall";
            if (!trash && icon != null)
            {
                Sprite del = PhoneIcons.Material("delete");
                trash = del != null && icon == del;
            }
            if (!string.IsNullOrEmpty(pressCue))
                PhoneSfx.BindCue(button, onClick, pressCue);
            else
                PhoneSfx.BindPress(button, onClick, backSound, trash);
            return button;
        }

        /// <summary>Phone chip for a built-in Material icon name. Falls back to <paramref name="fallback"/> text if the picture is missing.</summary>
        public static Button MaterialChip(Transform parent, string iconName, string fallback, UnityAction onClick, Vector2 size)
        {
            return CreateIconChip(parent, fallback, PhoneIcons.Material(iconName), onClick, false, size, true, iconName == "arrow_back", iconName == "delete");
        }

        public static void SetChipIcon(Button button, Sprite icon, string glyph, bool tintIcon = true)
        {
            if (button == null)
                return;
            Transform art = button.transform.Find("I");
            Transform label = button.transform.Find("G");
            Color fg = tintIcon ? ButtonText : Color.white;
            if (icon != null)
            {
                if (art == null)
                {
                    var created = CreateImage(button.transform, "I", icon, fg);
                    Stretch(created, 6f, 6f);
                    var createdImg = created.GetComponent<Image>();
                    createdImg.raycastTarget = false;
                    createdImg.preserveAspect = true;
                    createdImg.type = Image.Type.Simple;
                }
                else
                {
                    var img = art.GetComponent<Image>();
                    if (img != null)
                    {
                        img.sprite = icon;
                        img.color = fg;
                    }
                    art.gameObject.SetActive(true);
                }
                if (label != null)
                    label.gameObject.SetActive(false);
                return;
            }
            if (art != null)
                art.gameObject.SetActive(false);
            if (label == null)
            {
                var tmp = CreateLabel(button.transform, "G", glyph ?? string.Empty, 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                tmp.color = ButtonText;
                Stretch(tmp.rectTransform, 2f, 2f);
                return;
            }
            label.gameObject.SetActive(true);
            var text = label.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.text = glyph ?? string.Empty;
        }

        public static void SetCircleIcon(Button button, Sprite icon)
        {
            if (button == null || icon == null)
                return;
            var labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
                labels[i].gameObject.SetActive(false);
            Transform art = button.transform.Find("I");
            if (art == null)
            {
                var created = CreateImage(button.transform, "I", icon, ButtonText);
                Stretch(created, 16f, 16f);
                var createdImg = created.GetComponent<Image>();
                createdImg.raycastTarget = false;
                createdImg.preserveAspect = true;
                createdImg.type = Image.Type.Simple;
                return;
            }
            art.gameObject.SetActive(true);
            var img = art.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = icon;
                img.color = ButtonText;
            }
        }

        public static Button CreateCircleButton(Transform parent, string label, UnityAction onClick, float diameter, Color color)
        {
            var rt = CreateImage(parent, "CircleBtn_" + Sanitize(label), Circle(), color);
            var img = rt.GetComponent<Image>();
            img.type = Image.Type.Simple;
            img.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.colors = TintColors();
            var tmp = CreateLabel(rt, "Text", label, Mathf.Max(14f, diameter * 0.28f), FontStyles.Normal, TextAlignmentOptions.Center);
            tmp.color = ButtonText;
            Stretch(tmp.rectTransform, 4f, 4f);
            ApplySize(rt, new Vector2(diameter, diameter));
            PhoneSfx.BindPress(button, onClick, false, false);
            return button;
        }

        public static ScrollRect CreateScrollView(Transform parent, out RectTransform content)
        {
            var root = CreateImage(parent, "Scroll", White(), new Color(1f, 1f, 1f, 0f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.08f;
            scroll.scrollSensitivity = 28f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(root, false);
            Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f);
            var vpImg = viewport.GetComponent<Image>();
            vpImg.sprite = White();
            vpImg.color = new Color(1f, 1f, 1f, 0f);
            vpImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            return scroll;
        }

        /// <summary>
        /// Left-to-right flick scroller. The view stays the width of its parent.
        /// Put pictures in <paramref name="content"/>; a content fitter grows it to the right.
        /// </summary>
        public static ScrollRect CreateHorizontalScroll(Transform parent, out RectTransform content)
        {
            var root = CreateImage(parent, "HScroll", White(), new Color(1f, 1f, 1f, 0f));
            var rootLe = root.gameObject.GetComponent<LayoutElement>() ?? root.gameObject.AddComponent<LayoutElement>();
            rootLe.minWidth = 0f;
            rootLe.preferredWidth = 0f;
            rootLe.flexibleWidth = 1f;
            rootLe.layoutPriority = 2;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.08f;
            scroll.scrollSensitivity = 28f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(root, false);
            Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f);
            var vpImg = viewport.GetComponent<Image>();
            vpImg.sprite = White();
            vpImg.color = new Color(1f, 1f, 1f, 0f);
            vpImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport.transform, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var fit = contentGo.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;
            return scroll;
        }

        public static Slider CreateSlider(Transform parent, float min, float max, float value, bool wholeNumbers, UnityAction<float> onChanged)
        {
            var root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rootLe = root.AddComponent<LayoutElement>();
            rootLe.flexibleWidth = 1f;
            rootLe.minWidth = 80f;
            rootLe.minHeight = 22f;
            rootLe.preferredHeight = 22f;

            Color fillColor = Accent;
            Color track = new Color(fillColor.r * 0.28f, fillColor.g * 0.28f, fillColor.b * 0.28f, 1f);
            var bg = CreateImage(root.transform, "Background", White(), track);
            Stretch(bg, 0f, 0f);
            var bgImg = bg.GetComponent<Image>();
            bgImg.type = Image.Type.Simple;
            bgImg.raycastTarget = true;

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 0f, 0f);

            var fill = CreateImage(fillArea.transform, "Fill", White(), fillColor);
            fill.GetComponent<Image>().type = Image.Type.Simple;
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().raycastTarget = false;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            var ha = handleArea.GetComponent<RectTransform>();
            ha.anchorMin = Vector2.zero;
            ha.anchorMax = Vector2.one;
            ha.offsetMin = Vector2.zero;
            ha.offsetMax = Vector2.zero;

            var handle = CreateImage(handleArea.transform, "Handle", White(), new Color(1f, 1f, 1f, 0f));
            var handleRt = handle.GetComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(1f, 1f);
            handleRt.anchoredPosition = Vector2.zero;
            var handleImg = handle.GetComponent<Image>();
            handleImg.raycastTarget = false;

            var slider = root.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(v =>
            {
                if (onChanged != null)
                    onChanged(v);
                PhoneSfx.PlayTick();
            });
            var drag = root.AddComponent<FillDrag>();
            drag.Slider = slider;
            return slider;
        }

        private sealed class FillDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public Slider Slider;

            public void OnPointerDown(PointerEventData eventData)
            {
                Apply(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                Apply(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                PhoneTheme.EndLivePaint();
            }

            private void Apply(PointerEventData eventData)
            {
                if (Slider == null)
                    return;
                var rt = transform as RectTransform;
                if (rt == null)
                    return;
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out local))
                    return;
                bool vertical = Slider.direction == Slider.Direction.BottomToTop || Slider.direction == Slider.Direction.TopToBottom;
                float t = vertical
                    ? Mathf.InverseLerp(rt.rect.yMin, rt.rect.yMax, local.y)
                    : Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
                if (Slider.direction == Slider.Direction.RightToLeft || Slider.direction == Slider.Direction.TopToBottom)
                    t = 1f - t;
                Slider.value = Mathf.Lerp(Slider.minValue, Slider.maxValue, Mathf.Clamp01(t));
            }
        }

        public static Slider CreateThinSlider(Transform parent, bool vertical, float min, float max, float value, UnityAction<float> onChanged)
        {
            Slider slider = CreateSlider(parent, min, max, value, false, onChanged);
            var le = slider.GetComponent<LayoutElement>();
            if (vertical)
            {
                le.minWidth = 28f;
                le.preferredWidth = 28f;
                le.flexibleWidth = 0f;
                le.minHeight = 180f;
                le.preferredHeight = 240f;
                le.flexibleHeight = 1f;
                slider.direction = Slider.Direction.BottomToTop;
            }
            else
            {
                le.minWidth = 80f;
                le.preferredWidth = 200f;
                le.flexibleWidth = 1f;
                le.minHeight = 28f;
                le.preferredHeight = 32f;
                le.flexibleHeight = 0f;
                slider.direction = Slider.Direction.LeftToRight;
            }
            RectTransform handleRt = slider.handleRect;
            if (handleRt != null)
            {
                handleRt.sizeDelta = new Vector2(16f, 16f);
                if (vertical)
                {
                    handleRt.anchorMin = new Vector2(0.5f, 0f);
                    handleRt.anchorMax = new Vector2(0.5f, 0f);
                    RectTransform ha = handleRt.parent as RectTransform;
                    if (ha != null)
                    {
                        ha.offsetMin = new Vector2(0f, 8f);
                        ha.offsetMax = new Vector2(0f, -8f);
                    }
                }
            }
            return slider;
        }

        /// <summary>
        /// One track with start and end handles. Portrait: bottom=start, top=end.
        /// Landscape: left=start, right=end. The track expands to fill leftover layout space.
        /// </summary>
        public static DualRangeSlider CreateDualRange(
            Transform parent,
            bool vertical,
            float min,
            float max,
            float low,
            float high,
            UnityAction<float> onLow,
            UnityAction<float> onHigh)
        {
            var root = new GameObject("DualRange", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var le = root.AddComponent<LayoutElement>();
            Color fillColor = Accent;
            Color track = new Color(fillColor.r * 0.28f, fillColor.g * 0.28f, fillColor.b * 0.28f, 1f);
            RectTransform bar;
            if (vertical)
            {
                le.minWidth = 120f;
                le.preferredWidth = 200f;
                le.flexibleWidth = 1f;
                le.minHeight = 140f;
                le.preferredHeight = 180f;
                le.flexibleHeight = 1f;
                bar = CreateImage(root.transform, "Track", White(), track);
                bar.anchorMin = new Vector2(0.5f, 0f);
                bar.anchorMax = new Vector2(0.5f, 1f);
                bar.pivot = new Vector2(0.5f, 0.5f);
                bar.sizeDelta = new Vector2(22f, 0f);
                bar.anchoredPosition = Vector2.zero;
            }
            else
            {
                le.minWidth = 160f;
                le.preferredWidth = 320f;
                le.flexibleWidth = 1f;
                le.minHeight = 44f;
                le.preferredHeight = 44f;
                le.flexibleHeight = 0f;
                bar = CreateImage(root.transform, "Track", White(), track);
                bar.anchorMin = new Vector2(0f, 1f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(0.5f, 1f);
                bar.sizeDelta = new Vector2(0f, 22f);
                bar.anchoredPosition = Vector2.zero;
            }
            var barImg = bar.GetComponent<Image>();
            barImg.type = Image.Type.Simple;
            barImg.raycastTarget = true;

            var fill = CreateImage(bar, "Fill", White(), fillColor);
            var fillImg = fill.GetComponent<Image>();
            fillImg.raycastTarget = false;
            fillImg.type = Image.Type.Simple;

            var lowH = CreateImage(bar, "Low", White(), new Color(1f, 1f, 1f, 0f));
            lowH.GetComponent<Image>().raycastTarget = false;
            var highH = CreateImage(bar, "High", White(), new Color(1f, 1f, 1f, 0f));
            highH.GetComponent<Image>().raycastTarget = false;

            var dual = bar.gameObject.AddComponent<DualRangeSlider>();
            dual.Bind(bar, fill, lowH, highH, vertical, min, max, low, high, onLow, onHigh);
            return dual;
        }

        public static void TintToggle(Button chip, bool on)
        {
            if (chip == null)
                return;
            Transform art = chip.transform.Find("I");
            var img = art != null ? art.GetComponent<Image>() : null;
            if (img != null)
                img.color = on ? PhoneTheme.ToggleOnColor : PhoneTheme.ToggleOffColor;
        }

        /// <summary>Short chip that hugs the toggle glyph. The icon art is a square with empty space above and below the switch.</summary>
        public static Button CreateToggleChip(Transform parent, bool on, UnityAction onClick)
        {
            string cue = on ? "toggle-off" : "toggle-on";
            var button = CreateIconChip(parent, on ? "On" : "Off", PhoneIcons.Material(on ? "toggle_on" : "toggle_off"), onClick, false, new Vector2(32f, 18f), true, false, false, cue);
            FitToggleArt(button);
            TintToggle(button, on);
            return button;
        }

        public static void FitToggleArt(Button chip)
        {
            if (chip == null)
                return;
            Transform art = chip.transform.Find("I");
            var rt = art != null ? art.GetComponent<RectTransform>() : null;
            if (rt == null)
                return;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(30f, 30f);
            rt.anchoredPosition = Vector2.zero;
        }

        public static Button CreateCheckRow(Transform parent, string label, bool on, UnityAction onClick)
        {
            var row = new GameObject("Chk_" + Sanitize(label), typeof(RectTransform));
            row.transform.SetParent(parent, false);
            Size(row, 36f);
            var h = AddHorizontal(row, 10f);
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            var boxRt = CreateImage(row.transform, "Box", Rounded(6), SurfaceAlt);
            var boxLe = boxRt.gameObject.AddComponent<LayoutElement>();
            boxLe.minWidth = 32f;
            boxLe.preferredWidth = 32f;
            boxLe.minHeight = 32f;
            boxLe.preferredHeight = 32f;
            boxLe.flexibleWidth = 0f;
            var boxImg = boxRt.GetComponent<Image>();
            boxImg.raycastTarget = true;
            var boxBtn = boxRt.gameObject.AddComponent<Button>();
            boxBtn.targetGraphic = boxImg;
            PhoneSfx.BindPress(boxBtn, onClick, false, false);
            var mark = CreateLabel(boxRt, "M", on ? "X" : string.Empty, 16f, FontStyles.Normal, TextAlignmentOptions.Center);
            Stretch(mark.rectTransform, 0f, 0f);
            var name = CreateLabel(row.transform, "L", label, 15f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return boxBtn;
        }

        public static Slider CreateSliderRow(Transform parent, string label, float min, float max, float value, UnityAction<float> onChanged)
        {
            return CreateSliderRow(parent, label, min, max, value, onChanged, null, null);
        }

        public static Slider CreateSliderRow(Transform parent, string label, float min, float max, float value, UnityAction<float> onChanged, System.Func<float, string> format)
        {
            return CreateSliderRow(parent, label, min, max, value, onChanged, format, null);
        }

        public static Slider CreateSliderRow(Transform parent, string label, float min, float max, float value, UnityAction<float> onChanged, System.Func<float, string> format, string icon)
        {
            var row = new GameObject("Slide_" + Sanitize(string.IsNullOrEmpty(label) ? icon : label), typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowLe = Size(row, 26f);
            rowLe.minWidth = 180f;
            rowLe.preferredWidth = 280f;
            rowLe.flexibleWidth = 1f;
            var h = AddHorizontal(row, 0f);
            h.padding = new RectOffset(0, 0, 0, 0);
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;
            Slider slider = CreateSlider(row.transform, min, max, value, false, null);
            var sliderLe = slider.GetComponent<LayoutElement>();
            sliderLe.minHeight = 26f;
            sliderLe.preferredHeight = 26f;
            TextMeshProUGUI pct = CreateLabel(slider.transform, "Pct", FormatValue(value, min, max, format), 12f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
            pct.color = Text;
            pct.raycastTarget = false;
            var pctRt = pct.rectTransform;
            pctRt.anchorMin = new Vector2(1f, 0f);
            pctRt.anchorMax = new Vector2(1f, 1f);
            pctRt.pivot = new Vector2(1f, 0.5f);
            pctRt.sizeDelta = new Vector2(44f, 0f);
            pctRt.anchoredPosition = new Vector2(-8f, 0f);
            IgnoreLayout(pct.gameObject);
            if (!string.IsNullOrEmpty(icon))
            {
                Sprite sprite = PhoneIcons.Material(icon);
                if (sprite != null)
                {
                    var glyph = CreateImage(slider.transform, "Ico", sprite, Text);
                    glyph.anchorMin = new Vector2(0f, 0.5f);
                    glyph.anchorMax = new Vector2(0f, 0.5f);
                    glyph.pivot = new Vector2(0f, 0.5f);
                    glyph.sizeDelta = new Vector2(16f, 16f);
                    glyph.anchoredPosition = new Vector2(8f, 0f);
                    var glyphImg = glyph.GetComponent<Image>();
                    glyphImg.preserveAspect = true;
                    glyphImg.raycastTarget = false;
                    IgnoreLayout(glyph.gameObject);
                }
            }
            pct.transform.SetAsLastSibling();
            if (!string.IsNullOrEmpty(label))
                SetTooltip(slider.gameObject, label);
            slider.onValueChanged.AddListener(v =>
            {
                pct.text = FormatValue(v, min, max, format);
                if (onChanged != null)
                    onChanged(v);
            });
            return slider;
        }

        private static string FormatValue(float value, float min, float max, System.Func<float, string> format)
        {
            if (format != null)
                return format(value);
            float t = max > min ? (value - min) / (max - min) : 0f;
            return Mathf.RoundToInt(t * 100f) + "%";
        }

        public static void IgnoreLayout(GameObject go)
        {
            if (go == null)
                return;
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        public static TMP_InputField CreateInput(Transform parent, string placeholder, int characterLimit = 120)
        {
            var root = CreateImage(parent, "Input", Rounded(14), Surface);
            var le = root.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 40f;
            le.preferredHeight = 40f;
            le.minWidth = 80f;
            le.flexibleWidth = 1f;

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(root, false);
            Stretch(textArea.GetComponent<RectTransform>(), 10f, 6f);

            var text = CreateLabel(textArea.transform, "Text", string.Empty, 16f);
            Stretch(text.rectTransform, 0f, 0f);
            text.raycastTarget = true;

            var ph = CreateLabel(textArea.transform, "Placeholder", placeholder, 16f, FontStyles.Italic);
            Stretch(ph.rectTransform, 0f, 0f);
            ph.color = TextDim;

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = root.GetComponent<Image>();
            input.textViewport = textArea.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = ph;
            input.caretColor = Accent;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.24f, 0.86f, 0.52f, 0.28f);
            input.onFocusSelectAll = false;
            input.restoreOriginalTextOnEscape = false;
            input.characterLimit = characterLimit;
            if (text.font != null)
                input.fontAsset = text.font;
            return input;
        }

        public static void FocusInput(TMP_InputField input)
        {
            if (input == null || !input.IsActive())
                return;
            try
            {
                EventSystem es = EventSystem.current;
                if (es != null)
                    es.SetSelectedGameObject(input.gameObject);
                input.Select();
                input.ActivateInputField();
            }
            catch
            {
            }
        }

        public static TMP_InputField CreateMultiline(Transform parent, string placeholder, int characterLimit = 4000)
        {
            TMP_InputField input = CreateInput(parent, placeholder, characterLimit);
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            var le = input.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = 160f;
                le.preferredHeight = 220f;
                le.flexibleHeight = 1f;
            }
#pragma warning disable CS0618
            if (input.textComponent != null)
            {
                input.textComponent.alignment = TextAlignmentOptions.TopLeft;
                input.textComponent.enableWordWrapping = true;
            }
            if (input.placeholder != null)
            {
                var ph = input.placeholder as TextMeshProUGUI;
                if (ph != null)
                {
                    ph.alignment = TextAlignmentOptions.TopLeft;
                    ph.enableWordWrapping = true;
                }
            }
#pragma warning restore CS0618
            return input;
        }

        public static void Wrap(TextMeshProUGUI tmp)
        {
            if (tmp == null)
                return;
#pragma warning disable CS0618
            tmp.enableWordWrapping = true;
#pragma warning restore CS0618
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        public static VerticalLayoutGroup AddVertical(GameObject go, float spacing, RectOffset padding)
        {
            var v = go.GetComponent<VerticalLayoutGroup>() ?? go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(12, 12, 12, 12);
            v.childAlignment = TextAnchor.UpperCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }

        public static HorizontalLayoutGroup AddHorizontal(GameObject go, float spacing)
        {
            var h = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = false;
            h.childForceExpandWidth = true;
            return h;
        }

        /// <summary>True while the phone bezel is landscape. Use this in OnOrientation rebuilds.</summary>
        public static bool Landscape
        {
            get { return PiPhoneApi.IsLandscape; }
        }

        /// <summary>Approximate inner content width for the current orientation.</summary>
        public static float ContentWidth()
        {
            return PhoneMenu.InnerWidth;
        }

        /// <summary>Cap a control width so landscape rows stay on the phone screen.</summary>
        public static float FitWidth(float wanted)
        {
            float max = ContentWidth() - 16f;
            if (max < 48f)
                max = Landscape ? 792f : 352f;
            if (wanted > max)
                return max;
            if (wanted < 48f)
                return 48f;
            return wanted;
        }

        /// <summary>Two equal columns for landscape app layouts. Parent should be a vertical layout (app Content).</summary>
        public static void CreateSplit(Transform parent, out RectTransform left, out RectTransform right)
        {
            var row = new GameObject("Split", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var le = row.AddComponent<LayoutElement>();
            le.flexibleHeight = 1f;
            le.minHeight = Landscape ? 120f : 180f;
            var h = AddHorizontal(row, 10f);
            h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.UpperCenter;
            left = CreatePane(row.transform, "Left");
            right = CreatePane(row.transform, "Right");
        }

        /// <summary>
        /// Landscape: two columns. Portrait: both outs are the parent. Rebuild from
        /// <see cref="PiPhoneApp.OnOrientation"/> so lists and media use the extra width.
        /// </summary>
        public static void SplitIfLandscape(Transform parent, out RectTransform left, out RectTransform right)
        {
            if (Landscape)
            {
                CreateSplit(parent, out left, out right);
                return;
            }
            var rt = parent as RectTransform;
            if (rt == null && parent != null)
                rt = parent.GetComponent<RectTransform>();
            left = rt;
            right = rt;
        }

        /// <summary>Photo/video thumbnail grid that fills the phone width (more columns in landscape).</summary>
        public static GridLayoutGroup ApplyMediaGrid(RectTransform content)
        {
            if (content == null)
                return null;
            var grid = content.GetComponent<GridLayoutGroup>() ?? content.gameObject.AddComponent<GridLayoutGroup>();
            int cols = Landscape ? 6 : 3;
            const float spacing = 8f;
            const float pad = 8f;
            float cell = Mathf.Floor((ContentWidth() - pad * 2f - spacing * (cols - 1)) / cols);
            if (cell < 48f)
                cell = 48f;
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(spacing, spacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.padding = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);
            grid.childAlignment = TextAnchor.UpperLeft;
            FitVertical(content.gameObject);
            return grid;
        }

        public static RectTransform CreatePane(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            le.minWidth = 80f;
            AddVertical(go, 8f, new RectOffset(4, 4, 4, 4));
            return go.GetComponent<RectTransform>();
        }

        /// <summary>
        /// A box for a picture your app keeps repainting, such as a game surface or a video.
        /// On a shared cast, watchers get what is inside as moving pictures and everything outside
        /// it described as usual. Parent the picture under the returned box and stretch it yourself.
        /// Use one per screen. With two showing, the whole app area is sent as moving pictures.
        /// See <see cref="PiPhoneApp.CastVideo"/>.
        /// </summary>
        public static RectTransform CreateVideoCastContainer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(PhoneCastVideoArea));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            le.minWidth = 80f;
            return go.GetComponent<RectTransform>();
        }

        public static ContentSizeFitter FitVertical(GameObject go)
        {
            var f = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        public static LayoutElement Size(GameObject go, float height, float width = 0f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
            if (width > 0f)
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            return le;
        }

        public static void Stretch(RectTransform rt, float padX, float padY)
        {
            if (rt == null)
                return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static void FitContained(RawImage raw, Texture tex)
        {
            if (raw == null || tex == null)
                return;
            int h = tex.height > 0 ? tex.height : 1;
            FitContained(raw, tex.width / (float)h);
        }

        public static void FitContained(RawImage raw, float aspect)
        {
            if (raw == null)
                return;
            if (aspect < 0.05f)
                aspect = 1f;
            var fitter = raw.GetComponent<AspectRatioFitter>();
            if (fitter == null)
                fitter = raw.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = aspect;
        }

        public static void StretchTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        public static void StretchBottom(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        public static void ApplySize(RectTransform rt, Vector2 size)
        {
            rt.sizeDelta = size;
            var le = rt.GetComponent<LayoutElement>() ?? rt.gameObject.AddComponent<LayoutElement>();
            float w = size.x > 0f ? FitWidth(size.x) : size.x;
            le.preferredWidth = w;
            le.preferredHeight = size.y;
            le.minHeight = size.y;
            le.minWidth = w > 0f ? Mathf.Min(48f, w) : le.minWidth;
            le.flexibleWidth = w > 0f ? 1f : le.flexibleWidth;
        }

        public static ColorBlock TintColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.88f, 0.92f, 1f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = new Color(0.88f, 0.92f, 1f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        public static Sprite White()
        {
            if (_white != null)
                return _white;
            Texture2D tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _white.name = "PiP_White";
            return _white;
        }

        public static Sprite Circle()
        {
            if (_circle != null)
                return _circle;
            _circle = MakeCircleSprite(64);
            return _circle;
        }

        public static Sprite Shape(int radius)
        {
            if (radius <= 0)
                return White();
            return Rounded(radius);
        }

        public static Sprite ButtonShape()
        {
            return Shape(PhoneTheme.ButtonRadius);
        }

        public static Sprite ChipShape()
        {
            int r = PhoneTheme.ButtonRadius;
            if (r <= 0)
                return White();
            return Rounded(Mathf.Max(2, r * 10 / 18));
        }

        public static Sprite IconWellShape()
        {
            return Shape(PhoneTheme.IconRadius);
        }

        public static Sprite Rounded(int radius)
        {
            int r = Mathf.Clamp(radius, 2, 48);
            Sprite sprite;
            if (RoundedCache.TryGetValue(r, out sprite) && sprite != null)
                return sprite;
            sprite = MakeRoundedSprite(r);
            RoundedCache[r] = sprite;
            return sprite;
        }

        public static Sprite Wallpaper()
        {
            if (_wallpaper != null)
                return _wallpaper;
            const int w = 8;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color top = new Color(0.14f, 0.36f, 0.48f, 1f);
            Color mid = new Color(0.10f, 0.18f, 0.32f, 1f);
            Color bot = new Color(0.05f, 0.07f, 0.14f, 1f);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = t < 0.5f
                    ? Color.Lerp(bot, mid, t * 2f)
                    : Color.Lerp(mid, top, (t - 0.5f) * 2f);
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply(false, true);
            _wallpaper = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            _wallpaper.name = "PiP_Wallpaper";
            return _wallpaper;
        }

        private static TMP_FontAsset _readableFont;

        /// <summary>
        /// Digits and other text that must stay readable. Uses a system face, not the game font's bold.
        /// </summary>
        public static void UseReadableBold(TextMeshProUGUI text)
        {
            if (text == null)
                return;
            if (text.GetComponent<ReadableLabel>() == null)
                text.gameObject.AddComponent<ReadableLabel>();
            TMP_FontAsset font = ReadableFont();
            if (font != null)
                text.font = font;
            text.fontStyle = FontStyles.Bold;
        }

        private static TMP_FontAsset ReadableFont()
        {
            if (_readableFont != null)
                return _readableFont;
            try
            {
                Font os = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Calibri" }, 64);
                if (os == null)
                    return null;
                _readableFont = TMP_FontAsset.CreateFontAsset(os);
                if (_readableFont != null)
                {
                    _readableFont.name = "PiP_Readable";
                    _readableFont.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch
            {
                _readableFont = null;
            }
            return _readableFont;
        }

        private sealed class ReadableLabel : MonoBehaviour
        {
        }

        private sealed class FontBasis : MonoBehaviour
        {
            public float Size;
        }

        public static void ApplyFontSizes(Transform root)
        {
            if (root == null)
                return;
            float scale = PhoneTheme.FontScale;
            FontBasis[] marks = root.GetComponentsInChildren<FontBasis>(true);
            for (int i = 0; i < marks.Length; i++)
            {
                FontBasis mark = marks[i];
                if (mark == null)
                    continue;
                var tmp = mark.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    tmp.fontSize = mark.Size * scale;
            }
        }

        public static void ApplyFont(TextMeshProUGUI text)
        {
            if (text == null)
                return;
            if (text.GetComponent<ReadableLabel>() != null)
            {
                UseReadableBold(text);
                return;
            }
            TMP_FontAsset font = GetGameFont();
            text.fontStyle = FontStyles.Normal;
            if (font != null)
                text.font = font;
        }

        public static void ApplyAllFonts(Transform root)
        {
            if (root == null)
                return;
            var tmps = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                TextMeshProUGUI tmp = tmps[i];
                if (tmp == null)
                    continue;
                Transform p = tmp.transform.parent;
                if (p != null && p.GetComponent<TMP_InputField>() != null)
                    continue;
                ApplyFont(tmp);
            }
        }

        public static void InvalidateFont()
        {
            _cachedFont = null;
            _cachedSceneHandle = -1;
        }

        public static bool IsTyping()
        {
            try
            {
                EventSystem es = EventSystem.current;
                if (es == null)
                    return false;
                GameObject sel = es.currentSelectedGameObject;
                if (sel == null)
                    return false;
                TMP_InputField tmp = sel.GetComponent<TMP_InputField>();
                if (tmp != null && tmp.isFocused)
                    return true;
                InputField ugui = sel.GetComponent<InputField>();
                if (ugui != null && ugui.isFocused)
                    return true;
            }
            catch
            {
            }
            return false;
        }

        public static void ReleaseUiFocus()
        {
            try
            {
                EventSystem es = EventSystem.current;
                if (es == null)
                    return;
                GameObject sel = es.currentSelectedGameObject;
                if (sel == null)
                    return;
                if (sel.GetComponentInParent<PhoneMenu>() == null)
                    return;
                es.SetSelectedGameObject(null);
            }
            catch
            {
            }
        }

        public static Button GetPauseMenuDonor(PauseMenuMainPage page)
        {
            if (page == null)
                return null;
            return page.m_resumeButton ?? page.m_settingsButton ?? page.m_quitButton;
        }

        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "Btn";
            char[] chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '_';
            }
            return new string(chars);
        }

        private static Sprite MakeRoundedSprite(int radius)
        {
            int size = Mathf.Max(radius * 4, 32);
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float r = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = CornerAlpha(x, y, size, r);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "PiP_Round" + radius;
            return sprite;
        }

        private static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            float cx = (size - 1) * 0.5f;
            float radius = cx - 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y - cx;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - dist + 1f);
                    byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PiP_Circle";
            return sprite;
        }

        private static float CornerAlpha(int x, int y, int size, float radius)
        {
            float min = radius;
            float max = size - 1 - radius;
            float px = x;
            float py = y;
            float cx = px;
            float cy = py;
            if (px < min && py < min)
            {
                cx = min;
                cy = min;
            }
            else if (px > max && py < min)
            {
                cx = max;
                cy = min;
            }
            else if (px < min && py > max)
            {
                cx = min;
                cy = max;
            }
            else if (px > max && py > max)
            {
                cx = max;
                cy = max;
            }
            else
                return 1f;

            float dx = px - cx;
            float dy = py - cy;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius - dist + 1f);
        }

        private static TMP_FontAsset GetGameFont()
        {
            int scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            if (_cachedFont != null && _cachedSceneHandle == scene)
                return _cachedFont;

            try
            {
                if (GUIManager.instance != null)
                {
                    var fromGui = GUIManager.instance.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (fromGui != null && fromGui.font != null)
                    {
                        _cachedFont = fromGui.font;
                        _cachedSceneHandle = scene;
                        return _cachedFont;
                    }
                }
            }
            catch
            {
            }

            try
            {
                var any = UObject.FindFirstObjectByType<TextMeshProUGUI>(FindObjectsInactive.Include);
                if (any != null && any.font != null)
                {
                    _cachedFont = any.font;
                    _cachedSceneHandle = scene;
                    return _cachedFont;
                }
            }
            catch
            {
            }

            return _cachedFont;
        }

        /// <summary>
        /// Hover text for a button or icon. Empty text removes it.
        /// Shows the line above the control while the pointer is over it.
        /// </summary>
        public static void SetTooltip(GameObject target, string text)
        {
            if (target == null)
                return;
            var tip = target.GetComponent<PhoneTip>();
            if (string.IsNullOrEmpty(text))
            {
                if (tip != null)
                {
                    tip.Text = null;
                    tip.enabled = false;
                }
                return;
            }
            if (tip == null)
                tip = target.AddComponent<PhoneTip>();
            tip.enabled = true;
            tip.Text = text;
        }

        internal static void ShowTooltip(string text, RectTransform around)
        {
            if (string.IsNullOrEmpty(text) || around == null)
                return;
            Canvas canvas = around.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            Canvas rootCanvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;
            Transform root = rootCanvas.transform;
            if (_tipRoot == null)
            {
                var bg = CreateImage(root, "Tooltip", Rounded(10), new Color(0.08f, 0.09f, 0.11f, 0.96f));
                _tipRoot = bg;
                bg.GetComponent<Image>().raycastTarget = false;
                _tipLabel = CreateLabel(bg, "T", text, 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                _tipLabel.raycastTarget = false;
                Wrap(_tipLabel);
                _tipLabel.gameObject.AddComponent<LayoutElement>();
                var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var v = AddVertical(bg.gameObject, 0f, new RectOffset(10, 10, 6, 6));
                v.childAlignment = TextAnchor.MiddleLeft;
                v.childForceExpandWidth = false;
                v.childForceExpandHeight = false;
            }
            else if (_tipRoot.parent != root)
                _tipRoot.SetParent(root, false);
            _tipLabel.text = text;
            Vector2 pref = _tipLabel.GetPreferredValues(text, 220f, 0f);
            var le = _tipLabel.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredWidth = Mathf.Clamp(Mathf.Ceil(pref.x), 24f, 220f);
                le.preferredHeight = Mathf.Ceil(Mathf.Max(18f, pref.y));
            }
            _tipRoot.gameObject.SetActive(true);
            _tipRoot.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            PlaceTooltip(around, rootCanvas);
        }

        private static void PlaceTooltip(RectTransform around, Canvas canvas)
        {
            var rootRt = canvas.transform as RectTransform;
            if (rootRt == null || _tipRoot == null)
                return;
            Vector3[] corners = new Vector3[4];
            around.GetWorldCorners(corners);
            Vector3 world = (corners[1] + corners[2]) * 0.5f;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRt, screen, cam, out local))
                return;
            _tipRoot.anchorMin = _tipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _tipRoot.pivot = new Vector2(0.5f, 0f);
            float tipW = Mathf.Max(24f, _tipRoot.rect.width);
            float tipH = Mathf.Max(18f, _tipRoot.rect.height);
            float halfRootW = rootRt.rect.width * 0.5f;
            float halfRootH = rootRt.rect.height * 0.5f;
            float x = Mathf.Clamp(local.x, -halfRootW + tipW * 0.5f + 8f, halfRootW - tipW * 0.5f - 8f);
            float y = local.y + 6f;
            if (y + tipH > halfRootH - 8f)
            {
                _tipRoot.pivot = new Vector2(0.5f, 1f);
                y = local.y - around.rect.height - 6f;
            }
            _tipRoot.anchoredPosition = new Vector2(x, y);
        }

        internal static void HideTooltip()
        {
            if (_tipRoot != null)
                _tipRoot.gameObject.SetActive(false);
        }
    }

    internal sealed class PhoneTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Text;

        public void OnPointerEnter(PointerEventData eventData)
        {
            PhoneUi.ShowTooltip(Text, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PhoneUi.HideTooltip();
        }

        private void OnDisable()
        {
            PhoneUi.HideTooltip();
        }
    }
}
