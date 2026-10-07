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
        private static readonly float[] Volumes = new float[ChannelCount] { 0.7f, 0.7f, 0.7f, 0.7f, 0.7f };

        private const int StreamCarrierRate = 44100;
        private static Component _streamSource;
        private static volatile Action<float[], int> _streamReader;
        private static volatile bool _streamLive;
        private static volatile string _streamError;
        private static volatile float _streamGain;
        private static float _streamGainNow;

        public static void Ensure()
        {
            VoiceIo.Ensure();
        }

        public static void Play(PhoneAudioChannel channel, object clip, bool loop)
        {
            Play(channel, clip, loop, 0f);
        }

        public static void Play(PhoneAudioChannel channel, object clip, bool loop, float duckSeconds)
        {
            Start(channel, clip, loop, duckSeconds, true);
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
            float duck = loop ? 0f : VoiceIo.ClipSeconds(clip) + 0.45f;
            return Start(PhoneAudioChannel.Media, clip, loop, duck, fade);
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
            _streamGain = VolumeOf(PhoneAudioChannel.Media);
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

        private static bool Start(PhoneAudioChannel channel, object clip, bool loop, float duckSeconds, bool fade)
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
            if (duckSeconds > 0f)
                MusicPlayer.DuckFor(duckSeconds);
            Type type = VoiceIo.AudioSourceType;
            VoiceIo.TrySet(source, "ignoreListenerPause", true);
            VoiceIo.TrySet(source, "ignoreListenerVolume", true);
            VoiceIo.TrySet(source, "bypassEffects", true);
            VoiceIo.TrySet(source, "bypassListenerEffects", true);
            VoiceIo.TrySet(source, "mute", false);
            VoiceIo.TrySet(source, "volume", VolumeOf(channel));
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
            Component source = Sources[(int)channel];
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

        public static void ApplyVolume()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                Component source = Sources[i];
                if (source != null)
                    VoiceIo.TrySet(source, "volume", VolumeOf((PhoneAudioChannel)i));
            }
            _streamGain = VolumeOf(PhoneAudioChannel.Media);
            MusicPlayer.ApplyVolume();
        }

        private static bool SourcePlaying(Component source)
        {
            if (source == null)
                return false;
            try
            {
                PropertyInfo prop = VoiceIo.AudioSourceType.GetProperty("isPlaying");
                if (prop == null)
                    return false;
                return (bool)prop.GetValue(source, null);
            }
            catch
            {
                return false;
            }
        }

        private static Component SourceFor(PhoneAudioChannel channel)
        {
            int index = (int)channel;
            if (Sources[index] != null)
                return Sources[index];
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
            VoiceIo.TrySet(source, "volume", VolumeOf(channel));
            VoiceIo.TrySet(source, "pitch", 1f);
            VoiceIo.TrySet(source, "outputAudioMixerGroup", null);
            Sources[index] = source;
            return source;
        }
    }
}
