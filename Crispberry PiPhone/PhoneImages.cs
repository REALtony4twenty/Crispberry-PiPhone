using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class PhoneImages
    {
        private static readonly Type ImageConv = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
        private static readonly MethodInfo EncodePngMethod = FindEncode();
        private static readonly MethodInfo EncodeJpgMethod = FindJpg();
        private static readonly MethodInfo LoadImageMethod = FindLoad();

        private static readonly Type UwrType = Type.GetType("UnityEngine.Networking.UnityWebRequest, UnityEngine.UnityWebRequestModule");
        private static readonly Dictionary<string, Texture2D> FileCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        private static MethodInfo FindEncode()
        {
            if (ImageConv == null)
                return null;
            return ImageConv.GetMethod("EncodeToPNG", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D) }, null);
        }

        private static MethodInfo FindJpg()
        {
            if (ImageConv == null)
                return null;
            MethodInfo two = ImageConv.GetMethod("EncodeToJPG", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(int) }, null);
            if (two != null)
                return two;
            return ImageConv.GetMethod("EncodeToJPG", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D) }, null);
        }

        private static MethodInfo FindLoad()
        {
            if (ImageConv == null)
                return null;
            MethodInfo two = ImageConv.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(byte[]) }, null);
            if (two != null)
                return two;
            MethodInfo three = ImageConv.GetMethod("LoadImage", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
            if (three != null)
                return three;
            MethodInfo[] methods = ImageConv.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "LoadImage")
                    continue;
                ParameterInfo[] p = methods[i].GetParameters();
                if (p.Length >= 2 && p[0].ParameterType == typeof(Texture2D) && p[1].ParameterType == typeof(byte[]))
                    return methods[i];
            }
            return null;
        }

        public static byte[] EncodePng(Texture2D tex)
        {
            if (tex == null || EncodePngMethod == null)
                return new byte[0];
            try
            {
                byte[] png = EncodePngMethod.Invoke(null, new object[] { tex }) as byte[];
                return png ?? new byte[0];
            }
            catch (Exception ex)
            {
                Plugin.LogError("EncodePng failed: " + ex.Message);
                return new byte[0];
            }
        }

        public static bool LoadImage(Texture2D tex, byte[] bytes)
        {
            if (tex == null || bytes == null || bytes.Length == 0 || LoadImageMethod == null)
                return false;
            try
            {
                ParameterInfo[] p = LoadImageMethod.GetParameters();
                object result;
                if (p.Length >= 3)
                    result = LoadImageMethod.Invoke(null, new object[] { tex, bytes, false });
                else
                    result = LoadImageMethod.Invoke(null, new object[] { tex, bytes });
                if (result is bool && !(bool)result)
                    return false;
                return tex.width > 2 || tex.height > 2;
            }
            catch (Exception ex)
            {
                Plugin.LogError("LoadImage failed: " + ex.Message);
                return false;
            }
        }

        public static byte[] EncodeJpg(Texture2D tex, int quality)
        {
            if (tex == null || EncodeJpgMethod == null)
                return EncodePng(tex);
            try
            {
                object jpg;
                if (EncodeJpgMethod.GetParameters().Length >= 2)
                    jpg = EncodeJpgMethod.Invoke(null, new object[] { tex, quality });
                else
                    jpg = EncodeJpgMethod.Invoke(null, new object[] { tex });
                return jpg as byte[] ?? EncodePng(tex);
            }
            catch
            {
                return EncodePng(tex);
            }
        }

        public static Texture2D LoadTexture(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;
            if (PhoneGif.IsGif(bytes))
            {
                Texture2D gif = PhoneGif.FirstFrame(bytes);
                if (gif != null)
                    return gif;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            if (!LoadImage(tex, bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            return tex;
        }

        public static Texture2D LoadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                DropFile(path);
                return null;
            }
            Texture2D cached;
            if (FileCache.TryGetValue(path, out cached) && cached != null)
                return cached;
            try
            {
                cached = LoadTexture(System.IO.File.ReadAllBytes(path));
            }
            catch (Exception ex)
            {
                Plugin.LogError("LoadFile failed: " + ex.Message);
                return null;
            }
            if (cached != null)
            {
                cached.hideFlags = HideFlags.HideAndDontSave;
                FileCache[path] = cached;
            }
            return cached;
        }

        private static void DropFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            Texture2D cached;
            if (!FileCache.TryGetValue(path, out cached))
                return;
            FileCache.Remove(path);
            if (cached != null)
                UnityEngine.Object.Destroy(cached);
        }

        public static IEnumerator Download(string url, Action<byte[], string, string> done)
        {
            return Download(url, done, 0);
        }

        private static IEnumerator Download(string url, Action<byte[], string, string> done, int depth)
        {
            if (done == null)
                yield break;
            if (depth > 2)
            {
                done(null, null, "Couldn't find a media file.");
                yield break;
            }
            if (string.IsNullOrEmpty(url) || UwrType == null)
            {
                done(null, null, UwrType == null ? "Download unavailable." : "Enter a link.");
                yield break;
            }

            string trimmed = RewriteMediaUrl(url.Trim());
            if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                trimmed = "https://" + trimmed;
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.IndexOf('.') > 0)
                    trimmed = "https://" + trimmed;
                else
                {
                    done(null, null, "Paste a media link first.");
                    yield break;
                }
            }

            object req = null;
            try
            {
                MethodInfo get = UwrType.GetMethod("Get", new[] { typeof(string) });
                req = get.Invoke(null, new object[] { trimmed });
                PrepareRequest(req);
                MethodInfo send = UwrType.GetMethod("SendWebRequest", Type.EmptyTypes);
                send.Invoke(req, null);
            }
            catch (Exception ex)
            {
                done(null, null, "Couldn't start download.");
                Plugin.LogError("Download start failed: " + ex.Message);
                yield break;
            }

            PropertyInfo isDone = UwrType.GetProperty("isDone");
            while (req != null && !(bool)isDone.GetValue(req, null))
                yield return null;

            byte[] data = null;
            string header = null;
            string follow = null;
            string finishError = null;
            try
            {
                PropertyInfo errorProp = UwrType.GetProperty("error");
                string err = errorProp != null ? errorProp.GetValue(req, null) as string : null;
                if (!string.IsNullOrEmpty(err))
                    finishError = err;
                else
                {
                    PropertyInfo handlerProp = UwrType.GetProperty("downloadHandler");
                    object handler = handlerProp != null ? handlerProp.GetValue(req, null) : null;
                    PropertyInfo dataProp = handler != null ? handler.GetType().GetProperty("data") : null;
                    data = dataProp != null ? dataProp.GetValue(handler, null) as byte[] : null;
                    if (data == null || data.Length == 0)
                        finishError = "Empty download.";
                    else
                    {
                        header = GetHeader(req, "Content-Type");
                        if (LooksLikeHtml(data, header))
                        {
                            follow = ExtractMediaUrl(data, trimmed);
                            if (string.IsNullOrEmpty(follow) || string.Equals(follow, trimmed, StringComparison.OrdinalIgnoreCase))
                                finishError = "That link is a page, not a media file.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Download finish failed: " + ex.Message);
                finishError = "Download failed.";
            }

            if (!string.IsNullOrEmpty(follow) && string.IsNullOrEmpty(finishError))
            {
                IEnumerator inner = Download(follow, done, depth + 1);
                while (inner.MoveNext())
                    yield return inner.Current;
                yield break;
            }
            if (!string.IsNullOrEmpty(finishError))
            {
                done(null, null, finishError);
                yield break;
            }

            string ext = GuessExt(trimmed, header);
            if (PhoneGif.IsGif(data))
                ext = ".gif";
            done(data, ext, null);
        }

        internal static void PrepareRequest(object req)
        {
            if (req == null || UwrType == null)
                return;
            try
            {
                MethodInfo set = UwrType.GetMethod("SetRequestHeader", new[] { typeof(string), typeof(string) });
                if (set == null)
                    return;
                set.Invoke(req, new object[] { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" });
                set.Invoke(req, new object[] { "Accept", "image/gif,image/*,application/json,text/html,*/*" });
            }
            catch
            {
            }
        }

        private static string RewriteMediaUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return url;
            string u = url.Trim();
            int q = u.IndexOf('?');
            string noQuery = q >= 0 ? u.Substring(0, q) : u;
            string lower = noQuery.ToLowerInvariant();

            if (lower.IndexOf("giphy.com/gifs/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("giphy.com/media/", StringComparison.Ordinal) >= 0)
            {
                string id = LastPathToken(noQuery);
                int dash = id.LastIndexOf('-');
                if (dash >= 0 && dash < id.Length - 2)
                    id = id.Substring(dash + 1);
                if (id.Length >= 4)
                    return "https://i.giphy.com/" + id + ".gif";
            }
            if (lower.IndexOf("i.giphy.com/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("media.giphy.com/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("media0.giphy.com/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("media1.giphy.com/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("media2.giphy.com/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("media3.giphy.com/", StringComparison.Ordinal) >= 0
                || lower.IndexOf("media4.giphy.com/", StringComparison.Ordinal) >= 0)
                return u;

            if (lower.IndexOf("imgur.com/", StringComparison.Ordinal) >= 0 && lower.IndexOf("i.imgur.com/", StringComparison.Ordinal) < 0)
            {
                string id = LastPathToken(noQuery);
                if (id == "a" || id == "gallery" || id == "t")
                    return u;
                if (id.Length >= 3 && id.IndexOf('.') < 0)
                    return "https://i.imgur.com/" + id + ".gif";
            }
            return u;
        }

        private static string LastPathToken(string url)
        {
            int slash = url.LastIndexOf('/');
            string token = slash >= 0 ? url.Substring(slash + 1) : url;
            int q = token.IndexOf('?');
            if (q >= 0)
                token = token.Substring(0, q);
            return token;
        }

        private static bool LooksLikeHtml(byte[] data, string contentType)
        {
            if (contentType != null && contentType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (data == null || data.Length < 16)
                return false;
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
                return false;
            if (data[0] == 0x89 && data[1] == 0x50)
                return false;
            if (data[0] == 0xFF && data[1] == 0xD8)
                return false;
            string start = System.Text.Encoding.ASCII.GetString(data, 0, data.Length < 64 ? data.Length : 64).TrimStart();
            return start.StartsWith("<", StringComparison.Ordinal) || start.IndexOf("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ExtractMediaUrl(byte[] htmlBytes, string pageUrl)
        {
            string html = System.Text.Encoding.UTF8.GetString(htmlBytes ?? new byte[0]);
            html = html.Replace("\\/", "/");
            string direct = FirstCdnGif(html);
            if (!string.IsNullOrEmpty(direct))
                return direct;
            string[] keys = { "og:image", "twitter:image", "og:video", "contentUrl" };
            for (int k = 0; k < keys.Length; k++)
            {
                string found = AfterAttr(html, keys[k]);
                if (!string.IsNullOrEmpty(found) && found.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    && found.IndexOf(".gif", StringComparison.OrdinalIgnoreCase) >= 0)
                    return found;
            }
            int gif = html.IndexOf(".gif", StringComparison.OrdinalIgnoreCase);
            if (gif > 12)
            {
                int start = html.LastIndexOf("http", gif);
                if (start >= 0 && gif + 4 - start < 240)
                    return html.Substring(start, gif + 4 - start).Replace("&amp;", "&");
            }
            return null;
        }

        internal static string FirstCdnGif(string text)
        {
            List<string> all = CollectCdnGifs(text, 1);
            return all.Count > 0 ? all[0] : null;
        }

        internal static List<string> CollectCdnGifs(string text, int max)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(text) || max < 1)
                return list;
            text = text.Replace("\\/", "/");
            string[] hosts =
            {
                "https://media.tenor.com/",
                "https://c.tenor.com/",
                "https://i.giphy.com/",
                "https://media.giphy.com/",
                "https://media0.giphy.com/",
                "https://media1.giphy.com/",
                "https://media2.giphy.com/",
                "https://media3.giphy.com/",
                "https://media4.giphy.com/"
            };
            int pos = 0;
            while (list.Count < max && pos < text.Length)
            {
                int best = -1;
                for (int i = 0; i < hosts.Length; i++)
                {
                    int h = text.IndexOf(hosts[i], pos, StringComparison.OrdinalIgnoreCase);
                    if (h >= 0 && (best < 0 || h < best))
                        best = h;
                }
                if (best < 0)
                    break;
                int end = text.IndexOfAny(new[] { '"', '\'', '<', '>', ' ', '\\', '\n', '\r', ')' }, best + 8);
                if (end < 0)
                    end = Math.Min(text.Length, best + 240);
                string url = text.Substring(best, end - best);
                pos = best + 8;
                if (url.IndexOf(".gif", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!ContainsIgnore(list, url))
                    list.Add(url);
            }
            return list;
        }

        private static bool ContainsIgnore(List<string> list, string url)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], url, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string AfterAttr(string html, string key)
        {
            int i = html.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0)
                return null;
            int content = html.IndexOf("content=\"", i, StringComparison.OrdinalIgnoreCase);
            if (content < 0 || content - i > 180)
                content = html.LastIndexOf("content=\"", i, StringComparison.OrdinalIgnoreCase);
            if (content < 0)
                return null;
            content += 9;
            int end = html.IndexOf('"', content);
            if (end <= content)
                return null;
            return html.Substring(content, end - content).Replace("&amp;", "&");
        }

        private static string GetHeader(object req, string name)
        {
            try
            {
                MethodInfo m = UwrType.GetMethod("GetResponseHeader", new[] { typeof(string) });
                if (m == null)
                    return null;
                return m.Invoke(req, new object[] { name }) as string;
            }
            catch
            {
                return null;
            }
        }

        private static string GuessExt(string url, string contentType)
        {
            string ct = contentType != null ? contentType.ToLowerInvariant() : string.Empty;
            if (ct.IndexOf("gif") >= 0) return ".gif";
            if (ct.IndexOf("jpeg") >= 0 || ct.IndexOf("jpg") >= 0) return ".jpg";
            if (ct.IndexOf("webp") >= 0) return ".webp";
            if (ct.IndexOf("png") >= 0) return ".png";

            string path = url;
            int q = path.IndexOf('?');
            if (q >= 0)
                path = path.Substring(0, q);
            path = path.ToLowerInvariant();
            if (path.EndsWith(".gif")) return ".gif";
            if (path.EndsWith(".jpg") || path.EndsWith(".jpeg")) return ".jpg";
            if (path.EndsWith(".webp")) return ".webp";
            return ".png";
        }

        public static string GuessExtFromBytes(byte[] data, string kindHint)
        {
            if (data != null && data.Length >= 12)
            {
                if (data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
                    return ".gif";
                if (data[0] == 0xFF && data[1] == 0xD8)
                    return ".jpg";
                if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                    return ".png";
                if (data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F')
                {
                    if (data[8] == (byte)'A' && data[9] == (byte)'V' && data[10] == (byte)'I')
                        return ".avi";
                    if (data[8] == (byte)'W' && data[9] == (byte)'A' && data[10] == (byte)'V' && data[11] == (byte)'E')
                        return ".wav";
                    return ".wav";
                }
                if (data[4] == (byte)'f' && data[5] == (byte)'t' && data[6] == (byte)'y' && data[7] == (byte)'p')
                    return ".mp4";
                if (data[0] == (byte)'I' && data[1] == (byte)'D' && data[2] == (byte)'3')
                    return ".mp3";
                if (data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
                    return ".mp3";
            }
            if (kindHint == "gif")
                return ".gif";
            if (kindHint == "vid")
                return ".mp4";
            if (kindHint == "aud")
                return ".mp3";
            return ".jpg";
        }
    }
}
