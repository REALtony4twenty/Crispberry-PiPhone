using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UObject = UnityEngine.Object;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Home-screen icons: 128px max, cached sprites, one decode per key.
    /// Mods should call <see cref="PiPhoneApp.SetIcon(byte[])"/> / <see cref="FromBytes"/>.
    /// </summary>
    public static class PhoneIcons
    {
        public const int MaxSize = 128;
        /// <summary>Decode budget. Output is always clamped to <see cref="MaxSize"/>.</summary>
        public const int MaxBytes = 2 * 1024 * 1024;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> GlyphByApp = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Assembly Asm = typeof(PhoneIcons).Assembly;

        /// <summary>
        /// White-on-transparent Google Material Design icon (Apache-2.0).
        /// Tint with <see cref="Image.color"/>; do not punch rounded corners.
        /// </summary>
        public static Sprite Material(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            string key = "m:" + name;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;
            byte[] bytes = ReadResource("Crispberry_PiPhone.Icons.material." + name + ".png")
                ?? FindResource("material." + name + ".png")
                ?? FindResource(name + ".png");
            if (bytes == null)
                return null;
            Texture2D tex = PhoneImages.LoadTexture(bytes);
            if (tex == null)
                return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PiP_Md_" + name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Kenney Playing Cards Pack (CC0). Pixel art; point-filtered, no rounded-corner punch.
        /// </summary>
        public static Sprite Card(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            string key = "card:" + name;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;
            byte[] bytes = ReadResource("Crispberry_PiPhone.Icons.cards." + name + ".png")
                ?? FindResource("cards." + name + ".png")
                ?? FindResource(name + ".png");
            if (bytes == null)
                return null;
            Texture2D tex = PhoneImages.LoadTexture(bytes);
            if (tex == null)
                return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PiP_Card_" + name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Builtin(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            string key = "b:" + name;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;
            byte[] bytes = ReadResource("Crispberry_PiPhone.Icons.icon_" + name + ".png")
                ?? FindResource("icon_" + name + ".png");
            Sprite sprite = FromBytes(bytes, true);
            if (sprite != null)
                Cache[key] = sprite;
            return sprite;
        }

        public static Sprite FromBytes(byte[] pngOrJpg)
        {
            return FromBytes(pngOrJpg, false);
        }

        internal static Sprite FromBytes(byte[] pngOrJpg, bool allowOversizeFile)
        {
            if (pngOrJpg == null || pngOrJpg.Length < 24)
            {
                if (!allowOversizeFile)
                    Reject("icon bytes are empty or too short to be an image.");
                return null;
            }
            if (!allowOversizeFile && pngOrJpg.Length > MaxBytes)
            {
                Reject("icon is " + pngOrJpg.Length + " bytes; decode limit is " + MaxBytes + ". Clamp a Texture2D or shrink the file.");
                return null;
            }
            Texture2D tex = PhoneImages.LoadTexture(pngOrJpg);
            if (tex == null)
            {
                Reject("could not decode icon bytes as PNG/JPG.");
                return null;
            }
            return FromTexture(tex, true);
        }

        public static Sprite FromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Reject("icon file is missing: " + (path ?? "(null)"));
                return null;
            }
            string key = "f:" + path;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;
            FileInfo info = new FileInfo(path);
            if (info.Length > MaxBytes)
            {
                Reject("icon file '" + path + "' is " + info.Length + " bytes; decode limit is " + MaxBytes + ".");
                return null;
            }
            Sprite sprite = FromBytes(File.ReadAllBytes(path), true);
            if (sprite != null)
                Cache[key] = sprite;
            return sprite;
        }

        public static Sprite FromTexture(Texture2D tex, bool consumeSource)
        {
            if (tex == null)
                return null;
            Texture2D ready = Clamp(tex, consumeSource);
            if (ready == null)
                return null;
            ready.wrapMode = TextureWrapMode.Clamp;
            ready.filterMode = FilterMode.Bilinear;
            ready.hideFlags = HideFlags.HideAndDontSave;
            PunchRoundedCorners(ready);
            Sprite sprite = Sprite.Create(ready, new Rect(0f, 0f, ready.width, ready.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PiP_Icon";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static Sprite ClampSprite(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return sprite;
            if (sprite.texture.width <= MaxSize && sprite.texture.height <= MaxSize)
                return sprite;
            return FromTexture(sprite.texture, false);
        }

        public static RectTransform CreateView(Transform parent, PiPhoneApp app, float size, bool circle)
        {
            bool custom = app != null && app.IconSprite != null;
            Sprite maskSprite = circle ? PhoneUi.Circle() : PhoneUi.IconWellShape();
            if (custom)
            {
                var well = PhoneUi.CreateImage(parent, "Icon", maskSprite, Color.white);
                well.sizeDelta = new Vector2(size, size);
                PhoneUi.Size(well.gameObject, size, size);
                var mask = well.gameObject.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var art = PhoneUi.CreateImage(well, "Art", app.IconSprite, Color.white);
                PhoneUi.Stretch(art, 0f, 0f);
                var artImg = art.GetComponent<Image>();
                artImg.type = Image.Type.Simple;
                artImg.preserveAspect = true;
                artImg.raycastTarget = false;
                return well;
            }

            Color tint = app != null ? app.IconBackground : PhoneUi.SurfaceAlt;
            var icon = PhoneUi.CreateImage(parent, "Icon", maskSprite, tint);
            icon.sizeDelta = new Vector2(size, size);
            PhoneUi.Size(icon.gameObject, size, size);
            var img = icon.GetComponent<Image>();
            img.type = circle ? Image.Type.Simple : img.type;
            Sprite md = MaterialFor(app);
            if (md != null)
            {
                Color fg = app != null && app.IconForeground != Color.white ? app.IconForeground : PhoneTheme.IconColor;
                var art = PhoneUi.CreateImage(icon, "Md", md, fg);
                PhoneUi.Stretch(art, size * 0.18f, size * 0.18f);
                var artImg = art.GetComponent<Image>();
                artImg.type = Image.Type.Simple;
                artImg.preserveAspect = true;
                artImg.raycastTarget = false;
            }
            else if (app != null)
            {
                string glyph = string.IsNullOrEmpty(app.IconGlyph)
                    ? (string.IsNullOrEmpty(app.DisplayName) ? "?" : app.DisplayName.Substring(0, 1).ToUpperInvariant())
                    : app.IconGlyph;
                var g = PhoneUi.CreateLabel(icon, "Glyph", glyph, Mathf.Max(14f, size * 0.4f), TMPro.FontStyles.Normal, TMPro.TextAlignmentOptions.Center);
                g.color = app.IconForeground;
                PhoneUi.Stretch(g.rectTransform, 2f, 2f);
            }
            return icon;
        }

        internal static void BindBuiltins()
        {
            Glyph(BuiltinApps.PhoneId, "phone");
            Glyph(BuiltinApps.MessagesId, "chat");
            Glyph(BuiltinApps.VoicemailId, "voicemail");
            Glyph(BuiltinApps.SettingsId, "settings");
            Glyph(BuiltinApps.NotesId, "note");
            Glyph(BuiltinApps.CameraId, "photo_camera");
            Glyph(BuiltinApps.PhotosId, "photo_library");
            Glyph(BuiltinApps.VoiceMemosId, "mic");
            Glyph(BuiltinApps.SoundsId, "library_music");
            Glyph(BuiltinApps.StoreId, "store");
            Glyph(BuiltinApps.ClosetId, "style");
            Glyph(BuiltinApps.MakeNotiId, "add_alert");
            Glyph(BuiltinApps.SnakeId, "gesture");
            Glyph(BuiltinApps.Game2048Id, "grid_on");
            Glyph(BuiltinApps.MinesId, "flag");
            Glyph(BuiltinApps.SudokuId, "view_module");
            Glyph(BuiltinApps.SolitaireId, "view_column");
        }

        private static void Glyph(string appId, string materialName)
        {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(materialName))
                return;
            GlyphByApp[appId] = materialName;
        }

        public static Sprite PaintStacker()
        {
            return Paint(64, (px, w) =>
            {
                Fill(px, w, 64, 0, 0, 64, 64, new Color32(18, 22, 28, 255));
                Color32 c = new Color32(48, 196, 214, 255);
                Fill(px, w, 64, 16, 34, 32, 12, c);
                Fill(px, w, 64, 26, 18, 12, 16, c);
            });
        }

        public static Sprite PaintFourAcross()
        {
            return Paint(64, (px, w) =>
            {
                Fill(px, w, 64, 0, 0, 64, 64, new Color32(20, 48, 96, 255));
                Disc(px, w, 64, 18, 40, 8, new Color32(220, 56, 56, 255));
                Disc(px, w, 64, 36, 40, 8, new Color32(240, 200, 48, 255));
                Disc(px, w, 64, 18, 22, 8, new Color32(240, 200, 48, 255));
                Disc(px, w, 64, 36, 22, 8, new Color32(220, 56, 56, 255));
            });
        }

        public static Sprite PaintBrickBreak()
        {
            return Paint(64, (px, w) =>
            {
                Fill(px, w, 64, 0, 0, 64, 64, new Color32(28, 16, 16, 255));
                Color32 brick = new Color32(220, 86, 64, 255);
                Fill(px, w, 64, 10, 42, 20, 8, brick);
                Fill(px, w, 64, 34, 42, 20, 8, brick);
                Fill(px, w, 64, 22, 32, 20, 8, brick);
                Fill(px, w, 64, 16, 12, 32, 6, new Color32(236, 236, 240, 255));
                Disc(px, w, 64, 40, 22, 4, new Color32(255, 255, 255, 255));
            });
        }

        public static Sprite PaintEcho()
        {
            return Paint(64, (px, w) =>
            {
                Fill(px, w, 64, 0, 0, 64, 64, new Color32(16, 18, 20, 255));
                Fill(px, w, 64, 8, 34, 22, 22, new Color32(48, 196, 96, 255));
                Fill(px, w, 64, 34, 34, 22, 22, new Color32(220, 64, 64, 255));
                Fill(px, w, 64, 8, 8, 22, 22, new Color32(240, 196, 48, 255));
                Fill(px, w, 64, 34, 8, 22, 22, new Color32(56, 120, 220, 255));
            });
        }

        private static Sprite Paint(int size, System.Action<Color32[], int> draw)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            draw(px, size);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return FromTexture(tex, true);
        }

        private static void Fill(Color32[] px, int w, int h, int x, int y, int rw, int rh, Color32 c)
        {
            int x1 = Mathf.Max(0, x);
            int y1 = Mathf.Max(0, y);
            int x2 = Mathf.Min(w, x + rw);
            int y2 = Mathf.Min(h, y + rh);
            for (int yy = y1; yy < y2; yy++)
            {
                int row = yy * w;
                for (int xx = x1; xx < x2; xx++)
                    px[row + xx] = c;
            }
        }

        private static void Disc(Color32[] px, int w, int h, int cx, int cy, int r, Color32 c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= h)
                    continue;
                int row = y * w;
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= w)
                        continue;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        px[row + x] = c;
                }
            }
        }

        private static Sprite MaterialFor(PiPhoneApp app)
        {
            if (app == null || string.IsNullOrEmpty(app.Id))
                return null;
            string name;
            if (!GlyphByApp.TryGetValue(app.Id, out name) || string.IsNullOrEmpty(name))
                return null;
            return Material(name);
        }

        private static void PunchRoundedCorners(Texture2D tex)
        {
            if (tex == null)
                return;
            try
            {
                int w = tex.width;
                int h = tex.height;
                if (w < 8 || h < 8)
                    return;
                Color32[] px = tex.GetPixels32();
                float r = Mathf.Min(w, h) * 0.22f;
                if (r < 2f)
                    r = 2f;
                float r2 = r * r;
                float maxX = w - 1 - r;
                float maxY = h - 1 - r;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = 0f;
                        float dy = 0f;
                        if (x < r)
                            dx = r - x;
                        else if (x > maxX)
                            dx = x - maxX;
                        if (y < r)
                            dy = r - y;
                        else if (y > maxY)
                            dy = y - maxY;
                        if (dx > 0f && dy > 0f && dx * dx + dy * dy > r2)
                        {
                            int i = y * w + x;
                            px[i].a = 0;
                        }
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
            }
            catch
            {
            }
        }

        private static Texture2D Clamp(Texture2D tex, bool consumeSource)
        {
            int w = tex.width;
            int h = tex.height;
            if (w <= MaxSize && h <= MaxSize)
            {
                if (!consumeSource)
                    return tex;
                return tex;
            }
            float scale = MaxSize / (float)Mathf.Max(w, h);
            int nw = Mathf.Max(2, Mathf.RoundToInt(w * scale));
            int nh = Mathf.Max(2, Mathf.RoundToInt(h * scale));
            var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
            small.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (consumeSource)
                UObject.Destroy(tex);
            return small;
        }

        private static void Reject(string reason)
        {
            Plugin.LogInfo("Icon skipped: " + reason);
        }

        private static byte[] FindResource(string fileName)
        {
            string[] names = Asm.GetManifestResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != null && names[i].EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    return ReadResource(names[i]);
            }
            return null;
        }

        private static byte[] ReadResource(string name)
        {
            try
            {
                using (Stream stream = Asm.GetManifestResourceStream(name))
                {
                    if (stream == null)
                        return null;
                    var buf = new byte[stream.Length];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = stream.Read(buf, read, buf.Length - read);
                        if (n <= 0)
                            break;
                        read += n;
                    }
                    return buf;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
