using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Records MJPEG AVI (PCM audio muxed in) and plays those files on the phone.
    /// MP4/H.264 encode is skipped — AVI is the format that actually works in PEAK.
    /// </summary>
    internal static class PhoneVideo
    {
        public const int Fps = 30;
        public const int MaxSeconds = 20;

        private static readonly Type PlayerType = Type.GetType("UnityEngine.Video.VideoPlayer, UnityEngine.VideoModule");

        public static bool CanPlay
        {
            get { return PlayerType != null; }
        }

        public static byte[] DecodeAudioToWav(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            IMFSourceReader reader = null;
            try
            {
                int hr = Native.MFStartup(Native.MfVersion, 0);
                if (hr < 0)
                    return null;
                hr = Native.MFCreateSourceReaderFromURL(path, IntPtr.Zero, out reader);
                if (hr < 0)
                {
                    try
                    {
                        hr = Native.MFCreateSourceReaderFromURL(new Uri(path).AbsoluteUri, IntPtr.Zero, out reader);
                    }
                    catch
                    {
                        hr = -1;
                    }
                }
                if (hr < 0 || reader == null)
                    return null;

                const int firstAudio = unchecked((int)0xFFFFFFFD);
                IMFMediaType pcmType;
                hr = Native.MFCreateMediaType(out pcmType);
                if (hr < 0)
                    return null;
                Guid major = Native.MfMtMajorType;
                Guid sub = Native.MfMtSubtype;
                Guid audio = Native.MfMediaTypeAudio;
                Guid pcm = Native.MfAudioFormatPcm;
                Guid ch = Native.MfMtAudioNumChannels;
                Guid rateKey = Native.MfMtAudioSamplesPerSecond;
                Guid bits = Native.MfMtAudioBitsPerSample;
                Guid block = Native.MfMtAudioBlockAlignment;
                Guid avg = Native.MfMtAudioAvgBytesPerSecond;
                pcmType.SetGUID(ref major, ref audio);
                pcmType.SetGUID(ref sub, ref pcm);
                pcmType.SetUINT32(ref ch, 1);
                pcmType.SetUINT32(ref rateKey, 44100);
                pcmType.SetUINT32(ref bits, 16);
                pcmType.SetUINT32(ref block, 2);
                pcmType.SetUINT32(ref avg, 88200);
                hr = reader.SetCurrentMediaType(firstAudio, IntPtr.Zero, pcmType);
                Release(pcmType);
                if (hr < 0)
                {
                    Plugin.LogError("[Audio] SetCurrentMediaType 0x" + hr.ToString("X8"));
                    return null;
                }

                using (var ms = new MemoryStream())
                {
                    while (true)
                    {
                        int streamIndex;
                        int flags;
                        long time;
                        IMFSample sample;
                        hr = reader.ReadSample(firstAudio, 0, out streamIndex, out flags, out time, out sample);
                        if (hr < 0)
                            break;
                        if ((flags & 2) != 0)
                            break;
                        if (sample == null)
                            continue;
                        IMFMediaBuffer buffer;
                        if (sample.ConvertToContiguousBuffer(out buffer) < 0 || buffer == null)
                        {
                            Release(sample);
                            continue;
                        }
                        IntPtr data;
                        int maxLen;
                        int curLen;
                        buffer.Lock(out data, out maxLen, out curLen);
                        if (curLen > 0 && data != IntPtr.Zero)
                        {
                            var chunk = new byte[curLen];
                            Marshal.Copy(data, chunk, 0, curLen);
                            ms.Write(chunk, 0, curLen);
                        }
                        buffer.Unlock();
                        Release(buffer);
                        Release(sample);
                    }
                    if (ms.Length < 64)
                        return null;
                    return VoiceIo.WrapWav(ms.ToArray(), 44100, 1);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Audio decode failed: " + ex.Message);
                return null;
            }
            finally
            {
                Release(reader);
                try { Native.MFShutdown(); } catch { }
            }
        }

        public sealed class Recorder : IDisposable
        {
            private IMFSinkWriter _writer;
            private AviWriter _avi;
            private int _stream;
            private int _width;
            private int _height;
            private long _time;
            private long _duration;
            private bool _open;
            private byte[] _frameBytes;
            private int _inputKind;
            private int _audioStream = -1;
            private long _audioTime;
            private int _audioRate = 44100;
            private float _voicePeak;
            private readonly List<float> _pcm = new List<float>(44100);
            private GameAudioTap _tap;
            private bool _logged;
            public string OutputFile;

            public bool IsOpen
            {
                get { return _open; }
            }

            public bool Start(string path, int width, int height)
            {
                Dispose();
                width &= ~1;
                height &= ~1;
                if (width < 16 || height < 16 || string.IsNullOrEmpty(path))
                    return false;
                string avi = Path.ChangeExtension(path, ".avi");
                _avi = new AviWriter();
                if (_avi.Start(avi, width, height))
                {
                    OutputFile = avi;
                    _width = width;
                    _height = height;
                    _open = true;
                    BeginAudioCapture();
                    Log("Recording MJPEG AVI " + avi);
                    return true;
                }
                _avi = null;
                Log("AVI encoder failed.");
                return false;
            }

            private bool TryH264(string path, int width, int height)
            {
                int[] kinds = { 1, 2, 0, 3 };
                bool[] hw = { false, true };
                bool[] audio = { true, false };
                for (int a = 0; a < audio.Length; a++)
                {
                    for (int h = 0; h < hw.Length; h++)
                    {
                        for (int k = 0; k < kinds.Length; k++)
                        {
                            string label = "H264 " + InputName(kinds[k]) + (hw[h] ? " hw" : " sw") + (audio[a] ? "+audio" : "");
                            if (TryMediaFoundation(path, width, height, hw[h], audio[a] ? 8000000 : 8000000, audio[a], kinds[k], label))
                                return true;
                        }
                    }
                }
                return false;
            }

            private static string InputName(int kind)
            {
                if (kind == 1) return "YUY2";
                if (kind == 2) return "I420";
                if (kind == 3) return "RGB32";
                return "NV12";
            }

            private bool TryMediaFoundation(string path, int width, int height, bool hardware, int bitrate, bool withAudio, int inputKind, string label)
            {
                IMFSinkWriter writer = null;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? PhoneStore.PhotoDir);
                    if (File.Exists(path))
                        File.Delete(path);

                    int hr = Native.MFStartup(Native.MfVersion, 0);
                    if (hr < 0)
                        return Fail(label + " MFStartup", hr);

                    IMFAttributes attrs;
                    hr = Native.MFCreateAttributes(out attrs, 4);
                    if (hr < 0)
                        return Fail(label + " MFCreateAttributes", hr);
                    SetU32(attrs, Native.MfReadwriteEnableHardwareTransforms, hardware ? 1 : 0);
                    SetU32(attrs, Native.MfSinkWriterDisableThrottling, 1);
                    SetGuidAttr(attrs, Native.MfTranscodeContainerType, Native.MfTranscodeContainerMpeg4);

                    hr = Native.MFCreateSinkWriterFromURL(path, IntPtr.Zero, attrs, out writer);
                    Release(attrs);
                    if (hr < 0)
                    {
                        Release(writer);
                        return Fail(label + " SinkWriter", hr);
                    }

                    IMFMediaType outType;
                    hr = Native.MFCreateMediaType(out outType);
                    if (hr < 0)
                        return Fail(label + " out type", hr);
                    SetGuid(outType, Native.MfMtMajorType, Native.MfMediaTypeVideo);
                    SetGuid(outType, Native.MfMtSubtype, Native.MfVideoFormatH264);
                    SetU32(outType, Native.MfMtAvgBitrate, bitrate);
                    SetU32(outType, Native.MfMtMpeg2Profile, 66);
                    SetU32(outType, Native.MfMtInterlaceMode, 2);
                    SetU64(outType, Native.MfMtFrameSize, Pack(width, height));
                    SetU64(outType, Native.MfMtFrameRate, Pack(Fps, 1));
                    SetU64(outType, Native.MfMtPixelAspectRatio, Pack(1, 1));

                    int stream;
                    hr = writer.AddStream(outType, out stream);
                    Release(outType);
                    if (hr < 0)
                    {
                        Release(writer);
                        return Fail(label + " AddStream", hr);
                    }

                    int audioStream = -1;
                    if (withAudio)
                    {
                        audioStream = TryAddAudioStream(writer);
                        if (audioStream < 0)
                            withAudio = false;
                    }

                    IMFMediaType inType;
                    hr = Native.MFCreateMediaType(out inType);
                    if (hr < 0)
                        return Fail(label + " in type", hr);
                    SetGuid(inType, Native.MfMtMajorType, Native.MfMediaTypeVideo);
                    SetGuid(inType, Native.MfMtSubtype, InputSubtype(inputKind));
                    SetU32(inType, Native.MfMtInterlaceMode, 2);
                    SetU64(inType, Native.MfMtFrameSize, Pack(width, height));
                    SetU64(inType, Native.MfMtFrameRate, Pack(Fps, 1));
                    SetU64(inType, Native.MfMtPixelAspectRatio, Pack(1, 1));
                    hr = writer.SetInputMediaType(stream, inType, IntPtr.Zero);
                    Release(inType);
                    if (hr < 0)
                    {
                        Release(writer);
                        return FailQuiet(label + " SetInput", hr);
                    }

                    if (audioStream >= 0 && !TrySetAudioInput(writer, audioStream))
                    {
                        Release(writer);
                        try { Native.MFShutdown(); } catch { }
                        return false;
                    }

                    hr = writer.BeginWriting();
                    if (hr < 0)
                    {
                        Release(writer);
                        return Fail(label + " BeginWriting", hr);
                    }

                    _writer = writer;
                    _stream = stream;
                    _audioStream = audioStream;
                    _inputKind = inputKind;
                    _width = width;
                    _height = height;
                    _time = 0;
                    _audioTime = 0;
                    _duration = 10000000L / Fps;
                    _frameBytes = new byte[InputBufferSize(inputKind, width, height)];
                    _open = true;
                    OutputFile = path;
                    BeginAudioCapture();
                    Log("Recording " + width + "x" + height + " " + label);
                    return true;
                }
                catch (Exception ex)
                {
                    Log(label + " exception: " + ex.GetType().Name + " " + ex.Message);
                    if (writer != null)
                        Release(writer);
                    try { Native.MFShutdown(); } catch { }
                    return false;
                }
            }

            private static Guid InputSubtype(int kind)
            {
                if (kind == 1) return Native.MfVideoFormatYuy2;
                if (kind == 2) return Native.MfVideoFormatI420;
                if (kind == 3) return Native.MfVideoFormatRgb32;
                return Native.MfVideoFormatNv12;
            }

            private static int InputBufferSize(int kind, int width, int height)
            {
                if (kind == 1) return width * height * 2;
                if (kind == 3) return width * height * 4;
                return width * height * 3 / 2;
            }

            private static void SetGuidAttr(IMFAttributes t, Guid key, Guid value)
            {
                t.SetGUID(ref key, ref value);
            }

            private static int TryAddAudioStream(IMFSinkWriter writer)
            {
                IMFMediaType outType;
                int hr = Native.MFCreateMediaType(out outType);
                if (hr < 0)
                    return -1;
                SetGuid(outType, Native.MfMtMajorType, Native.MfMediaTypeAudio);
                SetGuid(outType, Native.MfMtSubtype, Native.MfAudioFormatAac);
                SetU32(outType, Native.MfMtAudioNumChannels, 1);
                SetU32(outType, Native.MfMtAudioSamplesPerSecond, 44100);
                SetU32(outType, Native.MfMtAudioBitsPerSample, 16);
                SetU32(outType, Native.MfMtAudioAvgBytesPerSecond, 16000);
                SetU32(outType, Native.MfMtAacPayloadType, 0);
                int stream;
                hr = writer.AddStream(outType, out stream);
                Release(outType);
                if (hr < 0)
                {
                    Plugin.LogError("[Video] AddStream AAC 0x" + hr.ToString("X8"));
                    return -1;
                }
                return stream;
            }

            private static bool TrySetAudioInput(IMFSinkWriter writer, int stream)
            {
                IMFMediaType inType;
                int hr = Native.MFCreateMediaType(out inType);
                if (hr < 0)
                    return false;
                SetGuid(inType, Native.MfMtMajorType, Native.MfMediaTypeAudio);
                SetGuid(inType, Native.MfMtSubtype, Native.MfAudioFormatPcm);
                SetU32(inType, Native.MfMtAudioNumChannels, 1);
                SetU32(inType, Native.MfMtAudioSamplesPerSecond, 44100);
                SetU32(inType, Native.MfMtAudioBitsPerSample, 16);
                SetU32(inType, Native.MfMtAudioBlockAlignment, 2);
                SetU32(inType, Native.MfMtAudioAvgBytesPerSecond, 88200);
                hr = writer.SetInputMediaType(stream, inType, IntPtr.Zero);
                Release(inType);
                if (hr < 0)
                {
                    Plugin.LogError("[Video] SetInput PCM 0x" + hr.ToString("X8"));
                    return false;
                }
                return true;
            }

            private static void Log(string message)
            {
                Plugin.LogInfo("[Video] " + message);
            }

            public void WriteFrame(Texture2D tex)
            {
                if (!_open || tex == null)
                    return;
                CollectAudioFrame();
                if (_avi != null)
                {
                    _avi.Write(tex);
                    return;
                }
                if (_writer == null || _frameBytes == null)
                    return;
                try
                {
                    Color32[] pixels = tex.GetPixels32();
                    CopyFrame(pixels, tex.width, tex.height, _width, _height, _frameBytes, _inputKind);

                    IMFMediaBuffer buffer;
                    int hr = Native.MFCreateMemoryBuffer(_frameBytes.Length, out buffer);
                    if (hr < 0)
                        return;
                    IntPtr data;
                    int maxLen, curLen;
                    buffer.Lock(out data, out maxLen, out curLen);
                    Marshal.Copy(_frameBytes, 0, data, _frameBytes.Length);
                    buffer.Unlock();
                    buffer.SetCurrentLength(_frameBytes.Length);

                    IMFSample sample;
                    hr = Native.MFCreateSample(out sample);
                    if (hr < 0)
                    {
                        Release(buffer);
                        return;
                    }
                    sample.AddBuffer(buffer);
                    sample.SetSampleTime(_time);
                    sample.SetSampleDuration(_duration);
                    _writer.WriteSample(_stream, sample);
                    _time += _duration;
                    Release(sample);
                    Release(buffer);
                }
                catch (Exception ex)
                {
                    if (_logged)
                        return;
                    _logged = true;
                    Plugin.LogError("Video frame encode failed: " + ex.Message);
                }
            }

            public void Dispose()
            {
                if (!string.IsNullOrEmpty(OutputFile))
                {
                    if (_voicePeak > 0f)
                        Plugin.LogInfo("[Video] Mic peak " + _voicePeak.ToString("0.00") + " while recording.");
                    else
                        Plugin.LogInfo("[Video] No microphone samples in this recording.");
                }
                FlushSidecarWav();
                StopAudioCapture();
                if (_avi != null)
                {
                    try { _avi.Dispose(); } catch { }
                    _avi = null;
                }
                if (_writer != null)
                {
                    try { _writer.FinalizeWriter(); } catch { }
                    Release(_writer);
                    _writer = null;
                    try { Native.MFShutdown(); } catch { }
                }
                _audioStream = -1;
                _open = false;
            }

            private void BeginAudioCapture()
            {
                _pcm.Clear();
                _audioTime = 0;
                VoiceIo.StartCapture(MaxSeconds + 4, _audioRate);
                try
                {
                    Type listenerType = Type.GetType("UnityEngine.AudioListener, UnityEngine.AudioModule");
                    if (listenerType == null)
                        return;
                    UnityEngine.Object[] found = UnityEngine.Object.FindObjectsByType(listenerType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                    if (found == null || found.Length == 0)
                        return;
                    var host = found[0] as Component;
                    if (host == null)
                        return;
                    _tap = host.gameObject.GetComponent<GameAudioTap>();
                    if (_tap == null)
                        _tap = host.gameObject.AddComponent<GameAudioTap>();
                }
                catch (Exception ex)
                {
                    Log("Game audio tap failed: " + ex.Message);
                }
            }

            private void StopAudioCapture()
            {
                VoiceIo.StopCapture();
                if (_tap != null)
                {
                    UnityEngine.Object.Destroy(_tap);
                    _tap = null;
                }
            }

            private void CollectAudioFrame()
            {
                int needed = _audioRate / Fps;
                if (needed < 1)
                    return;
                var mix = new float[needed];
                try { VoiceIo.PullCapture(mix); } catch { }
                float peak = 0f;
                for (int i = 0; i < mix.Length; i++)
                {
                    float s = mix[i] * 4f;
                    if (s > 1f) s = 1f;
                    else if (s < -1f) s = -1f;
                    mix[i] = s;
                    float a = s < 0f ? -s : s;
                    if (a > peak)
                        peak = a;
                }
                if (peak > _voicePeak)
                    _voicePeak = peak;
                if (_tap != null)
                    _tap.MixInto(mix, _audioRate);
                if (_writer != null && _audioStream >= 0)
                    WritePcmSample(mix);
                else if (_avi != null)
                    _avi.WritePcm(mix);
                else
                    _pcm.AddRange(mix);
            }

            private void WritePcmSample(float[] mix)
            {
                if (mix == null || mix.Length == 0 || _writer == null)
                    return;
                var pcm = new byte[mix.Length * 2];
                for (int i = 0; i < mix.Length; i++)
                {
                    float s = Mathf.Clamp(mix[i], -1f, 1f);
                    short v = (short)Mathf.RoundToInt(s * short.MaxValue);
                    pcm[i * 2] = (byte)(v & 0xff);
                    pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
                }
                IMFMediaBuffer buffer;
                int hr = Native.MFCreateMemoryBuffer(pcm.Length, out buffer);
                if (hr < 0)
                    return;
                IntPtr data;
                int maxLen, curLen;
                buffer.Lock(out data, out maxLen, out curLen);
                Marshal.Copy(pcm, 0, data, pcm.Length);
                buffer.Unlock();
                buffer.SetCurrentLength(pcm.Length);
                IMFSample sample;
                hr = Native.MFCreateSample(out sample);
                if (hr < 0)
                {
                    Release(buffer);
                    return;
                }
                sample.AddBuffer(buffer);
                long duration = mix.Length * 10000000L / _audioRate;
                sample.SetSampleTime(_audioTime);
                sample.SetSampleDuration(duration);
                _writer.WriteSample(_audioStream, sample);
                _audioTime += duration;
                Release(sample);
                Release(buffer);
            }

            private void FlushSidecarWav()
            {
                if (_pcm.Count < 64 || string.IsNullOrEmpty(OutputFile))
                    return;
                if (_audioStream >= 0 || _avi != null)
                    return;
                VoiceIo.WriteMonoWav(Path.ChangeExtension(OutputFile, ".wav"), _pcm, _audioRate);
                _pcm.Clear();
            }

            private static void SetGuid(IMFMediaType t, Guid key, Guid value)
            {
                t.SetGUID(ref key, ref value);
            }

            private static void SetU32(IMFMediaType t, Guid key, int value)
            {
                t.SetUINT32(ref key, value);
            }

            private static void SetU64(IMFMediaType t, Guid key, ulong value)
            {
                t.SetUINT64(ref key, value);
            }

            private static void SetU32(IMFAttributes t, Guid key, int value)
            {
                t.SetUINT32(ref key, value);
            }

            private static int Bitrate(int w, int h)
            {
                int bits = w * h * Fps / 6;
                if (bits < 2500000)
                    bits = 2500000;
                if (bits > 8000000)
                    bits = 8000000;
                return bits;
            }

            private static bool FailQuiet(string what, int hr)
            {
                Plugin.LogInfo("[Video] " + what + " 0x" + hr.ToString("X8"));
                try { Native.MFShutdown(); } catch { }
                return false;
            }

            private static bool Fail(string what, int hr)
            {
                Plugin.LogError("[Video] " + what + " 0x" + hr.ToString("X8"));
                try { Native.MFShutdown(); } catch { }
                return false;
            }
        }

        private sealed class AviWriter : IDisposable
        {
            private FileStream _fs;
            private readonly List<int> _offsets = new List<int>();
            private readonly List<int> _sizes = new List<int>();
            private readonly List<string> _tags = new List<string>();
            private int _width;
            private int _height;
            private int _moviStart;
            private bool _open;
            private int _audioRate = 44100;

            public bool Start(string path, int width, int height)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? PhoneStore.PhotoDir);
                    if (File.Exists(path))
                        File.Delete(path);
                    _width = width;
                    _height = height;
                    _fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    WriteHeaderPlaceholder();
                    _open = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError("AVI start failed: " + ex.Message);
                    Dispose();
                    return false;
                }
            }

            public void Write(Texture2D tex)
            {
                if (!_open || _fs == null || tex == null)
                    return;
                byte[] jpg = PhoneImages.EncodeJpg(tex, 95);
                if (jpg == null || jpg.Length == 0)
                    return;
                if ((jpg.Length & 1) == 1)
                {
                    var padded = new byte[jpg.Length + 1];
                    Buffer.BlockCopy(jpg, 0, padded, 0, jpg.Length);
                    jpg = padded;
                }
                _offsets.Add((int)_fs.Position);
                _tags.Add("00dc");
                WriteAscii("00dc");
                WriteInt(jpg.Length);
                _fs.Write(jpg, 0, jpg.Length);
                _sizes.Add(jpg.Length);
            }

            public void WritePcm(float[] mix)
            {
                if (!_open || _fs == null || mix == null || mix.Length == 0)
                    return;
                var pcm = new byte[mix.Length * 2];
                for (int i = 0; i < mix.Length; i++)
                {
                    float s = Mathf.Clamp(mix[i], -1f, 1f);
                    short v = (short)Mathf.RoundToInt(s * short.MaxValue);
                    pcm[i * 2] = (byte)(v & 0xff);
                    pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
                }
                if ((pcm.Length & 1) == 1)
                {
                    var padded = new byte[pcm.Length + 1];
                    Buffer.BlockCopy(pcm, 0, padded, 0, pcm.Length);
                    pcm = padded;
                }
                _offsets.Add((int)_fs.Position);
                _tags.Add("01wb");
                WriteAscii("01wb");
                WriteInt(pcm.Length);
                _fs.Write(pcm, 0, pcm.Length);
                _sizes.Add(pcm.Length);
            }

            public void Dispose()
            {
                if (_fs == null)
                    return;
                try
                {
                    if (_open)
                        Finish();
                }
                catch (Exception ex)
                {
                    Plugin.LogError("AVI finish failed: " + ex.Message);
                }
                try { _fs.Dispose(); } catch { }
                _fs = null;
                _open = false;
            }

            private void WriteHeaderPlaceholder()
            {
                WriteAscii("RIFF");
                WriteInt(0);
                WriteAscii("AVI ");
                WriteAscii("LIST");
                WriteInt(4 + 64 + 124 + 100);
                WriteAscii("hdrl");
                WriteAscii("avih");
                WriteInt(56);
                WriteInt(1000000 / Fps);
                WriteInt(_width * _height * 3 + _audioRate * 2);
                WriteInt(0);
                WriteInt(0x10);
                WriteInt(0);
                WriteInt(0);
                WriteInt(2);
                WriteInt(_width * _height * 3);
                WriteInt(_width);
                WriteInt(_height);
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
                WriteAscii("LIST");
                WriteInt(4 + 64 + 48);
                WriteAscii("strl");
                WriteAscii("strh");
                WriteInt(56);
                WriteAscii("vids");
                WriteAscii("MJPG");
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
                WriteInt(1);
                WriteInt(Fps);
                WriteInt(0);
                WriteInt(0);
                WriteInt(_width * _height);
                WriteInt(-1);
                WriteInt(0);
                WriteShort(0);
                WriteShort(0);
                WriteShort((short)_width);
                WriteShort((short)_height);
                WriteAscii("strf");
                WriteInt(40);
                WriteInt(40);
                WriteInt(_width);
                WriteInt(_height);
                WriteShort(1);
                WriteShort(24);
                WriteAscii("MJPG");
                WriteInt(_width * _height * 3);
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
                WriteAscii("LIST");
                WriteInt(4 + 64 + 24);
                WriteAscii("strl");
                WriteAscii("strh");
                WriteInt(56);
                WriteAscii("auds");
                WriteInt(1);
                WriteInt(0);
                WriteInt(0);
                WriteInt(0);
                WriteInt(1);
                WriteInt(_audioRate);
                WriteInt(0);
                WriteInt(0);
                WriteInt(_audioRate * 2);
                WriteInt(-1);
                WriteInt(2);
                WriteShort(0);
                WriteShort(0);
                WriteShort(0);
                WriteShort(0);
                WriteAscii("strf");
                WriteInt(16);
                WriteShort(1);
                WriteShort(1);
                WriteInt(_audioRate);
                WriteInt(_audioRate * 2);
                WriteShort(2);
                WriteShort(16);
                WriteAscii("LIST");
                _moviStart = (int)_fs.Position;
                WriteInt(0);
                WriteAscii("movi");
            }

            private void Finish()
            {
                int moviEnd = (int)_fs.Position;
                _fs.Position = _moviStart;
                WriteInt(moviEnd - _moviStart);
                _fs.Position = moviEnd;
                WriteAscii("idx1");
                WriteInt(_offsets.Count * 16);
                for (int i = 0; i < _offsets.Count; i++)
                {
                    WriteAscii(_tags[i]);
                    WriteInt(0x10);
                    WriteInt(_offsets[i] - (_moviStart + 8));
                    WriteInt(_sizes[i]);
                }
                int size = (int)_fs.Length;
                _fs.Position = 4;
                WriteInt(size - 8);
                _fs.Position = 48;
                WriteInt(_offsets.Count);
                _fs.Position = 140;
                WriteInt(_offsets.Count);
                _fs.Flush();
            }

            private void WriteAscii(string s)
            {
                for (int i = 0; i < s.Length; i++)
                    _fs.WriteByte((byte)s[i]);
            }

            private void WriteInt(int value)
            {
                _fs.WriteByte((byte)(value & 0xff));
                _fs.WriteByte((byte)((value >> 8) & 0xff));
                _fs.WriteByte((byte)((value >> 16) & 0xff));
                _fs.WriteByte((byte)((value >> 24) & 0xff));
            }

            private void WriteShort(int value)
            {
                _fs.WriteByte((byte)(value & 0xff));
                _fs.WriteByte((byte)((value >> 8) & 0xff));
            }
        }

        public static Component Play(GameObject host, RawImage target, string path)
        {
            if (host == null || target == null || PlayerType == null || string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                var existing = host.GetComponent(PlayerType);
                if (existing != null)
                    UnityEngine.Object.Destroy(existing);

                Component player = host.AddComponent(PlayerType);
                Set(player, "playOnAwake", false);
                Set(player, "isLooping", true);
                Set(player, "audioOutputMode", 2);
                Set(player, "renderMode", 1);
                Set(player, "source", 1);
                Set(player, "aspectRatio", 3);
                string url = path;
                if (path.IndexOf("://", StringComparison.Ordinal) < 0)
                    url = new Uri(path).AbsoluteUri;
                Set(player, "url", url);

                int rw = 1280;
                int rh = 720;
                Texture still = target.texture;
                if (still != null && still.height > still.width)
                {
                    rw = 720;
                    rh = 1280;
                }
                var rt = new RenderTexture(rw, rh, 0, RenderTextureFormat.ARGB32);
                rt.Create();
                Set(player, "targetTexture", rt);
                target.texture = rt;
                PhoneUi.FitContained(target, rw / (float)rh);
                PlaySidecarWav(path);
                PlayerType.GetMethod("Prepare", Type.EmptyTypes).Invoke(player, null);
                PlayerType.GetMethod("Play", Type.EmptyTypes).Invoke(player, null);
                return player;
            }
            catch (Exception ex)
            {
                Plugin.LogError("Video play failed: " + ex.Message);
                return null;
            }
        }

        public static bool IsAvi(string path)
        {
            return LooksLikeAvi(path) || (!string.IsNullOrEmpty(path) && path.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) && !LooksLikeMp4(path));
        }

        public static bool IsVideoPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            string e = Path.GetExtension(path);
            if (string.Equals(e, ".avi", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, ".mp4", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, ".mov", StringComparison.OrdinalIgnoreCase)
                || string.Equals(e, ".webm", StringComparison.OrdinalIgnoreCase))
                return true;
            return LooksLikeAvi(path) || LooksLikeMp4(path);
        }

        public static bool LooksLikeAvi(string path)
        {
            byte[] h = ReadHead(path, 12);
            return h != null && h.Length >= 12
                && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
                && h[8] == (byte)'A' && h[9] == (byte)'V' && h[10] == (byte)'I';
        }

        public static bool LooksLikeMp4(string path)
        {
            byte[] h = ReadHead(path, 8);
            return h != null && h.Length >= 8
                && h[4] == (byte)'f' && h[5] == (byte)'t' && h[6] == (byte)'y' && h[7] == (byte)'p';
        }

        private static byte[] ReadHead(string path, int count)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || count < 1)
                return null;
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var buf = new byte[count];
                    int n = fs.Read(buf, 0, count);
                    if (n < count)
                        return null;
                    return buf;
                }
            }
            catch
            {
                return null;
            }
        }

        public static IEnumerator PlayFile(GameObject host, RawImage target, string path)
        {
            if (LooksLikeAvi(path))
            {
                IEnumerator avi = PlayAvi(path, target);
                bool started = false;
                while (avi.MoveNext())
                {
                    started = true;
                    yield return avi.Current;
                }
                if (started)
                    yield break;
            }
            if (Play(host, target, path) == null)
                PhoneMenu.Toast("Couldn't play this video.");
        }

        public static IEnumerator PlayAvi(string path, RawImage target)
        {
            if (target == null || string.IsNullOrEmpty(path) || !File.Exists(path))
                yield break;
            List<long> offsets;
            List<int> sizes;
            if (!ReadAviIndex(path, out offsets, out sizes) || offsets.Count == 0)
                yield break;
            float wait = 1f / Fps;
            Texture2D last = null;
            bool audioStarted = false;
            float start = Time.unscaledTime;
            int i = 0;
            while (target != null && i < offsets.Count)
            {
                byte[] jpg = ReadAviFrame(path, offsets[i], sizes[i]);
                Texture2D frame = PhoneImages.LoadTexture(jpg);
                if (frame != null)
                {
                    target.texture = frame;
                    PhoneUi.FitContained(target, frame);
                    if (last != null)
                        UnityEngine.Object.Destroy(last);
                    last = frame;
                }
                if (!audioStarted)
                {
                    audioStarted = true;
                    start = Time.unscaledTime;
                    PlayMuxedAviAudio(path);
                }
                i++;
                float targetTime = start + i * wait;
                while (target != null && Time.unscaledTime < targetTime)
                    yield return null;
            }
        }

        private static void PlayMuxedAviAudio(string path)
        {
            byte[] pcm = ReadAviPcm(path);
            if (pcm != null && pcm.Length >= 64)
            {
                object clip = VoiceIo.BoostPlayback(VoiceIo.FromPcm16(pcm, 44100, 1));
                if (clip != null)
                {
                    VoiceIo.Play(clip);
                    return;
                }
            }
            PlaySidecarWav(path);
        }

        private static byte[] ReadAviPcm(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var ms = new MemoryStream())
                {
                    long movi = FindFourCc(fs, "movi");
                    if (movi < 0)
                        return null;
                    long pos = movi + 4;
                    while (pos + 8 < fs.Length)
                    {
                        fs.Position = pos;
                        string tag = ReadFourCc(fs);
                        int size = ReadInt(fs);
                        if (size < 0 || pos + 8 + size > fs.Length)
                            break;
                        if (tag == "01wb" && size > 0)
                        {
                            var buf = new byte[size];
                            int read = fs.Read(buf, 0, size);
                            if (read > 0)
                                ms.Write(buf, 0, read);
                        }
                        if (tag == "idx1")
                            break;
                        pos += 8 + size + (size & 1);
                    }
                    return ms.Length < 64 ? null : ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static void PlaySidecarWav(string videoPath)
        {
            if (string.IsNullOrEmpty(videoPath))
                return;
            string wav = Path.ChangeExtension(videoPath, ".wav");
            if (!File.Exists(wav))
                return;
            if (VoiceIo.Run(PhoneSounds.LoadAndPlay(wav)) == null && PhoneMenu.InstanceHost != null)
                PhoneMenu.InstanceHost.StartHostCoroutine(PhoneSounds.LoadAndPlay(wav));
        }

        private sealed class GameAudioTap : MonoBehaviour
        {
            private readonly List<float> _samples = new List<float>(8192);
            private int _rate = 44100;

            private void Awake()
            {
                try
                {
                    Type settings = Type.GetType("UnityEngine.AudioSettings, UnityEngine.AudioModule");
                    if (settings != null)
                    {
                        PropertyInfo prop = settings.GetProperty("outputSampleRate");
                        if (prop != null)
                            _rate = (int)prop.GetValue(null, null);
                    }
                }
                catch
                {
                }
                if (_rate < 8000)
                    _rate = 44100;
            }

            private void OnAudioFilterRead(float[] data, int channels)
            {
                if (data == null || data.Length == 0)
                    return;
                lock (_samples)
                {
                    if (channels <= 1)
                    {
                        for (int i = 0; i < data.Length; i++)
                            _samples.Add(data[i]);
                    }
                    else
                    {
                        for (int i = 0; i < data.Length; i += channels)
                        {
                            float s = 0f;
                            for (int c = 0; c < channels; c++)
                                s += data[i + c];
                            _samples.Add(s / channels);
                        }
                    }
                    int cap = _rate * 4;
                    if (_samples.Count > cap)
                        _samples.RemoveRange(0, _samples.Count - cap);
                }
            }

            public void MixInto(float[] dest, int destRate)
            {
                if (dest == null || dest.Length == 0)
                    return;
                int srcRate = _rate > 8000 ? _rate : 44100;
                int needSrc = dest.Length * srcRate / destRate;
                if (needSrc < 1)
                    needSrc = dest.Length;
                float[] copy;
                lock (_samples)
                {
                    int n = needSrc < _samples.Count ? needSrc : _samples.Count;
                    copy = new float[n];
                    for (int i = 0; i < n; i++)
                        copy[i] = _samples[i];
                    if (n > 0)
                        _samples.RemoveRange(0, n);
                }
                if (copy.Length == 0)
                    return;
                for (int i = 0; i < dest.Length; i++)
                {
                    float srcPos = i * (srcRate / (float)destRate);
                    int i0 = (int)srcPos;
                    if (i0 >= copy.Length)
                        break;
                    int i1 = i0 + 1 < copy.Length ? i0 + 1 : i0;
                    float t = srcPos - i0;
                    dest[i] = Mathf.Clamp(dest[i] + copy[i0] * (1f - t) + copy[i1] * t, -1f, 1f);
                }
            }
        }

        private static bool ReadAviIndex(string path, out List<long> offsets, out List<int> sizes)
        {
            offsets = new List<long>();
            sizes = new List<int>();
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long movi = FindFourCc(fs, "movi");
                    if (movi < 0)
                        return false;
                    long pos = movi + 4;
                    while (pos + 8 < fs.Length)
                    {
                        fs.Position = pos;
                        string tag = ReadFourCc(fs);
                        int size = ReadInt(fs);
                        if (size < 0 || pos + 8 + size > fs.Length)
                            break;
                        if (tag == "00dc" && size > 16)
                        {
                            offsets.Add(pos + 8);
                            sizes.Add(size);
                        }
                        if (tag == "idx1")
                            break;
                        pos += 8 + size + (size & 1);
                    }
                }
                return offsets.Count > 0;
            }
            catch (Exception ex)
            {
                Plugin.LogError("[Video] AVI index failed: " + ex.Message);
                return false;
            }
        }

        private static byte[] ReadAviFrame(string path, long offset, int size)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (offset < 0 || size < 1 || offset + size > fs.Length)
                        return null;
                    fs.Position = offset;
                    var buf = new byte[size];
                    int read = fs.Read(buf, 0, size);
                    if (read < 2 || buf[0] != 0xFF || buf[1] != 0xD8)
                    {
                        int start = 0;
                        for (int i = 0; i < read - 1; i++)
                        {
                            if (buf[i] == 0xFF && buf[i + 1] == 0xD8)
                            {
                                start = i;
                                break;
                            }
                        }
                        if (start > 0)
                        {
                            var cut = new byte[read - start];
                            Buffer.BlockCopy(buf, start, cut, 0, cut.Length);
                            return cut;
                        }
                    }
                    return buf;
                }
            }
            catch
            {
                return null;
            }
        }

        private static long FindFourCc(FileStream fs, string tag)
        {
            var needle = new[] { (byte)tag[0], (byte)tag[1], (byte)tag[2], (byte)tag[3] };
            fs.Position = 0;
            int match = 0;
            int b;
            while ((b = fs.ReadByte()) >= 0)
            {
                if ((byte)b == needle[match])
                {
                    match++;
                    if (match == 4)
                        return fs.Position - 4;
                }
                else
                    match = (byte)b == needle[0] ? 1 : 0;
            }
            return -1;
        }

        private static string ReadFourCc(FileStream fs)
        {
            int a = fs.ReadByte();
            int b = fs.ReadByte();
            int c = fs.ReadByte();
            int d = fs.ReadByte();
            if (a < 0 || d < 0)
                return string.Empty;
            return new string(new[] { (char)a, (char)b, (char)c, (char)d });
        }

        private static int ReadInt(FileStream fs)
        {
            int a = fs.ReadByte();
            int b = fs.ReadByte();
            int c = fs.ReadByte();
            int d = fs.ReadByte();
            return a | (b << 8) | (c << 16) | (d << 24);
        }

        public static void StopAll()
        {
            VoiceIo.StopPlay();
        }

        public static void Stop(GameObject host)
        {
            VoiceIo.StopPlay();
            if (host == null || PlayerType == null)
                return;
            try
            {
                var player = host.GetComponent(PlayerType);
                if (player == null)
                    return;
                object rt = Get(player, "targetTexture");
                PlayerType.GetMethod("Stop", Type.EmptyTypes).Invoke(player, null);
                UnityEngine.Object.Destroy(player);
                var tex = rt as RenderTexture;
                if (tex != null)
                {
                    tex.Release();
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch
            {
            }
        }

        public static Texture2D GrabRgb(RenderTexture source)
        {
            if (source == null)
                return null;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = source;
            var tex = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static void CopyFrame(Color32[] src, int srcW, int srcH, int dstW, int dstH, byte[] dst, int kind)
        {
            if (kind == 1)
                CopyYuy2(src, srcW, srcH, dstW, dstH, dst);
            else if (kind == 2)
                CopyI420(src, srcW, srcH, dstW, dstH, dst);
            else if (kind == 3)
                CopyBgraBottomUp(src, srcW, srcH, dstW, dstH, dst);
            else
                CopyNv12(src, srcW, srcH, dstW, dstH, dst);
        }

        private static void CopyYuy2(Color32[] src, int srcW, int srcH, int dstW, int dstH, byte[] dst)
        {
            int w = dstW < srcW ? dstW : srcW;
            int h = dstH < srcH ? dstH : srcH;
            w &= ~1;
            for (int y = 0; y < h; y++)
            {
                int srcRow = (srcH - 1 - y) * srcW;
                int dstRow = y * dstW * 2;
                for (int x = 0; x < w; x += 2)
                {
                    Color32 a = src[srcRow + x];
                    Color32 b = src[srcRow + x + 1];
                    int i = dstRow + x * 2;
                    dst[i] = Luma(a);
                    dst[i + 1] = ChromaU(a, b);
                    dst[i + 2] = Luma(b);
                    dst[i + 3] = ChromaV(a, b);
                }
            }
        }

        private static void CopyI420(Color32[] src, int srcW, int srcH, int dstW, int dstH, byte[] dst)
        {
            int w = dstW < srcW ? dstW : srcW;
            int h = dstH < srcH ? dstH : srcH;
            w &= ~1;
            h &= ~1;
            int ySize = dstW * dstH;
            int uSize = ySize / 4;
            for (int y = 0; y < h; y++)
            {
                int srcRow = (srcH - 1 - y) * srcW;
                int dstRow = y * dstW;
                for (int x = 0; x < w; x++)
                    dst[dstRow + x] = Luma(src[srcRow + x]);
            }
            for (int y = 0; y < h; y += 2)
            {
                int srcRow = (srcH - 1 - y) * srcW;
                int uvRow = (y / 2) * (dstW / 2);
                for (int x = 0; x < w; x += 2)
                {
                    Color32 a = src[srcRow + x];
                    Color32 b = src[srcRow + x + 1];
                    dst[ySize + uvRow + x / 2] = ChromaU(a, b);
                    dst[ySize + uSize + uvRow + x / 2] = ChromaV(a, b);
                }
            }
        }

        private static byte Luma(Color32 c)
        {
            float y = 0.257f * c.r + 0.504f * c.g + 0.098f * c.b + 16f;
            if (y < 16f) y = 16f;
            if (y > 235f) y = 235f;
            return (byte)y;
        }

        private static byte ChromaU(Color32 a, Color32 b)
        {
            int r = (a.r + b.r) >> 1;
            int g = (a.g + b.g) >> 1;
            int bl = (a.b + b.b) >> 1;
            float u = -0.148f * r - 0.291f * g + 0.439f * bl + 128f;
            if (u < 16f) u = 16f;
            if (u > 240f) u = 240f;
            return (byte)u;
        }

        private static byte ChromaV(Color32 a, Color32 b)
        {
            int r = (a.r + b.r) >> 1;
            int g = (a.g + b.g) >> 1;
            int bl = (a.b + b.b) >> 1;
            float v = 0.439f * r - 0.368f * g - 0.071f * bl + 128f;
            if (v < 16f) v = 16f;
            if (v > 240f) v = 240f;
            return (byte)v;
        }

        private static void CopyNv12(Color32[] src, int srcW, int srcH, int dstW, int dstH, byte[] dst)
        {
            int w = dstW < srcW ? dstW : srcW;
            int h = dstH < srcH ? dstH : srcH;
            w &= ~1;
            h &= ~1;
            int ySize = dstW * dstH;
            for (int y = 0; y < h; y++)
            {
                int srcRow = (srcH - 1 - y) * srcW;
                int dstRow = y * dstW;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = src[srcRow + x];
                    float luma = 0.257f * c.r + 0.504f * c.g + 0.098f * c.b + 16f;
                    if (luma < 16f) luma = 16f;
                    if (luma > 235f) luma = 235f;
                    dst[dstRow + x] = (byte)luma;
                }
            }
            for (int y = 0; y < h; y += 2)
            {
                int srcRow = (srcH - 1 - y) * srcW;
                int dstRow = ySize + (y / 2) * dstW;
                for (int x = 0; x < w; x += 2)
                {
                    Color32 a = src[srcRow + x];
                    Color32 b = src[srcRow + x + 1];
                    int r = (a.r + b.r) >> 1;
                    int g = (a.g + b.g) >> 1;
                    int bl = (a.b + b.b) >> 1;
                    float u = -0.148f * r - 0.291f * g + 0.439f * bl + 128f;
                    float v = 0.439f * r - 0.368f * g - 0.071f * bl + 128f;
                    if (u < 16f) u = 16f;
                    if (u > 240f) u = 240f;
                    if (v < 16f) v = 16f;
                    if (v > 240f) v = 240f;
                    dst[dstRow + x] = (byte)u;
                    dst[dstRow + x + 1] = (byte)v;
                }
            }
        }

        private static void CopyBgraBottomUp(Color32[] src, int srcW, int srcH, int dstW, int dstH, byte[] dst)
        {
            int w = dstW < srcW ? dstW : srcW;
            int h = dstH < srcH ? dstH : srcH;
            for (int y = 0; y < h; y++)
            {
                int srcRow = (srcH - 1 - y) * srcW;
                int dstRow = y * dstW * 4;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = src[srcRow + x];
                    int i = dstRow + x * 4;
                    dst[i] = c.b;
                    dst[i + 1] = c.g;
                    dst[i + 2] = c.r;
                    dst[i + 3] = 255;
                }
            }
        }

        private static ulong Pack(int hi, int lo)
        {
            return ((ulong)(uint)hi << 32) | (uint)lo;
        }

        private static void Set(object obj, string name, object value)
        {
            PropertyInfo p = obj.GetType().GetProperty(name);
            if (p != null && p.CanWrite)
                p.SetValue(obj, value, null);
        }

        private static object Get(object obj, string name)
        {
            PropertyInfo p = obj.GetType().GetProperty(name);
            return p != null ? p.GetValue(obj, null) : null;
        }

        private static void Release(object com)
        {
            if (com == null)
                return;
            try { Marshal.ReleaseComObject(com); } catch { }
        }

        private static class Native
        {
            public const int MfVersion = 0x00020070;
            public static readonly Guid MfMediaTypeVideo = new Guid("73646976-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfMediaTypeAudio = new Guid("73647561-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfVideoFormatH264 = new Guid("34363248-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfVideoFormatRgb32 = new Guid("00000016-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfVideoFormatNv12 = new Guid("3231564E-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfVideoFormatYuy2 = new Guid("32595559-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfVideoFormatI420 = new Guid("30323449-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfAudioFormatAac = new Guid("00001610-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfAudioFormatPcm = new Guid("00000001-0000-0010-8000-00AA00389B71");
            public static readonly Guid MfMtMajorType = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
            public static readonly Guid MfMtSubtype = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
            public static readonly Guid MfMtAvgBitrate = new Guid("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
            public static readonly Guid MfMtInterlaceMode = new Guid("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
            public static readonly Guid MfMtFrameSize = new Guid("1652c33d-d6b2-4012-b834-72030849a37d");
            public static readonly Guid MfMtFrameRate = new Guid("c459a2e8-3d80-40a4-b1c9-2d91daaa376c");
            public static readonly Guid MfMtPixelAspectRatio = new Guid("c6376a1e-8d0a-4027-9830-3df888598889");
            public static readonly Guid MfMtDefaultStride = new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
            public static readonly Guid MfMtMpeg2Profile = new Guid("ad76a80b-2d5c-4e0b-b375-64e520137036");
            public static readonly Guid MfMtSampleSize = new Guid("dad3ab78-1990-408b-bce2-eba673dacc10");
            public static readonly Guid MfMtFixedSizeSamples = new Guid("b8ebefaf-b718-4e04-b0a9-116775e3321b");
            public static readonly Guid MfMtAudioNumChannels = new Guid("37e48bf4-d954-423b-8aa4-5547682d1ad4");
            public static readonly Guid MfMtAudioSamplesPerSecond = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
            public static readonly Guid MfMtAudioAvgBytesPerSecond = new Guid("1aab75c8-cfef-451c-ab95-ac034adc5d84");
            public static readonly Guid MfMtAudioBlockAlignment = new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
            public static readonly Guid MfMtAudioBitsPerSample = new Guid("f2deb57b-47e3-4bbc-9d17-1b2d1b17ad38");
            public static readonly Guid MfMtAacPayloadType = new Guid("bf364724-8b8c-4bb7-ba58-6ae7ebc3e16e");
            public static readonly Guid MfReadwriteEnableHardwareTransforms = new Guid("a634a91c-822b-41b9-a494-4de4643612b0");
            public static readonly Guid MfSinkWriterDisableThrottling = new Guid("08b845d8-2b74-4afe-9d53-be16d2d5ae4c");
            public static readonly Guid MfTranscodeContainerType = new Guid("150ff23f-4abc-478b-ac4f-e1916fba1cca");
            public static readonly Guid MfTranscodeContainerMpeg4 = new Guid("dc6cd05d-b9d0-40ef-bd35-fa622c1ab960");

            [DllImport("mfplat.dll")]
            public static extern int MFStartup(int version, int dwFlags);

            [DllImport("mfplat.dll")]
            public static extern int MFShutdown();

            [DllImport("mfplat.dll")]
            public static extern int MFCreateAttributes(out IMFAttributes attrs, int cInitialSize);

            [DllImport("mfplat.dll")]
            public static extern int MFCreateMediaType(out IMFMediaType type);

            [DllImport("mfplat.dll")]
            public static extern int MFCreateMemoryBuffer(int cbMaxLength, out IMFMediaBuffer buffer);

            [DllImport("mfplat.dll")]
            public static extern int MFCreateSample(out IMFSample sample);

            [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
            public static extern int MFCreateSinkWriterFromURL(string pwszOutputURL, IntPtr pByteStream, IMFAttributes pAttributes, out IMFSinkWriter ppSinkWriter);

            [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode)]
            public static extern int MFCreateSourceReaderFromURL(string pwszURL, IntPtr pAttributes, out IMFSourceReader ppSourceReader);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
        private interface IMFAttributes
        {
            void GetItem();
            void GetItemType();
            void CompareItem();
            void Compare();
            void GetUINT32();
            void GetUINT64();
            void GetDouble();
            void GetGUID();
            void GetStringLength();
            void GetString();
            void GetAllocatedString();
            void GetBlobSize();
            void GetBlob();
            void GetAllocatedBlob();
            void GetUnknown();
            void SetItem();
            void DeleteItem();
            void DeleteAllItems();
            void SetUINT32([In] ref Guid guidKey, int unValue);
            void SetUINT64([In] ref Guid guidKey, ulong unValue);
            void SetDouble();
            void SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
            void SetString();
            void SetBlob();
            void SetUnknown();
            void LockStore();
            void UnlockStore();
            void GetCount();
            void GetItemByIndex();
            void CopyAllItems();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555")]
        private interface IMFMediaType
        {
            void GetItem();
            void GetItemType();
            void CompareItem();
            void Compare();
            void GetUINT32();
            void GetUINT64();
            void GetDouble();
            void GetGUID();
            void GetStringLength();
            void GetString();
            void GetAllocatedString();
            void GetBlobSize();
            void GetBlob();
            void GetAllocatedBlob();
            void GetUnknown();
            void SetItem();
            void DeleteItem();
            void DeleteAllItems();
            void SetUINT32([In] ref Guid guidKey, int unValue);
            void SetUINT64([In] ref Guid guidKey, ulong unValue);
            void SetDouble();
            void SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
            void SetString();
            void SetBlob();
            void SetUnknown();
            void LockStore();
            void UnlockStore();
            void GetCount();
            void GetItemByIndex();
            void CopyAllItems();
            void GetMajorType();
            void IsCompressedFormat();
            void IsEqual();
            void GetRepresentation();
            void FreeRepresentation();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4")]
        private interface IMFSample
        {
            void GetItem();
            void GetItemType();
            void CompareItem();
            void Compare();
            void GetUINT32();
            void GetUINT64();
            void GetDouble();
            void GetGUID();
            void GetStringLength();
            void GetString();
            void GetAllocatedString();
            void GetBlobSize();
            void GetBlob();
            void GetAllocatedBlob();
            void GetUnknown();
            void SetItem();
            void DeleteItem();
            void DeleteAllItems();
            void SetUINT32([In] ref Guid guidKey, int unValue);
            void SetUINT64([In] ref Guid guidKey, ulong unValue);
            void SetDouble();
            void SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
            void SetString();
            void SetBlob();
            void SetUnknown();
            void LockStore();
            void UnlockStore();
            void GetCount();
            void GetItemByIndex();
            void CopyAllItems();
            void GetSampleFlags();
            void SetSampleFlags();
            void GetSampleTime();
            void SetSampleTime(long hnsSampleTime);
            void GetSampleDuration();
            void SetSampleDuration(long hnsSampleDuration);
            void GetBufferCount();
            void GetBufferByIndex();
            [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
            void AddBuffer(IMFMediaBuffer pBuffer);
            void RemoveBufferByIndex();
            void RemoveAllBuffers();
            void GetTotalLength();
            void CopyToBuffer();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("045FA593-8799-42b8-BC8D-8968C6453507")]
        private interface IMFMediaBuffer
        {
            void Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
            void Unlock();
            void GetCurrentLength();
            void SetCurrentLength(int cbCurrentLength);
            void GetMaxLength();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("70ae66f2-c809-4e4f-8915-552d7185ceb4")]
        private interface IMFSourceReader
        {
            void GetStreamSelection();
            void SetStreamSelection();
            void GetNativeMediaType();
            void GetCurrentMediaType();
            [PreserveSig] int SetCurrentMediaType(int dwStreamIndex, IntPtr pdwReserved, IMFMediaType pMediaType);
            void SetCurrentPosition();
            [PreserveSig] int ReadSample(int dwStreamIndex, int dwControlFlags, out int pdwActualStreamIndex, out int pdwStreamFlags, out long pllTimestamp, out IMFSample ppSample);
            void Flush();
            void GetServiceForStream();
            void GetPresentationAttribute();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d")]
        private interface IMFSinkWriter
        {
            [PreserveSig] int AddStream(IMFMediaType pTargetMediaType, out int pdwStreamIndex);
            [PreserveSig] int SetInputMediaType(int dwStreamIndex, IMFMediaType pInputMediaType, IntPtr pEncodingParameters);
            [PreserveSig] int BeginWriting();
            [PreserveSig] int WriteSample(int dwStreamIndex, IMFSample pSample);
            void SendStreamTick();
            void PlaceMarker();
            void NotifyEndOfSegment();
            void Flush();
            void FinalizeWriter();
            void GetServiceForStream();
            void GetStatistics();
        }
    }
}
