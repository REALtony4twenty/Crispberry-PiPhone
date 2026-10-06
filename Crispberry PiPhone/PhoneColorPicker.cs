using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal sealed class PhoneColorPicker : MonoBehaviour
    {
        private const float FieldW = 220f;
        private const float FieldH = 120f;
        private const float HueH = 18f;

        internal Action<Color> OnChanged;

        private RectTransform _svRt;
        private RectTransform _hueRt;
        private Image _svImg;
        private RectTransform _svMark;
        private RectTransform _hueMark;
        private Image _preview;
        private Slider _brightness;
        private Slider _opacity;
        private TMP_InputField _hex;
        private Texture2D _svTex;
        private Texture2D _hueTex;
        private Sprite _svSprite;
        private Sprite _hueSprite;
        private float _hueUsed = -1f;
        private bool _hexEdit;
        private bool _writingHex;
        private float _a = 1f;
        private float _h;
        private float _s = 1f;
        private float _v = 1f;
        private bool _suppress;

        internal static PhoneColorPicker Create(Transform parent, Color start, Action<Color> onChanged)
        {
            var go = new GameObject("ColorPicker", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 280f;
            le.preferredHeight = -1f;
            le.flexibleWidth = 1f;
            le.flexibleHeight = 0f;
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            PhoneUi.AddVertical(go, 6f, new RectOffset(4, 4, 4, 4)).childAlignment = TextAnchor.UpperCenter;

            var picker = go.AddComponent<PhoneColorPicker>();
            picker.OnChanged = onChanged;
            picker.Build();
            picker.SetColor(start, false);
            return picker;
        }

        internal void SetColor(Color color, bool notify)
        {
            Color.RGBToHSV(color, out _h, out _s, out _v);
            _a = Mathf.Clamp01(color.a);
            _suppress = true;
            if (_brightness != null)
            {
                _brightness.SetValueWithoutNotify(_v);
                WritePct(_brightness, Mathf.RoundToInt(_v * 100f) + "%");
            }
            if (_opacity != null)
            {
                _opacity.SetValueWithoutNotify(_a);
                WritePct(_opacity, Mathf.RoundToInt(_a * 100f) + "%");
            }
            EnsureSv();
            UpdateMarkers();
            UpdatePreview();
            WriteHex();
            _suppress = false;
            if (notify)
                RaiseChanged();
        }

        internal Color Current
        {
            get
            {
                Color color = Color.HSVToRGB(_h, _s, _v);
                color.a = _a;
                return color;
            }
        }

        private void Build()
        {
            _svRt = MakeField("Sv", FieldH);
            _svImg = _svRt.GetComponent<Image>();
            var svHit = _svRt.gameObject.AddComponent<FieldHit>();
            svHit.Picker = this;
            svHit.Hue = false;
            _svMark = MakeMark(_svRt);

            _hueRt = MakeField("Hue", HueH);
            BuildHue(_hueRt.GetComponent<Image>());
            var hueHit = _hueRt.gameObject.AddComponent<FieldHit>();
            hueHit.Picker = this;
            hueHit.Hue = true;
            _hueMark = MakeMark(_hueRt);

            var hexRow = new GameObject("Hex", typeof(RectTransform));
            hexRow.transform.SetParent(transform, false);
            PhoneUi.Size(hexRow, 36f);
            var hexLayout = PhoneUi.AddHorizontal(hexRow, 6f);
            hexLayout.childForceExpandWidth = false;
            hexLayout.childAlignment = TextAnchor.MiddleCenter;
            _hex = PhoneUi.CreateInput(hexRow.transform, "#RRGGBB", 9);
            PhoneUi.SetClickable(_hex.gameObject, true);
            var hexLe = _hex.GetComponent<LayoutElement>();
            hexLe.flexibleWidth = 1f;
            hexLe.minWidth = 120f;
            _hex.onSelect.AddListener(delegate { _hexEdit = true; });
            _hex.onDeselect.AddListener(delegate { _hexEdit = false; });
            _hex.onEndEdit.AddListener(ApplyHex);
            var copy = PhoneUi.CreateIconChip(hexRow.transform, "Copy", null, CopyHex, false, new Vector2(64f, 32f));
            PhoneUi.SetTooltip(copy.gameObject, "Copy color code");

            var previewRt = PhoneUi.CreateImage(transform, "Preview", PhoneUi.Rounded(10), Color.white);
            PhoneUi.Size(previewRt.gameObject, 22f);
            _preview = previewRt.GetComponent<Image>();
            _preview.raycastTarget = false;

            _brightness = PhoneUi.CreateSliderRow(transform, "Brightness", 0f, 1f, _v, v =>
            {
                if (_suppress)
                    return;
                _v = Mathf.Clamp01(v);
                PhoneTheme.LivePaint = true;
                UpdateMarkers();
                UpdatePreview();
                WriteHex();
                RaiseChanged();
            }, null, "brightness_6");
            _opacity = PhoneUi.CreateSliderRow(transform, "Opacity", 0f, 1f, _a, v =>
            {
                if (_suppress)
                    return;
                _a = Mathf.Clamp01(v);
                PhoneTheme.LivePaint = true;
                UpdatePreview();
                WriteHex();
                RaiseChanged();
            }, null, "opacity");
        }

        private RectTransform MakeField(string name, float height)
        {
            var rt = PhoneUi.CreateImage(transform, name, PhoneUi.White(), Color.white);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minWidth = FieldW;
            le.preferredWidth = FieldW;
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            var img = rt.GetComponent<Image>();
            img.raycastTarget = true;
            return rt;
        }

        private static RectTransform MakeMark(RectTransform parent)
        {
            var mark = PhoneUi.CreateImage(parent, "Mark", PhoneUi.Circle(), Color.white);
            mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
            mark.pivot = new Vector2(0.5f, 0.5f);
            mark.sizeDelta = new Vector2(12f, 12f);
            mark.GetComponent<Image>().raycastTarget = false;
            PhoneUi.IgnoreLayout(mark.gameObject);
            return mark;
        }

        private void BuildHue(Image image)
        {
            const int n = 180;
            _hueTex = new Texture2D(n, 1, TextureFormat.RGBA32, false);
            _hueTex.wrapMode = TextureWrapMode.Clamp;
            _hueTex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[n];
            for (int x = 0; x < n; x++)
                pixels[x] = Color.HSVToRGB(x / (float)(n - 1), 1f, 1f);
            _hueTex.SetPixels(pixels);
            _hueTex.Apply(false, true);
            _hueSprite = Sprite.Create(_hueTex, new Rect(0f, 0f, n, 1f), new Vector2(0.5f, 0.5f), 100f);
            image.sprite = _hueSprite;
            image.type = Image.Type.Simple;
        }

        private void EnsureSv()
        {
            if (_svTex != null && Mathf.Abs(_hueUsed - _h) < 0.002f)
                return;
            const int n = 48;
            if (_svTex == null)
            {
                _svTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                _svTex.wrapMode = TextureWrapMode.Clamp;
                _svTex.filterMode = FilterMode.Bilinear;
            }
            var pixels = new Color[n * n];
            float span = n - 1f;
            for (int y = 0; y < n; y++)
            {
                float v = y / span;
                for (int x = 0; x < n; x++)
                {
                    float s = x / span;
                    pixels[y * n + x] = Color.HSVToRGB(_h, s, v);
                }
            }
            _svTex.SetPixels(pixels);
            _svTex.Apply(false, false);
            if (_svSprite != null)
                Destroy(_svSprite);
            _svSprite = Sprite.Create(_svTex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            if (_svImg != null)
                _svImg.sprite = _svSprite;
            _hueUsed = _h;
        }

        internal void HitSv(float x, float y)
        {
            _s = Mathf.Clamp01(x);
            _v = Mathf.Clamp01(y);
            _suppress = true;
            if (_brightness != null)
            {
                _brightness.SetValueWithoutNotify(_v);
                WritePct(_brightness, Mathf.RoundToInt(_v * 100f) + "%");
            }
            _suppress = false;
            PhoneTheme.LivePaint = true;
            UpdateMarkers();
            UpdatePreview();
            WriteHex();
            RaiseChanged();
        }

        internal void HitHue(float x)
        {
            _h = Mathf.Clamp01(x);
            PhoneTheme.LivePaint = true;
            EnsureSv();
            UpdateMarkers();
            UpdatePreview();
            WriteHex();
            RaiseChanged();
        }

        private void ApplyHex(string raw)
        {
            _hexEdit = false;
            if (_writingHex || _suppress || string.IsNullOrEmpty(raw))
                return;
            string text = raw.Trim();
            if (text.Length > 0 && text[0] != '#')
                text = "#" + text;
            Color parsed;
            if (!ColorUtility.TryParseHtmlString(text, out parsed))
            {
                WriteHex();
                return;
            }
            int digits = text.Length - 1;
            if (digits <= 6)
                parsed.a = _a;
            SetColor(parsed, true);
        }

        private void CopyHex()
        {
            GUIUtility.systemCopyBuffer = "#" + ColorUtility.ToHtmlStringRGBA(Current);
        }

        private void WriteHex()
        {
            if (_hex == null || _hexEdit)
                return;
            _writingHex = true;
            _hex.text = "#" + ColorUtility.ToHtmlStringRGBA(Current);
            _writingHex = false;
        }

        private static void WritePct(Slider slider, string text)
        {
            if (slider == null)
                return;
            Transform row = slider.transform.parent;
            if (row == null)
                return;
            var labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == "Pct")
                    labels[i].text = text;
            }
        }

        private void UpdateMarkers()
        {
            Place(_svMark, _svRt, _s, _v);
            Place(_hueMark, _hueRt, _h, 0.5f);
            if (_svMark != null)
                _svMark.GetComponent<Image>().color = _v > 0.55f ? Color.black : Color.white;
        }

        private static void Place(RectTransform mark, RectTransform field, float x, float y)
        {
            if (mark == null || field == null)
                return;
            mark.anchorMin = mark.anchorMax = new Vector2(x, y);
            mark.anchoredPosition = Vector2.zero;
        }

        private void UpdatePreview()
        {
            if (_preview != null)
                _preview.color = Current;
        }

        private void RaiseChanged()
        {
            if (_suppress)
                return;
            Action<Color> handler = OnChanged;
            if (handler != null)
                handler(Current);
        }

        private void OnDestroy()
        {
            if (PhoneTheme.LivePaint)
                PhoneTheme.EndLivePaint();
            if (_svSprite != null)
                Destroy(_svSprite);
            if (_hueSprite != null)
                Destroy(_hueSprite);
            if (_svTex != null)
                Destroy(_svTex);
            if (_hueTex != null)
                Destroy(_hueTex);
        }

        private sealed class FieldHit : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            internal PhoneColorPicker Picker;
            internal bool Hue;

            public void OnPointerDown(PointerEventData eventData)
            {
                Hit(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                Hit(eventData);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                PhoneTheme.EndLivePaint();
            }

            private void Hit(PointerEventData eventData)
            {
                if (Picker == null || eventData == null)
                    return;
                var rt = transform as RectTransform;
                if (rt == null)
                    return;
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out local))
                    return;
                Rect rect = rt.rect;
                float x = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
                float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
                if (Hue)
                    Picker.HitHue(x);
                else
                    Picker.HitSv(x, y);
            }
        }
    }
}
