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
        private const float Handle = 24f;

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
                    _fill.anchorMin = new Vector2(0.22f, t0);
                    _fill.anchorMax = new Vector2(0.78f, t1);
                    _fill.offsetMin = Vector2.zero;
                    _fill.offsetMax = Vector2.zero;
                }
            }
            else
            {
                PlaceHandle(_lowH, new Vector2(t0, 0.5f));
                PlaceHandle(_highH, new Vector2(t1, 0.5f));
                if (_fill != null)
                {
                    _fill.anchorMin = new Vector2(t0, 0.22f);
                    _fill.anchorMax = new Vector2(t1, 0.78f);
                    _fill.offsetMin = Vector2.zero;
                    _fill.offsetMax = Vector2.zero;
                }
            }
        }

        private static void PlaceHandle(RectTransform handle, Vector2 anchor)
        {
            if (handle == null)
                return;
            handle.anchorMin = anchor;
            handle.anchorMax = anchor;
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(Handle, Handle);
        }
    }
}
