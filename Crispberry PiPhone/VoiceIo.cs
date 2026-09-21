using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class VoiceIo
    {
        public const int SampleRate = 44100;
        public const int MaxSeconds = 8;

        private static readonly Assembly AudioAsm = Assembly.Load("UnityEngine.AudioModule");
        private static readonly Type ClipType = AudioAsm.GetType("UnityEngine.AudioClip");
        private static readonly Type SourceType = AudioAsm.GetType("UnityEngine.AudioSource");
        private static readonly Type MicType = AudioAsm.GetType("UnityEngine.Microphone");
        private static readonly MethodInfo ClipCreate = ClipType.GetMethod("Create", new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool) });
        private static readonly MethodInfo ClipGetData = ClipType.GetMethod("GetData", new[] { typeof(float[]), typeof(int) });
        private static readonly MethodInfo ClipSetData = ClipType.GetMethod("SetData", new[] { typeof(float[]), typeof(int) });
        private static readonly MethodInfo ClipSamples = ClipType.GetProperty("samples").GetGetMethod();
        private static readonly MethodInfo ClipChannels = ClipType.GetProperty("channels").GetGetMethod();
        private static readonly MethodInfo ClipFreq = ClipType.GetProperty("frequency").GetGetMethod();
        private static readonly MethodInfo MicStart = MicType.GetMethod("Start", new[] { typeof(string), typeof(bool), typeof(int), typeof(int) });
        private static readonly MethodInfo MicEnd = MicType.GetMethod("End", new[] { typeof(string) });
        private static readonly MethodInfo MicPos = MicType.GetMethod("GetPosition", new[] { typeof(string) });
        private static readonly PropertyInfo MicDevices = MicType.GetProperty("devices");
        private static readonly FieldInfo RecorderVoice = typeof(Recorder).GetField("voice", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Component _source;
        private static string _device;
        private static object _recording;
        private static bool _recordingNow;
        private static object _capClip;
        private static string _capDevice;
        private static int _capPos;
        private static int _capChannels = 1;
        private static readonly object TapLock = new object();
        private static bool _captureTap;
        private static readonly FloatTap FloatProc = new FloatTap();
        private static readonly ShortTap ShortProc = new ShortTap();
        private static LocalVoiceAudioFloat _floatVoice;
        private static LocalVoiceAudioShort _shortVoice;
        private static bool _tapFailLogged;
        private static bool _gotPush;
        private static bool _pcmRecording;
        private static readonly List<float> PcmOut = new List<float>();
        private static int _pcmRate = 44100;
        private static int _pcmMax;
        private static int _tapRate = SampleRate;
        private static int _tapChannels = 1;
        private static int _voiceRate;
        private static int _pullRate = SampleRate;
        private static float[] _ring;
        private static int _ringWrite;
        private static int _ringCount;

        public static bool IsRecording
        {
            get { return _recordingNow; }
        }

        private static VoiceTapPump _pump;

        public static Coroutine Run(IEnumerator routine)
        {
            Ensure();
            if (_pump == null || routine == null)
                return null;
            return _pump.StartCoroutine(routine);
        }

        public static void Ensure()
        {
            if (_source != null)
                return;
            var go = new GameObject("PiP_Voice");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _source = go.AddComponent(SourceType);
            _pump = go.AddComponent<VoiceTapPump>();
            TrySet(_source, "playOnAwake", false);
            TrySet(_source, "spatialBlend", 0f);
            TrySet(_source, "ignoreListenerPause", true);
            TrySet(_source, "ignoreListenerVolume", true);
            TrySet(_source, "bypassEffects", true);
            TrySet(_source, "bypassListenerEffects", true);
            TrySet(_source, "mute", false);
            TrySet(_source, "volume", 1f);
            TrySet(_source, "pitch", 1f);
            TrySet(_source, "outputAudioMixerGroup", null);
        }

        internal static int CaptureRate
        {
            get { return _tapRate > 0 ? _tapRate : 16000; }
        }

        internal static int CaptureChannels
        {
            get { return _tapChannels < 1 ? 1 : _tapChannels; }
        }

        public static bool StartRecord()
        {
            return StartRecord(MaxSeconds);
        }

        public static bool StartRecord(int seconds)
        {
            Ensure();
            StopRecord();
            int len = seconds < 2 ? 2 : seconds;
            PcmOut.Clear();
            _pcmRate = 0;
            _pcmMax = 48000 * len;
            _tapRate = 0;
            _tapChannels = 1;
            if (!StartCapture(len, 48000))
                return false;
            _pcmRecording = true;
            _recordingNow = true;
            return true;
        }

        public static bool StartCapture(int seconds, int rate)
        {
            Ensure();
            StopCapture();
            int hz = rate > 0 ? rate : 44100;
            int len = seconds < 2 ? 2 : seconds;
            _pullRate = hz;
            if (AttachGameTap(len))
            {
                lock (TapLock)
                {
                    int cap = Mathf.Max(hz, Mathf.Max(_tapRate, 48000) * len);
                    _ring = new float[cap];
                    _ringWrite = 0;
                    _ringCount = 0;
                    _gotPush = false;
                    _captureTap = true;
                }
                return true;
            }
            if (GameRecorder() != null)
                return false;
            return StartExclusiveCapture(len, hz);
        }

        public static void StopCapture()
        {
            bool exclusive = _capClip != null;
            _captureTap = false;
            lock (TapLock)
            {
                _ring = null;
                _ringWrite = 0;
                _ringCount = 0;
            }
            if (exclusive)
            {
                try { MicEnd.Invoke(null, new object[] { _capDevice }); } catch { }
                _capClip = null;
                _capPos = 0;
                RestoreGameRecorder();
            }
            ReleaseTapIfIdle();
        }

        public static void PullCapture(float[] dest)
        {
            if (dest == null || dest.Length == 0)
                return;
            if (_captureTap)
            {
                PullTap(dest);
                return;
            }
            if (_capClip == null)
                return;
            int pos = 0;
            try { pos = (int)MicPos.Invoke(null, new object[] { _capDevice }); }
            catch { return; }
            int total = (int)ClipSamples.Invoke(_capClip, null);
            if (total < 1)
                return;
            int ch = _capChannels < 1 ? 1 : _capChannels;
            int available = pos >= _capPos ? pos - _capPos : total - _capPos + pos;
            int take = dest.Length < available ? dest.Length : available;
            if (take < 1)
                return;
            var buf = new float[total * ch];
            ClipGetData.Invoke(_capClip, new object[] { buf, 0 });
            for (int i = 0; i < take; i++)
            {
                int idx = _capPos + i;
                if (idx >= total)
                    idx -= total;
                float s = 0f;
                int baseIdx = idx * ch;
                for (int c = 0; c < ch; c++)
                    s += buf[baseIdx + c];
                dest[i] = s / ch;
            }
            _capPos = (_capPos + take) % total;
        }

        public static void WriteMonoWav(string path, List<float> samples, int rate)
        {
            if (string.IsNullOrEmpty(path) || samples == null || samples.Count < 64)
                return;
            try
            {
                var pcm = new byte[samples.Count * 2];
                for (int i = 0; i < samples.Count; i++)
                {
                    float s = Mathf.Clamp(samples[i], -1f, 1f);
                    short v = (short)Mathf.RoundToInt(s * short.MaxValue);
                    pcm[i * 2] = (byte)(v & 0xff);
                    pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
                }
                File.WriteAllBytes(path, WrapWav(pcm, rate > 0 ? rate : 44100, 1));
            }
            catch (Exception ex)
            {
                Plugin.LogError("Wav write failed: " + ex.Message);
            }
        }

        public static object StopRecord()
        {
            if (!_recordingNow)
                return null;
            _recordingNow = false;
            if (_pcmRecording)
            {
                _pcmRecording = false;
                float[] copy = PcmOut.ToArray();
                PcmOut.Clear();
                StopCapture();
                if (copy.Length < 64)
                    return null;
                int srcRate = _tapRate > 8000 ? _tapRate : DspRate();
                Plugin.LogInfo("Voice record " + copy.Length + " samples at " + srcRate + " Hz, " + (_tapChannels < 1 ? 1 : _tapChannels) + " ch. Encoder rate " + _voiceRate + " is not written.");
                return Boost(ClipFromMono(copy, srcRate));
            }
            int pos = 0;
            try { pos = (int)MicPos.Invoke(null, new object[] { _device }); } catch { }
            try { MicEnd.Invoke(null, new object[] { _device }); } catch { }
            object clip = _recording;
            _recording = null;
            RestoreGameRecorder();
            if (clip == null)
                return null;
            if (pos <= 0)
                return Boost(clip);
            return Boost(Trim(clip, pos));
        }

        public static void Play(object clip)
        {
            Play(clip, false);
        }

        public static void Play(object clip, bool loop)
        {
            Ensure();
            if (clip == null || _source == null)
            {
                Plugin.LogError("Voice play skipped: no clip.");
                return;
            }
            TrySet(_source, "ignoreListenerPause", true);
            TrySet(_source, "ignoreListenerVolume", true);
            TrySet(_source, "bypassEffects", true);
            TrySet(_source, "bypassListenerEffects", true);
            TrySet(_source, "mute", false);
            TrySet(_source, "volume", PhoneTheme.RingVolume);
            TrySet(_source, "pitch", 1f);
            TrySet(_source, "spatialBlend", 0f);
            TrySet(_source, "outputAudioMixerGroup", null);
            if (!loop)
                FadeEdges(clip);
            try
            {
                SourceType.GetMethod("Stop", Type.EmptyTypes).Invoke(_source, null);
                TrySet(_source, "loop", loop);
                SourceType.GetProperty("clip").SetValue(_source, clip, null);
                SourceType.GetMethod("Play", Type.EmptyTypes).Invoke(_source, null);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Voice play failed: " + ex.Message);
            }
        }

        public static void StopPlay()
        {
            if (_source == null)
                return;
            try
            {
                SourceType.GetMethod("Stop", Type.EmptyTypes).Invoke(_source, null);
            }
            catch
            {
            }
        }

        public static bool IsPlaying()
        {
            if (_source == null)
                return false;
            try
            {
                var prop = SourceType.GetProperty("isPlaying");
                if (prop == null)
                    return false;
                return (bool)prop.GetValue(_source, null);
            }
            catch
            {
                return false;
            }
        }

        public static bool LocalIsTalking()
        {
            try
            {
                Recorder rec = VoiceClientHandler.m_LocalRecorder;
                if (rec != null && rec.TransmitEnabled)
                    return true;
            }
            catch
            {
            }
            return false;
        }

        public static void PlayFile(string file)
        {
            string path = PhoneStore.AudioPath(file);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Plugin.LogError("Voice file missing: " + (path ?? file));
                return;
            }
            object clip = FromWav(File.ReadAllBytes(path));
            if (clip == null)
            {
                Plugin.LogError("Voice wav could not be read: " + path);
                return;
            }
            MusicPlayer.DuckFor(ClipSeconds(clip) + 0.45f);
            Play(clip);
        }

        public static float ClipSeconds(object clip)
        {
            if (clip == null || ClipSamples == null || ClipFreq == null)
                return 0f;
            int samples = (int)ClipSamples.Invoke(clip, null);
            int rate = (int)ClipFreq.Invoke(clip, null);
            return rate > 0 ? samples / (float)rate : 0f;
        }

        public static object Slice(object clip, float startSec, float endSec)
        {
            if (clip == null || ClipCreate == null)
                return null;
            int samples = (int)ClipSamples.Invoke(clip, null);
            int channels = (int)ClipChannels.Invoke(clip, null);
            int rate = (int)ClipFreq.Invoke(clip, null);
            if (samples <= 0 || channels <= 0 || rate <= 0)
                return clip;
            int start = Mathf.Clamp(Mathf.RoundToInt(startSec * rate), 0, Mathf.Max(0, samples - 1));
            int end = Mathf.Clamp(Mathf.RoundToInt(endSec * rate), start + 1, samples);
            int count = end - start;
            var data = new float[samples * channels];
            ClipGetData.Invoke(clip, new object[] { data, 0 });
            var slice = new float[count * channels];
            System.Array.Copy(data, start * channels, slice, 0, slice.Length);
            object created = ClipCreate.Invoke(null, new object[] { "PiP_Slice", count, channels, rate, false });
            ClipSetData.Invoke(created, new object[] { slice, 0 });
            return created;
        }

        public static byte[] ToWav(object clip)
        {
            if (clip == null)
                return new byte[0];
            int samples = (int)ClipSamples.Invoke(clip, null);
            int channels = (int)ClipChannels.Invoke(clip, null);
            int rate = (int)ClipFreq.Invoke(clip, null);
            var data = new float[samples * channels];
            ClipGetData.Invoke(clip, new object[] { data, 0 });
            Amplify(data, 3f);
            var pcm = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                float s = Mathf.Clamp(data[i], -1f, 1f);
                short v = (short)Mathf.RoundToInt(s * short.MaxValue);
                pcm[i * 2] = (byte)(v & 0xff);
                pcm[i * 2 + 1] = (byte)((v >> 8) & 0xff);
            }
            return WrapWav(pcm, rate, (short)channels);
        }

        public static object FromPcm16(byte[] pcm, int rate, int channels)
        {
            if (pcm == null || pcm.Length < 2)
                return null;
            if (channels < 1)
                channels = 1;
            return FromWav(WrapWav(pcm, rate > 0 ? rate : SampleRate, (short)channels));
        }

        public static object FromWav(byte[] wav)
        {
            int channels;
            int rate;
            int bits;
            int format;
            int dataOffset;
            int dataLen;
            if (!ParseWav(wav, out channels, out rate, out bits, out format, out dataOffset, out dataLen))
                return null;
            float[] samples = DecodePcm(wav, dataOffset, dataLen, channels, bits, format);
            if (samples == null || samples.Length < channels)
                return null;
            if (channels < 1)
                channels = 1;
            int length = samples.Length / channels;
            if (length < 1)
                return null;
            object clip = ClipCreate.Invoke(null, new object[] { "PiP_Voice", length, channels, rate > 0 ? rate : SampleRate, false });
            ClipSetData.Invoke(clip, new object[] { samples, 0 });
            return clip;
        }

        private static bool ParseWav(byte[] wav, out int channels, out int rate, out int bits, out int format, out int dataOffset, out int dataLen)
        {
            channels = 1;
            rate = SampleRate;
            bits = 16;
            format = 1;
            dataOffset = 0;
            dataLen = 0;
            if (wav == null || wav.Length < 16)
                return false;
            if (wav[0] != (byte)'R' || wav[1] != (byte)'I' || wav[2] != (byte)'F' || wav[3] != (byte)'F')
                return false;
            if (wav[8] != (byte)'W' || wav[9] != (byte)'A' || wav[10] != (byte)'V' || wav[11] != (byte)'E')
                return false;
            bool haveFmt = false;
            int pos = 12;
            while (pos + 8 <= wav.Length)
            {
                int size = BitConverter.ToInt32(wav, pos + 4);
                if (size < 0)
                    break;
                char c0 = (char)wav[pos];
                char c1 = (char)wav[pos + 1];
                char c2 = (char)wav[pos + 2];
                char c3 = (char)wav[pos + 3];
                int body = pos + 8;
                if (c0 == 'f' && c1 == 'm' && c2 == 't' && c3 == ' ')
                {
                    if (body + 16 > wav.Length)
                        return false;
                    format = BitConverter.ToInt16(wav, body);
                    channels = BitConverter.ToInt16(wav, body + 2);
                    rate = BitConverter.ToInt32(wav, body + 4);
                    bits = BitConverter.ToInt16(wav, body + 14);
                    if (format == 0xFFFE && body + 24 <= wav.Length)
                        format = BitConverter.ToInt16(wav, body + 24);
                    haveFmt = true;
                }
                else if (c0 == 'd' && c1 == 'a' && c2 == 't' && c3 == 'a')
                {
                    dataOffset = body;
                    dataLen = size;
                    if (dataOffset + dataLen > wav.Length)
                        dataLen = wav.Length - dataOffset;
                    if (haveFmt)
                        return dataLen > 0;
                }
                pos = body + size + (size & 1);
            }
            return haveFmt && dataLen > 0;
        }

        private static float[] DecodePcm(byte[] wav, int offset, int length, int channels, int bits, int format)
        {
            if (wav == null || offset < 0 || length < 1 || offset + length > wav.Length)
                return null;
            if (format == 3 && bits == 32)
            {
                int n = length / 4;
                var samples = new float[n];
                for (int i = 0; i < n; i++)
                    samples[i] = BitConverter.ToSingle(wav, offset + i * 4);
                return samples;
            }
            if (format != 1)
                return null;
            if (bits == 8)
            {
                var samples = new float[length];
                for (int i = 0; i < length; i++)
                    samples[i] = (wav[offset + i] - 128) / 128f;
                return samples;
            }
            if (bits == 16)
            {
                int n = length / 2;
                var samples = new float[n];
                for (int i = 0; i < n; i++)
                {
                    short v = (short)(wav[offset + i * 2] | (wav[offset + i * 2 + 1] << 8));
                    samples[i] = v / (float)short.MaxValue;
                }
                return samples;
            }
            if (bits == 24)
            {
                int n = length / 3;
                var samples = new float[n];
                for (int i = 0; i < n; i++)
                {
                    int b0 = wav[offset + i * 3];
                    int b1 = wav[offset + i * 3 + 1];
                    int b2 = wav[offset + i * 3 + 2];
                    int v = b0 | (b1 << 8) | (b2 << 16);
                    if ((v & 0x800000) != 0)
                        v |= unchecked((int)0xFF000000);
                    samples[i] = v / 8388607f;
                }
                return samples;
            }
            if (bits == 32)
            {
                int n = length / 4;
                var samples = new float[n];
                for (int i = 0; i < n; i++)
                {
                    int v = BitConverter.ToInt32(wav, offset + i * 4);
                    samples[i] = v / (float)int.MaxValue;
                }
                return samples;
            }
            return null;
        }

        public static List<byte[]> Chunk(byte[] data, int size)
        {
            var list = new List<byte[]>();
            if (data == null || data.Length == 0)
            {
                list.Add(new byte[0]);
                return list;
            }
            int offset = 0;
            while (offset < data.Length)
            {
                int n = Math.Min(size, data.Length - offset);
                var part = new byte[n];
                Buffer.BlockCopy(data, offset, part, 0, n);
                list.Add(part);
                offset += n;
            }
            return list;
        }

        private static bool StartExclusiveRecord(int seconds)
        {
            try
            {
                _device = null;
                _recording = MicStart.Invoke(null, new object[] { _device, false, seconds, SampleRate });
                _recordingNow = _recording != null;
                return _recordingNow;
            }
            catch (Exception ex)
            {
                Plugin.LogError("Mic start failed: " + ex.Message);
                _recordingNow = false;
                return false;
            }
        }

        private static bool StartExclusiveCapture(int seconds, int hz)
        {
            try
            {
                _capDevice = null;
                _capClip = MicStart.Invoke(null, new object[] { _capDevice, true, seconds, hz });
                _capPos = 0;
                _capChannels = 1;
                if (_capClip != null)
                    _capChannels = (int)ClipChannels.Invoke(_capClip, null);
                if (_capChannels < 1)
                    _capChannels = 1;
                return _capClip != null;
            }
            catch (Exception ex)
            {
                Plugin.LogError("Mic capture failed: " + ex.Message);
                _capClip = null;
                return false;
            }
        }

        private static Recorder GameRecorder()
        {
            try
            {
                return VoiceClientHandler.m_LocalRecorder;
            }
            catch
            {
                return null;
            }
        }

        private static bool AttachGameTap(int seconds)
        {
            try
            {
                Recorder rec = GameRecorder();
                if (rec == null)
                    return false;
                if (!TryHookVoice(rec))
                {
                    if (!rec.RecordingEnabled)
                        rec.RecordingEnabled = true;
                    if (RecorderVoice != null && RecorderVoice.GetValue(rec) == null)
                        rec.RestartRecording();
                    if (!TryHookVoice(rec))
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (!_tapFailLogged)
                {
                    _tapFailLogged = true;
                    Plugin.LogError("Voice tap failed: " + ex.Message);
                }
                return false;
            }
        }

        internal static bool EnsureGameCapture()
        {
            return AttachGameTap(MaxSeconds);
        }

        private static bool TryHookVoice(Recorder rec)
        {
            if (rec == null || RecorderVoice == null)
                return false;
            object raw = RecorderVoice.GetValue(rec);
            LocalVoiceAudioFloat floats = raw as LocalVoiceAudioFloat;
            if (floats != null)
            {
                ApplyVoiceInfo(floats.Info);
                if (_floatVoice != floats)
                {
                    UnhookVoice();
                    floats.AddPreProcessor(FloatProc);
                    _floatVoice = floats;
                }
                return true;
            }
            LocalVoiceAudioShort shorts = raw as LocalVoiceAudioShort;
            if (shorts != null)
            {
                ApplyVoiceInfo(shorts.Info);
                if (_shortVoice != shorts)
                {
                    UnhookVoice();
                    shorts.AddPreProcessor(ShortProc);
                    _shortVoice = shorts;
                }
                return true;
            }
            return false;
        }

        private static void UnhookVoice()
        {
            try
            {
                if (_floatVoice != null)
                    _floatVoice.RemoveProcessor(FloatProc);
            }
            catch
            {
            }
            try
            {
                if (_shortVoice != null)
                    _shortVoice.RemoveProcessor(ShortProc);
            }
            catch
            {
            }
            _floatVoice = null;
            _shortVoice = null;
        }

        private static void ReadVoiceInfo(Recorder rec)
        {
            if (rec == null || RecorderVoice == null)
                return;
            object raw = RecorderVoice.GetValue(rec);
            LocalVoiceAudioFloat floats = raw as LocalVoiceAudioFloat;
            if (floats != null)
            {
                ApplyVoiceInfo(floats.Info);
                return;
            }
            LocalVoiceAudioShort shorts = raw as LocalVoiceAudioShort;
            if (shorts != null)
                ApplyVoiceInfo(shorts.Info);
        }

        private static void ApplyVoiceInfo(VoiceInfo info)
        {
            // The tap sits before Photon's 48000 -> 24000 encode resample, so the
            // frames are still the mic rate. VoiceInfo.SamplingRate is the encoder.
            _voiceRate = info.SamplingRate;
            _tapChannels = info.Channels > 0 ? info.Channels : 1;
            _tapRate = DspRate();
        }

        private static void ReleaseTapIfIdle()
        {
            if (_captureTap)
                return;
            UnhookVoice();
        }

        internal static void RestoreGameRecorder()
        {
            try
            {
                Recorder rec = GameRecorder();
                if (rec == null)
                    return;
                rec.RestartRecording();
            }
            catch (Exception ex)
            {
                if (!_tapFailLogged)
                {
                    _tapFailLogged = true;
                    Plugin.LogError("Voice restore failed: " + ex.Message);
                }
            }
        }

        private static void PullTap(float[] dest)
        {
            int srcRate = _tapRate > 0 ? _tapRate : SampleRate;
            int dstRate = _pullRate > 0 ? _pullRate : srcRate;
            int need = dest.Length;
            if (srcRate != dstRate)
                need = Mathf.Max(1, dest.Length * srcRate / dstRate);
            float[] src;
            lock (TapLock)
            {
                if (_ring == null || _ringCount < 1)
                    return;
                int take = need < _ringCount ? need : _ringCount;
                src = new float[take];
                int read = _ringWrite - _ringCount;
                if (read < 0)
                    read += _ring.Length;
                for (int i = 0; i < take; i++)
                {
                    src[i] = _ring[read];
                    read++;
                    if (read >= _ring.Length)
                        read = 0;
                }
                _ringCount -= take;
            }
            if (src.Length == dest.Length)
            {
                for (int i = 0; i < dest.Length; i++)
                    dest[i] = src[i];
                return;
            }
            if (src.Length < 1)
                return;
            for (int i = 0; i < dest.Length; i++)
            {
                float t = dest.Length == 1 ? 0f : i * (src.Length - 1) / (float)(dest.Length - 1);
                int a = (int)t;
                if (a >= src.Length - 1)
                {
                    dest[i] = src[src.Length - 1];
                    continue;
                }
                float f = t - a;
                dest[i] = src[a] + (src[a + 1] - src[a]) * f;
            }
        }

        private static int DspRate()
        {
            try
            {
                Type settings = AudioAsm.GetType("UnityEngine.AudioSettings");
                if (settings != null)
                {
                    PropertyInfo prop = settings.GetProperty("outputSampleRate");
                    if (prop != null)
                    {
                        int rate = (int)prop.GetValue(null, null);
                        if (rate > 8000)
                            return rate;
                    }
                }
            }
            catch
            {
            }
            return SampleRate;
        }

        private static object ClipFromMono(float[] samples, int rate)
        {
            if (samples == null || samples.Length < 1 || ClipCreate == null)
                return null;
            int hz = rate > 0 ? rate : SampleRate;
            object clip = ClipCreate.Invoke(null, new object[] { "PiP_Rec", samples.Length, 1, hz, false });
            ClipSetData.Invoke(clip, new object[] { samples, 0 });
            return clip;
        }

        internal static void OnTapFloat(float[] buf)
        {
            if (buf == null || buf.Length == 0)
                return;
            _gotPush = true;
            PushTap(buf, buf.Length, false);
        }

        internal static void OnTapShort(short[] buf)
        {
            if (buf == null || buf.Length == 0)
                return;
            _gotPush = true;
            var samples = new float[buf.Length];
            for (int i = 0; i < buf.Length; i++)
                samples[i] = buf[i] / 32768f;
            PushTap(samples, samples.Length, false);
        }

        private static int _lastBufStamp;

        internal static void PumpLastBuffer()
        {
            if (_pcmRecording || !_captureTap || _gotPush)
                return;
            try
            {
                Character local = Character.localCharacter;
                if (local == null)
                    return;
                CharacterVoiceHandler handler = local.GetComponentInChildren<CharacterVoiceHandler>();
                if (handler == null)
                    return;
                float[] buf = handler.LastAudioBuffer;
                if (buf == null || buf.Length < 8)
                    return;
                int stamp = buf.Length;
                stamp ^= buf[0].GetHashCode();
                stamp ^= buf[buf.Length - 1].GetHashCode();
                if (stamp == _lastBufStamp)
                    return;
                _lastBufStamp = stamp;
                int frames = buf.Length / 2;
                if (frames > 0)
                {
                    var mono = new float[frames];
                    for (int i = 0; i < frames; i++)
                        mono[i] = (buf[i * 2] + buf[i * 2 + 1]) * 0.5f;
                    PushTap(mono, mono.Length, true);
                }
                else
                    PushTap(buf, buf.Length, true);
            }
            catch
            {
            }
        }

        private static void PushTap(float[] buf, int count, bool alreadyMono)
        {
            int ch = alreadyMono ? 1 : (_tapChannels < 1 ? 1 : _tapChannels);
            lock (TapLock)
            {
                if (_captureTap && _ring != null && _ring.Length > 0)
                {
                    if (ch <= 1)
                    {
                        for (int i = 0; i < count; i++)
                            RingWrite(buf[i]);
                    }
                    else
                    {
                        int frames = count / ch;
                        for (int f = 0; f < frames; f++)
                        {
                            float s = 0f;
                            int baseIdx = f * ch;
                            for (int c = 0; c < ch; c++)
                                s += buf[baseIdx + c];
                            RingWrite(s / ch);
                        }
                    }
                }
            }
        }

        private static void RingWrite(float sample)
        {
            _ring[_ringWrite] = sample;
            _ringWrite++;
            if (_ringWrite >= _ring.Length)
                _ringWrite = 0;
            if (_ringCount < _ring.Length)
                _ringCount++;
        }

        private sealed class VoiceTapPump : MonoBehaviour
        {
            private void Update()
            {
                PumpLastBuffer();
                PumpPcmRecord();
            }
        }

        private static void PumpPcmRecord()
        {
            if (!_pcmRecording || PcmOut.Count >= _pcmMax)
                return;
            float[] chunk = DrainRing();
            if (chunk == null || chunk.Length == 0)
                return;
            if (_pcmRate < 8000)
                _pcmRate = _tapRate > 8000 ? _tapRate : DspRate();
            int take = chunk.Length;
            if (PcmOut.Count + take > _pcmMax)
                take = _pcmMax - PcmOut.Count;
            for (int i = 0; i < take; i++)
                PcmOut.Add(chunk[i]);
        }

        private static float[] DrainRing()
        {
            lock (TapLock)
            {
                if (_ring == null || _ringCount < 1)
                    return null;
                int take = _ringCount;
                var src = new float[take];
                int read = _ringWrite - _ringCount;
                if (read < 0)
                    read += _ring.Length;
                for (int i = 0; i < take; i++)
                {
                    src[i] = _ring[read];
                    read++;
                    if (read >= _ring.Length)
                        read = 0;
                }
                _ringCount = 0;
                return src;
            }
        }

        private sealed class FloatTap : IProcessor<float>
        {
            public float[] Process(float[] buf)
            {
                OnTapFloat(buf);
                return buf;
            }

            public void Dispose()
            {
            }
        }

        private sealed class ShortTap : IProcessor<short>
        {
            public short[] Process(short[] buf)
            {
                OnTapShort(buf);
                return buf;
            }

            public void Dispose()
            {
            }
        }

        private static void TrySet(object source, string property, object value)
        {
            if (source == null)
                return;
            PropertyInfo prop = SourceType.GetProperty(property);
            if (prop == null || !prop.CanWrite)
                return;
            try { prop.SetValue(source, value, null); }
            catch { }
        }

        private static void FadeEdges(object clip)
        {
            if (clip == null)
                return;
            try
            {
                int samples = (int)ClipSamples.Invoke(clip, null);
                int channels = (int)ClipChannels.Invoke(clip, null);
                int rate = (int)ClipFreq.Invoke(clip, null);
                if (samples < 32 || channels < 1 || rate < 1)
                    return;
                var data = new float[samples * channels];
                ClipGetData.Invoke(clip, new object[] { data, 0 });
                int fade = Mathf.Clamp(rate * 8 / 1000, 8, samples / 4);
                for (int i = 0; i < fade; i++)
                {
                    float g = i / (float)fade;
                    for (int c = 0; c < channels; c++)
                    {
                        data[i * channels + c] *= g;
                        data[(samples - 1 - i) * channels + c] *= g;
                    }
                }
                ClipSetData.Invoke(clip, new object[] { data, 0 });
            }
            catch
            {
            }
        }

        private static object Boost(object clip)
        {
            if (clip == null)
                return null;
            try
            {
                int samples = (int)ClipSamples.Invoke(clip, null);
                int channels = (int)ClipChannels.Invoke(clip, null);
                if (samples < 1 || channels < 1)
                    return clip;
                var data = new float[samples * channels];
                ClipGetData.Invoke(clip, new object[] { data, 0 });
                Amplify(data, 3f);
                ClipSetData.Invoke(clip, new object[] { data, 0 });
            }
            catch (Exception ex)
            {
                Plugin.LogError("Voice boost failed: " + ex.Message);
            }
            return clip;
        }

        internal static object BoostPlayback(object clip)
        {
            if (clip == null)
                return null;
            try
            {
                int samples = (int)ClipSamples.Invoke(clip, null);
                int channels = (int)ClipChannels.Invoke(clip, null);
                if (samples < 1 || channels < 1)
                    return clip;
                var data = new float[samples * channels];
                ClipGetData.Invoke(clip, new object[] { data, 0 });
                Amplify(data, 8f);
                ClipSetData.Invoke(clip, new object[] { data, 0 });
            }
            catch (Exception ex)
            {
                Plugin.LogError("Video boost failed: " + ex.Message);
            }
            return clip;
        }

        private static void Amplify(float[] data, float maxGain)
        {
            if (data == null || data.Length == 0)
                return;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > peak)
                    peak = a;
            }
            if (peak < 0.00015f || peak >= 0.85f)
                return;
            float gain = 0.85f / peak;
            float cap = maxGain < 1f ? 1f : maxGain;
            if (gain > cap)
                gain = cap;
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);
        }

        private static string FirstMic()
        {
            string[] devices = MicDevices.GetValue(null, null) as string[];
            if (devices != null && devices.Length > 0)
                return devices[0];
            return null;
        }

        private static object Trim(object clip, int samples)
        {
            int channels = (int)ClipChannels.Invoke(clip, null);
            int freq = (int)ClipFreq.Invoke(clip, null);
            int max = (int)ClipSamples.Invoke(clip, null);
            int count = Mathf.Clamp(samples, 1, max);
            var data = new float[count * channels];
            ClipGetData.Invoke(clip, new object[] { data, 0 });
            object trimmed = ClipCreate.Invoke(null, new object[] { "PiP_Rec", count, channels, freq, false });
            ClipSetData.Invoke(trimmed, new object[] { data, 0 });
            return trimmed;
        }

        internal static byte[] WrapWav(byte[] pcm, int rate, short channels)
        {
            int dataLen = pcm.Length;
            var wav = new byte[44 + dataLen];
            WriteAscii(wav, 0, "RIFF");
            WriteInt(wav, 4, 36 + dataLen);
            WriteAscii(wav, 8, "WAVE");
            WriteAscii(wav, 12, "fmt ");
            WriteInt(wav, 16, 16);
            WriteShort(wav, 20, 1);
            WriteShort(wav, 22, channels);
            WriteInt(wav, 24, rate);
            WriteInt(wav, 28, rate * channels * 2);
            WriteShort(wav, 32, (short)(channels * 2));
            WriteShort(wav, 34, 16);
            WriteAscii(wav, 36, "data");
            WriteInt(wav, 40, dataLen);
            Buffer.BlockCopy(pcm, 0, wav, 44, dataLen);
            return wav;
        }

        private static void WriteAscii(byte[] buf, int offset, string s)
        {
            for (int i = 0; i < s.Length; i++)
                buf[offset + i] = (byte)s[i];
        }

        private static void WriteInt(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xff);
            buf[offset + 1] = (byte)((value >> 8) & 0xff);
            buf[offset + 2] = (byte)((value >> 16) & 0xff);
            buf[offset + 3] = (byte)((value >> 24) & 0xff);
        }

        private static void WriteShort(byte[] buf, int offset, short value)
        {
            buf[offset] = (byte)(value & 0xff);
            buf[offset + 1] = (byte)((value >> 8) & 0xff);
        }
    }
}
