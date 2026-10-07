using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal enum PhoneAudioChannel
    {
        Media,
        Call,
        Ringtone,
        Notification,
        System
    }

    internal static class PhoneAudio
    {
        public const int ChannelCount = 5;

        private static readonly Type ClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
        private static readonly Component[] Sources = new Component[ChannelCount];
        private static readonly Component[] ShotSources = new Component[ChannelCount];
        private static MethodInfo _oneShot;
        private static Component _vibrateSource;
        private static readonly float[] Volumes = new float[ChannelCount] { 1f, 1f, 1f, 1f, 1f };
        private static float _master = 0.7f;
        private static float _music = 0.7f;

        private const int StreamCarrierRate = 44100;
        private static Component _streamSource;
        private static volatile Action<float[], int> _streamReader;
        private static volatile bool _streamLive;
        private static volatile string _streamError;
        private static volatile float _streamGain;
        private static float _streamGainNow;
        private static float[] _streamPlace;

        private static Component _musicSource;
        private static float _musicVol = -1f;
        private static float _gain = 1f;
        private static float _gainTarget = 1f;
        private static float _alertUntil;
        private static bool _callMuted;
        private static PropertyInfo _isPlaying;
        private static Pump _pump;

        private const float SpeakerNear = 3f;
        private const float SpeakerFar = 40f;
        private static volatile bool _spatial;

        private const int MixRate = 44100;
        private const int MixBlock = 3528;
        private const float MixBlockSeconds = 0.08f;
        private static readonly int[] ImaIndex = { -1, -1, -1, -1, 2, 4, 6, 8 };
        private static readonly int[] ImaStep =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
            34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658,
            724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
            3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };
        private static volatile bool _mixOpen;
        private static float _mixClock;
        private static readonly float[] MixOut = new float[MixBlock];
        private static readonly MixVoice[] MixVoices = new MixVoice[ChannelCount + 2];
        private static readonly List<MixShot> MixShots = new List<MixShot>();
        private static readonly HashSet<int> MixDeaf = new HashSet<int>();
        private static readonly List<int> MixAudience = new List<int>();
        private static PropertyInfo _srcClip;
        private static PropertyInfo _srcLoop;
        private static PropertyInfo _srcTimeSamples;
        private static PropertyInfo _srcVolume;
        private static float[] _shotBuf;
        private static int _mixSeq;
        private static int _mixPredictor;
        private static int _mixIndex;
        private static readonly object TapLock = new object();
        private static float[] _tap;
        private static int _tapHead;
        private static int _tapCount;
        private static float[] _tapBlock;
        private static float[] _tapDrain;

        private const int ListenStart = MixRate / 5;
        private const int ListenMost = MixRate * 3 / 5;
        private static GameObject _listenHost;
        private static Component _listenSource;
        private static bool _listening;
        private static string _listenId;
        private static volatile int _listenRate = MixRate;
        private static readonly object ListenLock = new object();
        private static readonly float[] ListenBuf = new float[MixRate];
        private static int _listenHead;
        private static int _listenCount;
        private static bool _listenFilling = true;
        private static double _listenFrac;
        private static float[] _listenPlace;
        private static float[] _listenPcm;
        private static float _listenLast;
        private static int _listenRung = -1;
        private static int _listenSeq;

        private sealed class MixVoice
        {
            public bool Live;
            public int ClipId;
            public double Cursor;
            public float[] Buf;
        }

        private sealed class MixShot
        {
            public int Channel;
            public object Clip;
            public int ClipId;
            public float Scale;
            public float Began;
            public int Samples;
            public int Channels;
            public int Rate;
            public double Cursor = -1.0;
        }

        public static void Ensure()
        {
            VoiceIo.Ensure();
            if (_pump != null)
                return;
            GameObject host = VoiceIo.Host;
            if (host != null)
                _pump = host.AddComponent<Pump>();
        }

        private sealed class Pump : MonoBehaviour
        {
            private void Update()
            {
                Tick();
            }
        }

        private static void Tick()
        {
            if (AlertSounding())
                _alertUntil = Time.unscaledTime + 0.4f;
            CallService.Phase phase = PhoneTheme.PrioritizeCallAudio ? CallService.State : CallService.Phase.Idle;
            bool muted = phase == CallService.Phase.Active;
            if (muted != _callMuted)
            {
                _callMuted = muted;
                if (muted)
                    MusicPlayer.PauseForCall();
                else
                    MusicPlayer.ResumeAfterCall();
            }
            bool dimmed = phase == CallService.Phase.Incoming || (PhoneTones.DuckMusic && Time.unscaledTime < _alertUntil);
            _gainTarget = muted ? 0f : (dimmed ? 0.08f : 1f);
            float gain = Mathf.MoveTowards(_gain, _gainTarget, Time.unscaledDeltaTime / 0.28f);
            if (gain != _gain)
            {
                _gain = gain;
                WriteMediaVolume();
            }
            WriteMusicVolume();
            FollowCast();
            FollowWatch();
            MixTick();
        }

        private static void FollowCast()
        {
            Vector3 point;
            bool spatial = PhoneCast.TrySpeakerPoint(out point);
            GameObject host = VoiceIo.Host;
            if (spatial && host != null)
                host.transform.position = point;
            if (spatial == _spatial)
                return;
            _spatial = spatial;
            for (int i = 0; i < ChannelCount; i++)
            {
                WriteSpatial(Sources[i]);
                WriteSpatial(ShotSources[i]);
            }
            WriteSpatial(_musicSource);
            WriteSpatial(_vibrateSource);
            WriteSpatial(_streamSource);
        }

        private static void WriteSpatial(Component source)
        {
            if (source == null)
                return;
            VoiceIo.TrySet(source, "spatialBlend", _spatial ? 1f : 0f);
            VoiceIo.TrySet(source, "dopplerLevel", 0f);
            VoiceIo.TrySet(source, "minDistance", SpeakerNear);
            VoiceIo.TrySet(source, "maxDistance", SpeakerFar);
        }

        private static void FollowWatch()
        {
            Vector3 point;
            string id;
            int owner;
            bool watching = PhoneCast.TryWatchPoint(out point, out id, out owner);
            Component source = watching ? ListenSourceFor() : _listenSource;
            if (watching && source == null)
                watching = false;
            if (watching)
                _listenHost.transform.position = point;
            if (watching == _listening && (!watching || id == _listenId))
                return;
            _listening = watching;
            _listenId = watching ? id : null;
            ResetListen();
            if (!watching)
            {
                StopSource(source);
                return;
            }
            _listenRate = VoiceIo.DspRate();
            try
            {
                if (!SourcePlaying(source))
                    VoiceIo.AudioSourceType.GetMethod("Play", Type.EmptyTypes).Invoke(source, null);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Cast listen failed: " + ex.Message);
            }
        }

        private static void ResetListen()
        {
            lock (ListenLock)
            {
                _listenHead = 0;
                _listenCount = 0;
                _listenFilling = true;
                _listenFrac = 0.0;
            }
            _listenRung = -1;
            _listenSeq = 0;
            _listenLast = 0f;
        }

        private static Component ListenSourceFor()
        {
            if (_listenSource != null)
                return _listenSource;
            Type type = VoiceIo.AudioSourceType;
            if (type == null)
                return null;
            object carrier = NewCarrier();
            if (carrier == null)
                return null;
            var go = new GameObject("PiP_CastListen");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Component source = go.AddComponent(type);
            VoiceIo.TrySet(source, "playOnAwake", false);
            VoiceIo.TrySet(source, "loop", true);
            VoiceIo.TrySet(source, "spatialBlend", 1f);
            VoiceIo.TrySet(source, "dopplerLevel", 0f);
            VoiceIo.TrySet(source, "minDistance", SpeakerNear);
            VoiceIo.TrySet(source, "maxDistance", SpeakerFar);
            VoiceIo.TrySet(source, "ignoreListenerPause", true);
            VoiceIo.TrySet(source, "ignoreListenerVolume", true);
            VoiceIo.TrySet(source, "bypassEffects", false);
            VoiceIo.TrySet(source, "bypassListenerEffects", true);
            VoiceIo.TrySet(source, "mute", false);
            VoiceIo.TrySet(source, "volume", 1f);
            VoiceIo.TrySet(source, "pitch", 1f);
            VoiceIo.TrySet(source, "outputAudioMixerGroup", null);
            VoiceIo.TrySet(source, "clip", carrier);
            go.AddComponent<ListenTap>();
            _listenHost = go;
            _listenSource = source;
            return source;
        }

        private sealed class ListenTap : MonoBehaviour
        {
            private void OnAudioFilterRead(float[] data, int channels)
            {
                RenderListen(data, channels);
            }
        }

        public static void HearCast(int rung, int seq, int predictor, int index, byte[] data)
        {
            if (!_listening || data == null || rung < 0 || rung > 3 || data.Length < 2)
                return;
            int grow = rung == 0 ? 4 : (rung == 1 ? 2 : 1);
            int count = rung == 3 ? data.Length / 2 : data.Length * 2;
            if (count * grow > MixBlock)
                return;
            if (rung != _listenRung)
                _listenRung = rung;
            else if (seq <= _listenSeq && _listenSeq - seq < 64)
                return;
            _listenSeq = seq;
            float[] pcm = _listenPcm;
            if (pcm == null)
            {
                pcm = new float[MixBlock];
                _listenPcm = pcm;
            }
            predictor = Mathf.Clamp(predictor, -32768, 32767);
            index = Mathf.Clamp(index, 0, 88);
            float last = _listenLast;
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                float s;
                if (rung == 3)
                    s = (short)(data[i * 2] | (data[i * 2 + 1] << 8)) / 32768f;
                else
                {
                    int code = (i & 1) == 0 ? data[i >> 1] & 15 : data[i >> 1] >> 4;
                    int step = ImaStep[index];
                    int delta = step >> 3;
                    if ((code & 4) != 0)
                        delta += step;
                    if ((code & 2) != 0)
                        delta += step >> 1;
                    if ((code & 1) != 0)
                        delta += step >> 2;
                    predictor = Mathf.Clamp((code & 8) != 0 ? predictor - delta : predictor + delta, -32768, 32767);
                    index = Mathf.Clamp(index + ImaIndex[code & 7], 0, 88);
                    s = predictor / 32768f;
                }
                for (int g = 1; g <= grow; g++)
                    pcm[n++] = last + (s - last) * g / grow;
                last = s;
            }
            _listenLast = last;
            lock (ListenLock)
            {
                int size = ListenBuf.Length;
                for (int i = 0; i < n; i++)
                {
                    ListenBuf[(_listenHead + _listenCount) % size] = pcm[i];
                    if (_listenCount < size)
                        _listenCount++;
                    else
                        _listenHead = (_listenHead + 1) % size;
                }
                if (_listenCount > ListenMost)
                {
                    _listenHead = (_listenHead + _listenCount - ListenStart) % size;
                    _listenCount = ListenStart;
                }
            }
        }

        private static void RenderListen(float[] data, int channels)
        {
            if (data == null)
                return;
            if (channels <= 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            try
            {
                float[] place = _listenPlace;
                if (place == null || place.Length < data.Length)
                {
                    place = new float[data.Length];
                    _listenPlace = place;
                }
                Array.Copy(data, place, data.Length);
                Array.Clear(data, 0, data.Length);
                int frames = data.Length / channels;
                double step = MixRate / (double)_listenRate;
                lock (ListenLock)
                {
                    if (_listenFilling && _listenCount >= ListenStart)
                        _listenFilling = false;
                    if (_listenFilling)
                        return;
                    int size = ListenBuf.Length;
                    for (int i = 0; i < frames; i++)
                    {
                        if (_listenCount < 2)
                        {
                            _listenFilling = true;
                            _listenFrac = 0.0;
                            return;
                        }
                        float a = ListenBuf[_listenHead];
                        float b = ListenBuf[(_listenHead + 1) % size];
                        float sample = a + (b - a) * (float)_listenFrac;
                        int at = i * channels;
                        for (int c = 0; c < channels; c++)
                            data[at + c] = sample * place[at + c];
                        _listenFrac += step;
                        while (_listenFrac >= 1.0 && _listenCount > 0)
                        {
                            _listenFrac -= 1.0;
                            _listenHead = (_listenHead + 1) % size;
                            _listenCount--;
                        }
                    }
                }
            }
            catch
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        private static void MixTick()
        {
            bool open = PhoneCast.AudienceOpen;
            if (open != _mixOpen)
            {
                if (open)
                    OpenTap();
                _mixOpen = open;
                if (!open)
                    ForgetMix();
            }
            if (!open)
                return;
            _mixClock += Time.unscaledDeltaTime;
            for (int i = 0; i < 3 && _mixClock >= MixBlockSeconds; i++)
            {
                _mixClock -= MixBlockSeconds;
                MixOneBlock();
            }
            if (_mixClock >= MixBlockSeconds)
                _mixClock = 0f;
        }

        private static void OpenTap()
        {
            int rate = VoiceIo.DspRate();
            lock (TapLock)
            {
                if (_tap == null || _tap.Length != rate)
                    _tap = new float[rate];
                _tapHead = 0;
                _tapCount = 0;
            }
        }

        private static void ForgetMix()
        {
            MixShots.Clear();
            for (int i = 0; i < MixVoices.Length; i++)
            {
                if (MixVoices[i] == null)
                    continue;
                MixVoices[i].Live = false;
                MixVoices[i].ClipId = 0;
            }
            lock (TapLock)
            {
                _tapHead = 0;
                _tapCount = 0;
            }
            MixDeaf.Clear();
            _mixPredictor = 0;
            _mixIndex = 0;
            _mixClock = 0f;
        }

        private static void MixOneBlock()
        {
            Array.Clear(MixOut, 0, MixBlock);
            for (int i = 0; i < ChannelCount; i++)
                MixSource(i, Sources[i]);
            MixSource(ChannelCount, _musicSource);
            MixSource(ChannelCount + 1, _vibrateSource);
            MixShotVoices();
            MixTap();
            SendMix();
        }

        private static bool SourceProps()
        {
            if (_srcVolume != null)
                return true;
            Type type = VoiceIo.AudioSourceType;
            if (type == null)
                return false;
            _srcClip = type.GetProperty("clip");
            _srcLoop = type.GetProperty("loop");
            _srcTimeSamples = type.GetProperty("timeSamples");
            if (_srcClip == null || _srcLoop == null || _srcTimeSamples == null)
                return false;
            _srcVolume = type.GetProperty("volume");
            return _srcVolume != null;
        }

        private static void MixSource(int slot, Component source)
        {
            MixVoice voice = MixVoices[slot];
            if (voice == null)
            {
                voice = new MixVoice();
                MixVoices[slot] = voice;
            }
            bool was = voice.Live;
            voice.Live = false;
            if (!SourcePlaying(source) || !SourceProps())
                return;
            object clip;
            bool loop;
            int at;
            float volume;
            try
            {
                clip = _srcClip.GetValue(source, null);
                loop = (bool)_srcLoop.GetValue(source, null);
                at = (int)_srcTimeSamples.GetValue(source, null);
                volume = (float)_srcVolume.GetValue(source, null);
            }
            catch
            {
                return;
            }
            UnityEngine.Object live = clip as UnityEngine.Object;
            if (live == null)
                return;
            int id = live.GetInstanceID();
            int samples;
            int channels;
            int rate;
            if (MixDeaf.Contains(id) || !VoiceIo.ClipShape(clip, out samples, out channels, out rate))
                return;
            double gap = Math.Abs(voice.Cursor - at);
            if (loop && gap > samples * 0.5)
                gap = samples - gap;
            if (!was || voice.ClipId != id || gap > rate * 0.25)
                voice.Cursor = at;
            voice.ClipId = id;
            if (!MixClip(clip, samples, channels, rate, loop, voice.Cursor, volume, ref voice.Buf))
            {
                MixDeaf.Add(id);
                return;
            }
            voice.Live = true;
            voice.Cursor += MixBlock * (rate / (double)MixRate);
            if (loop)
                voice.Cursor %= samples;
        }

        private static void MixShotVoices()
        {
            if (MixShots.Count == 0 || !SourceProps())
                return;
            float now = Time.realtimeSinceStartup;
            for (int i = MixShots.Count - 1; i >= 0; i--)
            {
                MixShot shot = MixShots[i];
                double at = (now - shot.Began) * (double)shot.Rate;
                if (shot.Cursor < 0.0 || Math.Abs(shot.Cursor - at) > shot.Rate * 0.25)
                    shot.Cursor = at;
                Component source = ShotSources[shot.Channel];
                if (shot.Cursor >= shot.Samples || source == null || MixDeaf.Contains(shot.ClipId))
                {
                    MixShots.RemoveAt(i);
                    continue;
                }
                float volume;
                try
                {
                    volume = (float)_srcVolume.GetValue(source, null) * shot.Scale;
                }
                catch
                {
                    volume = 0f;
                }
                if (!MixClip(shot.Clip, shot.Samples, shot.Channels, shot.Rate, false, shot.Cursor, volume, ref _shotBuf))
                {
                    MixDeaf.Add(shot.ClipId);
                    MixShots.RemoveAt(i);
                    continue;
                }
                shot.Cursor += MixBlock * (shot.Rate / (double)MixRate);
            }
        }

        private static bool MixClip(object clip, int samples, int channels, int rate, bool loop, double cursor, float gain, ref float[] buf)
        {
            double step = rate / (double)MixRate;
            int need = (int)Math.Ceiling(MixBlock * step) + 2;
            if (loop)
                cursor %= samples;
            int first = (int)cursor;
            if (gain <= 0.0001f || first >= samples)
                return true;
            bool whole = samples < need;
            int from = whole ? 0 : first;
            int frames = whole ? samples : need;
            if (buf == null || buf.Length != frames * channels)
                buf = new float[frames * channels];
            if (!VoiceIo.ReadClip(clip, buf, from))
                return false;
            for (int i = 0; i < MixBlock; i++)
            {
                double at = cursor + i * step;
                int p = (int)at;
                float a = MixFrame(buf, channels, samples, loop, from, p);
                float b = MixFrame(buf, channels, samples, loop, from, p + 1);
                MixOut[i] += (a + (b - a) * (float)(at - p)) * gain;
            }
            return true;
        }

        private static float MixFrame(float[] buf, int channels, int samples, bool loop, int from, int p)
        {
            if (p >= samples)
            {
                if (!loop)
                    return 0f;
                p %= samples;
            }
            int index = p - from;
            if (index < 0)
                index += samples;
            int at = index * channels;
            if (at + channels > buf.Length)
                return 0f;
            if (channels == 1)
                return buf[at];
            float sum = 0f;
            for (int c = 0; c < channels; c++)
                sum += buf[at + c];
            return sum / channels;
        }

        private static void MixTap()
        {
            float[] ring = _tap;
            if (ring == null)
                return;
            int size = ring.Length;
            int want = Mathf.RoundToInt(size * MixBlockSeconds);
            float[] drain = _tapDrain;
            if (drain == null || drain.Length != want + 1)
            {
                drain = new float[want + 1];
                _tapDrain = drain;
            }
            int got;
            lock (TapLock)
            {
                got = Math.Min(want, _tapCount);
                for (int i = 0; i < got; i++)
                    drain[i] = ring[(_tapHead + i) % size];
                _tapHead = (_tapHead + got) % size;
                _tapCount -= got;
                int keep = size * 3 / 10;
                if (_tapCount > keep)
                {
                    _tapHead = (_tapHead + _tapCount - keep) % size;
                    _tapCount = keep;
                }
            }
            if (got == 0)
                return;
            Array.Clear(drain, got, drain.Length - got);
            double step = want / (double)MixBlock;
            for (int i = 0; i < MixBlock; i++)
            {
                double at = i * step;
                int p = (int)at;
                MixOut[i] += drain[p] + (drain[p + 1] - drain[p]) * (float)(at - p);
            }
        }

        private static void TapWrite(float[] block, int count)
        {
            lock (TapLock)
            {
                float[] ring = _tap;
                if (ring == null)
                    return;
                int size = ring.Length;
                for (int i = 0; i < count; i++)
                {
                    ring[(_tapHead + _tapCount) % size] = block[i];
                    if (_tapCount < size)
                        _tapCount++;
                    else
                        _tapHead = (_tapHead + 1) % size;
                }
            }
        }

        private static void SendMix()
        {
            float peak = 0f;
            for (int i = 0; i < MixBlock; i++)
            {
                float s = Mathf.Clamp(MixOut[i], -1f, 1f);
                MixOut[i] = s;
                float level = Mathf.Abs(s);
                if (level > peak)
                    peak = level;
            }
            if (peak < 0.0005f)
            {
                _mixPredictor = 0;
                _mixIndex = 0;
                return;
            }
            string device = PhoneCast.CopyAudience(MixAudience);
            if (MixAudience.Count == 0)
                return;
            int predictor = _mixPredictor;
            int index = _mixIndex;
            byte[] data = EncodeIma(2, ref _mixPredictor, ref _mixIndex);
            _mixSeq++;
            PhoneNet.SendCastAudio(MixAudience.ToArray(), device, 1, _mixSeq, predictor, index, data);
        }

        private static byte[] EncodeIma(int group, ref int predictor, ref int index)
        {
            int count = MixBlock / group;
            var data = new byte[count / 2];
            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                int at = i * group;
                for (int g = 0; g < group; g++)
                    sum += MixOut[at + g];
                int sample = Mathf.RoundToInt(sum / group * 32767f);
                int step = ImaStep[index];
                int diff = sample - predictor;
                int code = 0;
                if (diff < 0)
                {
                    code = 8;
                    diff = -diff;
                }
                int delta = step >> 3;
                if (diff >= step)
                {
                    code |= 4;
                    diff -= step;
                    delta += step;
                }
                if (diff >= step >> 1)
                {
                    code |= 2;
                    diff -= step >> 1;
                    delta += step >> 1;
                }
                if (diff >= step >> 2)
                {
                    code |= 1;
                    delta += step >> 2;
                }
                predictor = Mathf.Clamp((code & 8) != 0 ? predictor - delta : predictor + delta, -32768, 32767);
                index = Mathf.Clamp(index + ImaIndex[code & 7], 0, 88);
                if ((i & 1) == 0)
                    data[i >> 1] = (byte)code;
                else
                    data[i >> 1] |= (byte)(code << 4);
            }
            return data;
        }

        private static bool AlertSounding()
        {
            return SourcePlaying(Sources[(int)PhoneAudioChannel.Ringtone])
                || SourcePlaying(ShotSources[(int)PhoneAudioChannel.Ringtone])
                || SourcePlaying(Sources[(int)PhoneAudioChannel.Notification])
                || SourcePlaying(ShotSources[(int)PhoneAudioChannel.Notification])
                || SourcePlaying(_vibrateSource);
        }

        private static float LevelOf(PhoneAudioChannel channel)
        {
            float level = EffectiveVolume(channel);
            return channel == PhoneAudioChannel.Media ? level * _gain : level;
        }

        private static void WriteMediaVolume()
        {
            float level = LevelOf(PhoneAudioChannel.Media);
            Component source = Sources[(int)PhoneAudioChannel.Media];
            if (source != null)
                VoiceIo.TrySet(source, "volume", level);
            Component shots = ShotSources[(int)PhoneAudioChannel.Media];
            if (shots != null)
                VoiceIo.TrySet(shots, "volume", level);
            _streamGain = level;
        }

        public static bool MusicReady
        {
            get { return _musicSource != null; }
        }

        public static bool MusicSounding
        {
            get { return SourcePlaying(_musicSource); }
        }

        public static bool MusicHeld
        {
            get { return _gainTarget <= 0.9f; }
        }

        public static bool PlayMusic(object clip)
        {
            if (clip == null)
                return false;
            if (_musicSource == null)
            {
                _musicSource = NewSource(0f);
                _musicVol = -1f;
            }
            Component source = _musicSource;
            if (source == null)
                return false;
            Type type = VoiceIo.AudioSourceType;
            try
            {
                type.GetMethod("Stop", Type.EmptyTypes).Invoke(source, null);
                type.GetProperty("clip").SetValue(source, clip, null);
                type.GetProperty("time").SetValue(source, 0f, null);
                WriteMusicVolume();
                type.GetMethod("Play", Type.EmptyTypes).Invoke(source, null);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Music play failed: " + ex.Message);
                return false;
            }
            return true;
        }

        public static void PauseMusic()
        {
            if (_musicSource == null)
                return;
            try
            {
                VoiceIo.AudioSourceType.GetMethod("Pause", Type.EmptyTypes).Invoke(_musicSource, null);
            }
            catch
            {
            }
        }

        public static void ResumeMusic()
        {
            if (_musicSource == null)
                return;
            try
            {
                VoiceIo.AudioSourceType.GetMethod("UnPause", Type.EmptyTypes).Invoke(_musicSource, null);
            }
            catch
            {
            }
        }

        private static void WriteMusicVolume()
        {
            if (_musicSource == null)
                return;
            float v = _music * EffectiveVolume(PhoneAudioChannel.Media) * _gain;
            if (Mathf.Abs(v - _musicVol) < 0.002f)
                return;
            _musicVol = v;
            VoiceIo.TrySet(_musicSource, "volume", v);
        }

        public static void Play(PhoneAudioChannel channel, object clip, bool loop)
        {
            Start(channel, clip, loop, true);
        }

        public static void PlayOneShot(PhoneAudioChannel channel, object clip, float scale)
        {
            if (channel == PhoneAudioChannel.Call || clip == null || scale <= 0.001f)
                return;
            Component source = ShotSourceFor(channel);
            if (source == null)
                return;
            if (_oneShot == null)
                _oneShot = VoiceIo.AudioSourceType.GetMethod("PlayOneShot", new[] { ClipType, typeof(float) });
            if (_oneShot == null)
                return;
            try
            {
                _oneShot.Invoke(source, new object[] { clip, Mathf.Clamp01(scale) });
            }
            catch (Exception ex)
            {
                Plugin.LogError("Sfx play failed: " + ex.Message);
                return;
            }
            if (!_mixOpen)
                return;
            UnityEngine.Object live = clip as UnityEngine.Object;
            var shot = new MixShot();
            if (live == null || !VoiceIo.ClipShape(clip, out shot.Samples, out shot.Channels, out shot.Rate))
                return;
            shot.Channel = (int)channel;
            shot.Clip = clip;
            shot.ClipId = live.GetInstanceID();
            shot.Scale = Mathf.Clamp01(scale);
            shot.Began = Time.realtimeSinceStartup;
            MixShots.Add(shot);
        }

        public static void StopOneShots()
        {
            for (int i = 0; i < ChannelCount; i++)
                StopSource(ShotSources[i]);
            MixShots.Clear();
        }

        public static void PlayVibrate(object clip, float volume)
        {
            if (clip == null || volume <= 0.001f)
                return;
            if (_vibrateSource == null)
                _vibrateSource = NewSource(volume);
            Component source = _vibrateSource;
            if (source == null)
                return;
            Type type = VoiceIo.AudioSourceType;
            VoiceIo.TrySet(source, "volume", Mathf.Clamp01(volume));
            try
            {
                type.GetMethod("Stop", Type.EmptyTypes).Invoke(source, null);
                type.GetProperty("clip").SetValue(source, clip, null);
                type.GetMethod("Play", Type.EmptyTypes).Invoke(source, null);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Vibrate play failed: " + ex.Message);
            }
        }

        public static bool PlayMedia(byte[] data, int rate, int channels, bool loop)
        {
            if (data == null)
                return false;
            object clip;
            try
            {
                bool wav = data.Length >= 12 && data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F';
                clip = wav ? VoiceIo.FromWav(data) : VoiceIo.FromPcm16(data, rate, channels);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Media decode failed: " + ex.Message);
                return false;
            }
            return StartMedia(clip, loop, true);
        }

        public static bool PlayMediaClip(object clip, bool loop)
        {
            return StartMedia(clip, loop, false);
        }

        private static bool StartMedia(object clip, bool loop, bool fade)
        {
            UnityEngine.Object live = clip as UnityEngine.Object;
            if (live == null || ClipType == null || !ClipType.IsInstanceOfType(clip))
                return false;
            return Start(PhoneAudioChannel.Media, clip, loop, fade);
        }

        public static bool PlayMediaStream(Action<float[], int> reader)
        {
            if (reader == null)
                return false;
            Component source = StreamSourceFor();
            if (source == null)
                return false;
            _streamLive = false;
            _streamReader = reader;
            _streamError = null;
            _streamGain = LevelOf(PhoneAudioChannel.Media);
            _streamGainNow = _streamGain;
            VoiceIo.TrySet(source, "mute", false);
            VoiceIo.TrySet(source, "volume", 1f);
            try
            {
                if (!SourcePlaying(source))
                    VoiceIo.AudioSourceType.GetMethod("Play", Type.EmptyTypes).Invoke(source, null);
            }
            catch (Exception ex)
            {
                _streamReader = null;
                Plugin.LogError("Media stream failed: " + ex.Message);
                return false;
            }
            _streamLive = true;
            return true;
        }

        private static void StopStream()
        {
            _streamLive = false;
            _streamReader = null;
            if (_streamSource == null)
                return;
            try
            {
                VoiceIo.AudioSourceType.GetMethod("Stop", Type.EmptyTypes).Invoke(_streamSource, null);
            }
            catch
            {
            }
        }

        private static void RenderStream(float[] data, int channels)
        {
            if (data == null)
                return;
            Action<float[], int> reader = _streamReader;
            if (!_streamLive || reader == null || channels <= 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            try
            {
                float[] place = null;
                if (_spatial)
                {
                    place = _streamPlace;
                    if (place == null || place.Length < data.Length)
                    {
                        place = new float[data.Length];
                        _streamPlace = place;
                    }
                    Array.Copy(data, place, data.Length);
                }
                Array.Clear(data, 0, data.Length);
                reader(data, channels);
                float from = _streamGainNow;
                float to = _streamGain;
                int frames = data.Length / channels;
                float[] tap = null;
                if (_mixOpen)
                {
                    tap = _tapBlock;
                    if (tap == null || tap.Length < frames)
                    {
                        tap = new float[frames];
                        _tapBlock = tap;
                    }
                }
                for (int i = 0; i < frames; i++)
                {
                    float gain = from + (to - from) * (i + 1) / frames;
                    int at = i * channels;
                    float sum = 0f;
                    for (int c = 0; c < channels; c++)
                    {
                        sum += data[at + c] * gain;
                        data[at + c] *= place != null ? gain * place[at + c] : gain;
                    }
                    if (tap != null)
                        tap[i] = sum / channels;
                }
                _streamGainNow = to;
                if (tap != null)
                    TapWrite(tap, frames);
            }
            catch (Exception ex)
            {
                _streamLive = false;
                _streamReader = null;
                _streamError = ex.Message;
                Array.Clear(data, 0, data.Length);
            }
        }

        private static void ReportStream()
        {
            string error = _streamError;
            if (error == null)
                return;
            _streamError = null;
            StopStream();
            Plugin.LogError("Media stream reader threw: " + error);
        }

        private static Component StreamSourceFor()
        {
            if (_streamSource != null)
                return _streamSource;
            GameObject host = VoiceIo.Host;
            Type type = VoiceIo.AudioSourceType;
            if (host == null || type == null)
                return null;
            object carrier = NewCarrier();
            if (carrier == null)
                return null;
            var go = new GameObject("PiP_MediaStream");
            go.transform.SetParent(host.transform, false);
            Component source = go.AddComponent(type);
            VoiceIo.TrySet(source, "playOnAwake", false);
            VoiceIo.TrySet(source, "loop", true);
            WriteSpatial(source);
            VoiceIo.TrySet(source, "ignoreListenerPause", true);
            VoiceIo.TrySet(source, "ignoreListenerVolume", true);
            VoiceIo.TrySet(source, "bypassEffects", false);
            VoiceIo.TrySet(source, "bypassListenerEffects", true);
            VoiceIo.TrySet(source, "mute", false);
            VoiceIo.TrySet(source, "volume", 1f);
            VoiceIo.TrySet(source, "pitch", 1f);
            VoiceIo.TrySet(source, "outputAudioMixerGroup", null);
            VoiceIo.TrySet(source, "clip", carrier);
            go.AddComponent<StreamTap>();
            _streamSource = source;
            return source;
        }

        private static object NewCarrier()
        {
            byte[] level = new byte[StreamCarrierRate * 2];
            for (int i = 0; i < level.Length; i += 2)
            {
                level[i] = 0xFF;
                level[i + 1] = 0x7F;
            }
            return VoiceIo.FromPcm16(level, StreamCarrierRate, 1);
        }

        private sealed class StreamTap : MonoBehaviour
        {
            private void OnAudioFilterRead(float[] data, int channels)
            {
                RenderStream(data, channels);
            }

            private void Update()
            {
                ReportStream();
            }
        }

        private static bool Start(PhoneAudioChannel channel, object clip, bool loop, bool fade)
        {
            if (channel == PhoneAudioChannel.Call)
                return false;
            if (clip == null)
            {
                Plugin.LogError("Voice play skipped: no clip.");
                return false;
            }
            Component source = SourceFor(channel);
            if (source == null)
            {
                Plugin.LogError("Voice play skipped: no source.");
                return false;
            }
            Type type = VoiceIo.AudioSourceType;
            VoiceIo.TrySet(source, "ignoreListenerPause", true);
            VoiceIo.TrySet(source, "ignoreListenerVolume", true);
            VoiceIo.TrySet(source, "bypassEffects", true);
            VoiceIo.TrySet(source, "bypassListenerEffects", true);
            VoiceIo.TrySet(source, "mute", false);
            VoiceIo.TrySet(source, "volume", LevelOf(channel));
            VoiceIo.TrySet(source, "pitch", 1f);
            WriteSpatial(source);
            VoiceIo.TrySet(source, "outputAudioMixerGroup", null);
            if (!loop && fade)
                VoiceIo.FadeEdges(clip);
            try
            {
                type.GetMethod("Stop", Type.EmptyTypes).Invoke(source, null);
                VoiceIo.TrySet(source, "loop", loop);
                type.GetProperty("clip").SetValue(source, clip, null);
                type.GetMethod("Play", Type.EmptyTypes).Invoke(source, null);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Voice play failed: " + ex.Message);
                return false;
            }
            return true;
        }

        public static void Stop(PhoneAudioChannel channel)
        {
            if (channel == PhoneAudioChannel.Call)
                return;
            if (channel == PhoneAudioChannel.Media)
                StopStream();
            StopSource(Sources[(int)channel]);
            StopSource(ShotSources[(int)channel]);
            for (int i = MixShots.Count - 1; i >= 0; i--)
            {
                if (MixShots[i].Channel == (int)channel)
                    MixShots.RemoveAt(i);
            }
        }

        private static void StopSource(Component source)
        {
            if (source == null)
                return;
            try
            {
                VoiceIo.AudioSourceType.GetMethod("Stop", Type.EmptyTypes).Invoke(source, null);
            }
            catch
            {
            }
        }

        public static void StopAll()
        {
            for (int i = 0; i < ChannelCount; i++)
                Stop((PhoneAudioChannel)i);
        }

        public static bool IsPlaying(PhoneAudioChannel channel)
        {
            if (channel == PhoneAudioChannel.Media && (MusicPlayer.Playing || _streamLive))
                return true;
            return SourcePlaying(Sources[(int)channel]);
        }

        public static bool IsAnyPlaying()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                if (IsPlaying((PhoneAudioChannel)i))
                    return true;
            }
            return false;
        }

        public static float VolumeOf(PhoneAudioChannel channel)
        {
            return Volumes[(int)channel];
        }

        public static void SetVolume(PhoneAudioChannel channel, float value)
        {
            Volumes[(int)channel] = Mathf.Clamp01(value);
        }

        public static float MasterVolume
        {
            get { return _master; }
            set { _master = Mathf.Clamp01(value); }
        }

        public static float MusicVolume
        {
            get { return _music; }
            set { _music = Mathf.Clamp01(value); }
        }

        public static float EffectiveVolume(PhoneAudioChannel channel)
        {
            return VolumeOf(channel) * _master;
        }

        public static void ApplyVolume()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                Component source = Sources[i];
                if (source != null)
                    VoiceIo.TrySet(source, "volume", LevelOf((PhoneAudioChannel)i));
                Component shots = ShotSources[i];
                if (shots != null)
                    VoiceIo.TrySet(shots, "volume", LevelOf((PhoneAudioChannel)i));
            }
            _streamGain = LevelOf(PhoneAudioChannel.Media);
            WriteMusicVolume();
        }

        private static bool SourcePlaying(Component source)
        {
            if (source == null)
                return false;
            try
            {
                if (_isPlaying == null)
                    _isPlaying = VoiceIo.AudioSourceType.GetProperty("isPlaying");
                if (_isPlaying == null)
                    return false;
                return (bool)_isPlaying.GetValue(source, null);
            }
            catch
            {
                return false;
            }
        }

        private static Component SourceFor(PhoneAudioChannel channel)
        {
            int index = (int)channel;
            if (Sources[index] == null)
                Sources[index] = NewSource(LevelOf(channel));
            return Sources[index];
        }

        private static Component ShotSourceFor(PhoneAudioChannel channel)
        {
            int index = (int)channel;
            if (ShotSources[index] == null)
                ShotSources[index] = NewSource(LevelOf(channel));
            return ShotSources[index];
        }

        private static Component NewSource(float volume)
        {
            GameObject host = VoiceIo.Host;
            Type type = VoiceIo.AudioSourceType;
            if (host == null || type == null)
                return null;
            Component source = host.AddComponent(type);
            VoiceIo.TrySet(source, "playOnAwake", false);
            VoiceIo.TrySet(source, "loop", false);
            WriteSpatial(source);
            VoiceIo.TrySet(source, "ignoreListenerPause", true);
            VoiceIo.TrySet(source, "ignoreListenerVolume", true);
            VoiceIo.TrySet(source, "bypassEffects", true);
            VoiceIo.TrySet(source, "bypassListenerEffects", true);
            VoiceIo.TrySet(source, "mute", false);
            VoiceIo.TrySet(source, "volume", volume);
            VoiceIo.TrySet(source, "pitch", 1f);
            VoiceIo.TrySet(source, "outputAudioMixerGroup", null);
            return source;
        }
    }
}
