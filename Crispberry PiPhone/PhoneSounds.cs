using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Photon.Pun;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class PhoneSounds
    {
        private static Type _uwrType;
        private static Type _uwrAudio;
        private static Type _dhAudio;
        private static Type _audioTypeEnum;
        private static bool _audioTypesTried;
        private static string _previewPath;

        public static void PlayTone(int hz, float seconds)
        {
            MusicPlayer.DuckFor(Mathf.Max(0.8f, seconds + 0.35f));
            VoiceIo.Play(MakeBeep(hz, seconds));
        }

        public static void PlayRingtone()
        {
            PlayRingtoneFor(null);
        }

        public static void PlayRingtoneFor(string contactId)
        {
            PlayId(PhoneTones.ResolveRing(contactId), 880, 0.45f, 20f, true);
        }

        public static void StopRing()
        {
            StopPreview();
        }

        public static bool IsPreviewing(string path)
        {
            return !string.IsNullOrEmpty(_previewPath)
                && string.Equals(_previewPath, path, StringComparison.OrdinalIgnoreCase);
        }

        public static void TogglePreview(string path)
        {
            if (IsPreviewing(path))
            {
                StopPreview();
                return;
            }
            StopPreview();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            _previewPath = path;
            if (PhoneMenu.InstanceHost != null)
                VoiceIo.Run(LoadAndPlay(path, false));
            else
                PlayFileImmediate(path, false);
        }

        public static void StopPreview()
        {
            _previewPath = null;
            VoiceIo.StopPlay();
        }

        public static void PlayText()
        {
            PlayTextFor(null);
        }

        public static void PlayTextFor(string contactId)
        {
            PlayId(PhoneTones.ResolveText(contactId), 1200, 0.12f, 1.6f);
        }

        public static void PlayVibrate(bool call)
        {
            MusicPlayer.DuckFor(call ? 2.2f : 0.55f);
            VoiceIo.Play(MakeBuzz(call ? 0.7f : 0.32f), false);
        }

        public static void PlayNotify()
        {
            PlayApp(null);
        }

        public static void PlayApp(string appId)
        {
            PlayId(PhoneTones.ResolveApp(appId), 990, 0.16f, 1.6f);
        }

        public static void PlayId(string id, int fallbackHz, float fallbackSec)
        {
            PlayId(id, fallbackHz, fallbackSec, 1.6f, false);
        }

        public static void PlayId(string id, int fallbackHz, float fallbackSec, float duckSec)
        {
            PlayId(id, fallbackHz, fallbackSec, duckSec, false);
        }

        public static void PlayId(string id, int fallbackHz, float fallbackSec, float duckSec, bool loop)
        {
            MusicPlayer.DuckFor(duckSec);
            SoundItem item = PhoneStore.FindSound(id);
            if (item != null)
            {
                string path = PhoneStore.SoundPath(item.File);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    if (VoiceIo.Run(LoadAndPlay(path, loop)) == null)
                        PlayFileImmediate(path, loop);
                    return;
                }
            }
            VoiceIo.Play(MakeBeep(fallbackHz, fallbackSec), loop);
        }

        public static IEnumerator LoadClip(string path, Action<object> done)
        {
            object clip = null;
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                try { clip = VoiceIo.FromWav(File.ReadAllBytes(path)); } catch { }
            }
            if (clip != null)
            {
                if (done != null)
                    done(clip);
                yield break;
            }

            byte[] wav = null;
            try { wav = PhoneVideo.DecodeAudioToWav(path); } catch { }
            if (wav != null)
            {
                try { clip = VoiceIo.FromWav(wav); } catch { }
                if (clip != null)
                {
                    if (done != null)
                        done(clip);
                    yield break;
                }
            }

            yield return TryWwwClip(path, false, false);
            if (_wwwPlayed)
            {
                if (done != null)
                    done(_wwwClip);
                yield break;
            }

            if (EnsureWebAudio())
            {
                int[] types = GuessAudioTypes(path);
                for (int t = 0; t < types.Length && clip == null; t++)
                {
                    object req = null;
                    try
                    {
                        string url = FileUrl(path);
                        MethodInfo get = FindGetAudioClip();
                        if (get == null)
                            break;
                        ParameterInfo[] args = get.GetParameters();
                        object[] call;
                        if (args.Length == 2)
                            call = new object[] { url, Enum.ToObject(_audioTypeEnum, types[t]) };
                        else if (args.Length == 3)
                            call = new object[] { url, Enum.ToObject(_audioTypeEnum, types[t]), false };
                        else
                            call = new object[] { url, false, false, Enum.ToObject(_audioTypeEnum, types[t]) };
                        req = get.Invoke(null, call);
                        MethodInfo send = _uwrType.GetMethod("SendWebRequest", Type.EmptyTypes);
                        if (send != null)
                            send.Invoke(req, null);
                    }
                    catch
                    {
                        yield break;
                    }

                    PropertyInfo isDone = _uwrType.GetProperty("isDone");
                    while (req != null && !(bool)isDone.GetValue(req, null))
                        yield return null;

                    try
                    {
                        if (string.IsNullOrEmpty(ReadUwrError(req)))
                            clip = ReadAudioClip(req);
                    }
                    catch
                    {
                    }
                }
            }

            if (done != null)
                done(clip);
        }

        public static IEnumerator LoadAndPlay(string path)
        {
            yield return LoadAndPlay(path, false);
        }

        public static IEnumerator LoadAndPlay(string path, bool loop)
        {
            object clip = null;
            if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                try { clip = VoiceIo.FromWav(File.ReadAllBytes(path)); } catch { }
            }
            if (clip != null)
            {
                VoiceIo.Play(clip, loop);
                yield break;
            }

            byte[] wav = null;
            try { wav = PhoneVideo.DecodeAudioToWav(path); } catch (Exception ex) { Plugin.LogError("Sound MF decode: " + ex.Message); }
            if (wav != null)
            {
                try { clip = VoiceIo.FromWav(wav); } catch { }
                if (clip != null)
                {
                    VoiceIo.Play(clip, loop);
                    yield break;
                }
            }

            yield return TryWwwClip(path, true, loop);
            if (_wwwPlayed)
                yield break;
            if (EnsureWebAudio())
            {
                int[] types = GuessAudioTypes(path);
                bool played = false;
                for (int t = 0; t < types.Length && !played; t++)
                {
                    object req = null;
                    try
                    {
                        string url = FileUrl(path);
                        MethodInfo get = FindGetAudioClip();
                        if (get == null)
                            break;
                        ParameterInfo[] args = get.GetParameters();
                        object[] call;
                        if (args.Length == 2)
                            call = new object[] { url, Enum.ToObject(_audioTypeEnum, types[t]) };
                        else if (args.Length == 3)
                            call = new object[] { url, Enum.ToObject(_audioTypeEnum, types[t]), false };
                        else
                            call = new object[] { url, false, false, Enum.ToObject(_audioTypeEnum, types[t]) };
                        req = get.Invoke(null, call);
                        MethodInfo send = _uwrType.GetMethod("SendWebRequest", Type.EmptyTypes);
                        if (send != null)
                            send.Invoke(req, null);
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError("Sound load start failed: " + ex.Message);
                        yield break;
                    }

                    PropertyInfo isDone = _uwrType.GetProperty("isDone");
                    while (req != null && !(bool)isDone.GetValue(req, null))
                        yield return null;

                    try
                    {
                        string err = ReadUwrError(req);
                        if (!string.IsNullOrEmpty(err))
                        {
                            Plugin.LogError("Sound request failed (" + types[t] + "): " + err);
                            continue;
                        }
                        object loaded = ReadAudioClip(req);
                        if (loaded != null)
                        {
                            VoiceIo.Play(loaded, loop);
                            played = true;
                        }
                        else
                            Plugin.LogError("Sound clip was empty for " + path + " type " + types[t]);
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError("Sound play failed: " + ex.Message);
                    }
                }
                if (played)
                    yield break;
            }

            Plugin.LogError("Sound load failed for " + path);
            PhoneMenu.Toast("Couldn't play that file. Try a 16-bit WAV.");
        }

        private static bool _wwwPlayed;
        private static object _wwwClip;

        private static IEnumerator TryWwwClip(string path, bool play, bool loop)
        {
            _wwwPlayed = false;
            _wwwClip = null;
            Type wwwType = Type.GetType("UnityEngine.WWW, UnityEngine")
                ?? Type.GetType("UnityEngine.WWW, UnityEngine.UnityWebRequestWWWModule");
            if (wwwType == null)
                yield break;
            object www = null;
            try
            {
                www = Activator.CreateInstance(wwwType, FileUrl(path));
            }
            catch
            {
                yield break;
            }
            PropertyInfo isDone = wwwType.GetProperty("isDone");
            while (www != null && isDone != null && !(bool)isDone.GetValue(www, null))
                yield return null;
            try
            {
                MethodInfo get = wwwType.GetMethod("GetAudioClip", new[] { typeof(bool), typeof(bool) });
                object clip = get != null ? get.Invoke(www, new object[] { false, false }) : null;
                if (clip == null)
                {
                    MethodInfo get3 = null;
                    MethodInfo[] methods = wwwType.GetMethods();
                    for (int i = 0; i < methods.Length; i++)
                    {
                        if (methods[i].Name == "GetAudioClip" && methods[i].GetParameters().Length >= 1)
                        {
                            get3 = methods[i];
                            break;
                        }
                    }
                    if (get3 != null)
                    {
                        ParameterInfo[] args = get3.GetParameters();
                        if (args.Length == 3)
                            clip = get3.Invoke(www, new object[] { false, false, Enum.ToObject(args[2].ParameterType, 14) });
                    }
                }
                if (clip != null)
                {
                    if (play)
                        VoiceIo.Play(clip, loop);
                    _wwwClip = clip;
                    _wwwPlayed = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("WWW audio failed: " + ex.Message);
            }
        }

        private static bool EnsureWebAudio()
        {
            if (_uwrType != null && _uwrAudio != null && _audioTypeEnum != null)
                return true;
            if (_audioTypesTried)
                return false;
            _audioTypesTried = true;
            try
            {
                Assembly uwr = Assembly.Load("UnityEngine.UnityWebRequestModule");
                _uwrType = uwr != null ? uwr.GetType("UnityEngine.Networking.UnityWebRequest") : null;
                Assembly audioReq = Assembly.Load("UnityEngine.UnityWebRequestAudioModule");
                _uwrAudio = audioReq != null ? audioReq.GetType("UnityEngine.Networking.UnityWebRequestMultimedia") : null;
                _dhAudio = audioReq != null ? audioReq.GetType("UnityEngine.Networking.DownloadHandlerAudioClip") : null;
                Assembly audio = Assembly.Load("UnityEngine.AudioModule");
                _audioTypeEnum = audio != null ? audio.GetType("UnityEngine.AudioType") : null;
            }
            catch (Exception ex)
            {
                Plugin.LogError("Sound types failed: " + ex.Message);
            }
            return _uwrType != null && _uwrAudio != null && _audioTypeEnum != null;
        }

        private static MethodInfo FindGetAudioClip()
        {
            MethodInfo[] methods = _uwrAudio.GetMethods(BindingFlags.Public | BindingFlags.Static);
            MethodInfo two = null;
            MethodInfo three = null;
            MethodInfo four = null;
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != "GetAudioClip")
                    continue;
                int n = methods[i].GetParameters().Length;
                if (n == 2) two = methods[i];
                else if (n == 3) three = methods[i];
                else if (n == 4) four = methods[i];
            }
            return two != null ? two : (three != null ? three : four);
        }

        private static string FileUrl(string path)
        {
            try
            {
                return new Uri(path).AbsoluteUri;
            }
            catch
            {
                return "file:///" + path.Replace('\\', '/');
            }
        }

        private static string ReadUwrError(object req)
        {
            if (req == null)
                return "no request";
            PropertyInfo error = _uwrType.GetProperty("error");
            object err = error != null ? error.GetValue(req, null) : null;
            string text = err as string;
            if (!string.IsNullOrEmpty(text))
                return text;
            PropertyInfo result = _uwrType.GetProperty("result");
            if (result != null)
            {
                object value = result.GetValue(req, null);
                if (value != null && value.ToString() != "Success" && value.ToString() != "InProgress")
                    return value.ToString();
            }
            return null;
        }

        private static object ReadAudioClip(object req)
        {
            if (_dhAudio != null)
            {
                MethodInfo getContent = _dhAudio.GetMethod("GetContent", BindingFlags.Public | BindingFlags.Static, null, new[] { _uwrType }, null);
                if (getContent != null)
                    return getContent.Invoke(null, new object[] { req });
            }
            PropertyInfo handlerProp = _uwrType.GetProperty("downloadHandler");
            object handler = handlerProp != null ? handlerProp.GetValue(req, null) : null;
            if (handler == null)
                return null;
            PropertyInfo clipProp = handler.GetType().GetProperty("audioClip");
            return clipProp != null ? clipProp.GetValue(handler, null) : null;
        }

        private static void PlayFileImmediate(string path)
        {
            PlayFileImmediate(path, false);
        }

        private static void PlayFileImmediate(string path, bool loop)
        {
            if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                object clip = VoiceIo.FromWav(File.ReadAllBytes(path));
                if (clip != null)
                    VoiceIo.Play(clip, loop);
            }
        }

        public static IEnumerator PickAudioFile(Action<string> done)
        {
            string picked = null;
            bool finished = false;
            int prevTimeout = 10000;
            try
            {
                if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
                {
                    prevTimeout = PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout;
                    PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = 180000;
                }
                PhotonNetwork.KeepAliveInBackground = 180f;
            }
            catch
            {
            }

            var thread = new Thread(() =>
            {
                try
                {
                    picked = ShowDialog();
                }
                catch (Exception ex)
                {
                    Plugin.LogError("File picker failed: " + ex.Message);
                }
                finished = true;
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            while (!finished)
            {
                try
                {
                    if (PhotonNetwork.IsConnected)
                        PhotonNetwork.SendAllOutgoingCommands();
                }
                catch
                {
                }
                yield return null;
            }

            try
            {
                if (PhotonNetwork.NetworkingClient != null && PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
                    PhotonNetwork.NetworkingClient.LoadBalancingPeer.DisconnectTimeout = prevTimeout;
            }
            catch
            {
            }

            if (done != null)
                done(picked);
        }

        private static string ShowDialog()
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.filter = "Audio\0*.mp3;*.wav;*.ogg;*.aiff;*.aif;*.flac;*.m4a;*.wma\0All\0*.*\0\0";
            ofn.file = new string(new char[520]);
            ofn.maxFile = 520;
            ofn.title = "Add sound";
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
            ofn.owner = IntPtr.Zero;
            if (!GetOpenFileName(ref ofn))
                return null;
            return string.IsNullOrEmpty(ofn.file) ? null : ofn.file.TrimEnd('\0');
        }

        private static int[] GuessAudioTypes(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".wav") return new[] { 20, 0 };
            if (ext == ".ogg") return new[] { 14, 0 };
            if (ext == ".aiff" || ext == ".aif") return new[] { 2, 0 };
            if (ext == ".xm" || ext == ".mod") return new[] { 10, 0 };
            return new[] { 13, 0 };
        }

        private static object MakeBuzz(float seconds)
        {
            int rate = 22050;
            int n = Mathf.Max(64, (int)(rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float gate = Mathf.Sin(2f * Mathf.PI * 16f * t) > 0f ? 1f : 0.12f;
                data[i] = Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.7f * gate;
            }
            return ClipFromSamples("PiP_Buzz", data, rate);
        }

        private static object MakeBeep(int hz, float seconds)
        {
            int rate = 22050;
            int n = Mathf.Max(64, (int)(rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = 1f - i / (float)n;
                data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * 0.35f * env;
            }
            return ClipFromSamples("PiP_Beep", data, rate);
        }

        private static object ClipFromSamples(string name, float[] data, int rate)
        {
            Type clipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
            if (clipType == null || data == null)
                return null;
            MethodInfo create = clipType.GetMethod("Create", new[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(bool) });
            object clip = create.Invoke(null, new object[] { name, data.Length, 1, rate, false });
            clipType.GetMethod("SetData", new[] { typeof(float[]), typeof(int) }).Invoke(clip, new object[] { data, 0 });
            return clip;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GetOpenFileName(ref OpenFileName ofn);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct OpenFileName
        {
            public int structSize;
            public IntPtr owner;
            public IntPtr instance;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public string file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData;
            public IntPtr hook;
            public string templateName;
            public IntPtr reservedPtr;
            public int reservedInt;
            public int flagsEx;
        }
    }
}
