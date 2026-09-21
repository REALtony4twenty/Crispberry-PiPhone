using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal sealed class PhoneColorPicker : MonoBehaviour
    {
        private const float WheelSize = 140f;

        internal Action<Color> OnChanged;

        private RectTransform _wheelRt;
        private Image _preview;
        private RectTransform _marker;
        private Slider _brightness;
        private Texture2D _wheelTex;
        private Sprite _wheelSprite;
        private float _h;
        private float _s = 1f;
        private float _v = 1f;
        private bool _suppress;

        internal static PhoneColorPicker Create(Transform parent, Color start, Action<Color> onChanged)
        {
            var go = new GameObject("ColorPicker", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 220f;
            le.preferredHeight = 220f;
            le.flexibleWidth = 1f;
            PhoneUi.AddVertical(go, 8f, new RectOffset(4, 4, 4, 4)).childAlignment = TextAnchor.UpperCenter;

            var picker = go.AddComponent<PhoneColorPicker>();
            picker.OnChanged = onChanged;
            picker.Build();
            picker.SetColor(start, false);
            return picker;
        }

        internal void SetColor(Color color, bool notify)
        {
            Color.RGBToHSV(color, out _h, out _s, out _v);
            _suppress = true;
            if (_brightness != null)
            {
                _brightness.SetValueWithoutNotify(_v);
                Transform row = _brightness.transform.parent;
                if (row != null)
                {
                    var labels = row.GetComponentsInChildren<TextMeshProUGUI>(true);
                    for (int i = 0; i < labels.Length; i++)
                    {
                        if (labels[i] != null && labels[i].name == "Pct")
                            labels[i].text = Mathf.RoundToInt(_v * 100f) + "%";
                    }
                }
            }
            UpdateMarker();
            UpdatePreview();
            _suppress = false;
            if (notify)
                RaiseChanged();
        }

        internal Color Current
        {
            get { return Color.HSVToRGB(_h, _s, _v); }
        }

        private void Build()
        {
            var wheelGo = new GameObject("Wheel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            wheelGo.transform.SetParent(transform, false);
            var wheelLe = wheelGo.AddComponent<LayoutElement>();
            wheelLe.preferredWidth = WheelSize;
            wheelLe.preferredHeight = WheelSize;
            wheelLe.minWidth = WheelSize;
            wheelLe.minHeight = WheelSize;
            _wheelRt = wheelGo.GetComponent<RectTransform>();
            _wheelRt.sizeDelta = new Vector2(WheelSize, WheelSize);

            _wheelTex = MakeWheelTexture();
            _wheelSprite = Sprite.Create(_wheelTex, new Rect(0, 0, _wheelTex.width, _wheelTex.height), new Vector2(0.5f, 0.5f), 100f);
            _wheelSprite.name = "PiP_ColorWheel";
            var wheelImg = wheelGo.GetComponent<Image>();
            wheelImg.sprite = _wheelSprite;
            wheelImg.raycastTarget = true;
            wheelImg.preserveAspect = true;

            var hit = wheelGo.AddComponent<WheelHit>();
            hit.OnHit = OnWheelHit;

            var markerGo = new GameObject("Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerGo.transform.SetParent(wheelGo.transform, false);
            _marker = markerGo.GetComponent<RectTransform>();
            _marker.anchorMin = _marker.anchorMax = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(14f, 14f);
            var markerImg = markerGo.GetComponent<Image>();
            markerImg.sprite = PhoneUi.Circle();
            markerImg.color = Color.white;
            markerImg.raycastTarget = false;

            var previewRt = PhoneUi.CreateImage(transform, "Preview", PhoneUi.Rounded(10), Color.white);
            PhoneUi.Size(previewRt.gameObject, 28f);
            _preview = previewRt.GetComponent<Image>();
            _preview.raycastTarget = false;

            _brightness = PhoneUi.CreateSliderRow(transform, "Brightness", 0f, 1f, _v, v =>
            {
                if (_suppress)
                    return;
                _v = Mathf.Clamp01(v);
                UpdatePreview();
                RaiseChanged();
            });
        }

        private void OnWheelHit(Vector2 local01)
        {
            float nx = local01.x * 2f - 1f;
            float ny = local01.y * 2f - 1f;
            float radius = Mathf.Sqrt(nx * nx + ny * ny);
            if (radius > 1.05f)
                return;
            if (radius > 1f)
                radius = 1f;
            _s = radius;
            float hue = Mathf.Atan2(ny, nx) / (Mathf.PI * 2f);
            if (hue < 0f)
                hue += 1f;
            _h = hue;
            UpdateMarker();
            UpdatePreview();
            RaiseChanged();
        }

        private void UpdateMarker()
        {
            if (_marker == null)
                return;
            float angle = _h * Mathf.PI * 2f;
            float r = _s * (WheelSize * 0.5f - 4f);
            _marker.anchoredPosition = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            _marker.GetComponent<Image>().color = _v > 0.55f ? Color.black : Color.white;
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

        private static Texture2D MakeWheelTexture()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[size * size];
            float mid = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - mid) / mid;
                    float ny = (y - mid) / mid;
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    if (radius > 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }
                    float hue = Mathf.Atan2(ny, nx) / (Mathf.PI * 2f);
                    if (hue < 0f)
                        hue += 1f;
                    Color c = Color.HSVToRGB(hue, radius, 1f);
                    c.a = 1f;
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private void OnDestroy()
        {
            if (_wheelSprite != null)
                Destroy(_wheelSprite);
            if (_wheelTex != null)
                Destroy(_wheelTex);
        }

        private sealed class WheelHit : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            internal Action<Vector2> OnHit;

            public void OnPointerDown(PointerEventData eventData)
            {
                Hit(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                Hit(eventData);
            }

            private void Hit(PointerEventData eventData)
            {
                var rt = transform as RectTransform;
                if (rt == null || eventData == null)
                    return;
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out local))
                    return;
                Rect rect = rt.rect;
                float x = Mathf.InverseLerp(rect.xMin, rect.xMax, local.x);
                float y = Mathf.InverseLerp(rect.yMin, rect.yMax, local.y);
                Action<Vector2> handler = OnHit;
                if (handler != null)
                    handler(new Vector2(x, y));
            }
        }
    }
}
