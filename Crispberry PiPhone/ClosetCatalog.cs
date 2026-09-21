using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Soft-wires cretapark More Customizations into Closet. Custom .pcab packs
    /// (Valen's Hats, etc.) are injected into the game catalog so GetList / SetCharacter*
    /// see them without opening the passport.
    /// </summary>
    internal static class ClosetCatalog
    {
        internal const string MoreCustomizationsGuid = "MoreCustomizations";

        private static bool _tried;
        private static bool _logged;
        private static Type _pluginType;
        private static int _baseHats = -1;
        private static int _overrideHats = -1;

        internal static void Ensure()
        {
            try
            {
                Customization catalog = Customization.Instance;
                if (catalog == null)
                    return;
                Type plugin = PluginType();
                if (plugin == null)
                    return;
                object data = GetStatic(plugin, "AllCustomizationsData");
                if (data == null)
                    return;
                IEnumerable pairs = data as IEnumerable;
                if (pairs == null)
                    return;

                ReadHatCounts(plugin, catalog);
                bool added = false;
                foreach (object kv in pairs)
                {
                    if (kv == null)
                        continue;
                    Type kvType = kv.GetType();
                    object key = GetProp(kvType, kv, "Key");
                    object list = GetProp(kvType, kv, "Value");
                    if (key == null || list == null)
                        continue;
                    Customization.Type type = (Customization.Type)key;
                    added |= AppendMissing(catalog, type, list as IEnumerable);
                }
                if (added)
                    Plugin.LogInfo("Closet: added More Customizations options to the catalog.");
            }
            catch (Exception ex)
            {
                LogOnce("Closet More Customizations: " + ex.Message);
            }
        }

        internal static int ApplyIndex(Customization.Type type, int listIndex)
        {
            if (type != Customization.Type.Hat)
                return listIndex;
            EnsureHatCounts();
            if (_baseHats > 0 && listIndex >= _baseHats)
                return listIndex + Math.Max(0, _overrideHats);
            return listIndex;
        }

        internal static int ListIndex(Customization.Type type, int applied)
        {
            if (type != Customization.Type.Hat)
                return applied;
            EnsureHatCounts();
            if (_baseHats > 0 && _overrideHats > 0 && applied >= _baseHats + _overrideHats)
                return applied - _overrideHats;
            return applied;
        }

        internal static string PrettyName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;
            int us = raw.IndexOf('_');
            if (us <= 0 || us >= raw.Length - 1)
                return raw;
            int dummy;
            if (int.TryParse(raw.Substring(0, us), out dummy))
                return raw.Substring(us + 1);
            return raw;
        }

        private static void EnsureHatCounts()
        {
            if (_baseHats >= 0)
                return;
            try
            {
                Customization catalog = Customization.Instance;
                Type plugin = PluginType();
                if (catalog != null)
                    ReadHatCounts(plugin, catalog);
            }
            catch (Exception ex)
            {
                LogOnce("Closet hat counts: " + ex.Message);
            }
        }

        private static void ReadHatCounts(Type plugin, Customization catalog)
        {
            int mcBase = plugin != null ? ReadInt(plugin, "BaseHatCount") : 0;
            int mcOver = plugin != null ? ReadInt(plugin, "OverrideHatCount") : 0;
            if (mcBase > 0)
                _baseHats = mcBase;
            else if (_baseHats < 0 && catalog.hats != null)
                _baseHats = catalog.hats.Length;
            if (mcOver > 0)
                _overrideHats = mcOver;
            else if (_overrideHats < 0)
                _overrideHats = CountOverrideHats(catalog);
        }

        private static int CountOverrideHats(Customization catalog)
        {
            int n = 0;
            if (catalog == null || catalog.fits == null)
                return 0;
            for (int i = 0; i < catalog.fits.Length; i++)
            {
                if (catalog.fits[i] != null && catalog.fits[i].overrideHat)
                    n++;
            }
            return n;
        }

        private static bool AppendMissing(Customization catalog, Customization.Type type, IEnumerable items)
        {
            if (items == null)
                return false;
            CustomizationOption[] src = catalog.GetList(type);
            var list = new List<CustomizationOption>(src != null ? src.Length + 8 : 8);
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (src != null)
            {
                for (int i = 0; i < src.Length; i++)
                {
                    list.Add(src[i]);
                    string n = OptionKey(src[i]);
                    if (!string.IsNullOrEmpty(n))
                        names.Add(n);
                }
            }
            bool added = false;
            foreach (object item in items)
            {
                if (item == null || !IsValid(item))
                    continue;
                string key = ObjName(item);
                if (string.IsNullOrEmpty(key) || names.Contains(key))
                    continue;
                CustomizationOption opt = MakeOption(catalog, type, item);
                if (opt == null)
                    continue;
                list.Add(opt);
                names.Add(key);
                added = true;
            }
            if (!added)
                return false;
            WriteList(catalog, type, list.ToArray());
            return true;
        }

        private static CustomizationOption MakeOption(Customization catalog, Customization.Type type, object data)
        {
            CustomizationOption opt = ScriptableObject.CreateInstance<CustomizationOption>();
            opt.name = ObjName(data);
            opt.type = type;
            opt.requiredAchievement = 0;
            opt.texture = IconOf(data);
            if (type != Customization.Type.Fit)
                return opt;
            try
            {
                Type t = data.GetType();
                opt.fitMesh = GetProp(t, data, "FitMesh") as Mesh;
                opt.isSkirt = GetBool(t, data, "IsSkirt");
                opt.noPants = GetBool(t, data, "NoPants");
                opt.drawUnderEye = GetBool(t, data, "DrawUnderEye");
                Material template = null;
                if (catalog.fits != null)
                {
                    for (int i = 0; i < catalog.fits.Length; i++)
                    {
                        if (catalog.fits[i] != null && catalog.fits[i].fitMaterial != null)
                        {
                            template = catalog.fits[i].fitMaterial;
                            break;
                        }
                    }
                }
                if (template != null)
                {
                    Texture main = GetProp(t, data, "FitMainTexture") as Texture;
                    Texture shoes = GetProp(t, data, "FitShoeTexture") as Texture;
                    Texture hat = GetProp(t, data, "FitOverrideHatTexture") as Texture;
                    Texture pants = GetProp(t, data, "FitOverridePantsTexture") as Texture;
                    opt.fitMaterial = UnityEngine.Object.Instantiate(template);
                    if (main != null)
                        opt.fitMaterial.SetTexture("_MainTex", main);
                    opt.fitMaterialShoes = UnityEngine.Object.Instantiate(template);
                    if (shoes != null)
                        opt.fitMaterialShoes.SetTexture("_MainTex", shoes);
                    if (hat != null)
                    {
                        opt.fitMaterialOverrideHat = UnityEngine.Object.Instantiate(template);
                        opt.fitMaterialOverrideHat.SetTexture("_MainTex", hat);
                    }
                    if (pants != null)
                    {
                        opt.fitMaterialOverridePants = UnityEngine.Object.Instantiate(template);
                        opt.fitMaterialOverridePants.SetTexture("_MainTex", pants);
                    }
                }
            }
            catch (Exception ex)
            {
                LogOnce("Closet custom outfit: " + ex.Message);
            }
            return opt;
        }

        private static void WriteList(Customization catalog, Customization.Type type, CustomizationOption[] arr)
        {
            if (type == Customization.Type.Skin) catalog.skins = arr;
            else if (type == Customization.Type.Accessory) catalog.accessories = arr;
            else if (type == Customization.Type.Eyes) catalog.eyes = arr;
            else if (type == Customization.Type.Mouth) catalog.mouths = arr;
            else if (type == Customization.Type.Fit) catalog.fits = arr;
            else if (type == Customization.Type.Hat) catalog.hats = arr;
            else if (type == Customization.Type.Sash) catalog.sashes = arr;
            else if (type == Customization.Type.Medal) catalog.medals = arr;
        }

        private static Type PluginType()
        {
            if (_tried)
                return _pluginType;
            _tried = true;
            _pluginType = Type.GetType("MoreCustomizations.MoreCustomizationsPlugin, MoreCustomizations", false);
            if (_pluginType != null)
                return _pluginType;
            Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                try
                {
                    if (asms[i] == null || asms[i].GetName().Name != "MoreCustomizations")
                        continue;
                    _pluginType = asms[i].GetType("MoreCustomizations.MoreCustomizationsPlugin");
                    if (_pluginType != null)
                        return _pluginType;
                }
                catch
                {
                }
            }
            return null;
        }

        private static object GetStatic(Type type, string name)
        {
            PropertyInfo p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            return p != null ? p.GetValue(null, null) : null;
        }

        private static int ReadInt(Type type, string name)
        {
            object v = GetStatic(type, name);
            return v is int ? (int)v : 0;
        }

        private static object GetProp(Type type, object obj, string name)
        {
            PropertyInfo p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return p != null ? p.GetValue(obj, null) : null;
        }

        private static bool GetBool(Type type, object obj, string name)
        {
            object v = GetProp(type, obj, name);
            return v is bool && (bool)v;
        }

        private static bool IsValid(object data)
        {
            object v = GetProp(data.GetType(), data, "IsValid");
            if (v is bool)
                return (bool)v;
            return true;
        }

        private static Texture IconOf(object data)
        {
            return GetProp(data.GetType(), data, "IconTexture") as Texture;
        }

        private static string ObjName(object data)
        {
            UnityEngine.Object uo = data as UnityEngine.Object;
            return uo != null ? uo.name : string.Empty;
        }

        private static string OptionKey(CustomizationOption opt)
        {
            return opt != null ? opt.name : string.Empty;
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
