using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Marks a box whose contents reach cast watchers as moving pictures.
    /// </summary>
    internal sealed class PhoneCastVideoArea : MonoBehaviour
    {
    }

    /// <summary>
    /// The pictures a cast cannot describe from the phone's own kit. The caster
    /// fingerprints each one, tells a still picture from one that keeps being
    /// repainted, and sends a still one once to every watcher who asks for it.
    /// </summary>
    internal static class PhoneCastPictures
    {
        private const int MaxSide = 512;
        private const int ProbeSide = 32;
        private const int ChunkBytes = 4000;
        private const int MaxFlights = 4;
        private const int MaxChunks = 96;
        private const float MovingMemory = 5f;
        private const float ChangeWindow = 2f;
        private const float BytesPerSecond = 64000f;
        private const long MaxKept = 8388608;
        private const int MaxAsk = 64;
        private const int MaxWantsEach = 128;
        private const int MaxKeyLength = 96;
        private const int MaxParts = 8;
        private const int MaxHave = 512;
        private const int MaxAsked = 2048;
        private const long MaxHaveBytes = 67108864;

        private sealed class Entry
        {
            public Texture Tex;
            public Rect Rect;
            public Vector4 Border;
            public float Ppu = 100f;
            public float Seen;
            public float Units;
            public int Side;
            public bool Busy;
            public int Ticket;
            public float BusySince;
            public bool HasProbe;
            public ulong Probe;
            public int Calm;
            public float NextProbe;
            public float ChangedAt = -100f;
            public float MovedAt = -100f;
            public uint Count;
            public bool Touched;
            public float CheckedAt = -100f;
            public bool Stale;
            public string Key;
            public string Prev;
            public bool Flat;
            public Color32 FlatColor;
        }

        private sealed class Blob
        {
            public byte[] Bytes;
            public string Border;
        }

        private sealed class Want
        {
            public int Actor;
            public string Key;
            public float Until;
        }

        private sealed class Result
        {
            public Entry Entry;
            public bool Probe;
            public byte[] Raw;
            public int W;
            public int H;
            public int Side;
        }

        private sealed class Got
        {
            public Texture2D Tex;
            public Sprite Sprite;
        }

        private sealed class Part
        {
            public byte[][] Chunks;
            public int Count;
            public string Border;
        }

        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Entry> Alias = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Blob> Blobs = new Dictionary<string, Blob>(StringComparer.Ordinal);
        private static readonly List<Want> Wants = new List<Want>();
        private static readonly List<Result> Results = new List<Result>();
        private static readonly List<string> Dead = new List<string>();
        private static readonly System.Diagnostics.Stopwatch Budget = new System.Diagnostics.Stopwatch();
        private static int _flights;
        private static int _cast;
        private static long _kept;
        private static float _allowance;
        private static Want _sending;
        private static Blob _sendingBlob;
        private static int _sendingChunk;
        private static int _sent;
        private static int _sentBytes;
        private static bool _encodeFailed;

        private static readonly Dictionary<string, Got> Have = new Dictionary<string, Got>(StringComparer.Ordinal);
        private static readonly Dictionary<string, float> Asked = new Dictionary<string, float>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Part> Parts = new Dictionary<string, Part>(StringComparer.Ordinal);
        private static readonly HashSet<string> Need = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<string> Asking = new List<string>();
        private static bool _repaint;
        private static long _haveBytes;

        internal static string KeyFor(Texture tex, Rect rect, Vector4 border, float ppu, float units, string alias, out bool moving, out bool flat, out Color flatColor)
        {
            moving = false;
            flat = false;
            flatColor = Color.white;
            if (tex == null || rect.width < 1f || rect.height < 1f)
                return "p";
            string id = tex.GetInstanceID().ToString(CultureInfo.InvariantCulture) + ":"
                + Mathf.RoundToInt(rect.x) + "," + Mathf.RoundToInt(rect.y) + "," + Mathf.RoundToInt(rect.width) + "," + Mathf.RoundToInt(rect.height) + ":"
                + Mathf.RoundToInt(border.x) + "," + Mathf.RoundToInt(border.y) + "," + Mathf.RoundToInt(border.z) + "," + Mathf.RoundToInt(border.w);
            Entry entry;
            if (!Entries.TryGetValue(id, out entry))
            {
                entry = new Entry();
                entry.Tex = tex;
                entry.Rect = rect;
                entry.Border = border;
                entry.Ppu = ppu > 0.01f ? ppu : 100f;
                entry.Count = tex.updateCount;
                Entries[id] = entry;
            }
            entry.Seen = Time.unscaledTime;
            if (units > entry.Units)
                entry.Units = units;
            if (alias != null)
                Alias[alias] = entry;
            if (IsMoving(entry))
            {
                moving = true;
                return "p";
            }
            if (entry.Key == null)
                return "p";
            if (entry.Flat && alias == null)
            {
                flat = true;
                flatColor = entry.FlatColor;
                return string.Empty;
            }
            return entry.Prev != null ? entry.Key + "<" + entry.Prev : entry.Key;
        }

        private static bool IsMoving(Entry entry)
        {
            return entry.Tex is RenderTexture || Time.unscaledTime - entry.MovedAt < MovingMemory;
        }

        internal static void Tick()
        {
            TickWatch();
            if (!PhoneCast.Casting)
            {
                if (Entries.Count > 0 || Blobs.Count > 0 || Wants.Count > 0)
                    EndCast();
                return;
            }
            Drain();
            if (!PhoneCast.Sharing)
                return;
            float now = Time.unscaledTime;
            foreach (KeyValuePair<string, Entry> pair in Entries)
            {
                Entry entry = pair.Value;
                if (entry.Tex == null)
                {
                    Dead.Add(pair.Key);
                    continue;
                }
                if (entry.Busy)
                {
                    if (now - entry.BusySince > 5f)
                    {
                        entry.Busy = false;
                        entry.Ticket++;
                        _flights--;
                    }
                    continue;
                }
                if (now - entry.Seen > 1f || entry.Tex is RenderTexture)
                    continue;
                uint count = entry.Tex.updateCount;
                if (count != entry.Count)
                {
                    entry.Count = count;
                    entry.Touched = true;
                    if (entry.NextProbe > now + 0.15f)
                        entry.NextProbe = now + 0.15f;
                }
                if (_flights >= MaxFlights)
                    continue;
                bool moving = now - entry.MovedAt < MovingMemory;
                if (!moving && entry.Calm >= 1 && (entry.Key == null || entry.Stale || WantSide(entry) > entry.Side))
                    Read(entry, false, now);
                else if (now >= entry.NextProbe)
                    Read(entry, true, now);
            }
            for (int i = 0; i < Dead.Count; i++)
                Entries.Remove(Dead[i]);
            Dead.Clear();
            SendOne(now);
        }

        private static int WantSide(Entry entry)
        {
            float source = Mathf.Max(entry.Rect.width, entry.Rect.height);
            float want = Mathf.Max(32f, entry.Units * 1.5f);
            int side = 32;
            while (side < want && side < MaxSide)
                side *= 2;
            return Mathf.Max(1, Mathf.Min(side, Mathf.CeilToInt(source)));
        }

        private static void Read(Entry entry, bool probe, float now)
        {
            Texture tex = entry.Tex;
            if (tex.width < 1 || tex.height < 1)
                return;
            int side = probe ? ProbeSide : WantSide(entry);
            int w = ProbeSide;
            int h = ProbeSide;
            if (!probe)
            {
                float fit = Mathf.Min(1f, side / Mathf.Max(entry.Rect.width, entry.Rect.height));
                w = Mathf.Max(1, Mathf.RoundToInt(entry.Rect.width * fit));
                h = Mathf.Max(1, Mathf.RoundToInt(entry.Rect.height * fit));
            }
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt,
                new Vector2(entry.Rect.width / tex.width, entry.Rect.height / tex.height),
                new Vector2(entry.Rect.x / tex.width, entry.Rect.y / tex.height));
            entry.Busy = true;
            entry.BusySince = now;
            entry.Ticket++;
            _flights++;
            int ticket = entry.Ticket;
            int cast = _cast;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req => Done(req, entry, rt, probe, side, ticket, cast));
        }

        private static void Done(AsyncGPUReadbackRequest req, Entry entry, RenderTexture rt, bool probe, int side, int ticket, int cast)
        {
            RenderTexture.ReleaseTemporary(rt);
            if (cast != _cast || ticket != entry.Ticket || !entry.Busy)
                return;
            entry.Busy = false;
            _flights--;
            if (req.hasError)
            {
                entry.NextProbe = Time.unscaledTime + 1f;
                return;
            }
            NativeArray<byte> data = req.GetData<byte>();
            var result = new Result();
            result.Entry = entry;
            result.Probe = probe;
            result.Raw = new byte[data.Length];
            data.CopyTo(result.Raw);
            result.W = req.width;
            result.H = req.height;
            result.Side = side;
            Results.Add(result);
        }

        private static void Drain()
        {
            if (Results.Count == 0)
                return;
            Budget.Restart();
            int done = 0;
            float now = Time.unscaledTime;
            while (done < Results.Count && Budget.Elapsed.TotalMilliseconds < 3.0)
            {
                Result result = Results[done];
                done++;
                Entry entry = result.Entry;
                if (result.Probe)
                {
                    ulong hash = Fnv(result.Raw, 14695981039346656037UL);
                    if (!entry.HasProbe)
                    {
                        entry.HasProbe = true;
                        entry.Calm = 0;
                    }
                    else if (hash != entry.Probe)
                    {
                        entry.Calm = 0;
                        if (now - entry.ChangedAt < ChangeWindow)
                        {
                            entry.MovedAt = now;
                            if (entry.Key != null)
                                entry.Prev = entry.Key;
                            entry.Key = null;
                            entry.Flat = false;
                            entry.Stale = false;
                        }
                        else
                            entry.Stale = true;
                        entry.ChangedAt = now;
                    }
                    else
                    {
                        entry.Calm++;
                        if (entry.Touched && entry.Key != null && now - entry.CheckedAt > 2f)
                        {
                            entry.Stale = true;
                            entry.CheckedAt = now;
                        }
                    }
                    entry.Touched = false;
                    entry.Probe = hash;
                    bool settled = entry.Key != null && !entry.Stale && now - entry.MovedAt >= MovingMemory;
                    entry.NextProbe = now + (settled ? 0.5f : 0.15f);
                }
                else
                    Finish(entry, result);
            }
            Results.RemoveRange(0, done);
        }

        private static void Finish(Entry entry, Result result)
        {
            byte[] raw = result.Raw;
            int w = result.W;
            int h = result.H;
            if (raw.Length < w * h * 4 || w < 1 || h < 1)
                return;
            float fit = w / Mathf.Max(1f, entry.Rect.width);
            string border = string.Empty;
            if (entry.Border.sqrMagnitude > 0.01f)
            {
                border = Mathf.RoundToInt(entry.Border.x * fit) + "," + Mathf.RoundToInt(entry.Border.y * fit) + ","
                    + Mathf.RoundToInt(entry.Border.z * fit) + "," + Mathf.RoundToInt(entry.Border.w * fit) + ","
                    + (entry.Ppu * fit).ToString("0.###", CultureInfo.InvariantCulture);
            }
            ulong hash = Fnv(raw, 14695981039346656037UL);
            unchecked
            {
                hash = (hash ^ (ulong)w) * 1099511628211UL;
                hash = (hash ^ (ulong)h) * 1099511628211UL;
                for (int i = 0; i < border.Length; i++)
                    hash = (hash ^ border[i]) * 1099511628211UL;
            }
            bool clear = false;
            bool flat = true;
            for (int i = 0; i < w * h * 4; i += 4)
            {
                if (raw[i + 3] != 255)
                    clear = true;
                if (raw[i] != raw[0] || raw[i + 1] != raw[1] || raw[i + 2] != raw[2] || raw[i + 3] != raw[3])
                {
                    flat = false;
                    if (clear)
                        break;
                }
            }
            byte[] bytes = null;
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.LoadRawTextureData(raw);
                tex.Apply(false);
                if (clear)
                    bytes = PhoneImages.EncodePng(tex);
                else
                {
                    bytes = PhoneImages.EncodeJpg(tex, 85);
                    if (w * h <= 128 * 128)
                    {
                        byte[] png = PhoneImages.EncodePng(tex);
                        if (png != null && png.Length > 0 && (bytes == null || bytes.Length == 0 || png.Length < bytes.Length))
                            bytes = png;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_encodeFailed)
                    Plugin.LogError("Cast picture encode failed: " + ex.Message);
                _encodeFailed = true;
            }
            if (tex != null)
                UnityEngine.Object.Destroy(tex);
            if (bytes == null || bytes.Length == 0 || bytes.Length > ChunkBytes * MaxChunks || _kept + bytes.Length > MaxKept)
            {
                entry.NextProbe = Time.unscaledTime + 2f;
                entry.Calm = 0;
                return;
            }
            string key = "x" + hash.ToString("x16");
            if (!Blobs.ContainsKey(key))
            {
                var blob = new Blob();
                blob.Bytes = bytes;
                blob.Border = border;
                Blobs[key] = blob;
                _kept += bytes.Length;
            }
            if (entry.Key != null && entry.Key != key)
                entry.Prev = entry.Key;
            if (entry.Prev == key)
                entry.Prev = null;
            entry.Key = key;
            entry.Side = result.Side;
            entry.Stale = false;
            entry.Flat = flat;
            entry.FlatColor = new Color32(raw[0], raw[1], raw[2], raw[3]);
        }

        private static ulong Fnv(byte[] data, ulong hash)
        {
            unchecked
            {
                for (int i = 0; i < data.Length; i++)
                {
                    hash ^= data[i];
                    hash *= 1099511628211UL;
                }
            }
            return hash;
        }

        internal static void OnWant(int actor, string keys)
        {
            if (string.IsNullOrEmpty(keys) || keys.Length > 16000)
                return;
            string[] list = keys.Split('\n');
            float until = Time.unscaledTime + 10f;
            int mine = 0;
            for (int j = 0; j < Wants.Count; j++)
            {
                if (Wants[j].Actor == actor)
                    mine++;
            }
            for (int i = 0; i < list.Length && i < MaxAsk && Wants.Count < 512; i++)
            {
                string key = list[i];
                if (key.Length < 2 || key.Length > MaxKeyLength)
                    continue;
                bool known = false;
                for (int j = 0; j < Wants.Count; j++)
                {
                    if (Wants[j].Actor == actor && Wants[j].Key == key)
                    {
                        Wants[j].Until = until;
                        known = true;
                        break;
                    }
                }
                if (known || mine >= MaxWantsEach)
                    continue;
                var want = new Want();
                want.Actor = actor;
                want.Key = key;
                want.Until = until;
                Wants.Add(want);
                mine++;
            }
        }

        private static void SendOne(float now)
        {
            _allowance = Mathf.Min(BytesPerSecond, _allowance + Time.unscaledDeltaTime * BytesPerSecond);
            if (_sending == null)
            {
                for (int i = 0; i < Wants.Count; i++)
                {
                    Want want = Wants[i];
                    if (now > want.Until)
                    {
                        Wants.RemoveAt(i);
                        i--;
                        continue;
                    }
                    Blob blob = Find(want.Key);
                    if (blob == null)
                        continue;
                    Wants.RemoveAt(i);
                    if (!PhoneCast.IsWatcher(want.Actor))
                        return;
                    _sending = want;
                    _sendingBlob = blob;
                    _sendingChunk = 0;
                    break;
                }
            }
            if (_sending == null)
                return;
            byte[] bytes = _sendingBlob.Bytes;
            int count = (bytes.Length + ChunkBytes - 1) / ChunkBytes;
            int offset = _sendingChunk * ChunkBytes;
            int len = Math.Min(ChunkBytes, bytes.Length - offset);
            if (_allowance < len)
                return;
            _allowance -= len;
            var chunk = new byte[len];
            Buffer.BlockCopy(bytes, offset, chunk, 0, len);
            PhoneNet.SendCastPicture(_sending.Actor, PhoneCast.DeviceId, _sending.Key, _sendingBlob.Border, _sendingChunk, count, chunk);
            _sendingChunk++;
            if (_sendingChunk < count)
                return;
            _sent++;
            _sentBytes += bytes.Length;
            _sending = null;
            _sendingBlob = null;
        }

        internal static string Summary()
        {
            string text = "Sent " + _sent + " pictures, " + PhoneNet.SizeLabel(_sentBytes) + ".";
            _sent = 0;
            _sentBytes = 0;
            return text;
        }

        private static Blob Find(string key)
        {
            Blob blob;
            if (key[0] == 'x')
                return Blobs.TryGetValue(key, out blob) ? blob : null;
            Entry entry;
            if (!Alias.TryGetValue(key, out entry) || entry.Key == null)
                return null;
            return Blobs.TryGetValue(entry.Key, out blob) ? blob : null;
        }

        internal static void EndCast()
        {
            _cast++;
            _flights = 0;
            Entries.Clear();
            Alias.Clear();
            Blobs.Clear();
            Wants.Clear();
            Results.Clear();
            _kept = 0;
            _sending = null;
            _sendingBlob = null;
        }

        internal static void BeginPaint()
        {
            Need.Clear();
        }

        internal static Sprite Lookup(string key)
        {
            Got got;
            string old = null;
            int cut = key.IndexOf('<');
            if (cut > 0)
            {
                old = key.Substring(cut + 1);
                key = key.Substring(0, cut);
            }
            if (Have.TryGetValue(key, out got) && got.Sprite != null)
                return got.Sprite;
            Need.Add(key);
            if (old != null && Have.TryGetValue(old, out got) && got.Sprite != null)
                return got.Sprite;
            return null;
        }

        private static void TickWatch()
        {
            if (_repaint)
            {
                _repaint = false;
                PhoneCastView.Repaint();
            }
            if (Need.Count == 0 || Have.Count >= MaxHave || _haveBytes >= MaxHaveBytes)
                return;
            Vector3 point;
            string id;
            int owner;
            if (!PhoneCast.TryWatchPoint(out point, out id, out owner))
                return;
            float now = Time.unscaledTime;
            Asking.Clear();
            foreach (string key in Need)
            {
                float at;
                bool asked = Asked.TryGetValue(key, out at);
                if (key.Length > MaxKeyLength || Have.ContainsKey(key) || (asked && now - at < 6f) || (!asked && Asked.Count >= MaxAsked))
                    continue;
                Asking.Add(key);
                if (Asking.Count >= MaxAsk)
                    break;
            }
            if (Asking.Count == 0)
                return;
            for (int i = 0; i < Asking.Count; i++)
                Asked[Asking[i]] = now;
            PhoneNet.SendCastWant(owner, id, string.Join("\n", Asking.ToArray()));
        }

        internal static void OnPicture(string key, string border, int index, int count, byte[] data)
        {
            if (string.IsNullOrEmpty(key) || data == null || data.Length > ChunkBytes || count < 1 || count > MaxChunks || index < 0 || index >= count)
                return;
            if (!Asked.ContainsKey(key) || Have.ContainsKey(key))
                return;
            Part part;
            if (!Parts.TryGetValue(key, out part) || part.Chunks.Length != count)
            {
                if (part == null && Parts.Count >= MaxParts)
                    return;
                part = new Part();
                part.Chunks = new byte[count][];
                part.Border = border ?? string.Empty;
                Parts[key] = part;
            }
            if (part.Chunks[index] != null)
                return;
            part.Chunks[index] = data;
            part.Count++;
            if (part.Count < count)
                return;
            Parts.Remove(key);
            int total = 0;
            for (int i = 0; i < count; i++)
                total += part.Chunks[i].Length;
            var bytes = new byte[total];
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                Buffer.BlockCopy(part.Chunks[i], 0, bytes, at, part.Chunks[i].Length);
                at += part.Chunks[i].Length;
            }
            if (!Fits(bytes, MaxSide))
                return;
            Texture2D tex = PhoneImages.LoadTexture(bytes);
            if (tex == null)
                return;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.hideFlags = HideFlags.HideAndDontSave;
            _haveBytes += (long)tex.width * tex.height * 4;
            Vector4 edge = Vector4.zero;
            float ppu = 100f;
            string[] f = part.Border.Split(',');
            if (f.Length >= 5)
            {
                float l;
                float b;
                float r;
                float t;
                float p;
                if (Num(f[0], out l) && Num(f[1], out b) && Num(f[2], out r) && Num(f[3], out t) && Num(f[4], out p)
                    && l >= 0f && b >= 0f && r >= 0f && t >= 0f && l + r < tex.width && b + t < tex.height && p > 0.01f)
                {
                    edge = new Vector4(l, b, r, t);
                    ppu = p;
                }
            }
            var got = new Got();
            got.Tex = tex;
            got.Sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, edge);
            got.Sprite.name = "PiP_CastPicture";
            got.Sprite.hideFlags = HideFlags.HideAndDontSave;
            Have[key] = got;
            Need.Remove(key);
            _repaint = true;
        }

        internal static bool Fits(byte[] bytes, int maxSide)
        {
            int w = 0;
            int h = 0;
            if (bytes == null)
                return false;
            if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 'P' && bytes[2] == 'N' && bytes[3] == 'G')
            {
                if (bytes[16] != 0 || bytes[17] != 0 || bytes[20] != 0 || bytes[21] != 0)
                    return false;
                w = (bytes[18] << 8) | bytes[19];
                h = (bytes[22] << 8) | bytes[23];
            }
            else if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                int i = 2;
                while (i + 9 < bytes.Length)
                {
                    if (bytes[i] != 0xFF)
                        return false;
                    int marker = bytes[i + 1];
                    if (marker == 0xFF)
                    {
                        i++;
                        continue;
                    }
                    if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                    {
                        i += 2;
                        continue;
                    }
                    int len = (bytes[i + 2] << 8) | bytes[i + 3];
                    if (len < 2)
                        return false;
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                    {
                        h = (bytes[i + 5] << 8) | bytes[i + 6];
                        w = (bytes[i + 7] << 8) | bytes[i + 8];
                        break;
                    }
                    i += 2 + len;
                }
            }
            return w >= 1 && h >= 1 && w <= maxSide && h <= maxSide;
        }

        private static bool Num(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        internal static void EndWatch()
        {
            foreach (KeyValuePair<string, Got> pair in Have)
            {
                if (pair.Value.Sprite != null)
                    UnityEngine.Object.Destroy(pair.Value.Sprite);
                if (pair.Value.Tex != null)
                    UnityEngine.Object.Destroy(pair.Value.Tex);
            }
            Have.Clear();
            Asked.Clear();
            Parts.Clear();
            Need.Clear();
            _haveBytes = 0;
            _repaint = false;
        }
    }
}
