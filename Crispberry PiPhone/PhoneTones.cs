using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Default + per-app + per-contact alert sounds. Empty id means "use default".
    /// </summary>
    internal static class PhoneTones
    {
        public static bool DuckMusic = true;

        private static readonly Dictionary<string, string> AppMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> RingMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> TextMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Load()
        {
            DuckMusic = true;
            AppMap.Clear();
            RingMap.Clear();
            TextMap.Clear();
            string path = Path.Combine(PhoneStore.RootDir, "tones.txt");
            if (!File.Exists(path))
                return;
            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] p = lines[i].Split('\t');
                    if (p.Length < 2)
                        continue;
                    string kind = p[0];
                    if (kind == "duck")
                        DuckMusic = p[1] != "0";
                    else if (kind == "app" && p.Length >= 3)
                        Put(AppMap, p[1], p[2]);
                    else if (kind == "contact" && p.Length >= 4)
                    {
                        if (p[2] == "ring")
                            Put(RingMap, p[1], p[3]);
                        else if (p[2] == "text")
                            Put(TextMap, p[1], p[3]);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Tones load failed: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                PhoneStore.EnsureDir();
                var sb = new StringBuilder();
                sb.Append("duck\t").Append(DuckMusic ? "1" : "0").Append('\n');
                WriteMap(sb, "app", AppMap);
                WriteContact(sb, "ring", RingMap);
                WriteContact(sb, "text", TextMap);
                File.WriteAllText(Path.Combine(PhoneStore.RootDir, "tones.txt"), sb.ToString());
            }
            catch (Exception ex)
            {
                Plugin.LogError("Tones save failed: " + ex.Message);
            }
        }

        public static void SetDuckMusic(bool on)
        {
            DuckMusic = on;
            Save();
        }

        public static string AppTone(string appId)
        {
            return Get(AppMap, appId);
        }

        public static void SetAppTone(string appId, string soundId)
        {
            Put(AppMap, appId, soundId);
            Save();
        }

        public static string ContactRing(string contactId)
        {
            return Get(RingMap, contactId);
        }

        public static string ContactText(string contactId)
        {
            return Get(TextMap, contactId);
        }

        public static void SetContactRing(string contactId, string soundId)
        {
            Put(RingMap, contactId, soundId);
            Save();
        }

        public static void SetContactText(string contactId, string soundId)
        {
            Put(TextMap, contactId, soundId);
            Save();
        }

        public static string ResolveApp(string appId)
        {
            string custom = AppTone(appId);
            return string.IsNullOrEmpty(custom) ? PhoneTheme.NotifyToneId : custom;
        }

        public static string ResolveRing(string contactId)
        {
            string custom = ContactRing(contactId);
            if (!string.IsNullOrEmpty(custom))
                return custom;
            string app = AppTone(BuiltinApps.PhoneId);
            return string.IsNullOrEmpty(app) ? PhoneTheme.RingtoneId : app;
        }

        public static string ResolveText(string contactId)
        {
            string custom = ContactText(contactId);
            if (!string.IsNullOrEmpty(custom))
                return custom;
            string app = AppTone(BuiltinApps.MessagesId);
            return string.IsNullOrEmpty(app) ? PhoneTheme.TextToneId : app;
        }

        public static void FillPicker(UnityEngine.Transform parent, System.Action redraw, System.Action<SoundItem> pick)
        {
            System.Collections.Generic.List<SoundItem> tones = PhoneStore.AlertTones();
            for (int i = 0; i < tones.Count; i++)
            {
                SoundItem s = tones[i];
                if (s == null)
                    continue;
                SoundItem captured = s;
                var row = new UnityEngine.GameObject("Tone", typeof(UnityEngine.RectTransform));
                row.transform.SetParent(parent, false);
                PhoneUi.Size(row, 40f);
                PhoneUi.AddHorizontal(row, 6f);
                var nameBtn = PhoneUi.CreateButton(row.transform, captured.Name, () =>
                {
                    PhoneSounds.StopPreview();
                    if (pick != null)
                        pick(captured);
                }, new UnityEngine.Vector2(220f, 36f));
                var nle = nameBtn.GetComponent<UnityEngine.UI.LayoutElement>();
                if (nle != null)
                    nle.flexibleWidth = 1f;
                string path = PhoneStore.SoundPath(captured.File);
                bool on = PhoneSounds.IsPreviewing(path);
                PhoneUi.CreateButton(row.transform, on ? "■" : "▶", () =>
                {
                    PhoneSounds.TogglePreview(path);
                    if (redraw != null)
                        redraw();
                }, new UnityEngine.Vector2(44f, 36f));
            }
        }

        public static string Label(string soundId)
        {
            return PhoneTheme.ToneName(soundId);
        }

        private static string Get(Dictionary<string, string> map, string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            string value;
            return map.TryGetValue(key, out value) ? (value ?? string.Empty) : string.Empty;
        }

        private static void Put(Dictionary<string, string> map, string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;
            if (string.IsNullOrEmpty(value))
                map.Remove(key);
            else
                map[key] = value;
        }

        private static void WriteMap(StringBuilder sb, string kind, Dictionary<string, string> map)
        {
            foreach (var kv in map)
            {
                if (string.IsNullOrEmpty(kv.Value))
                    continue;
                sb.Append(kind).Append('\t').Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
            }
        }

        private static void WriteContact(StringBuilder sb, string slot, Dictionary<string, string> map)
        {
            foreach (var kv in map)
            {
                if (string.IsNullOrEmpty(kv.Value))
                    continue;
                sb.Append("contact\t").Append(kv.Key).Append('\t').Append(slot).Append('\t').Append(kv.Value).Append('\n');
            }
        }
    }
}
