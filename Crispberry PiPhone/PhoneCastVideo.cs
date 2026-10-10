using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// The moving picture of an app that paints its own frame. The caster's
    /// app area is cut from the phone render, squeezed into small pictures,
    /// and sent to the players watching the cast, who show them on the board.
    /// </summary>
    internal static class PhoneCastVideo
    {
        private const int LongSide = 320;
        private const int ChunkBytes = 900;
        internal const int AudioCap = 1;
        private const int TopRung = 3;
        private const int JudgeFrames = 30;
        private const int LossLine = 10;

        private static readonly int[] RungQuality = { 25, 25, 40, 60 };
        private static readonly float[] RungRate = { 8f, 15f, 15f, 15f };

        private static readonly MethodInfo EncodeArray = FindEncodeArray();
        private static readonly Action<AsyncGPUReadbackRequest> ReadbackDone = OnReadback;
        private static readonly List<int> Audience = new List<int>();
        private static readonly List<int> Gone = new List<int>();
        private static readonly Dictionary<int, Eye> Eyes = new Dictionary<int, Eye>();
        private static readonly object Gate = new object();
        private static readonly AutoResetEvent Wake = new AutoResetEvent(false);

        private static RenderTexture _small;
        private static Texture2D _encodeTex;
        private static Thread _worker;
        private static byte[] _raw;
        private static int _rawW;
        private static int _rawH;
        private static bool _rawRgba;
        private static int _rawQuality;
        private static int _stage;
        private static int _tag;
        private static int _frameTag;
        private static bool _rgba;
        private static bool _mainEncode;
        private static bool _encodeFailed;
        private static bool _resultReady;
        private static byte[] _resultJpg;
        private static string _resultError;
        private static bool _open;
        private static float _nextShot;
        private static int _seq;
        private static int _rung = TopRung;

        private sealed class Eye
        {
            public int Rung = TopRung;
            public int Clean;
            public int Got;
            public int Lost;
            public float Seen;
            public readonly int[] Falls = new int[TopRung + 1];
            public readonly float[] BarUntil = new float[TopRung + 1];
        }

        private static Texture2D _tex;
        private static RawImage _hole;
        private static bool _haveFrame;
        private static string _watch = string.Empty;
        private static int _rxSeq;
        private static byte[][] _rxParts;
        private static int _rxGot;
        private static byte[] _rxParity;
        private static byte[] _pending;
        private static int _seeGot;
        private static int _seeLost;
        private static float _seeNext;

        private static MethodInfo FindEncodeArray()
        {
            Type conv = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            if (conv == null)
                return null;
            return conv.GetMethod("EncodeArrayToJPG", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Array), typeof(GraphicsFormat), typeof(uint), typeof(uint), typeof(uint), typeof(int) }, null);
        }

        internal static void Tick()
        {
            if (_stage == 2)
                Collect();
            RunCapture();
            TrackWatch();
            ShowNewest();
        }

        private static void RunCapture()
        {
            if (!PhoneCast.VideoOpen)
            {
                if (_open)
                {
                    _open = false;
                    _tag++;
                }
                if (_stage == 0 && _small != null)
                {
                    _small.Release();
                    UnityEngine.Object.Destroy(_small);
                    _small = null;
                }
                return;
            }
            float now = Time.unscaledTime;
            if (!_open)
            {
                _open = true;
                foreach (KeyValuePair<int, Eye> pair in Eyes)
                    pair.Value.Seen = now;
            }
            Climb(now);
            if (now < _nextShot)
                return;
            _nextShot = now + 1f / RungRate[_rung];
            if (_stage == 0)
                Capture(LongSide, RungQuality[_rung]);
        }

        private static void Climb(float now)
        {
            PhoneCast.CopyAudience(Audience);
            Gone.Clear();
            foreach (KeyValuePair<int, Eye> pair in Eyes)
            {
                if (!Audience.Contains(pair.Key))
                    Gone.Add(pair.Key);
            }
            for (int i = 0; i < Gone.Count; i++)
                Eyes.Remove(Gone[i]);
            int rung = TopRung;
            for (int i = 0; i < Audience.Count; i++)
            {
                Eye eye;
                if (!Eyes.TryGetValue(Audience[i], out eye))
                {
                    eye = new Eye();
                    eye.Seen = now;
                    Eyes[Audience[i]] = eye;
                }
                if (now - eye.Seen > 5f)
                {
                    eye.Seen = now;
                    StepDown(eye, now);
                }
                if (eye.Rung < rung)
                    rung = eye.Rung;
            }
            if (rung == _rung)
                return;
            _rung = rung;
            Plugin.LogInfo("Cast video: rung " + rung);
        }

        internal static void CastSeen(int actor, int received, int lost)
        {
            Eye eye;
            if (received < 0 || lost < 0 || received + lost <= 0 || !Eyes.TryGetValue(actor, out eye))
                return;
            float now = Time.unscaledTime;
            eye.Seen = now;
            eye.Got += received;
            eye.Lost += lost;
            if (eye.Got + eye.Lost < JudgeFrames)
                return;
            bool over = eye.Lost * 100L > (eye.Got + (long)eye.Lost) * LossLine;
            eye.Got = 0;
            eye.Lost = 0;
            if (over)
            {
                StepDown(eye, now);
                return;
            }
            eye.Clean++;
            if (eye.Clean < 5 || eye.Rung >= TopRung || now < eye.BarUntil[eye.Rung + 1])
                return;
            eye.Rung++;
            eye.Clean = 0;
        }

        private static void StepDown(Eye eye, float now)
        {
            eye.Clean = 0;
            eye.Got = 0;
            eye.Lost = 0;
            if (eye.Rung <= 0)
                return;
            int rung = eye.Rung;
            eye.Falls[rung]++;
            eye.BarUntil[rung] = now + 60f * (1 << Mathf.Min(eye.Falls[rung] - 1, 10));
            eye.Rung = rung - 1;
        }

        private static void Send(byte[] jpg)
        {
            if (!PhoneCast.VideoOpen)
                return;
            string device = PhoneCast.CopyAudience(Audience);
            if (Audience.Count == 0)
                return;
            int[] actors = Audience.ToArray();
            _seq++;
            int count = (jpg.Length + ChunkBytes - 1) / ChunkBytes;
            var parity = new byte[4 + Math.Min(ChunkBytes, jpg.Length)];
            parity[0] = (byte)jpg.Length;
            parity[1] = (byte)(jpg.Length >> 8);
            parity[2] = (byte)(jpg.Length >> 16);
            parity[3] = (byte)(jpg.Length >> 24);
            for (int i = 0; i < count; i++)
            {
                int offset = i * ChunkBytes;
                int len = Math.Min(ChunkBytes, jpg.Length - offset);
                var chunk = new byte[len];
                Buffer.BlockCopy(jpg, offset, chunk, 0, len);
                for (int j = 0; j < len; j++)
                    parity[4 + j] ^= chunk[j];
                PhoneNet.SendCastVideo(actors, device, _seq, i, count, chunk);
            }
            PhoneNet.SendCastVideo(actors, device, _seq, count, count, parity);
        }

        private static void Capture(int longSide, int quality)
        {
            RenderTexture src;
            RectTransform bezel = PhoneMenu.BezelRt;
            float xMin;
            float yMin;
            float xMax;
            float yMax;
            if (bezel == null || !PhoneCastMirror.VideoBox(bezel, out xMin, out yMin, out xMax, out yMax) || !PhoneCast.TryPhoneRender(out src))
                return;
            Rect box = bezel.rect;
            if (box.width < 1f || box.height < 1f)
                return;
            float u0 = Mathf.Clamp01((xMin - box.xMin) / box.width);
            float u1 = Mathf.Clamp01((xMax - box.xMin) / box.width);
            float v0 = Mathf.Clamp01((yMin - box.yMin) / box.height);
            float v1 = Mathf.Clamp01((yMax - box.yMin) / box.height);
            float fw = u1 - u0;
            float fh = v1 - v0;
            if (fw < 0.01f || fh < 0.01f)
                return;

            float pw = fw * src.width;
            float ph = fh * src.height;
            int w = pw >= ph ? longSide : Mathf.RoundToInt(longSide * pw / ph);
            int h = pw >= ph ? Mathf.RoundToInt(longSide * ph / pw) : longSide;
            EnsureSmall(Even(w), Even(h));

            Graphics.Blit(src, _small, new Vector2(fw, fh), new Vector2(u0, v0));
            _frameTag = _tag;
            _rawQuality = quality;
            _stage = 1;
            AsyncGPUReadback.Request(_small, 0, _rgba ? TextureFormat.RGBA32 : TextureFormat.RGB24, ReadbackDone);
        }

        private static int Even(int value)
        {
            value &= ~1;
            return value < 8 ? 8 : value;
        }

        private static void EnsureSmall(int w, int h)
        {
            if (_small != null && (_small.width != w || _small.height != h))
            {
                _small.Release();
                UnityEngine.Object.Destroy(_small);
                _small = null;
            }
            if (_small == null)
            {
                _small = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                _small.wrapMode = TextureWrapMode.Clamp;
                _small.filterMode = FilterMode.Bilinear;
                _small.Create();
            }
        }

        private static void OnReadback(AsyncGPUReadbackRequest req)
        {
            if (_stage != 1)
                return;
            if (req.hasError)
            {
                _stage = 0;
                if (!_rgba)
                {
                    _rgba = true;
                    Plugin.LogInfo("Cast video: reading the picture back as RGBA32.");
                }
                return;
            }
            NativeArray<byte> data = req.GetData<byte>();
            if (_raw == null || _raw.Length != data.Length)
                _raw = new byte[data.Length];
            data.CopyTo(_raw);
            _rawW = req.width;
            _rawH = req.height;
            _rawRgba = data.Length >= _rawW * _rawH * 4;

            if (!_mainEncode && EncodeArray == null)
            {
                _mainEncode = true;
                Plugin.LogInfo("Cast video: encoding on the main thread. EncodeArrayToJPG not found.");
            }
            if (_mainEncode)
            {
                EncodeOnMain();
                return;
            }
            if (_worker == null)
            {
                _worker = new Thread(Work);
                _worker.IsBackground = true;
                _worker.Start();
            }
            lock (Gate)
                _resultReady = false;
            _stage = 2;
            Wake.Set();
        }

        private static void Work()
        {
            while (true)
            {
                Wake.WaitOne();
                byte[] jpg = null;
                string error = null;
                try
                {
                    jpg = Encode();
                    if (jpg == null || jpg.Length == 0)
                        error = "the encoder returned nothing";
                }
                catch (Exception ex)
                {
                    error = Reason(ex);
                }
                lock (Gate)
                {
                    _resultJpg = jpg;
                    _resultError = error;
                    _resultReady = true;
                }
            }
        }

        private static byte[] Encode()
        {
            GraphicsFormat format = _rawRgba ? GraphicsFormat.R8G8B8A8_UNorm : GraphicsFormat.R8G8B8_UNorm;
            return EncodeArray.Invoke(null, new object[] { _raw, format, (uint)_rawW, (uint)_rawH, 0u, _rawQuality }) as byte[];
        }

        private static string Reason(Exception ex)
        {
            Exception inner = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
            return inner.GetType().Name + " " + inner.Message;
        }

        private static void Collect()
        {
            byte[] jpg;
            string error;
            lock (Gate)
            {
                if (!_resultReady)
                    return;
                _resultReady = false;
                jpg = _resultJpg;
                error = _resultError;
                _resultJpg = null;
            }
            if (error != null)
            {
                _mainEncode = true;
                Plugin.LogInfo("Cast video: encoding on the main thread. " + error);
                EncodeOnMain();
                return;
            }
            FrameDone(jpg);
        }

        private static void EncodeOnMain()
        {
            byte[] jpg = null;
            try
            {
                if (EncodeArray != null)
                    jpg = Encode();
                else
                {
                    TextureFormat format = _rawRgba ? TextureFormat.RGBA32 : TextureFormat.RGB24;
                    if (_encodeTex != null && (_encodeTex.width != _rawW || _encodeTex.height != _rawH || _encodeTex.format != format))
                    {
                        UnityEngine.Object.Destroy(_encodeTex);
                        _encodeTex = null;
                    }
                    if (_encodeTex == null)
                    {
                        _encodeTex = new Texture2D(_rawW, _rawH, format, false);
                        _encodeTex.hideFlags = HideFlags.HideAndDontSave;
                    }
                    _encodeTex.LoadRawTextureData(_raw);
                    _encodeTex.Apply(false);
                    jpg = PhoneImages.EncodeJpg(_encodeTex, _rawQuality);
                }
            }
            catch (Exception ex)
            {
                if (!_encodeFailed)
                    Plugin.LogError("Cast video encode failed: " + Reason(ex));
                _encodeFailed = true;
            }
            FrameDone(jpg);
        }

        private static void FrameDone(byte[] jpg)
        {
            _stage = 0;
            if (_frameTag != _tag || jpg == null || jpg.Length == 0)
                return;
            Send(jpg);
        }

        internal static void Receive(int seq, int index, int count, byte[] data)
        {
            if (data == null || count < 1 || index < 0 || index > count)
                return;
            if (seq < _rxSeq && _rxSeq - seq < 1000)
                return;
            if (seq != _rxSeq)
            {
                if (_rxParts != null)
                    _seeLost++;
                if (seq > _rxSeq && _rxSeq > 0)
                    _seeLost += seq - _rxSeq - 1;
                _rxSeq = seq;
                _rxParts = new byte[count][];
                _rxParity = null;
                _rxGot = 0;
            }
            if (_rxParts == null || _rxParts.Length != count)
                return;
            if (index == count)
                _rxParity = data;
            else if (_rxParts[index] == null)
            {
                _rxParts[index] = data;
                _rxGot++;
            }
            if (_rxGot < count && !(_rxGot == count - 1 && _rxParity != null && Repair()))
                return;

            int total = 0;
            for (int i = 0; i < count; i++)
                total += _rxParts[i].Length;
            var jpg = new byte[total];
            int at = 0;
            for (int i = 0; i < count; i++)
            {
                Buffer.BlockCopy(_rxParts[i], 0, jpg, at, _rxParts[i].Length);
                at += _rxParts[i].Length;
            }
            _rxParts = null;
            _rxParity = null;
            _seeGot++;
            _pending = jpg;
        }

        private static bool Repair()
        {
            int size = _rxParity.Length - 4;
            int total = _rxParity[0] | (_rxParity[1] << 8) | (_rxParity[2] << 16) | (_rxParity[3] << 24);
            int count = _rxParts.Length;
            if (size < 1 || total < 1)
                return false;
            int missing = -1;
            var chunk = new byte[size];
            Buffer.BlockCopy(_rxParity, 4, chunk, 0, size);
            for (int i = 0; i < count; i++)
            {
                byte[] part = _rxParts[i];
                if (part == null)
                {
                    missing = i;
                    continue;
                }
                if (part.Length > size)
                    return false;
                for (int j = 0; j < part.Length; j++)
                    chunk[j] ^= part[j];
            }
            if (missing < 0)
                return false;
            int len = missing == count - 1 ? total - size * (count - 1) : size;
            if (len < 1 || len > size)
                return false;
            if (len < size)
                Array.Resize(ref chunk, len);
            _rxParts[missing] = chunk;
            _rxGot++;
            return true;
        }

        internal static void Show(RawImage hole)
        {
            _hole = hole;
            if (hole == null)
            {
                _haveFrame = false;
                return;
            }
            EnsureTex();
            hole.texture = _tex;
            hole.color = _haveFrame ? Color.white : Color.black;
        }

        private static void EnsureTex()
        {
            if (_tex != null)
                return;
            _tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            _tex.hideFlags = HideFlags.HideAndDontSave;
            _tex.wrapMode = TextureWrapMode.Clamp;
        }

        private static void TrackWatch()
        {
            Vector3 point;
            string id;
            int owner;
            string watch = PhoneCast.TryWatchPoint(out point, out id, out owner) ? id : string.Empty;
            if (watch == _watch)
            {
                if (watch.Length > 0 && Time.unscaledTime >= _seeNext)
                {
                    _seeNext = Time.unscaledTime + 2f;
                    if (_seeGot + _seeLost > 0)
                    {
                        PhoneNet.SendCastSee(owner, id, _seeGot, _seeLost);
                        _seeGot = 0;
                        _seeLost = 0;
                    }
                }
                return;
            }
            _watch = watch;
            _rxSeq = 0;
            _rxParts = null;
            _rxParity = null;
            _rxGot = 0;
            _seeGot = 0;
            _seeLost = 0;
            _pending = null;
            _haveFrame = false;
            if (_hole != null)
                _hole.color = Color.black;
        }

        private static void ShowNewest()
        {
            if (_pending == null)
                return;
            byte[] jpg = _pending;
            _pending = null;
            EnsureTex();
            if (!PhoneImages.LoadImage(_tex, jpg))
                return;
            _haveFrame = true;
            if (_hole != null)
            {
                _hole.texture = _tex;
                _hole.color = Color.white;
            }
        }
    }
}
