using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Shared-track range control. Vertical: start at the bottom, end at the top.
    /// Horizontal: start at the left, end at the right.
    /// </summary>
    public sealed class DualRangeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IInitializePotentialDragHandler
    {
        private RectTransform _track;
        private RectTransform _fill;
        private RectTransform _lowH;
        private RectTransform _highH;
        private bool _vertical;
        private float _min;
        private float _max = 1f;
        private float _low;
        private float _high = 1f;
        private UnityAction<float> _onLow;
        private UnityAction<float> _onHigh;
        private RectTransform _startLab;
        private RectTransform _endLab;
        private bool _dragLow;

        public void Bind(
            RectTransform track,
            RectTransform fill,
            RectTransform lowHandle,
            RectTransform highHandle,
            bool vertical,
            float min,
            float max,
            float low,
            float high,
            UnityAction<float> onLow,
            UnityAction<float> onHigh)
        {
            _track = track;
            _fill = fill;
            _lowH = lowHandle;
            _highH = highHandle;
            _vertical = vertical;
            _min = min;
            _max = max;
            _low = low;
            _high = high;
            _onLow = onLow;
            _onHigh = onHigh;
            ApplyLayout();
        }

        public void Readouts(RectTransform start, RectTransform end)
        {
            _startLab = start;
            _endLab = end;
            ApplyLayout();
        }

        public void Set(float low, float high)
        {
            _low = low;
            _high = high;
            ApplyLayout();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            float v = ValueFrom(eventData);
            _dragLow = Mathf.Abs(v - _low) <= Mathf.Abs(v - _high);
            Fire(v);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Fire(ValueFrom(eventData));
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyLayout();
        }

        private void Fire(float v)
        {
            PhoneSfx.PlayTick();
            if (_dragLow)
            {
                if (_onLow != null)
                    _onLow(v);
            }
            else if (_onHigh != null)
                _onHigh(v);
        }

        private float ValueFrom(PointerEventData eventData)
        {
            if (_track == null)
                return _low;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, eventData.position, eventData.pressEventCamera, out local))
                return _dragLow ? _low : _high;
            Rect r = _track.rect;
            float t;
            if (_vertical)
                t = (local.y - r.yMin) / Mathf.Max(1f, r.height);
            else
                t = (local.x - r.xMin) / Mathf.Max(1f, r.width);
            t = Mathf.Clamp01(t);
            return _min + t * Span();
        }

        private float Span()
        {
            float span = _max - _min;
            return span < 0.0001f ? 0.0001f : span;
        }

        private void ApplyLayout()
        {
            if (_track == null)
                return;
            float t0 = Mathf.Clamp01((_low - _min) / Span());
            float t1 = Mathf.Clamp01((_high - _min) / Span());
            if (_vertical)
            {
                PlaceHandle(_lowH, new Vector2(0.5f, t0));
                PlaceHandle(_highH, new Vector2(0.5f, t1));
                if (_fill != null)
                {
                    _fill.anchorMin = new Vector2(0f, t0);
                    _fill.anchorMax = new Vector2(1f, t1);
                    _fill.offsetMin = Vector2.zero;
                    _fill.offsetMax = Vector2.zero;
                }
                PlaceSideReadout(_startLab, t0, true);
                PlaceSideReadout(_endLab, t1, false);
            }
            else
            {
                PlaceHandle(_lowH, new Vector2(t0, 0.5f));
                PlaceHandle(_highH, new Vector2(t1, 0.5f));
                if (_fill != null)
                {
                    _fill.anchorMin = new Vector2(t0, 0f);
                    _fill.anchorMax = new Vector2(t1, 1f);
                    _fill.offsetMin = Vector2.zero;
                    _fill.offsetMax = Vector2.zero;
                }
                PlaceReadout(_startLab, t0, 0f, true);
                PlaceReadout(_endLab, t1, 0f, true);
            }
        }

        private static void PlaceReadout(RectTransform lab, float x, float y, bool horizontal)
        {
            if (lab == null)
                return;
            float along = horizontal ? x : y;
            float px = 0.5f;
            if (along < 0.14f)
                px = 0f;
            else if (along > 0.86f)
                px = 1f;
            lab.anchorMin = lab.anchorMax = new Vector2(horizontal ? x : 0.5f, horizontal ? 0f : y);
            lab.pivot = new Vector2(px, 1f);
            lab.anchoredPosition = new Vector2(0f, -2f);
            lab.sizeDelta = new Vector2(56f, 16f);
        }

        private static void PlaceSideReadout(RectTransform lab, float y, bool left)
        {
            if (lab == null)
                return;
            lab.anchorMin = lab.anchorMax = new Vector2(left ? 0f : 1f, y);
            lab.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            lab.anchoredPosition = new Vector2(left ? -8f : 8f, 0f);
            lab.sizeDelta = new Vector2(56f, 16f);
        }

        private static void PlaceHandle(RectTransform handle, Vector2 anchor)
        {
            if (handle == null)
                return;
            handle.anchorMin = anchor;
            handle.anchorMax = anchor;
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(1f, 1f);
        }
    }
}
