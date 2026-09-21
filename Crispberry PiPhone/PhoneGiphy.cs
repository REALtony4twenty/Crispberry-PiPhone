using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal sealed class GiphyHit
    {
        public string Id;
        public string Title;
        public string Url;
        public string ThumbUrl;
    }

    internal static class PhoneGiphy
    {
        private static readonly Type UwrType = Type.GetType("UnityEngine.Networking.UnityWebRequest, UnityEngine.UnityWebRequestModule");

        public static IEnumerator Search(string query, Action<List<GiphyHit>, string> done)
        {
            if (done == null)
                yield break;
            if (string.IsNullOrEmpty(query) || query.Trim().Length == 0)
            {
                done(new List<GiphyHit>(), "Type a search.");
                yield break;
            }
            if (UwrType == null)
            {
                done(null, "Search unavailable.");
                yield break;
            }

            string q = query.Trim();
            List<GiphyHit> hits = null;
            List<string> urls = SearchUrls(q);
            for (int i = 0; i < urls.Count; i++)
            {
                string body = null;
                IEnumerator req = Fetch(urls[i], s => body = s);
                while (req.MoveNext())
                    yield return req.Current;
                hits = Parse(body);
                if (hits.Count > 0)
                    break;
            }

            if (hits == null || hits.Count == 0)
                done(hits ?? new List<GiphyHit>(), "No GIFs found.");
            else
                done(hits, null);
        }

        private static List<string> SearchUrls(string query)
        {
            var list = new List<string>();
            string enc = Uri.EscapeDataString(query);
            string slug = Slug(query);
            string key = Plugin.Instance != null && Plugin.Instance.TenorApiKey != null
                ? (Plugin.Instance.TenorApiKey.Value ?? string.Empty).Trim()
                : string.Empty;
            if (key.Length > 8)
            {
                list.Add("https://tenor.googleapis.com/v2/search?q=" + enc + "&key=" + Uri.EscapeDataString(key)
                    + "&client_key=crispberry_pip&limit=12&media_filter=tinygif,gif&contentfilter=medium");
            }
            list.Add("https://tenor.com/search/" + slug + "-gifs");
            list.Add("https://tenor.com/en_US/search/" + slug + "-gifs");
            list.Add("https://giphy.com/search/" + slug);
            return list;
        }

        private static string Slug(string query)
        {
            var sb = new System.Text.StringBuilder(query.Length);
            bool dash = false;
            for (int i = 0; i < query.Length; i++)
            {
                char c = query[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(char.ToLowerInvariant(c));
                    dash = false;
                }
                else if (!dash && sb.Length > 0)
                {
                    sb.Append('-');
                    dash = true;
                }
            }
            if (sb.Length > 0 && sb[sb.Length - 1] == '-')
                sb.Length--;
            return sb.Length > 0 ? sb.ToString() : "gif";
        }

        private static IEnumerator Fetch(string url, Action<string> done)
        {
            object req = null;
            try
            {
                MethodInfo get = UwrType.GetMethod("Get", new[] { typeof(string) });
                req = get.Invoke(null, new object[] { url });
                PhoneImages.PrepareRequest(req);
                MethodInfo send = UwrType.GetMethod("SendWebRequest", Type.EmptyTypes);
                send.Invoke(req, null);
            }
            catch (Exception ex)
            {
                Plugin.LogError("GIF search start: " + ex.Message);
                yield break;
            }

            PropertyInfo isDone = UwrType.GetProperty("isDone");
            float start = Time.realtimeSinceStartup;
            while (req != null && !(bool)isDone.GetValue(req, null))
            {
                if (Time.realtimeSinceStartup - start > 14f)
                    yield break;
                yield return null;
            }

            try
            {
                PropertyInfo handlerProp = UwrType.GetProperty("downloadHandler");
                object handler = handlerProp != null ? handlerProp.GetValue(req, null) : null;
                PropertyInfo textProp = handler != null ? handler.GetType().GetProperty("text") : null;
                string body = textProp != null ? textProp.GetValue(handler, null) as string : null;
                if (done != null)
                    done(body);
            }
            catch (Exception ex)
            {
                Plugin.LogError("GIF search finish: " + ex.Message);
            }
        }

        private static List<GiphyHit> Parse(string body)
        {
            var list = new List<GiphyHit>();
            if (string.IsNullOrEmpty(body))
                return list;
            List<string> urls = PhoneImages.CollectCdnGifs(body, 40);
            var seen = new Dictionary<string, GiphyHit>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < urls.Count; i++)
            {
                string url = urls[i];
                if (!IsDirectGif(url))
                    continue;
                string id = MediaId(url);
                GiphyHit hit;
                if (!seen.TryGetValue(id, out hit))
                {
                    hit = new GiphyHit
                    {
                        Id = id,
                        Title = "GIF",
                        Url = url,
                        ThumbUrl = url
                    };
                    seen[id] = hit;
                    list.Add(hit);
                }
                if (url.IndexOf("tinygif", StringComparison.OrdinalIgnoreCase) >= 0
                    || url.IndexOf("nanogif", StringComparison.OrdinalIgnoreCase) >= 0)
                    hit.ThumbUrl = url;
                if (url.IndexOf("tinygif", StringComparison.OrdinalIgnoreCase) >= 0)
                    hit.Url = url;
            }
            if (list.Count > 12)
                list.RemoveRange(12, list.Count - 12);
            return list;
        }

        private static bool IsDirectGif(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            string l = url.ToLowerInvariant();
            if (l.IndexOf(".gif", StringComparison.Ordinal) < 0)
                return false;
            if (l.IndexOf("/gifs/", StringComparison.Ordinal) >= 0 && l.IndexOf("i.giphy.com", StringComparison.Ordinal) < 0
                && l.IndexOf("media.giphy.com", StringComparison.Ordinal) < 0)
                return false;
            if (l.IndexOf("tenor.com/view", StringComparison.Ordinal) >= 0
                || l.IndexOf("tenor.com/search", StringComparison.Ordinal) >= 0)
                return false;
            return l.IndexOf("media.tenor.com", StringComparison.Ordinal) >= 0
                || l.IndexOf("c.tenor.com", StringComparison.Ordinal) >= 0
                || l.IndexOf("giphy.com", StringComparison.Ordinal) >= 0;
        }

        private static string MediaId(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "gif";
            string path = url;
            int q = path.IndexOf('?');
            if (q >= 0)
                path = path.Substring(0, q);
            string[] parts = path.Split('/');
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                if (string.IsNullOrEmpty(parts[i]) || parts[i].IndexOf('.') >= 0)
                    continue;
                if (parts[i].Length >= 6)
                    return parts[i];
            }
            return path;
        }
    }
}
