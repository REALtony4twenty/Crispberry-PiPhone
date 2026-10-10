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
    /// shapes and words, are sent each still picture once, and get moving
    /// pictures only where a picture keeps changing. A hidden screen is a padlock.
    /// </summary>
    internal static class PhoneCastMirror
    {
        internal static RawImage Hole;
        internal static bool HoleCloset;
        internal static RawImage VideoHole;
        internal static bool VideoKeep;
        internal static int VideoMode;
        internal static bool Truncated;
        internal static bool Dropped;

        private static readonly Vector3[] Corners = new Vector3[4];
        private static RectTransform _videoRect;
        private static RectTransform _box;
        private static RectTransform _largest;
        private static float _largestArea;
        private static int _boxes;
        private static bool _moving;
        private static bool _never;
        private static bool _videoAll;

        internal static string Capture(bool hideApp)
        {
            VideoMode = 0;
            _videoRect = null;
            _videoAll = false;
            _never = false;
            RectTransform bezel = PhoneMenu.BezelRt;
            if (bezel == null || !bezel.gameObject.activeInHierarchy)
                return string.Empty;
            Hole = null;
            VideoHole = null;
            PiPhoneApp app = hideApp ? null : PhoneMenu.OpenApp();
            _never = app != null && app.CastVideo == PiPhoneCastVideo.Off;
            string blob = Describe(bezel, hideApp);
            if (app == null || _never)
                return blob;
            bool incomplete = _moving || Truncated || Dropped || _boxes > 1;
            if (app.CastVideo != PiPhoneCastVideo.On && !incomplete)
            {
                if (_boxes == 1 && _box != null)
                {
                    VideoMode = 2;
                    _videoRect = _box;
                }
                return blob;
            }
            bool truncated = Truncated;
            bool dropped = Dropped;
            RectTransform area = PhoneMenu.AppContentRt;
            VideoMode = 1;
            if (_boxes == 0 && _largest != null && area != null && _largestArea >= Area(area) * 0.25f)
                _videoRect = _largest;
            _videoAll = true;
            blob = Describe(bezel, false);
            _videoAll = false;
            Truncated = truncated;
            Dropped = dropped;
            return blob;
        }

        private static string Describe(RectTransform bezel, bool hideApp)
        {
            Truncated = false;
            Dropped = false;
            _moving = false;
            _boxes = 0;
            _box = null;
            _largest = null;
            _largestArea = 0f;
            var sb = new StringBuilder(4096);
            sb.Append("M1|").Append(Mathf.RoundToInt(bezel.rect.width)).Append('|').Append(Mathf.RoundToInt(bezel.rect.height)).Append('\n');
            int count = 0;
            Walk(bezel, bezel, sb, hideApp, ref count);
            if (hideApp)
                Note(bezel, sb);
            return sb.ToString();
        }

        internal static void Paint(RectTransform parent, string blob)
        {
            Hole = null;
            HoleCloset = false;
            VideoHole = null;
            VideoKeep = false;
            PhoneCastPictures.BeginPaint();
            if (parent == null || string.IsNullOrEmpty(blob))
                return;
            string[] lines = blob.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                    continue;
                string[] f = line.Split('~');
                if (f.Length < 6)
                    continue;
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
                FontStyles style = f.Length > 10 ? (FontStyles)(int)Num(f[10]) : FontStyles.Normal;
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
                if (key == "s" || key == "h")
                {
                    var go = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    go.transform.SetParent(parent, false);
                    Place(go.GetComponent<RectTransform>(), x, y, w, h);
                    RawImage raw = go.GetComponent<RawImage>();
                    raw.color = Color.black;
                    raw.raycastTarget = false;
                    VideoHole = raw;
                    VideoKeep = key == "h";
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
                        Centre(art);
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
                    var label = PhoneUi.CreateLabel(parent, "T", text, size > 0 ? size : 14f, style, (TextAlignmentOptions)align);
                    label.color = color;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    Place(label.rectTransform, x, y, w, h);
                    continue;
                }
                if (key.Length > 1 && key[0] == 'x')
                {
                    Sprite sent = PhoneCastPictures.Lookup(key);
                    if (sent == null)
                    {
                        Block(parent, x, y, w, h, 8);
                        continue;
                    }
                    var pic = PhoneUi.CreateImage(parent, "X", sent, color);
                    Image picImg = pic.GetComponent<Image>();
                    picImg.raycastTarget = false;
                    picImg.type = (size & 2) != 0 && sent.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                    picImg.preserveAspect = (size & 1) != 0;
                    Place(pic, x, y, w, h);
                    if (picImg.preserveAspect)
                        Centre(pic);
                    continue;
                }
                Sprite sprite = SpriteFor(key);
                if (key == "p" || (sprite == null && key.Length > 0 && key[0] != 'r' && key != "c" && key != "w" && key != "logo"))
                {
                    Block(parent, x, y, w, h, 8);
                    continue;
                }
                var rt = PhoneUi.CreateImage(parent, "P", sprite != null ? sprite : PhoneUi.White(), color);
                Image img = rt.GetComponent<Image>();
                img.raycastTarget = false;
                img.type = key.Length > 0 && key[0] == 'r' ? Image.Type.Sliced : Image.Type.Simple;
                img.preserveAspect = key.Length > 0 && (key[0] == 'm' || key[0] == 'k' || key == "logo" || key == "c");
                Place(rt, x, y, w, h);
                if (img.preserveAspect)
                    Centre(rt);
            }
        }

        private static void Block(RectTransform parent, float x, float y, float w, float h, int radius)
        {
            var block = PhoneUi.CreateImage(parent, "Block", PhoneUi.Rounded(radius), new Color(0.16f, 0.17f, 0.2f, 1f));
            Place(block, x, y, w, h);
            block.GetComponent<Image>().raycastTarget = false;
        }

        internal static bool VideoBox(RectTransform bezel, out float xMin, out float yMin, out float xMax, out float yMax)
        {
            RectTransform picture = _videoRect != null && _videoRect.gameObject.activeInHierarchy ? _videoRect : null;
            return AreaBox(bezel, picture, out xMin, out yMin, out xMax, out yMax);
        }

        private static bool AreaBox(RectTransform bezel, RectTransform picture, out float xMin, out float yMin, out float xMax, out float yMax)
        {
            xMin = yMin = xMax = yMax = 0f;
            RectTransform area = PhoneMenu.AppContentRt;
            if (bezel == null || area == null)
                return false;
            Bounds(bezel, area, out xMin, out yMin, out xMax, out yMax);
            if (picture != null)
            {
                float x0;
                float y0;
                float x1;
                float y1;
                Bounds(bezel, picture, out x0, out y0, out x1, out y1);
                x0 = Mathf.Max(x0, xMin);
                y0 = Mathf.Max(y0, yMin);
                x1 = Mathf.Min(x1, xMax);
                y1 = Mathf.Min(y1, yMax);
                if (x1 - x0 >= 1f && y1 - y0 >= 1f)
                {
                    xMin = x0;
                    yMin = y0;
                    xMax = x1;
                    yMax = y1;
                }
            }
            return xMax - xMin >= 1f && yMax - yMin >= 1f;
        }

        private static void Bounds(RectTransform bezel, RectTransform node, out float xMin, out float yMin, out float xMax, out float yMax)
        {
            node.GetWorldCorners(Corners);
            xMin = float.MaxValue;
            yMin = float.MaxValue;
            xMax = float.MinValue;
            yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = bezel.InverseTransformPoint(Corners[i]);
                xMin = Mathf.Min(xMin, p.x);
                yMin = Mathf.Min(yMin, p.y);
                xMax = Mathf.Max(xMax, p.x);
                yMax = Mathf.Max(yMax, p.y);
            }
        }

        private static float Area(RectTransform rt)
        {
            Vector3 scale = rt.lossyScale;
            return Mathf.Abs(rt.rect.width * scale.x * rt.rect.height * scale.y);
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
            if (node == null)
                return;
            if (count > 640 || sb.Length > 36000)
            {
                Truncated = true;
                return;
            }
            if (!node.gameObject.activeInHierarchy)
                return;
            if (Physical(node.name))
                return;
            bool appZone = PhoneMenu.UnderAppContent(node);
            if (hideApp && appZone)
                return;
            if (node == PhoneMenu.AppContentRt && _videoAll)
            {
                count += WriteHole(sb, bezel, _videoRect, "s") ? 1 : 0;
                return;
            }
            if (appZone && node.GetComponent<PhoneCastVideoArea>() != null)
            {
                if (_never)
                    count += WriteBox(sb, bezel, node, Color.white, "p", 0, 0, string.Empty, 0, true) ? 1 : 0;
                else if (WriteHole(sb, bezel, node, "h"))
                {
                    count++;
                    _boxes++;
                    _box = node;
                }
                else
                    Dropped = true;
                return;
            }
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
                    {
                        Color color = raw.color;
                        string key = RawKey(raw);
                        if (key == "p")
                            key = Foreign(bezel, node, raw.texture, RawRect(raw), Vector4.zero, 100f, null, appZone, ref color);
                        count += WriteBox(sb, bezel, node, color, key, 0, 0, string.Empty, 0, appZone) ? 1 : 0;
                    }
                    else if (label != null && !string.IsNullOrEmpty(label.text))
                    {
                        string owner = AppOwner(label.transform);
                        string key = string.IsNullOrEmpty(owner) ? string.Empty : "n" + owner;
                        int align = (int)label.alignment;
                        count += WriteBox(sb, bezel, node, label.color, key, Mathf.RoundToInt(label.fontSize), align, label.text, (int)label.fontStyle, appZone) ? 1 : 0;
                    }
                    else if (image != null)
                    {
                        Color color = image.color;
                        int flags = 0;
                        string key = SpriteKey(image);
                        bool custom = key.Length > 1 && key[0] == 'g';
                        Sprite sprite = image.sprite;
                        if (custom)
                        {
                            Color unused = color;
                            Foreign(bezel, node, sprite.texture, SpriteRect(sprite), Vector4.zero, 100f, key, false, ref unused);
                        }
                        else if (key == "p")
                        {
                            bool sliced = image.type == Image.Type.Sliced;
                            key = Foreign(bezel, node, sprite.texture, SpriteRect(sprite), sliced ? sprite.border : Vector4.zero, sprite.pixelsPerUnit, null, appZone, ref color);
                            if (key.Length > 1 && key[0] == 'x')
                                flags = (image.preserveAspect ? 1 : 0) | (sliced ? 2 : 0);
                        }
                        count += WriteBox(sb, bezel, node, color, key, flags, 0, string.Empty, 0, appZone) ? 1 : 0;
                        if (custom)
                            return;
                    }
                }
            }
            for (int i = 0; i < node.childCount; i++)
                Walk(node.GetChild(i) as RectTransform, bezel, sb, hideApp, ref count);
        }

        private static string Foreign(RectTransform bezel, RectTransform node, Texture tex, Rect rect, Vector4 border, float ppu, string alias, bool appZone, ref Color color)
        {
            node.GetWorldCorners(Corners);
            Vector3 bl = bezel.InverseTransformPoint(Corners[0]);
            Vector3 tr = bezel.InverseTransformPoint(Corners[2]);
            float units = Mathf.Max(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
            bool moving;
            bool flat;
            Color flatColor;
            string key = PhoneCastPictures.KeyFor(tex, rect, border, ppu, units, alias, out moving, out flat, out flatColor);
            if (moving && appZone && !_never)
            {
                _moving = true;
                float area = Area(node);
                if (area > _largestArea)
                {
                    _largestArea = area;
                    _largest = node;
                }
            }
            if (flat)
                color *= flatColor;
            return key;
        }

        private static Rect RawRect(RawImage raw)
        {
            Texture tex = raw.texture;
            if (tex == null)
                return new Rect();
            Rect uv = raw.uvRect;
            if (uv.width > 0f && uv.height > 0f && uv.xMin >= 0f && uv.yMin >= 0f && uv.xMax <= 1f && uv.yMax <= 1f)
                return new Rect(uv.x * tex.width, uv.y * tex.height, uv.width * tex.width, uv.height * tex.height);
            return new Rect(0f, 0f, tex.width, tex.height);
        }

        private static Rect SpriteRect(Sprite sprite)
        {
            try
            {
                return sprite.textureRect;
            }
            catch (System.Exception)
            {
                return sprite.rect;
            }
        }

        private static bool WriteBox(StringBuilder sb, RectTransform bezel, RectTransform node, Color color, string key, int size, int align, string text, int style, bool appZone)
        {
            float x;
            float y;
            float w;
            float h;
            if (!Box(bezel, node, out x, out y, out w, out h))
            {
                if (appZone && Turned(bezel, node))
                    Dropped = true;
                return false;
            }
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
            if (style != 0 && !string.IsNullOrEmpty(text))
                sb.Append('~').Append(style);
            sb.Append('\n');
            return true;
        }

        private static bool Turned(RectTransform bezel, RectTransform node)
        {
            node.GetWorldCorners(Corners);
            Vector3 bl = bezel.InverseTransformPoint(Corners[0]);
            Vector3 tr = bezel.InverseTransformPoint(Corners[2]);
            return tr.x - bl.x < 0f || tr.y - bl.y < 0f;
        }

        private static bool WriteHole(StringBuilder sb, RectTransform bezel, RectTransform picture, string key)
        {
            float xMin;
            float yMin;
            float xMax;
            float yMax;
            if (picture != null && !picture.gameObject.activeInHierarchy)
                picture = null;
            if (!AreaBox(bezel, picture, out xMin, out yMin, out xMax, out yMax))
                return false;
            float bw = bezel.rect.width;
            float bh = bezel.rect.height;
            float x = xMin + bw * 0.5f;
            float y = bh * 0.5f - yMax;
            float w = xMax - xMin;
            float h = yMax - yMin;
            Clip(ref x, ref y, ref w, ref h, 0f, 0f, bw, bh);
            if (w < 1f || h < 1f)
                return false;
            sb.Append("c~").Append(Mathf.RoundToInt(x));
            sb.Append('~').Append(Mathf.RoundToInt(y));
            sb.Append('~').Append(Mathf.RoundToInt(w));
            sb.Append('~').Append(Mathf.RoundToInt(h));
            sb.Append("~FFFFFFFF~").Append(key).Append("~0~0~\n");
            return true;
        }

        private static void Note(RectTransform bezel, StringBuilder sb)
        {
            float w = bezel.rect.width;
            float h = bezel.rect.height;
            sb.Append("c~").Append(Mathf.RoundToInt(w * 0.5f - 48f));
            sb.Append('~').Append(Mathf.RoundToInt(h * 0.5f - 48f));
            sb.Append("~96~96~FFFFFFFF~mlock~0~0~\n");
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
                Sprite sent = PhoneCastPictures.Lookup("g" + id);
                if (sent == null)
                {
                    Block(parent, x, y, w, h, 16);
                    return;
                }
                var well = PhoneUi.CreateImage(parent, "Icon", PhoneUi.IconWellShape(), Color.white);
                well.GetComponent<Image>().raycastTarget = false;
                var mask = well.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var art = PhoneUi.CreateImage(well, "Art", sent, Color.white);
                PhoneUi.Stretch(art, 0f, 0f);
                Image artImg = art.GetComponent<Image>();
                artImg.type = Image.Type.Simple;
                artImg.preserveAspect = true;
                artImg.raycastTarget = false;
                Place(well, x, y, w, h);
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

        private static void Centre(RectTransform rt)
        {
            if (rt == null)
                return;
            Vector2 size = rt.sizeDelta;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition += new Vector2(size.x * 0.5f, -size.y * 0.5f);
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
