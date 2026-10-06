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
        private const int ChannelCount = 5;

        private static readonly Component[] Sources = new Component[ChannelCount];

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
            if (channel == PhoneAudioChannel.Call)
                return;
            if (clip == null)
            {
                Plugin.LogError("Voice play skipped: no clip.");
                return;
            }
            Component source = SourceFor(channel);
            if (source == null)
            {
                Plugin.LogError("Voice play skipped: no source.");
                return;
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
            if (!loop)
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
            }
        }

        public static void Stop(PhoneAudioChannel channel)
        {
            if (channel == PhoneAudioChannel.Call)
                return;
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
            if (channel == PhoneAudioChannel.Media && MusicPlayer.Playing)
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
            return PhoneTheme.RingVolume;
        }

        public static void ApplyVolume()
        {
            for (int i = 0; i < ChannelCount; i++)
            {
                Component source = Sources[i];
                if (source != null)
                    VoiceIo.TrySet(source, "volume", VolumeOf((PhoneAudioChannel)i));
            }
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
