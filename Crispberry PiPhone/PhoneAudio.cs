using System;
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

        private static Component _musicSource;
        private static float _musicVol = -1f;
        private static float _gain = 1f;
        private static float _gainTarget = 1f;
        private static float _alertUntil;
        private static bool _callMuted;
        private static PropertyInfo _isPlaying;
        private static Pump _pump;

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
            }
        }

        public static void StopOneShots()
        {
            for (int i = 0; i < ChannelCount; i++)
                StopSource(ShotSources[i]);
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
            Action<float[], int> reader = _streamReader;
            if (!_streamLive || reader == null || data == null || channels <= 0)
                return;
            try
            {
                reader(data, channels);
                float from = _streamGainNow;
                float to = _streamGain;
                int frames = data.Length / channels;
                for (int i = 0; i < frames; i++)
                {
                    float gain = from + (to - from) * (i + 1) / frames;
                    int at = i * channels;
                    for (int c = 0; c < channels; c++)
                        data[at + c] *= gain;
                }
                _streamGainNow = to;
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
            object carrier = VoiceIo.FromPcm16(new byte[StreamCarrierRate * 2], StreamCarrierRate, 1);
            if (carrier == null)
                return null;
            var go = new GameObject("PiP_MediaStream");
            go.transform.SetParent(host.transform, false);
            Component source = go.AddComponent(type);
            VoiceIo.TrySet(source, "playOnAwake", false);
            VoiceIo.TrySet(source, "loop", true);
            VoiceIo.TrySet(source, "spatialBlend", 0f);
            VoiceIo.TrySet(source, "ignoreListenerPause", true);
            VoiceIo.TrySet(source, "ignoreListenerVolume", true);
            VoiceIo.TrySet(source, "bypassEffects", true);
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
            VoiceIo.TrySet(source, "spatialBlend", 0f);
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
            VoiceIo.TrySet(source, "spatialBlend", 0f);
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
