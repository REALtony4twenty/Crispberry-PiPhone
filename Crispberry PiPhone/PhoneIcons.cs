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
        public static Sprite Logo()
        {
            const string key = "logo:crispberry";
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;
            byte[] bytes = ReadResource("Crispberry_PiPhone.Icons.logo.png") ?? FindResource("logo.png");
            if (bytes == null)
                return null;
            Texture2D tex = PhoneImages.LoadTexture(bytes);
            if (tex == null)
                return null;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PiP_Logo";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        public static Sprite Material(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            bool outline = !PhoneTheme.FilledIcons;
            string file = outline ? "o_" + name : name;
            string key = (outline ? "o:" : "m:") + name;
            Sprite cached;
            if (Cache.TryGetValue(key, out cached) && cached != null)
                return cached;
            byte[] bytes = ReadResource("Crispberry_PiPhone.Icons.material." + file + ".png")
                ?? FindResource("material." + file + ".png")
                ?? FindResource(file + ".png");
            if (bytes == null && outline)
            {
                bytes = ReadResource("Crispberry_PiPhone.Icons.material." + name + ".png")
                    ?? FindResource("material." + name + ".png")
                    ?? FindResource(name + ".png");
            }
            if (bytes == null && !outline)
            {
                bytes = ReadResource("Crispberry_PiPhone.Icons.material.o_" + name + ".png")
                    ?? FindResource("material.o_" + name + ".png")
                    ?? FindResource("o_" + name + ".png");
            }
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
            return FromBytes(pngOrJpg, allowOversizeFile, MaxSize);
        }

        internal static Sprite FromBytes(byte[] pngOrJpg, bool allowOversizeFile, int maxSize)
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
            return FromTexture(tex, true, maxSize);
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
            return FromTexture(tex, consumeSource, MaxSize);
        }

        /// <summary>Store-page art. Copied and clamped to 480px. You may destroy <paramref name="texture"/> after.</summary>
        public static Sprite Screenshot(Texture2D texture)
        {
            return FromTexture(texture, false, 480);
        }

        public static Sprite FromTexture(Texture2D tex, bool consumeSource, int maxSize)
        {
            if (tex == null)
                return null;
            if (maxSize < 8)
                maxSize = 8;
            Texture2D ready = Clamp(tex, consumeSource, maxSize);
            if (ready == null)
                return null;
            ready.wrapMode = TextureWrapMode.Clamp;
            ready.filterMode = FilterMode.Bilinear;
            ready.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(ready, new Rect(0f, 0f, ready.width, ready.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PiP_Icon";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static Sprite ClampSprite(Sprite sprite)
        {
            return ClampSprite(sprite, MaxSize);
        }

        public static Sprite ClampSprite(Sprite sprite, int maxSize)
        {
            if (sprite == null || sprite.texture == null)
                return sprite;
            Texture2D tex = sprite.texture as Texture2D;
            if (tex == null)
                return sprite;
            if (tex.width <= maxSize && tex.height <= maxSize)
                return sprite;
            return FromTexture(tex, false, maxSize);
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
            Glyph(BuiltinApps.ClosetId, "checkroom");
            Glyph(BuiltinApps.MakeNotiId, "add_alert");
            Glyph(BuiltinApps.SnakeId, "gesture");
            Glyph(BuiltinApps.Game2048Id, "grid_on");
            Glyph(BuiltinApps.MinesId, "bomb");
            Glyph(BuiltinApps.SudokuId, "background_grid_small");
            Glyph(BuiltinApps.SolitaireId, "playing_cards");
            Glyph(BuiltinApps.SimonId, "action_key");
            Glyph(BuiltinApps.Connect4Id, "transition_dissolve");
            Glyph(BuiltinApps.TetrisId, "browse");
            Glyph(BuiltinApps.BreakoutId, "tile_medium");
        }

        private static void Glyph(string appId, string materialName)
        {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(materialName))
                return;
            GlyphByApp[appId] = materialName;
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

        private static Texture2D Clamp(Texture2D tex, bool consumeSource)
        {
            return Clamp(tex, consumeSource, MaxSize);
        }

        private static Texture2D Clamp(Texture2D tex, bool consumeSource, int maxSize)
        {
            int w = tex.width;
            int h = tex.height;
            if (w <= maxSize && h <= maxSize)
                return tex;
            float scale = maxSize / (float)Mathf.Max(w, h);
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
