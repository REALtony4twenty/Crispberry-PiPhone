using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Soft-wires snosz SkinColorSliders. Closet shows a Custom tile on Skin only
    /// when that plugin is loaded, then opens its color-wheel UI on the phone.
    /// </summary>
    internal static class ClosetSkinSliders
    {
        internal const string PluginGuid = "com.snosz.skincolorsliders";

        private static bool _tried;
        private static Type _pluginType;
        private static bool _logged;
        private static GameObject _overlay;
        private static GameObject _wheel;
        private static Transform _homeParent;
        private static Vector3 _homePos;
        private static Vector3 _homeScale;
        private static Quaternion _homeRot;
        private static Vector2 _homeAnchorMin;
        private static Vector2 _homeAnchorMax;
        private static Vector2 _homePivot;
        private static Vector2 _homeSize;
        private static Vector2 _homeAnchored;

        internal static bool Available
        {
            get { return Instance() != null; }
        }

        internal static bool IsOpen
        {
            get { return _overlay != null && _overlay.activeSelf; }
        }

        internal static bool Hide()
        {
            if (!IsOpen)
                return false;
            RestoreWheel();
            if (_overlay != null)
                _overlay.SetActive(false);
            return true;
        }

        /// <summary>
        /// Skin Color Sliders keeps its own tint table. Passport skin taps go through
        /// that table; Closet has to do the same or vanilla swatches look like no-ops.
        /// </summary>
        internal static void ApplyPresetSash(int index, CustomizationOption option)
        {
            if (!Available)
                return;
            try
            {
                Type plugin = PluginType();
                if (plugin == null)
                    return;
                Type manager = plugin.Assembly.GetType("SkinColorSliders.SkinColorManager");
                if (manager == null)
                    return;
                Type partEnum = manager.GetNestedType("SkinPart");
                if (partEnum == null)
                    return;
                Color color;
                if (!TrySashTint(index, out color))
                {
                    if (option == null || option.color.a < 0.05f)
                        return;
                    color = option.color;
                }
                FieldInfo currentSash = manager.GetField("currentSashIndex", BindingFlags.Public | BindingFlags.Static);
                if (currentSash != null)
                    currentSash.SetValue(null, index);
                MethodInfo apply = manager.GetMethod("ApplyColor", new Type[] { partEnum, typeof(Color) });
                if (apply == null)
                    return;
                object sash = Enum.ToObject(partEnum, 4);
                apply.Invoke(null, new object[] { sash, color });
                MethodInfo dummy = manager.GetMethod("UpdateDummyColors", new Type[] { partEnum, typeof(Color) });
                if (dummy != null)
                    dummy.Invoke(null, new object[] { sash, color });
            }
            catch (Exception ex)
            {
                LogOnce("Closet sash color: " + ex.Message);
            }
        }

        private static bool TrySashTint(int index, out Color color)
        {
            color = Color.white;
            Character ch = Character.localCharacter;
            if (ch == null || ch.refs == null || ch.refs.customization == null || ch.refs.customization.refs == null)
                return false;
            Material[] mats = ch.refs.customization.refs.sashAscentMaterials;
            if (mats == null || index < 0 || index >= mats.Length || mats[index] == null)
                return false;
            if (!mats[index].HasProperty("_Tint"))
                return false;
            color = mats[index].GetColor("_Tint");
            return true;
        }

        internal static void ApplyPresetSkin(CustomizationOption option, int index)
        {
            if (!Available || option == null)
                return;
            try
            {
                Type plugin = PluginType();
                if (plugin == null)
                    return;
                Type manager = plugin.Assembly.GetType("SkinColorSliders.SkinColorManager");
                if (manager == null)
                    return;
                Type partEnum = manager.GetNestedType("SkinPart");
                if (partEnum == null)
                    return;
                int mesh = index > 8 ? 0 : index;
                FieldInfo currentIndex = manager.GetField("currentIndex", BindingFlags.Public | BindingFlags.Static);
                if (currentIndex != null)
                    currentIndex.SetValue(null, mesh);
                MethodInfo apply = manager.GetMethod("ApplyColor", new Type[] { partEnum, typeof(Color) });
                if (apply == null)
                    return;
                Color color = option.color.a > 0.05f ? option.color : Color.white;
                object face = Enum.ToObject(partEnum, 0);
                object body = Enum.ToObject(partEnum, 1);
                apply.Invoke(null, new object[] { face, color });
                apply.Invoke(null, new object[] { body, color });
                MethodInfo dummy = manager.GetMethod("UpdateDummyColors", new Type[] { partEnum, typeof(Color) });
                if (dummy != null)
                    dummy.Invoke(null, new object[] { face, color });
                object inst = Instance();
                object picker = Field(inst, "colorPicker");
                if (picker != null)
                {
                    PropertyInfo col = picker.GetType().GetProperty("CurrentColor");
                    if (col != null && col.CanWrite)
                        col.SetValue(picker, color, null);
                }
            }
            catch (Exception ex)
            {
                LogOnce("Closet skin color: " + ex.Message);
            }
        }

        internal static void Open(IPiPhoneHost host)
        {
            try
            {
                if (host != null && !host.IsLandscape)
                {
                    PiPhoneApi.SetUserLandscape(true);
                    host.StartHostCoroutine(OpenAfterRotate(host));
                    return;
                }
                OpenNow(host);
            }
            catch (Exception ex)
            {
                LogOnce("Closet Skin Color Sliders: " + ex.Message);
                if (host != null)
                    host.ShowToast("Couldn't open Skin Color Sliders.");
            }
        }

        private static IEnumerator OpenAfterRotate(IPiPhoneHost host)
        {
            yield return null;
            OpenNow(host);
        }

        private static void OpenNow(IPiPhoneHost host)
        {
            try
            {
                object inst = Instance();
                if (inst == null || host == null || host.Content == null)
                {
                    if (host != null)
                        host.ShowToast("Skin Color Sliders isn't ready.");
                    return;
                }
                GameObject wheel = EnsureWheel(inst, host);
                if (wheel == null)
                {
                    host.ShowToast("Couldn't open Skin Color Sliders.");
                    return;
                }
                EnsureOverlay(host);
                ParkWheel(wheel);
                Transform overlayHost = _overlay.transform.Find("Host");
                wheel.transform.SetParent(overlayHost != null ? overlayHost : _overlay.transform, false);
                Canvas.ForceUpdateCanvases();
                FitWheel(wheel);
                wheel.SetActive(true);
                _overlay.SetActive(true);
                _overlay.transform.SetAsLastSibling();
            }
            catch (Exception ex)
            {
                LogOnce("Closet Skin Color Sliders: " + ex.Message);
                if (host != null)
                    host.ShowToast("Couldn't open Skin Color Sliders.");
            }
        }

        private static GameObject EnsureWheel(object inst, IPiPhoneHost host)
        {
            GameObject wheel = Field(inst, "colorWheelUI") as GameObject;
            if (wheel != null)
                return wheel;
            PassportManager pm = UnityEngine.Object.FindFirstObjectByType<PassportManager>(FindObjectsInactive.Include);
            MethodInfo create = _pluginType.GetMethod("CreateAndInitializeColorWheel", BindingFlags.Public | BindingFlags.Instance);
            if (pm != null && create != null)
            {
                create.Invoke(inst, new object[] { pm });
                wheel = Field(inst, "colorWheelUI") as GameObject;
                if (wheel != null)
                    return wheel;
            }
            object bundle = Field(inst, "colorWheelUIBundle");
            if (bundle == null)
                return null;
            MethodInfo load = bundle.GetType().GetMethod("LoadAsset", new Type[] { typeof(string), typeof(Type) });
            GameObject prefab = load != null
                ? load.Invoke(bundle, new object[] { "SkinColorSlidersUIThemed", typeof(GameObject) }) as GameObject
                : null;
            if (prefab == null)
                return null;
            Transform parent = host.Content;
            wheel = UnityEngine.Object.Instantiate(prefab, parent);
            wheel.name = "SkinColorSlidersUIThemed";
            SetField(inst, "colorWheelUI", wheel);
            Type wheelUi = _pluginType.Assembly.GetType("SkinColorSliders.ColorWheelUI");
            if (wheelUi != null && wheel.GetComponent(wheelUi) == null)
                wheel.AddComponent(wheelUi);
            Type pickerType = _pluginType.Assembly.GetType("HSVPicker.ColorPicker");
            if (pickerType != null)
            {
                Component picker = wheel.GetComponentInChildren(pickerType, true);
                if (picker != null)
                    SetField(inst, "colorPicker", picker);
            }
            wheel.SetActive(false);
            return wheel;
        }

        private static void EnsureOverlay(IPiPhoneHost host)
        {
            if (_overlay != null)
                return;
            Transform body = host.Content.parent != null ? host.Content.parent : host.Content;
            var rt = PhoneUi.CreateImage(body, "ClosetSliders", PhoneUi.White(), new Color(0.04f, 0.05f, 0.06f, 0.82f));
            PhoneUi.IgnoreLayout(rt.gameObject);
            PhoneUi.Stretch(rt, 0f, 0f);
            _overlay = rt.gameObject;
            var hostGo = new GameObject("Host", typeof(RectTransform));
            hostGo.transform.SetParent(_overlay.transform, false);
            PhoneUi.Stretch(hostGo.GetComponent<RectTransform>(), 10f, 40f);
            Button done = PhoneUi.MaterialChip(_overlay.transform, "check", "Done", () => Hide(), new Vector2(36f, 32f));
            PhoneUi.IgnoreLayout(done.gameObject);
            var doneRt = done.GetComponent<RectTransform>();
            doneRt.anchorMin = new Vector2(1f, 1f);
            doneRt.anchorMax = new Vector2(1f, 1f);
            doneRt.pivot = new Vector2(1f, 1f);
            doneRt.sizeDelta = new Vector2(88f, 32f);
            doneRt.anchoredPosition = new Vector2(-8f, -8f);
        }

        private static void ParkWheel(GameObject wheel)
        {
            if (wheel == null || _wheel == wheel)
                return;
            _wheel = wheel;
            RectTransform rt = wheel.transform as RectTransform;
            _homeParent = wheel.transform.parent;
            _homePos = wheel.transform.localPosition;
            _homeScale = wheel.transform.localScale;
            _homeRot = wheel.transform.localRotation;
            if (rt != null)
            {
                _homeAnchorMin = rt.anchorMin;
                _homeAnchorMax = rt.anchorMax;
                _homePivot = rt.pivot;
                _homeSize = rt.sizeDelta;
                _homeAnchored = rt.anchoredPosition;
            }
        }

        private static void RestoreWheel()
        {
            if (_wheel == null)
                return;
            _wheel.SetActive(false);
            if (_homeParent != null)
                _wheel.transform.SetParent(_homeParent, false);
            _wheel.transform.localPosition = _homePos;
            _wheel.transform.localScale = _homeScale;
            _wheel.transform.localRotation = _homeRot;
            RectTransform rt = _wheel.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = _homeAnchorMin;
                rt.anchorMax = _homeAnchorMax;
                rt.pivot = _homePivot;
                rt.sizeDelta = _homeSize;
                rt.anchoredPosition = _homeAnchored;
            }
        }

        private static void FitWheel(GameObject wheel)
        {
            RectTransform rt = wheel.transform as RectTransform;
            if (rt == null)
                return;
            Canvas[] canvases = wheel.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null)
                    canvases[i].enabled = false;
            }
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            Vector2 size = rt.rect.size;
            if (size.x < 8f)
                size = rt.sizeDelta;
            if (size.x < 8f)
                size = new Vector2(720f, 540f);
            RectTransform host = rt.parent as RectTransform;
            float hw = host != null ? host.rect.width : 320f;
            float hh = host != null ? host.rect.height : 400f;
            if (hw < 8f)
                hw = 320f;
            if (hh < 8f)
                hh = 400f;
            float s = Mathf.Min(hw / size.x, hh / size.y) * 0.92f;
            if (s > 1f)
                s = 1f;
            if (s < 0.18f)
                s = 0.32f;
            rt.localScale = new Vector3(s, s, 1f);
        }

        private static object Instance()
        {
            Type t = PluginType();
            if (t == null)
                return null;
            FieldInfo f = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            return f != null ? f.GetValue(null) : null;
        }

        private static Type PluginType()
        {
            if (_tried)
                return _pluginType;
            _tried = true;
            _pluginType = Type.GetType("SkinColorSliders.SkinColorSliders, SkinColorSliders", false);
            if (_pluginType != null)
                return _pluginType;
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                try
                {
                    if (asms[i] == null || asms[i].GetName().Name != "SkinColorSliders")
                        continue;
                    _pluginType = asms[i].GetType("SkinColorSliders.SkinColorSliders");
                    if (_pluginType != null)
                        return _pluginType;
                }
                catch
                {
                }
            }
            return null;
        }

        private static object Field(object obj, string name)
        {
            if (obj == null)
                return null;
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(obj) : null;
        }

        private static void SetField(object obj, string name, object value)
        {
            if (obj == null)
                return;
            FieldInfo f = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null)
                f.SetValue(obj, value);
        }

        private static void LogOnce(string message)
        {
            if (_logged)
                return;
            _logged = true;
            Plugin.LogError(message);
        }
    }
}
