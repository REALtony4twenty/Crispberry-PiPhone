using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Crispberry_PiPhone
{
    /// <summary>Short clips embedded from Freesound. See Sounds/CREDITS.txt.</summary>
    internal static class PhoneSfx
    {
        private static readonly Dictionary<string, object> Cache = new Dictionary<string, object>(StringComparer.Ordinal);
        private static readonly HashSet<string> Missed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Assembly Asm = typeof(PhoneSfx).Assembly;
        private static string[] _resourceNames;

        public static void Play(string name)
        {
            Play(name, false);
        }

        public static void PlayUi(string name)
        {
            Play(name, true);
        }

        private static void Play(string name, bool ui)
        {
            if (ui && (name == "vibrate" || !PhoneTheme.UiCueOn(name)))
                return;
            object clip = ui ? UiClip(name) : Clip(name);
            if (clip == null)
                return;
            float vol = ui ? PhoneTheme.UiCueVolume(name) : PhoneTheme.GameCueVolume(OpenId(), name);
            if (vol <= 0.001f)
                return;
            PhoneAudio.PlayOneShot(ui ? PhoneAudioChannel.System : PhoneAudioChannel.Media, clip, vol);
        }

        /// <summary>The clip a Sounds-page row plays: its replacement file when one is set, else the built-in.</summary>
        public static object UiClip(string slot)
        {
            string file = PhoneTheme.UiCueFile(slot);
            return string.IsNullOrEmpty(file) ? Clip(slot) : Resolve(file);
        }

        /// <summary>Cues another mod may name. The Sounds-page rows, without vibrate.</summary>
        public static bool IsInterfaceCue(string name)
        {
            switch (name)
            {
                case "click":
                case "toggle-off":
                case "toggle-on":
                case "back-btn":
                case "hover":
                case "shutter":
                case "rec-start":
                case "rec-stop":
                case "tick":
                case "trash":
                case "type":
                case "back":
                    return true;
                default:
                    return false;
            }
        }

        private static string OpenId()
        {
            PiPhoneApp app = PhoneMenu.OpenApp();
            return app != null ? app.Id : null;
        }

        public static object GetClip(string name)
        {
            return ClipLabel(name) != null ? Clip(name) : null;
        }

        private static int _backFrame = -1;
        private static float _hoverAt;
        private static float _tickAt;
        private static int _blobId;
        private static readonly Dictionary<string, object> HoverClips = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static void HoldClick()
        {
            _backFrame = Time.frameCount;
        }

        public static void PlayBack()
        {
            HoldClick();
            PlayUi("back-btn");
        }

        public static bool ConsumeBack()
        {
            if (_backFrame != Time.frameCount)
                return false;
            _backFrame = -1;
            return true;
        }

        /// <summary>Side-key ticks. Always the built-in clip at the phone volume, with no Sounds-page slot.</summary>
        public static void PlayMaster(string name)
        {
            PhoneAudio.PlayOneShot(PhoneAudioChannel.System, Clip(name), 1f);
        }

        public static void PlayTick()
        {
            if (Time.unscaledTime < _tickAt)
                return;
            _tickAt = Time.unscaledTime + 0.06f;
            PlayUi("tick");
        }

        public static void PlayHover()
        {
            PlayAppHover(null);
        }

        public static void PlayAppHover(string appId)
        {
            if (Time.unscaledTime < _hoverAt)
                return;
            _hoverAt = Time.unscaledTime + 0.07f;
            object custom;
            if (!string.IsNullOrEmpty(appId) && HoverClips.TryGetValue(appId, out custom) && custom != null)
            {
                if (PhoneTheme.UiCueOn("hover"))
                    PhoneAudio.PlayOneShot(PhoneAudioChannel.System, custom, PhoneTheme.UiCueVolume("hover"));
                return;
            }
            PiPhoneApp app;
            if (!string.IsNullOrEmpty(appId) && PiPhoneApi.TryGetApp(appId, out app) && app != null && app.HoverSound != "hover" && IsInterfaceCue(app.HoverSound))
            {
                if (PhoneTheme.UiCueOn("hover"))
                    PhoneAudio.PlayOneShot(PhoneAudioChannel.System, Clip(app.HoverSound), PhoneTheme.UiCueVolume("hover"));
                return;
            }
            PlayUi("hover");
        }

        public static bool SetHoverClip(string appId, byte[] audio)
        {
            if (string.IsNullOrEmpty(appId))
                return false;
            if (audio == null || audio.Length < 32)
            {
                HoverClips.Remove(appId);
                return true;
            }
            object clip = FromBytes(audio);
            if (clip == null)
                return false;
            HoverClips[appId] = clip;
            return true;
        }

        public static object FromBytes(byte[] audio)
        {
            if (audio == null || audio.Length < 32)
                return null;
            byte[] wav = null;
            try
            {
                if (audio[0] == (byte)'R' && audio[1] == (byte)'I')
                    wav = TrimSilence(audio);
                else
                {
                    string dir = Path.Combine(Path.GetTempPath(), "PiPhoneSfx");
                    Directory.CreateDirectory(dir);
                    string path = Path.Combine(dir, "blob-" + (++_blobId) + ".mp3");
                    File.WriteAllBytes(path, audio);
                    wav = TrimSilence(PhoneVideo.DecodeAudioToWav(path));
                }
            }
            catch
            {
                wav = null;
            }
            return wav != null ? VoiceIo.FromWav(wav) : null;
        }

        public static void BindPress(UnityEngine.UI.Button button, UnityAction action, bool back = false, bool trash = false)
        {
            if (button == null)
                return;
            button.onClick.AddListener(() =>
            {
                if (action != null)
                    action();
                if (ConsumeBack())
                    return;
                PlayUi(trash ? "trash" : back ? "back-btn" : "click");
            });
        }

        public static void BindCue(UnityEngine.UI.Button button, UnityAction action, string cue)
        {
            if (button == null)
                return;
            button.onClick.AddListener(() =>
            {
                if (action != null)
                    action();
                if (ConsumeBack())
                    return;
                PlayUi(string.IsNullOrEmpty(cue) ? "click" : cue);
            });
        }

        public static void BindClip(UnityEngine.UI.Button button, UnityAction action, byte[] audio)
        {
            if (button == null)
                return;
            object clip = FromBytes(audio);
            button.onClick.AddListener(() =>
            {
                if (action != null)
                    action();
                if (ConsumeBack())
                    return;
                if (clip == null)
                {
                    PlayUi("click");
                    return;
                }
                if (!PhoneTheme.UiCueOn("click"))
                    return;
                PhoneAudio.PlayOneShot(PhoneAudioChannel.System, clip, PhoneTheme.UiCueVolume("click"));
            });
        }

        public static void PlayRaw(PhoneAudioChannel channel, string name, float scale)
        {
            PhoneAudio.PlayOneShot(channel, Clip(name), scale);
        }

        public static void PreviewUi(string slot)
        {
            Preview(slot, UiClip(slot));
        }

        /// <summary>Preview a candidate sound for a Sounds-page row, at that row's volume.</summary>
        public static void PreviewFile(string slot, string id)
        {
            Preview(slot, Resolve(id));
        }

        private static void Preview(string slot, object clip)
        {
            float vol = PhoneTheme.UiCueVolume(slot);
            if (slot == "vibrate")
                PhoneAudio.PlayVibrate(clip, vol);
            else
                PhoneAudio.PlayOneShot(PhoneAudioChannel.System, clip, vol);
        }

        public static void BindKeys(TMP_InputField field)
        {
            if (field == null)
                return;
            string prev = field.text ?? string.Empty;
            field.onValueChanged.AddListener(value =>
            {
                string now = value ?? string.Empty;
                if (!field.isFocused)
                {
                    prev = now;
                    return;
                }
                if (now.Length > prev.Length)
                    PlayUi("type");
                else if (now.Length < prev.Length)
                    PlayUi("back");
                prev = now;
            });
        }

        public static void SeedAlerts()
        {
            Warm();
            Drop("alert-candy", "Candy pop");
            Drop("alert-duuwip", "Doo-wip");
        }

        public static void Warm()
        {
            string[] names = ResourceNames();
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                if (string.IsNullOrEmpty(n) || !n.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    continue;
                int dot = n.LastIndexOf('.');
                int prev = dot > 0 ? n.LastIndexOf('.', dot - 1) : -1;
                string file = prev >= 0 ? n.Substring(prev + 1, dot - prev - 1) : n;
                try { Clip(file); }
                catch (Exception ex) { Plugin.LogError("Sound '" + file + "' warm: " + ex.Message); }
            }
        }

        private static void Drop(string name, string title)
        {
            try
            {
                PhoneStore.EnsureDir();
                string dest = Path.Combine(PhoneStore.AlertsDir, title + ".wav");
                if (File.Exists(dest))
                    return;
                byte[] wav = Wav(name);
                if (wav == null || wav.Length < 64)
                    return;
                File.WriteAllBytes(dest, wav);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Alert '" + title + "': " + ex.Message);
            }
        }

        internal struct Cue
        {
            public string Key;
            public string Label;
            public bool Tone;
            public int Hz;
            public float Seconds;

            public Cue(string key, string label)
            {
                Key = key;
                Label = label;
                Tone = false;
                Hz = 0;
                Seconds = 0f;
            }

            public Cue(string key, string label, int hz, float seconds)
            {
                Key = key;
                Label = label;
                Tone = true;
                Hz = hz;
                Seconds = seconds;
            }
        }

        public static Cue[] Cues(string appId)
        {
            if (appId == BuiltinApps.SnakeId)
                return new[]
                {
                    new Cue("snake-turn", "Turn"),
                    new Cue("snake-eat", "Apple"),
                    new Cue("snake-win", "Board full"),
                    new Cue("snake-die", "Hit", 196, 0.2f)
                };
            if (appId == BuiltinApps.TetrisId)
                return new[]
                {
                    new Cue("stack-rot", "Rotate"),
                    new Cue("stack-place", "Place"),
                    new Cue("stack-clear", "Clear"),
                    new Cue("stack-lose", "Lose")
                };
            if (appId == BuiltinApps.BreakoutId)
                return new[]
                {
                    new Cue("brick-wall", "Wall", 480, 0.03f),
                    new Cue("brick-paddle", "Paddle", 660, 0.04f),
                    new Cue("brick-solid", "Solid", 220, 0.04f),
                    new Cue("brick-hit", "Brick", 990, 0.05f)
                };
            if (appId == BuiltinApps.SimonId)
                return new[]
                {
                    new Cue("echo-pad", "Pad", 523, 0.12f),
                    new Cue("echo-win", "Success", 784, 0.12f),
                    new Cue("echo-lose", "Miss", 392, 0.12f)
                };
            if (appId == BuiltinApps.Connect4Id)
                return new[]
                {
                    new Cue("c4-land", "Land"),
                    new Cue("c4-reset", "Reset"),
                    new Cue("c4-win", "Four"),
                    new Cue("c4-draw", "Draw", 180, 0.16f)
                };
            if (appId == BuiltinApps.SudokuId)
                return new[]
                {
                    new Cue("sudo-sel", "Select"),
                    new Cue("sudo-ok", "Place"),
                    new Cue("sudo-bad", "Wrong"),
                    new Cue("sudo-win", "Done")
                };
            if (appId == BuiltinApps.SolitaireId)
                return new[]
                {
                    new Cue("sol-card", "Card"),
                    new Cue("sol-bad", "Illegal"),
                    new Cue("sol-win", "Win"),
                    new Cue("sol-lose", "Lose")
                };
            if (appId == BuiltinApps.Game2048Id)
                return new[]
                {
                    new Cue("g2048-slide", "Slide"),
                    new Cue("g2048-merge", "Merge"),
                    new Cue("g2048-dead", "No moves"),
                    new Cue("g2048-win", "2048")
                };
            if (appId == BuiltinApps.MinesId)
                return new[]
                {
                    new Cue("mine-dig", "Dig"),
                    new Cue("mine-flag", "Flag"),
                    new Cue("mine-boom", "Boom"),
                    new Cue("mine-win", "Win")
                };
            return new Cue[0];
        }

        public static Cue[] Library()
        {
            return new[]
            {
                new Cue("click", "Button"),
                new Cue("toggle-off", "Toggle off"),
                new Cue("toggle-on", "Toggle on"),
                new Cue("back-btn", "Back"),
                new Cue("hover", "Hover"),
                new Cue("vol-up", "Volume up"),
                new Cue("vol-down", "Volume down"),
                new Cue("shutter", "Shutter"),
                new Cue("rec-start", "Record start"),
                new Cue("rec-stop", "Record stop"),
                new Cue("tick", "Slider"),
                new Cue("trash", "Trash"),
                new Cue("vibrate", "Vibrate"),
                new Cue("ui-menu", "Menu"),
                new Cue("ui-on", "Selected"),
                new Cue("ui-press", "Press"),
                new Cue("type", "Typing"),
                new Cue("back", "Backspace"),
                new Cue("alert-candy", "Candy pop"),
                new Cue("alert-duuwip", "Doo-wip"),
                new Cue("snake-turn", "Snake turn"),
                new Cue("snake-eat", "Snake apple"),
                new Cue("snake-win", "Snake board full"),
                new Cue("stack-rot", "Stacker rotate"),
                new Cue("stack-place", "Stacker place"),
                new Cue("stack-clear", "Stacker clear"),
                new Cue("stack-lose", "Stacker lose"),
                new Cue("c4-land", "Four Across land"),
                new Cue("c4-reset", "Four Across reset"),
                new Cue("c4-win", "Four Across win"),
                new Cue("sudo-sel", "Sudoku select"),
                new Cue("sudo-ok", "Sudoku place"),
                new Cue("sudo-bad", "Sudoku wrong"),
                new Cue("sudo-win", "Sudoku done"),
                new Cue("sol-card", "Solitaire card"),
                new Cue("sol-bad", "Solitaire illegal"),
                new Cue("sol-win", "Solitaire win"),
                new Cue("sol-lose", "Solitaire lose"),
                new Cue("g2048-slide", "2048 slide"),
                new Cue("g2048-merge", "2048 merge"),
                new Cue("g2048-dead", "2048 no moves"),
                new Cue("g2048-win", "2048 win"),
                new Cue("mine-dig", "Mines dig"),
                new Cue("mine-flag", "Mines flag"),
                new Cue("mine-boom", "Mines boom"),
                new Cue("mine-win", "Mines win")
            };
        }

        public static string ClipLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            Cue[] all = Library();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Key == key)
                    return all[i].Label;
            }
            return null;
        }

        private static object Resolve(string id)
        {
            if (ClipLabel(id) != null)
                return Clip(id);
            return AlertClip(id);
        }

        private static object AlertClip(string id)
        {
            string cacheKey = "alert:" + id;
            object cached;
            if (Cache.TryGetValue(cacheKey, out cached))
                return cached;
            SoundItem item = PhoneStore.FindSound(id);
            if (item == null)
                return null;
            string path = PhoneStore.SoundPath(item.File);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            byte[] wav = null;
            try
            {
                if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    wav = TrimSilence(File.ReadAllBytes(path));
                else
                    wav = TrimSilence(PhoneVideo.DecodeAudioToWav(path));
            }
            catch
            {
                wav = null;
            }
            object clip = wav != null ? VoiceIo.FromWav(wav) : null;
            if (clip != null)
                Cache[cacheKey] = clip;
            return clip;
        }

        private static object Clip(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            object cached;
            if (Cache.TryGetValue(name, out cached))
                return cached;
            byte[] wav = Wav(name);
            object clip = wav != null ? VoiceIo.FromWav(wav) : null;
            if (clip == null)
            {
                if (Missed.Add(name))
                    Plugin.LogError("Sound '" + name + "' did not decode.");
                return null;
            }
            Cache[name] = clip;
            return clip;
        }

        private static byte[] Wav(string name)
        {
            byte[] mp3 = Bytes(name + ".mp3");
            if (mp3 == null || mp3.Length < 32)
                return null;
            string dir = Path.Combine(Path.GetTempPath(), "PiPhoneSfx");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".mp3");
            if (!File.Exists(path) || new FileInfo(path).Length != mp3.Length)
                File.WriteAllBytes(path, mp3);
            try
            {
                return TrimSilence(PhoneVideo.DecodeAudioToWav(path));
            }
            catch
            {
                return null;
            }
        }

        private static byte[] TrimSilence(byte[] wav)
        {
            if (wav == null || wav.Length < 44 + 64)
                return wav;
            if (wav[0] != (byte)'R' || wav[8] != (byte)'W')
                return wav;
            int channels = wav[22] | (wav[23] << 8);
            int rate = wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24);
            int bits = wav[34] | (wav[35] << 8);
            if (channels != 1 || bits != 16 || rate < 8000)
                return wav;
            int data = 44;
            int samples = (wav.Length - data) / 2;
            if (samples < 32)
                return wav;
            int first = 0;
            while (first < samples && Math.Abs(SampleAt(wav, data, first)) < 500)
                first++;
            int last = samples - 1;
            while (last > first && Math.Abs(SampleAt(wav, data, last)) < 500)
                last--;
            int pad = rate / 400;
            if (pad < 8)
                pad = 8;
            first = Math.Max(0, first - pad);
            last = Math.Min(samples - 1, last + pad);
            int keep = last - first + 1;
            if (keep < rate / 50 || (first == 0 && last == samples - 1))
                return wav;
            var pcm = new byte[keep * 2];
            Buffer.BlockCopy(wav, data + first * 2, pcm, 0, pcm.Length);
            int fade = rate / 1000;
            if (fade > keep / 4)
                fade = keep / 4;
            for (int i = 0; i < fade; i++)
            {
                float g = fade > 1 ? i / (float)(fade - 1) : 1f;
                Scale(pcm, i, g);
                Scale(pcm, keep - 1 - i, g);
            }
            return WrapPcm(pcm, rate);
        }

        private static int SampleAt(byte[] wav, int data, int index)
        {
            int o = data + index * 2;
            int v = wav[o] | (wav[o + 1] << 8);
            if (v >= 32768)
                v -= 65536;
            return v;
        }

        private static void Scale(byte[] pcm, int index, float g)
        {
            int o = index * 2;
            int v = pcm[o] | (pcm[o + 1] << 8);
            if (v >= 32768)
                v -= 65536;
            int s = (int)Math.Round(v * g);
            if (s > 32767)
                s = 32767;
            if (s < -32768)
                s = -32768;
            pcm[o] = (byte)(s & 0xff);
            pcm[o + 1] = (byte)((s >> 8) & 0xff);
        }

        private static byte[] WrapPcm(byte[] pcm, int rate)
        {
            var wav = new byte[44 + pcm.Length];
            wav[0] = (byte)'R'; wav[1] = (byte)'I'; wav[2] = (byte)'F'; wav[3] = (byte)'F';
            Write32(wav, 4, 36 + pcm.Length);
            wav[8] = (byte)'W'; wav[9] = (byte)'A'; wav[10] = (byte)'V'; wav[11] = (byte)'E';
            wav[12] = (byte)'f'; wav[13] = (byte)'m'; wav[14] = (byte)'t'; wav[15] = (byte)' ';
            Write32(wav, 16, 16);
            wav[20] = 1;
            wav[22] = 1;
            Write32(wav, 24, rate);
            Write32(wav, 28, rate * 2);
            wav[32] = 2;
            wav[34] = 16;
            wav[36] = (byte)'d'; wav[37] = (byte)'a'; wav[38] = (byte)'t'; wav[39] = (byte)'a';
            Write32(wav, 40, pcm.Length);
            Buffer.BlockCopy(pcm, 0, wav, 44, pcm.Length);
            return wav;
        }

        private static void Write32(byte[] b, int o, int v)
        {
            b[o] = (byte)(v & 0xff);
            b[o + 1] = (byte)((v >> 8) & 0xff);
            b[o + 2] = (byte)((v >> 16) & 0xff);
            b[o + 3] = (byte)((v >> 24) & 0xff);
        }

        private static string[] ResourceNames()
        {
            if (_resourceNames == null)
                _resourceNames = Asm.GetManifestResourceNames();
            return _resourceNames;
        }

        private static byte[] Bytes(string file)
        {
            string full = "Crispberry_PiPhone.Sounds." + file;
            Stream stream = Asm.GetManifestResourceStream(full);
            if (stream == null)
            {
                string[] names = ResourceNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].EndsWith(file, StringComparison.OrdinalIgnoreCase))
                    {
                        stream = Asm.GetManifestResourceStream(names[i]);
                        break;
                    }
                }
            }
            if (stream == null)
                return null;
            using (stream)
            {
                var data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                return data;
            }
        }
    }
}
