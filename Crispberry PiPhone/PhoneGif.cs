using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class PhoneGif
    {
        public static bool IsGif(byte[] bytes)
        {
            return bytes != null && bytes.Length > 6
                && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F';
        }

        public static Texture2D FirstFrame(byte[] bytes)
        {
            Clip clip = Decode(bytes, 1);
            if (clip == null || clip.Frames == null || clip.Frames.Length == 0)
                return null;
            return clip.Frames[0];
        }

        public sealed class Clip
        {
            public Texture2D[] Frames;
            public float[] Delays;
        }

        private static readonly Dictionary<string, Clip> FileCache = new Dictionary<string, Clip>();

        public static Clip DecodeFile(string path, int maxFrames)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            Clip clip;
            if (FileCache.TryGetValue(path, out clip) && clip != null && clip.Frames != null && clip.Frames.Length > 0)
                return clip;
            try
            {
                clip = Decode(File.ReadAllBytes(path), maxFrames);
            }
            catch (Exception ex)
            {
                Plugin.LogError("GIF file decode failed: " + ex.Message);
                return null;
            }
            if (clip != null)
                FileCache[path] = clip;
            return clip;
        }

        public static Clip Decode(byte[] bytes, int maxFrames)
        {
            if (!IsGif(bytes))
                return null;
            try
            {
                var r = new Reader(bytes);
                r.Skip(6);
                int width = r.U16();
                int height = r.U16();
                int packed = r.U8();
                r.U8();
                r.U8();
                int gctSize = (packed & 0x80) != 0 ? 1 << ((packed & 7) + 1) : 0;
                Color32[] gct = gctSize > 0 ? r.Palette(gctSize) : null;
                var canvas = new Color32[width * height];
                var frames = new List<Texture2D>();
                var delays = new List<float>();
                Color32[] lct = null;
                int delayCs = 10;
                int transIndex = -1;
                int dispose = 0;
                Color32[] backup = null;

                while (!r.Done)
                {
                    int b = r.U8();
                    if (b == 0x3B)
                        break;
                    if (b == 0x21)
                    {
                        int label = r.U8();
                        if (label == 0xF9)
                        {
                            r.U8();
                            int p = r.U8();
                            delayCs = r.U16();
                            if (delayCs < 2)
                                delayCs = 10;
                            transIndex = r.U8();
                            r.U8();
                            dispose = (p >> 2) & 7;
                            if ((p & 1) == 0)
                                transIndex = -1;
                        }
                        else
                        {
                            while (true)
                            {
                                int n = r.U8();
                                if (n == 0)
                                    break;
                                r.Skip(n);
                            }
                        }
                        continue;
                    }
                    if (b != 0x2C)
                        break;

                    int left = r.U16();
                    int top = r.U16();
                    int iw = r.U16();
                    int ih = r.U16();
                    int ip = r.U8();
                    bool interlace = (ip & 0x40) != 0;
                    if ((ip & 0x80) != 0)
                        lct = r.Palette(1 << ((ip & 7) + 1));
                    else
                        lct = null;
                    Color32[] pal = lct ?? gct;
                    if (pal == null)
                        break;
                    int minCode = r.U8();
                    byte[] index = Lzw(r, minCode, iw * ih);
                    if (dispose == 3)
                    {
                        backup = new Color32[canvas.Length];
                        Array.Copy(canvas, backup, canvas.Length);
                    }
                    Draw(canvas, width, height, index, iw, ih, left, top, pal, transIndex, interlace);
                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.SetPixels32(canvas);
                    tex.Apply(false, false);
                    frames.Add(tex);
                    delays.Add(delayCs / 100f);
                    if (dispose == 2)
                    {
                        ClearRect(canvas, width, left, top, iw, ih);
                    }
                    else if (dispose == 3 && backup != null)
                        Array.Copy(backup, canvas, canvas.Length);
                    if (maxFrames > 0 && frames.Count >= maxFrames)
                        break;
                }

                if (frames.Count == 0)
                    return null;
                return new Clip { Frames = frames.ToArray(), Delays = delays.ToArray() };
            }
            catch (Exception ex)
            {
                Plugin.LogError("GIF decode failed: " + ex.Message);
                return null;
            }
        }

        private static void Draw(Color32[] canvas, int cw, int ch, byte[] index, int iw, int ih, int left, int top, Color32[] pal, int trans, bool interlace)
        {
            int[] passRows = interlace ? new[] { 0, 4, 2, 1 } : new[] { 0 };
            int[] passStep = interlace ? new[] { 8, 8, 4, 2 } : new[] { 1 };
            int src = 0;
            for (int p = 0; p < passRows.Length; p++)
            {
                for (int y = passRows[p]; y < ih; y += passStep[p])
                {
                    int dy = top + y;
                    if (dy < 0 || dy >= ch)
                    {
                        src += iw;
                        continue;
                    }
                    int row = (ch - 1 - dy) * cw;
                    for (int x = 0; x < iw && src < index.Length; x++, src++)
                    {
                        int dx = left + x;
                        if (dx < 0 || dx >= cw)
                            continue;
                        int idx = index[src];
                        if (idx == trans)
                            continue;
                        if (idx >= 0 && idx < pal.Length)
                            canvas[row + dx] = pal[idx];
                    }
                }
            }
        }

        private static void ClearRect(Color32[] canvas, int cw, int left, int top, int iw, int ih)
        {
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < ih; y++)
            {
                int dy = top + y;
                if (dy < 0)
                    continue;
                int row = (canvas.Length / cw - 1 - dy) * cw;
                if (row < 0 || row >= canvas.Length)
                    continue;
                for (int x = 0; x < iw; x++)
                {
                    int dx = left + x;
                    if (dx >= 0 && dx < cw)
                        canvas[row + dx] = clear;
                }
            }
        }

        private static byte[] Lzw(Reader r, int minCodeSize, int expect)
        {
            var output = new byte[Math.Max(expect, 1)];
            int outPos = 0;
            int clear = 1 << minCodeSize;
            int end = clear + 1;
            int codeSize = minCodeSize + 1;
            int nextCode = end + 1;
            var prefix = new int[4096];
            var suffix = new byte[4096];
            var stack = new byte[4096];
            for (int i = 0; i < clear; i++)
            {
                prefix[i] = -1;
                suffix[i] = (byte)i;
            }

            var bits = new BitStream(r);
            int prev = -1;
            while (true)
            {
                int code = bits.Read(codeSize);
                if (code < 0 || code == end)
                    break;
                if (code == clear)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = end + 1;
                    prev = -1;
                    continue;
                }
                int inCode = code;
                int stackPos = 0;
                if (code >= nextCode)
                {
                    if (prev < 0)
                        break;
                    stack[stackPos++] = suffix[First(prefix, suffix, prev)];
                    code = prev;
                }
                while (code >= clear)
                {
                    stack[stackPos++] = suffix[code];
                    code = prefix[code];
                    if (code < 0 || stackPos >= stack.Length - 1)
                        break;
                }
                byte first = suffix[code];
                stack[stackPos++] = first;
                while (stackPos > 0 && outPos < output.Length)
                    output[outPos++] = stack[--stackPos];
                if (prev >= 0 && nextCode < 4096)
                {
                    prefix[nextCode] = prev;
                    suffix[nextCode] = first;
                    nextCode++;
                    if (nextCode >= (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }
                prev = inCode;
                if (outPos >= expect)
                    break;
            }
            bits.FinishBlock();
            return output;
        }

        private static int First(int[] prefix, byte[] suffix, int code)
        {
            int guard = 0;
            while (code >= 0 && prefix[code] >= 0 && guard++ < 4096)
                code = prefix[code];
            return code;
        }

        private sealed class BitStream
        {
            private readonly Reader _r;
            private int _bitBuf;
            private int _bitCount;
            private byte[] _block = new byte[0];
            private int _blockPos;

            public BitStream(Reader r)
            {
                _r = r;
            }

            public int Read(int bits)
            {
                while (_bitCount < bits)
                {
                    if (_blockPos >= _block.Length)
                    {
                        int n = _r.U8();
                        if (n == 0)
                            return -1;
                        _block = _r.Bytes(n);
                        _blockPos = 0;
                    }
                    _bitBuf |= _block[_blockPos++] << _bitCount;
                    _bitCount += 8;
                }
                int v = _bitBuf & ((1 << bits) - 1);
                _bitBuf >>= bits;
                _bitCount -= bits;
                return v;
            }

            public void FinishBlock()
            {
                while (true)
                {
                    int n = _r.U8();
                    if (n == 0)
                        return;
                    _r.Skip(n);
                }
            }
        }

        private sealed class Reader
        {
            private readonly byte[] _b;
            private int _i;

            public Reader(byte[] b)
            {
                _b = b;
            }

            public bool Done
            {
                get { return _i >= _b.Length; }
            }

            public int U8()
            {
                return _i < _b.Length ? _b[_i++] : 0;
            }

            public int U16()
            {
                int a = U8();
                return a | (U8() << 8);
            }

            public void Skip(int n)
            {
                _i = Math.Min(_b.Length, _i + n);
            }

            public byte[] Bytes(int n)
            {
                n = Math.Min(n, _b.Length - _i);
                var c = new byte[n];
                Buffer.BlockCopy(_b, _i, c, 0, n);
                _i += n;
                return c;
            }

            public Color32[] Palette(int count)
            {
                var p = new Color32[count];
                for (int i = 0; i < count; i++)
                    p[i] = new Color32((byte)U8(), (byte)U8(), (byte)U8(), 255);
                return p;
            }
        }
    }

    /// <summary>Plays a decoded GIF on a RawImage while the card is fully in the scroll viewport.</summary>
    internal sealed class PhoneGifPlay : MonoBehaviour
    {
        public RawImage Target;
        public PhoneGif.Clip Clip;
        public RectTransform Viewport;
        private float _elapsed;
        private int _frame;
        private bool _playing;

        private void Update()
        {
            if (Target == null || Clip == null || Clip.Frames == null || Clip.Frames.Length < 2)
                return;
            if (!FullyVisible())
            {
                if (_playing)
                {
                    _playing = false;
                    _frame = 0;
                    _elapsed = 0f;
                    if (Clip.Frames[0] != null)
                        Target.texture = Clip.Frames[0];
                }
                return;
            }
            _playing = true;
            _elapsed += Time.unscaledDeltaTime;
            float wait = Clip.Delays != null && _frame < Clip.Delays.Length ? Clip.Delays[_frame] : 0.1f;
            if (wait < 0.04f)
                wait = 0.04f;
            if (_elapsed < wait)
                return;
            _elapsed = 0f;
            _frame++;
            if (_frame >= Clip.Frames.Length)
                _frame = 0;
            if (Clip.Frames[_frame] != null)
                Target.texture = Clip.Frames[_frame];
        }

        private bool FullyVisible()
        {
            RectTransform card = transform as RectTransform;
            if (card == null || Viewport == null || !Viewport.gameObject.activeInHierarchy)
                return false;
            var cc = new Vector3[4];
            var vv = new Vector3[4];
            card.GetWorldCorners(cc);
            Viewport.GetWorldCorners(vv);
            const float slop = 6f;
            if (cc[0].x < vv[0].x - slop || cc[2].x > vv[2].x + slop)
                return false;
            float overlap = Mathf.Min(cc[2].y, vv[2].y) - Mathf.Max(cc[0].y, vv[0].y);
            if (overlap <= 0f)
                return false;
            float cardH = cc[2].y - cc[0].y;
            float viewH = vv[2].y - vv[0].y;
            if (cardH <= viewH + slop)
                return cc[0].y >= vv[0].y - slop && cc[2].y <= vv[2].y + slop;
            return overlap >= viewH - slop;
        }
    }
}
