using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Saved scout contacts keyed by Photon UserId (Steam id on PEAK).
    /// Custom name and photo stay on this phone; the live nick is RealName.
    /// </summary>
    internal static class PhoneContacts
    {
        private static readonly Dictionary<string, PiPhoneContact> Map = new Dictionary<string, PiPhoneContact>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        public static string Dir
        {
            get { return Path.Combine(PhoneStore.RootDir, "contacts"); }
        }

        public static void Ensure()
        {
            Load();
        }

        public static string Display(string id, string fallback)
        {
            PiPhoneContact c = Get(id);
            if (c != null && !string.IsNullOrEmpty(c.CustomName))
                return c.CustomName;
            if (c != null && !string.IsNullOrEmpty(c.RealName))
                return c.RealName;
            return string.IsNullOrEmpty(fallback) ? "Scout" : fallback;
        }

        public static string Real(string id, string fallback)
        {
            PiPhoneContact c = Get(id);
            if (c != null && !string.IsNullOrEmpty(c.RealName))
                return c.RealName;
            return string.IsNullOrEmpty(fallback) ? "Scout" : fallback;
        }

        public static PiPhoneContact Get(string id)
        {
            Load();
            if (string.IsNullOrEmpty(id))
                return null;
            PiPhoneContact c;
            return Map.TryGetValue(id, out c) ? c : null;
        }

        public static PiPhoneContact[] All()
        {
            Load();
            var list = new List<PiPhoneContact>(Map.Count);
            foreach (KeyValuePair<string, PiPhoneContact> kv in Map)
            {
                if (kv.Value != null)
                    list.Add(kv.Value);
            }
            return list.ToArray();
        }

        public static bool IsSaved(string id)
        {
            PiPhoneContact c = Get(id);
            return c != null && (!string.IsNullOrEmpty(c.CustomName) || !string.IsNullOrEmpty(c.PhotoFile));
        }

        public static bool BlocksCalls(string id)
        {
            PiPhoneContact c = Get(id);
            return c != null && c.BlockCalls;
        }

        public static bool BlocksTexts(string id)
        {
            PiPhoneContact c = Get(id);
            return c != null && c.BlockTexts;
        }

        public static void SetBlockCalls(string id, bool blocked)
        {
            PiPhoneContact c = EnsureContact(id);
            if (c == null)
                return;
            c.BlockCalls = blocked;
            Save();
        }

        public static void SetBlockTexts(string id, bool blocked)
        {
            PiPhoneContact c = EnsureContact(id);
            if (c == null)
                return;
            c.BlockTexts = blocked;
            Save();
        }

        public static void See(string id, string realName)
        {
            PiPhoneContact c = EnsureContact(id);
            if (c == null)
                return;
            if (!string.IsNullOrEmpty(realName) && c.RealName != realName)
            {
                c.RealName = realName;
                Save();
            }
        }

        public static void SetCustomName(string id, string name)
        {
            PiPhoneContact c = EnsureContact(id);
            if (c == null)
                return;
            c.CustomName = name ?? string.Empty;
            Save();
        }

        public static bool SetPhoto(string id, byte[] pngOrJpg)
        {
            if (pngOrJpg == null || pngOrJpg.Length < 24)
                return false;
            PiPhoneContact c = EnsureContact(id);
            if (c == null)
                return false;
            Directory.CreateDirectory(Dir);
            string file = SafeId(id) + ".png";
            File.WriteAllBytes(Path.Combine(Dir, file), pngOrJpg);
            c.PhotoFile = file;
            Save();
            return true;
        }

        private static PiPhoneContact EnsureContact(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            Load();
            PiPhoneContact c;
            if (!Map.TryGetValue(id, out c) || c == null)
            {
                c = new PiPhoneContact { Id = id };
                Map[id] = c;
            }
            return c;
        }

        public static Sprite Photo(string id)
        {
            PiPhoneContact c = Get(id);
            if (c == null || string.IsNullOrEmpty(c.PhotoFile))
                return null;
            string path = Path.Combine(Dir, c.PhotoFile);
            if (!File.Exists(path))
                return null;
            return PhoneIcons.FromFile(path);
        }

        public static RectTransform CreateAvatar(Transform parent, string id, float size)
        {
            Sprite photo = Photo(id);
            if (photo != null)
            {
                var well = PhoneUi.CreateImage(parent, "Face", PhoneUi.Circle(), Color.white);
                PhoneUi.Size(well.gameObject, size, size);
                var mask = well.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var art = PhoneUi.CreateImage(well, "Art", photo, Color.white);
                PhoneUi.Stretch(art, 0f, 0f);
                var img = art.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.type = Image.Type.Simple;
                return well;
            }
            var fallback = PhoneUi.CreateImage(parent, "Face", PhoneUi.Circle(), PhoneUi.SurfaceAlt);
            PhoneUi.Size(fallback.gameObject, size, size);
            Sprite md = PhoneIcons.Material("face");
            if (md != null)
            {
                var art = PhoneUi.CreateImage(fallback, "Md", md, Color.white);
                PhoneUi.Stretch(art, size * 0.18f, size * 0.18f);
                art.GetComponent<Image>().raycastTarget = false;
                art.GetComponent<Image>().preserveAspect = true;
            }
            return fallback;
        }

        private static string SafeId(string id)
        {
            var sb = new StringBuilder(id.Length);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        private static void Load()
        {
            if (_loaded)
                return;
            _loaded = true;
            try
            {
                Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, "contacts.txt");
                if (!File.Exists(path))
                    return;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] p = lines[i].Split('\t');
                    if (p.Length < 2 || string.IsNullOrEmpty(p[0]))
                        continue;
                    Map[p[0]] = new PiPhoneContact
                    {
                        Id = p[0],
                        CustomName = p.Length > 1 ? p[1] : string.Empty,
                        RealName = p.Length > 2 ? p[2] : string.Empty,
                        PhotoFile = p.Length > 3 ? p[3] : string.Empty,
                        BlockCalls = p.Length > 4 && p[4] == "1",
                        BlockTexts = p.Length > 5 && p[5] == "1"
                    };
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Contacts load: " + ex.Message);
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var sb = new StringBuilder();
                foreach (KeyValuePair<string, PiPhoneContact> kv in Map)
                {
                    PiPhoneContact c = kv.Value;
                    if (c == null)
                        continue;
                    sb.Append(c.Id).Append('\t')
                        .Append(c.CustomName ?? string.Empty).Append('\t')
                        .Append(c.RealName ?? string.Empty).Append('\t')
                        .Append(c.PhotoFile ?? string.Empty).Append('\t')
                        .Append(c.BlockCalls ? "1" : "0").Append('\t')
                        .Append(c.BlockTexts ? "1" : "0").Append('\n');
                }
                File.WriteAllText(Path.Combine(Dir, "contacts.txt"), sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.LogError("Contacts save: " + ex.Message);
            }
        }
    }
}
