using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// A copy of the phone the caster is looking at. Watchers rebuild the same
    /// pictures and words. Photos, songs, and apps they do not have are blocked.
    /// </summary>
    internal static class PhoneCastMirror
    {
        internal static RawImage Hole;
        internal static bool HoleCloset;

        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly HashSet<string> Hidden = new HashSet<string>();

        internal static string Capture(bool hideApp)
        {
            RectTransform bezel = PhoneMenu.BezelRt;
            if (bezel == null || !bezel.gameObject.activeInHierarchy)
                return string.Empty;
            Hole = null;
            CollectHidden();
            var sb = new StringBuilder(4096);
            sb.Append("M1|").Append(Mathf.RoundToInt(bezel.rect.width)).Append('|').Append(Mathf.RoundToInt(bezel.rect.height)).Append('\n');
            int count = 0;
            Walk(bezel, bezel, sb, hideApp, ref count);
            if (hideApp)
                Note(bezel, sb, "Not shared");
            return sb.ToString();
        }

        internal static void Paint(RectTransform parent, string blob)
        {
            Hole = null;
            HoleCloset = false;
            if (parent == null || string.IsNullOrEmpty(blob))
                return;
            string[] lines = blob.Split('\n');
            string openId = PhoneCastView.OpenApp;
            bool haveApp = string.IsNullOrEmpty(openId) || openId == "home" || openId == "#" || HasApp(openId);
            bool blocked = false;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                    continue;
                string[] f = line.Split('~');
                if (f.Length < 6)
                    continue;
                bool appZone = f[0] == "a";
                if (appZone && !haveApp)
                {
                    blocked = true;
                    continue;
                }
                float x = Num(f[1]);
                float y = Num(f[2]);
                float w = Num(f[3]);
                float h = Num(f[4]);
                if (w < 1f || h < 1f)
                    continue;
                Color color = Hex(f[5]);
                string key = f.Length > 6 ? f[6] : string.Empty;
                int size = f.Length > 7 ? (int)Num(f[7]) : 14;
                int align = f.Length > 8 ? (int)Num(f[8]) : (int)TextAlignmentOptions.Center;
                string text = f.Length > 9 ? Untext(f[9]) : string.Empty;
                if (key == "v" || key == "u")
                {
                    var go = new GameObject(key == "u" ? "Cam" : "Preview", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    go.transform.SetParent(parent, false);
                    Place(go.GetComponent<RectTransform>(), x, y, w, h);
                    RawImage raw = go.GetComponent<RawImage>();
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    Hole = raw;
                    HoleCloset = key == "u";
                    continue;
                }
                if (key.Length > 2 && key[0] == 'o')
                {
                    if (PaintOption(parent, key, color, x, y, w, h))
                        continue;
                }
                if (key.Length > 1 && (key[0] == 'b' || key[0] == 'e'))
                {
                    Sprite game = GameSprite(key);
                    if (game != null)
                    {
                        var art = PhoneUi.CreateImage(parent, "G", game, color);
                        Image artImg = art.GetComponent<Image>();
                        artImg.raycastTarget = false;
                        artImg.preserveAspect = true;
                        artImg.type = Image.Type.Simple;
                        Place(art, x, y, w, h);
                        continue;
                    }
                }
                if (key.Length > 1 && key[0] == 'g')
                {
                    AppIcon(parent, key.Substring(1), x, y, w, h);
                    continue;
                }
                if (!string.IsNullOrEmpty(text))
                {
                    if (key.Length > 1 && key[0] == 'n' && !HasApp(key.Substring(1)))
                        continue;
                    var label = PhoneUi.CreateLabel(parent, "T", text, size > 0 ? size : 14f, FontStyles.Normal, (TextAlignmentOptions)align);
                    label.color = color;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    Place(label.rectTransform, x, y, w, h);
                    continue;
                }
                Sprite sprite = SpriteFor(key);
                if (key == "p" || (sprite == null && key.Length > 0 && key[0] != 'r' && key != "c" && key != "w" && key != "logo"))
                {
                    var block = PhoneUi.CreateImage(parent, "Block", PhoneUi.Rounded(8), new Color(0.16f, 0.17f, 0.2f, 1f));
                    Place(block, x, y, w, h);
                    block.GetComponent<Image>().raycastTarget = false;
                    continue;
                }
                var rt = PhoneUi.CreateImage(parent, "P", sprite != null ? sprite : PhoneUi.White(), color);
                Image img = rt.GetComponent<Image>();
                img.raycastTarget = false;
                img.type = key.Length > 0 && key[0] == 'r' ? Image.Type.Sliced : Image.Type.Simple;
                img.preserveAspect = key.Length > 0 && (key[0] == 'm' || key[0] == 'k' || key == "logo" || key == "c");
                Place(rt, x, y, w, h);
            }
            if (blocked)
            {
                var note = PhoneUi.CreateLabel(parent, "Blocked", "Unavailable here", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
                note.color = Color.white;
                Place(note.rectTransform, 24f, 80f, 360f, 48f);
            }
        }

        internal static void ReadSize(string blob, out float w, out float h)
        {
            w = 420f;
            h = 860f;
            if (string.IsNullOrEmpty(blob))
                return;
            int nl = blob.IndexOf('\n');
            string head = nl > 0 ? blob.Substring(0, nl) : blob;
            string[] f = head.Split('|');
            if (f.Length < 3)
                return;
            float pw = Num(f[1]);
            float ph = Num(f[2]);
            if (pw > 32f && ph > 32f)
            {
                w = pw;
                h = ph;
            }
        }

        private static void Walk(RectTransform node, RectTransform bezel, StringBuilder sb, bool hideApp, ref int count)
        {
            if (node == null || count > 640 || sb.Length > 36000)
                return;
            if (!node.gameObject.activeInHierarchy)
                return;
            if (Physical(node.name))
                return;
            bool appZone = PhoneMenu.UnderAppContent(node);
            if (hideApp && appZone)
                return;
            Graphic graphic = node.GetComponent<Graphic>();
            if (graphic != null && graphic.enabled && graphic.color.a > 0.02f)
            {
                CanvasRenderer rend = graphic.canvasRenderer;
                if (rend == null || !rend.cull)
                {
                    RawImage raw = graphic as RawImage;
                    Image image = graphic as Image;
                    TextMeshProUGUI label = graphic as TextMeshProUGUI;
                    if (raw != null)
                        count += WriteBox(sb, bezel, node, raw.color, RawKey(raw), 0, 0, string.Empty, appZone) ? 1 : 0;
                    else if (label != null && !string.IsNullOrEmpty(label.text))
                    {
                        string text = ShownText(label.text);
                        string owner = AppOwner(label.transform);
                        string key = string.IsNullOrEmpty(owner) ? string.Empty : "n" + owner;
                        int align = (int)label.alignment;
                        count += WriteBox(sb, bezel, node, label.color, key, Mathf.RoundToInt(label.fontSize), align, text, appZone) ? 1 : 0;
                    }
                    else if (image != null)
                    {
                        string key = SpriteKey(image);
                        bool custom = key.Length > 1 && key[0] == 'g';
                        count += WriteBox(sb, bezel, node, image.color, key, 0, 0, string.Empty, appZone) ? 1 : 0;
                        if (custom)
                            return;
                    }
                }
            }
            for (int i = 0; i < node.childCount; i++)
                Walk(node.GetChild(i) as RectTransform, bezel, sb, hideApp, ref count);
        }

        private static bool WriteBox(StringBuilder sb, RectTransform bezel, RectTransform node, Color color, string key, int size, int align, string text, bool appZone)
        {
            float x;
            float y;
            float w;
            float h;
            if (!Box(bezel, node, out x, out y, out w, out h))
                return false;
            if (w < 1f || h < 1f)
                return false;
            sb.Append(appZone ? 'a' : 'c');
            sb.Append('~').Append(Mathf.RoundToInt(x));
            sb.Append('~').Append(Mathf.RoundToInt(y));
            sb.Append('~').Append(Mathf.RoundToInt(w));
            sb.Append('~').Append(Mathf.RoundToInt(h));
            sb.Append('~').Append(Hex(color));
            sb.Append('~').Append(key ?? string.Empty);
            sb.Append('~').Append(size);
            sb.Append('~').Append(align);
            sb.Append('~').Append(Esc(text));
            sb.Append('\n');
            return true;
        }

        private static void Note(RectTransform bezel, StringBuilder sb, string text)
        {
            float w = bezel.rect.width;
            float h = bezel.rect.height;
            sb.Append("c~").Append(Mathf.RoundToInt(w * 0.12f));
            sb.Append('~').Append(Mathf.RoundToInt(h * 0.42f));
            sb.Append('~').Append(Mathf.RoundToInt(w * 0.76f));
            sb.Append("~48~FFFFFFFF~~18~").Append((int)TextAlignmentOptions.Center);
            sb.Append('~').Append(Esc(text)).Append('\n');
        }

        private static bool Box(RectTransform bezel, RectTransform node, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            node.GetWorldCorners(Corners);
            Vector3 bl = bezel.InverseTransformPoint(Corners[0]);
            Vector3 tr = bezel.InverseTransformPoint(Corners[2]);
            float bw = bezel.rect.width;
            float bh = bezel.rect.height;
            x = bl.x + bw * 0.5f;
            y = bh * 0.5f - tr.y;
            w = tr.x - bl.x;
            h = tr.y - bl.y;
            Clip(ref x, ref y, ref w, ref h, 0f, 0f, bw, bh);
            if (w < 1f || h < 1f)
                return false;
            if (!ClipMasks(bezel, node, ref x, ref y, ref w, ref h))
                return false;
            return w > 0.5f && h > 0.5f;
        }

        private static bool ClipMasks(RectTransform bezel, RectTransform node, ref float x, ref float y, ref float w, ref float h)
        {
            Transform t = node.parent;
            while (t != null && t != bezel)
            {
                if (t.GetComponent<RectMask2D>() != null || t.GetComponent<Mask>() != null)
                {
                    RectTransform mask = t as RectTransform;
                    float mx;
                    float my;
                    float mw;
                    float mh;
                    if (mask == null || !Frame(bezel, mask, out mx, out my, out mw, out mh))
                        return false;
                    Clip(ref x, ref y, ref w, ref h, mx, my, mw, mh);
                    if (w < 1f || h < 1f)
                        return false;
                }
                t = t.parent;
            }
            return true;
        }

        private static void Clip(ref float x, ref float y, ref float w, ref float h, float mx, float my, float mw, float mh)
        {
            float x2 = Mathf.Min(x + w, mx + mw);
            float y2 = Mathf.Min(y + h, my + mh);
            x = Mathf.Max(x, mx);
            y = Mathf.Max(y, my);
            w = x2 - x;
            h = y2 - y;
        }

        private static bool Frame(RectTransform bezel, RectTransform node, out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            node.GetWorldCorners(Corners);
            Vector3 bl = bezel.InverseTransformPoint(Corners[0]);
            Vector3 tr = bezel.InverseTransformPoint(Corners[2]);
            float bw = bezel.rect.width;
            float bh = bezel.rect.height;
            x = bl.x + bw * 0.5f;
            y = bh * 0.5f - tr.y;
            w = tr.x - bl.x;
            h = tr.y - bl.y;
            return w > 0.5f && h > 0.5f;
        }

        private static bool Physical(string name)
        {
            return name == "VolUp" || name == "VolDn" || name == "Ringer" || name == "PunchHole";
        }

        private static string RawKey(RawImage raw)
        {
            string name = raw.gameObject.name;
            if (name == "Preview")
                return "v";
            if (name == "Cam")
                return "u";
            if (name.Length > 3 && name.StartsWith("Opt"))
                return "o" + name.Substring(3);
            return "p";
        }

        private static string SpriteKey(Image image)
        {
            Sprite sprite = image.sprite;
            if (sprite == null)
                return string.Empty;
            string n = sprite.name ?? string.Empty;
            if (n.Length == 0 || n == "PiP_White")
                return string.Empty;
            if (n == "PiP_Wallpaper")
                return "w";
            if (n == "PiP_Circle")
                return "c";
            if (n == "PiP_Logo")
                return "logo";
            if (n.StartsWith("PiP_Round"))
                return "r" + n.Substring(9);
            if (n.StartsWith("PiP_Md_"))
                return "m" + n.Substring(7);
            if (n.StartsWith("PiP_Card_"))
                return "k" + n.Substring(9);
            if (n == "PiP_Icon")
            {
                string app = AppOwner(image.transform);
                if (!string.IsNullOrEmpty(app))
                    return "g" + app;
            }
            string passport = ClosetApp.CastSpriteKey(sprite);
            if (!string.IsNullOrEmpty(passport))
                return passport;
            string emote = CameraApp.CastSpriteKey(sprite);
            if (!string.IsNullOrEmpty(emote))
                return emote;
            return "p";
        }

        private static bool PaintOption(RectTransform parent, string key, Color color, float x, float y, float w, float h)
        {
            int colon = key.IndexOf(':');
            int type;
            int index;
            if (colon <= 1 || !int.TryParse(key.Substring(1, colon - 1), out type) || !int.TryParse(key.Substring(colon + 1), out index))
                return false;
            Texture tex;
            Material mat;
            if (!ClosetApp.TryOption(type, index, out tex, out mat))
                return false;
            var go = new GameObject("Opt", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), x, y, w, h);
            RawImage raw = go.GetComponent<RawImage>();
            raw.texture = tex;
            raw.color = color;
            raw.raycastTarget = false;
            if (mat != null)
                raw.material = mat;
            return true;
        }

        private static Sprite GameSprite(string key)
        {
            int n;
            if (!int.TryParse(key.Substring(1), out n))
                return null;
            if (key[0] == 'b')
                return ClosetApp.PassportSprite(n);
            if (key[0] == 'e')
                return CameraApp.EmoteSprite(n);
            return null;
        }

        private static string AppOwner(Transform t)
        {
            while (t != null)
            {
                if (t.name.StartsWith("App_") && t.name.Length > 4)
                {
                    string raw = t.name.Substring(4);
                    PiPhoneApp[] apps = PiPhoneApi.GetApps();
                    for (int i = 0; i < apps.Length; i++)
                    {
                        PiPhoneApp app = apps[i];
                        if (app != null && PhoneUi.Sanitize(app.Id) == raw)
                            return app.Id;
                    }
                }
                t = t.parent;
            }
            return string.Empty;
        }

        private static void AppIcon(RectTransform parent, string id, float x, float y, float w, float h)
        {
            PiPhoneApp app;
            if (!PiPhoneApi.TryGetApp(id, out app) || app == null)
            {
                var block = PhoneUi.CreateImage(parent, "Gone", PhoneUi.Rounded(16), new Color(0.22f, 0.23f, 0.26f, 1f));
                Place(block, x, y, w, h);
                block.GetComponent<Image>().raycastTarget = false;
                var label = PhoneUi.CreateLabel(block, "U", "Unavailable", Mathf.Clamp(h * 0.22f, 9f, 14f), FontStyles.Bold, TextAlignmentOptions.Center);
                label.color = Color.white;
                PhoneUi.Stretch(label.rectTransform, 4f, 4f);
                return;
            }
            float side = Mathf.Max(8f, Mathf.Min(w, h));
            RectTransform icon = PhoneIcons.CreateView(parent, app, side, false);
            Place(icon, x, y, w, h);
        }

        private static Sprite SpriteFor(string key)
        {
            if (string.IsNullOrEmpty(key))
                return PhoneUi.White();
            if (key == "w")
                return PhoneUi.Wallpaper();
            if (key == "c")
                return PhoneUi.Circle();
            if (key == "logo")
                return PhoneIcons.Logo();
            if (key[0] == 'r')
            {
                int radius;
                if (int.TryParse(key.Substring(1), out radius))
                    return PhoneUi.Rounded(radius);
            }
            if (key[0] == 'm' && key.Length > 1)
                return PhoneIcons.Material(key.Substring(1));
            if (key[0] == 'k' && key.Length > 1)
                return PhoneIcons.Card(key.Substring(1));
            return null;
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null)
                return;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void CollectHidden()
        {
            Hidden.Clear();
            PiPhoneApp app = PhoneMenu.OpenApp();
            if (app == null || app.Id != BuiltinApps.SoundsId)
                return;
            List<SoundItem> tracks = PhoneStore.MusicTracks();
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null && !string.IsNullOrEmpty(tracks[i].Name))
                    Hidden.Add(tracks[i].Name);
            }
            for (int i = 0; i < PhoneStore.Playlists.Count; i++)
            {
                PlaylistItem list = PhoneStore.Playlists[i];
                if (list != null && !string.IsNullOrEmpty(list.Name))
                    Hidden.Add(list.Name);
            }
            if (!string.IsNullOrEmpty(MusicPlayer.CurrentName))
                Hidden.Add(MusicPlayer.CurrentName);
            if (!string.IsNullOrEmpty(MusicPlayer.UpcomingLine))
                Hidden.Add(MusicPlayer.UpcomingLine);
        }

        private static string ShownText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (Hidden.Contains(text))
                return "Not on this phone";
            return text;
        }

        private static bool HasApp(string id)
        {
            PiPhoneApp app;
            return PiPhoneApi.TryGetApp(id, out app) && app != null;
        }

        private static string Hex(Color color)
        {
            Color32 c = color;
            return c.r.ToString("X2") + c.g.ToString("X2") + c.b.ToString("X2") + c.a.ToString("X2");
        }

        private static Color Hex(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 8)
                return Color.white;
            byte r = Parse(text, 0);
            byte g = Parse(text, 2);
            byte b = Parse(text, 4);
            byte a = Parse(text, 6);
            return new Color32(r, g, b, a);
        }

        private static byte Parse(string text, int at)
        {
            byte n;
            if (byte.TryParse(text.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n))
                return n;
            return 255;
        }

        private static float Num(string text)
        {
            float n;
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out n))
                return n;
            return 0f;
        }

        private static string Esc(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            if (text.Length > 80)
                text = text.Substring(0, 80);
            return text.Replace('~', ' ').Replace('\n', ' ').Replace('\r', ' ');
        }

        private static string Untext(string text)
        {
            return text ?? string.Empty;
        }
    }
}
