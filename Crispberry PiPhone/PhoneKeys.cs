using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class PhoneKeys
    {
        public const string Open = "pip.open";
        public const string Landscape = "pip.landscape";
        public const string Answer = "pip.answer";
        public const string Decline = "pip.decline";
        public const string CamCursor = "pip.camera.cursor";
        public const string CamFlip = "pip.camera.flip";
        public const string CamRotate = "pip.camera.rotate";
        public const string CamShutter = "pip.camera.shutter";
        public const string CamZoomIn = "pip.camera.zoomin";
        public const string CamZoomOut = "pip.camera.zoomout";
        public const string MusicPlay = "pip.music.play";
        public const string MusicNext = "pip.music.next";
        public const string MusicPrev = "pip.music.prev";
        public const string GameUp = "pip.games.up";
        public const string GameDown = "pip.games.down";
        public const string GameLeft = "pip.games.left";
        public const string GameRight = "pip.games.right";
        public const string TetrisRotate = "pip.tetris.rotate";
        public const string TetrisDrop = "pip.tetris.drop";
        public const string BreakoutServe = "pip.breakout.serve";

        public static event Action Changed;

        private static readonly List<PiPhoneKeybind> Binds = new List<PiPhoneKeybind>(24);
        private static readonly Dictionary<string, Saved> SavedKeys = new Dictionary<string, Saved>(StringComparer.OrdinalIgnoreCase);
        private static string _captureId;
        private static Action _captureDone;
        private static bool _fileLoaded;

        private struct Saved
        {
            public KeyCode Key;
            public KeyCode Modifier;
        }

        public static void RegisterBuiltins()
        {
            string[] games =
            {
                BuiltinApps.SnakeId, BuiltinApps.Game2048Id, BuiltinApps.MinesId,
                BuiltinApps.TetrisId, BuiltinApps.BreakoutId
            };

            Add(new PiPhoneKeybind
            {
                Id = Open,
                Label = "Open phone",
                Group = "Phone",
                DefaultKey = KeyCode.P,
                DefaultModifier = KeyCode.LeftAlt,
                WithModifier = true,
                Global = true,
                Required = true,
                SortOrder = 0,
                OnPressed = PhoneMenu.Toggle
            });
            Add(new PiPhoneKeybind
            {
                Id = Landscape,
                Label = "Rotate phone",
                Group = "Phone",
                DefaultKey = KeyCode.F3,
                PhoneOpen = true,
                SortOrder = 1,
                OnPressed = PhoneMenu.ToggleUserLandscape
            });
            Add(new PiPhoneKeybind
            {
                Id = Answer,
                Label = "Answer call / open text",
                Group = "Alerts",
                DefaultKey = KeyCode.F1,
                Global = true,
                SortOrder = 10,
                OnPressed = AlertHud.HotkeyAnswer
            });
            Add(new PiPhoneKeybind
            {
                Id = Decline,
                Label = "Decline call / dismiss text",
                Group = "Alerts",
                DefaultKey = KeyCode.F2,
                Global = true,
                SortOrder = 11,
                OnPressed = AlertHud.HotkeyDecline
            });
            Add(new PiPhoneKeybind
            {
                Id = CamCursor,
                Label = "Walk around",
                Group = "Phone",
                PhoneOpen = true,
                DefaultKey = KeyCode.LeftAlt,
                SortOrder = 20,
                OnPressed = PhoneMenu.TogglePlayThrough
            });
            Add(new PiPhoneKeybind
            {
                Id = CamFlip,
                Label = "Flip camera",
                Group = "Camera",
                AppId = BuiltinApps.CameraId,
                DefaultKey = KeyCode.F,
                SortOrder = 21
            });
            Add(new PiPhoneKeybind
            {
                Id = CamRotate,
                Label = "Tall / wide",
                Group = "Camera",
                AppId = BuiltinApps.CameraId,
                DefaultKey = KeyCode.R,
                SortOrder = 22
            });
            Add(new PiPhoneKeybind
            {
                Id = CamShutter,
                Label = "Shutter",
                Group = "Camera",
                AppId = BuiltinApps.CameraId,
                DefaultKey = KeyCode.V,
                SortOrder = 23
            });
            Add(new PiPhoneKeybind
            {
                Id = CamZoomIn,
                Label = "Zoom in",
                Group = "Camera",
                AppId = BuiltinApps.CameraId,
                DefaultKey = KeyCode.None,
                SortOrder = 24
            });
            Add(new PiPhoneKeybind
            {
                Id = CamZoomOut,
                Label = "Zoom out",
                Group = "Camera",
                AppId = BuiltinApps.CameraId,
                DefaultKey = KeyCode.None,
                SortOrder = 25
            });
            Add(new PiPhoneKeybind
            {
                Id = MusicPlay,
                Label = "Play / pause music",
                Group = "Music",
                DefaultKey = KeyCode.None,
                Global = true,
                SortOrder = 30,
                OnPressed = MusicPlayer.Toggle
            });
            Add(new PiPhoneKeybind
            {
                Id = MusicNext,
                Label = "Next song",
                Group = "Music",
                DefaultKey = KeyCode.None,
                Global = true,
                SortOrder = 31,
                OnPressed = MusicPlayer.Next
            });
            Add(new PiPhoneKeybind
            {
                Id = MusicPrev,
                Label = "Previous song",
                Group = "Music",
                DefaultKey = KeyCode.None,
                Global = true,
                SortOrder = 32,
                OnPressed = MusicPlayer.Prev
            });
            Add(new PiPhoneKeybind
            {
                Id = GameUp,
                Label = "Up",
                Group = "Games",
                DefaultKey = KeyCode.W,
                AlsoApps = games,
                SortOrder = 40
            });
            Add(new PiPhoneKeybind
            {
                Id = GameDown,
                Label = "Down",
                Group = "Games",
                DefaultKey = KeyCode.S,
                AlsoApps = games,
                SortOrder = 41
            });
            Add(new PiPhoneKeybind
            {
                Id = GameLeft,
                Label = "Left",
                Group = "Games",
                DefaultKey = KeyCode.A,
                AlsoApps = games,
                SortOrder = 42
            });
            Add(new PiPhoneKeybind
            {
                Id = GameRight,
                Label = "Right",
                Group = "Games",
                DefaultKey = KeyCode.D,
                AlsoApps = games,
                SortOrder = 43
            });
            Add(new PiPhoneKeybind
            {
                Id = TetrisRotate,
                Label = "Rotate",
                Group = "Stacker",
                AppId = BuiltinApps.TetrisId,
                DefaultKey = KeyCode.None,
                SortOrder = 50
            });
            Add(new PiPhoneKeybind
            {
                Id = TetrisDrop,
                Label = "Hard drop",
                Group = "Stacker",
                AppId = BuiltinApps.TetrisId,
                DefaultKey = KeyCode.Space,
                SortOrder = 51
            });
            Add(new PiPhoneKeybind
            {
                Id = BreakoutServe,
                Label = "Serve",
                Group = "Brick Break",
                AppId = BuiltinApps.BreakoutId,
                DefaultKey = KeyCode.Space,
                SortOrder = 60
            });
        }

        public static void Add(PiPhoneKeybind bind)
        {
            if (bind == null || string.IsNullOrEmpty(bind.Id))
                return;
            int existing = IndexOf(bind.Id);
            if (existing >= 0)
            {
                bind.Key = Binds[existing].Key;
                bind.Modifier = Binds[existing].Modifier;
                Binds[existing] = bind;
            }
            else
            {
                bind.Key = bind.DefaultKey;
                bind.Modifier = bind.DefaultModifier;
                Binds.Add(bind);
            }
            ApplySaved(bind);
            EnforceRequired(bind);
            Sort();
            RaiseChanged();
        }

        public static bool Remove(string id)
        {
            int i = IndexOf(id);
            if (i < 0)
                return false;
            Binds.RemoveAt(i);
            RaiseChanged();
            return true;
        }

        public static PiPhoneKeybind Find(string id)
        {
            int i = IndexOf(id);
            return i >= 0 ? Binds[i] : null;
        }

        public static PiPhoneKeybind[] All()
        {
            return Binds.ToArray();
        }

        public static bool Down(string id)
        {
            PiPhoneKeybind bind = Find(id);
            return Down(bind);
        }

        public static bool Held(string id)
        {
            PiPhoneKeybind bind = Find(id);
            if (!Ready(bind) || !ShouldFire(bind))
                return false;
            if (!Input.GetKey(bind.Key))
                return false;
            return ModifierOk(bind.Modifier);
        }

        public static string Format(string id)
        {
            PiPhoneKeybind bind = Find(id);
            if (bind == null)
                return "None";
            return Format(bind.Key, bind.Modifier);
        }

        public static string Format(KeyCode key, KeyCode modifier)
        {
            if (key == KeyCode.None)
                return "None";
            if (modifier == KeyCode.None)
                return Pretty(key);
            return Pretty(modifier) + "+" + Pretty(key);
        }

        public static string Pretty(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None:
                    return "None";
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return "Ctrl";
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    return "Alt";
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.Space:
                    return "Space";
                default:
                    return key.ToString();
            }
        }

        public static void Set(string id, KeyCode key, KeyCode modifier)
        {
            PiPhoneKeybind bind = Find(id);
            if (bind == null)
                return;
            if (bind.Required && key == KeyCode.None)
                return;
            if (!bind.WithModifier)
                modifier = KeyCode.None;
            bind.Key = key;
            bind.Modifier = modifier;
            SavedKeys[id] = new Saved { Key = key, Modifier = modifier };
            Save();
            SyncConfig(bind);
            if (id == GameUp) PhoneTheme.SnakeUp = key;
            else if (id == GameDown) PhoneTheme.SnakeDown = key;
            else if (id == GameLeft) PhoneTheme.SnakeLeft = key;
            else if (id == GameRight) PhoneTheme.SnakeRight = key;
            RaiseChanged();
        }

        public static void Clear(string id)
        {
            PiPhoneKeybind bind = Find(id);
            if (bind == null || bind.Required)
                return;
            Set(id, KeyCode.None, KeyCode.None);
        }

        public static void BeginCapture(string id, Action onDone)
        {
            if (Find(id) == null)
                return;
            _captureId = id;
            _captureDone = onDone;
            Plugin.CapturingHotkey = true;
        }

        public static bool TickCapture()
        {
            if (!Plugin.CapturingHotkey || string.IsNullOrEmpty(_captureId))
                return Plugin.CapturingHotkey;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EndCapture();
                return true;
            }
            PiPhoneKeybind bind = Find(_captureId);
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (IsIgnored(key, bind != null && bind.WithModifier))
                    continue;
                if (!Input.GetKeyDown(key))
                    continue;
                KeyCode mod = KeyCode.None;
                if (bind != null && bind.WithModifier)
                {
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                        mod = KeyCode.LeftControl;
                    else if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                        mod = KeyCode.LeftAlt;
                    else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                        mod = KeyCode.LeftShift;
                }
                Set(_captureId, key, mod);
                EndCapture();
                return true;
            }
            return true;
        }

        public static void FirePressed()
        {
            if (Plugin.CapturingHotkey)
                return;
            for (int i = 0; i < Binds.Count; i++)
            {
                PiPhoneKeybind bind = Binds[i];
                if (bind == null || bind.OnPressed == null)
                    continue;
                if (bind.Id == Open && PhoneUi.IsTyping())
                    continue;
                if (!Down(bind))
                    continue;
                try { bind.OnPressed(); }
                catch (Exception ex) { Plugin.LogError("Keybind '" + bind.Id + "': " + ex.Message); }
            }
        }

        public static void Load()
        {
            SavedKeys.Clear();
            string path = Path.Combine(PhoneStore.RootDir, "keys.txt");
            _fileLoaded = File.Exists(path);
            if (_fileLoaded)
            {
                try
                {
                    string[] lines = File.ReadAllLines(path);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        int eq = line.IndexOf('=');
                        if (eq <= 0)
                            continue;
                        string raw = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();
                        bool isMod = raw.EndsWith(".mod", StringComparison.OrdinalIgnoreCase);
                        string id = isMod ? raw.Substring(0, raw.Length - 4) : raw;
                        KeyCode parsed = ParseKey(val, KeyCode.None);
                        Saved cur;
                        SavedKeys.TryGetValue(id, out cur);
                        if (isMod)
                            cur.Modifier = parsed;
                        else
                            cur.Key = parsed;
                        SavedKeys[id] = cur;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Keybinds load failed: " + ex.Message);
                }
            }
            for (int i = 0; i < Binds.Count; i++)
                ApplySaved(Binds[i]);
            if (!_fileLoaded)
                SeedFromLegacy();
            bool restored = false;
            for (int i = 0; i < Binds.Count; i++)
            {
                if (EnforceRequired(Binds[i]))
                    restored = true;
            }
            if (restored)
                Save();
        }

        private static void SeedFromLegacy()
        {
            Plugin p = Plugin.Instance;
            if (p != null)
            {
                if (p.MenuKey != null)
                    Set(Open, p.MenuKey.Value, p.MenuModifier != null ? p.MenuModifier.Value : KeyCode.None);
                if (p.LandscapeKey != null)
                    Set(Landscape, p.LandscapeKey.Value, KeyCode.None);
                if (p.AnswerKey != null)
                    Set(Answer, p.AnswerKey.Value, KeyCode.None);
                if (p.DeclineKey != null)
                    Set(Decline, p.DeclineKey.Value, KeyCode.None);
                if (p.CameraCursorKey != null)
                    Set(CamCursor, p.CameraCursorKey.Value, KeyCode.None);
                if (p.CameraFlipKey != null)
                    Set(CamFlip, p.CameraFlipKey.Value, KeyCode.None);
                if (p.CameraRotateKey != null)
                    Set(CamRotate, p.CameraRotateKey.Value, KeyCode.None);
                if (p.CameraShutterKey != null)
                    Set(CamShutter, p.CameraShutterKey.Value, KeyCode.None);
            }
            if (PhoneTheme.SnakeUp != KeyCode.W)
                Set(GameUp, PhoneTheme.SnakeUp, KeyCode.None);
            if (PhoneTheme.SnakeDown != KeyCode.S)
                Set(GameDown, PhoneTheme.SnakeDown, KeyCode.None);
            if (PhoneTheme.SnakeLeft != KeyCode.A)
                Set(GameLeft, PhoneTheme.SnakeLeft, KeyCode.None);
            if (PhoneTheme.SnakeRight != KeyCode.D)
                Set(GameRight, PhoneTheme.SnakeRight, KeyCode.None);
            Save();
        }

        private static void Save()
        {
            try
            {
                PhoneStore.EnsureDir();
                var sb = new StringBuilder();
                for (int i = 0; i < Binds.Count; i++)
                {
                    PiPhoneKeybind b = Binds[i];
                    if (b == null)
                        continue;
                    sb.Append(b.Id).Append('=').Append(b.Key).Append('\n');
                    if (b.WithModifier)
                        sb.Append(b.Id).Append(".mod=").Append(b.Modifier).Append('\n');
                }
                File.WriteAllText(Path.Combine(PhoneStore.RootDir, "keys.txt"), sb.ToString());
                _fileLoaded = true;
            }
            catch (Exception ex)
            {
                Plugin.LogError("Keybinds save failed: " + ex.Message);
            }
        }

        private static bool Down(PiPhoneKeybind bind)
        {
            if (!Ready(bind) || !ShouldFire(bind))
                return false;
            if (!Input.GetKeyDown(bind.Key))
                return false;
            return ModifierOk(bind.Modifier);
        }

        private static bool Ready(PiPhoneKeybind bind)
        {
            return bind != null && bind.Key != KeyCode.None;
        }

        internal static bool ShouldFire(PiPhoneKeybind bind)
        {
            if (bind == null)
                return false;
            if (bind.Global)
                return true;
            if (!PhoneMenu.IsOpen)
                return false;
            if (bind.PhoneOpen && string.IsNullOrEmpty(bind.AppId) && (bind.AlsoApps == null || bind.AlsoApps.Length == 0))
                return true;
            string current = PhoneMenu.CurrentAppId;
            if (string.IsNullOrEmpty(current))
                return false;
            if (!string.IsNullOrEmpty(bind.AppId) && string.Equals(bind.AppId, current, StringComparison.OrdinalIgnoreCase))
                return true;
            if (bind.AlsoApps == null)
                return false;
            for (int i = 0; i < bind.AlsoApps.Length; i++)
            {
                if (string.Equals(bind.AlsoApps[i], current, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool ModifierOk(KeyCode modifier)
        {
            if (modifier == KeyCode.None)
                return true;
            if (modifier == KeyCode.LeftControl || modifier == KeyCode.RightControl)
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (modifier == KeyCode.LeftAlt || modifier == KeyCode.RightAlt)
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (modifier == KeyCode.LeftShift || modifier == KeyCode.RightShift)
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return Input.GetKey(modifier);
        }

        private static void ApplySaved(PiPhoneKeybind bind)
        {
            if (bind == null)
                return;
            Saved saved;
            if (!SavedKeys.TryGetValue(bind.Id, out saved))
                return;
            if (bind.Required && saved.Key == KeyCode.None)
                return;
            bind.Key = saved.Key;
            if (bind.WithModifier)
                bind.Modifier = saved.Modifier;
        }

        private static bool EnforceRequired(PiPhoneKeybind bind)
        {
            if (bind == null || !bind.Required || bind.Key != KeyCode.None)
                return false;
            bind.Key = bind.DefaultKey;
            bind.Modifier = bind.DefaultModifier;
            SavedKeys[bind.Id] = new Saved { Key = bind.Key, Modifier = bind.Modifier };
            return true;
        }

        private static void SyncConfig(PiPhoneKeybind bind)
        {
            Plugin p = Plugin.Instance;
            if (p == null || bind == null)
                return;
            if (bind.Id == Open)
            {
                if (p.MenuKey != null) p.MenuKey.Value = bind.Key;
                if (p.MenuModifier != null) p.MenuModifier.Value = bind.Modifier;
            }
            else if (bind.Id == Landscape && p.LandscapeKey != null)
                p.LandscapeKey.Value = bind.Key;
            else if (bind.Id == Answer && p.AnswerKey != null)
                p.AnswerKey.Value = bind.Key;
            else if (bind.Id == Decline && p.DeclineKey != null)
                p.DeclineKey.Value = bind.Key;
            else if (bind.Id == CamCursor && p.CameraCursorKey != null)
                p.CameraCursorKey.Value = bind.Key;
            else if (bind.Id == CamFlip && p.CameraFlipKey != null)
                p.CameraFlipKey.Value = bind.Key;
            else if (bind.Id == CamRotate && p.CameraRotateKey != null)
                p.CameraRotateKey.Value = bind.Key;
            else if (bind.Id == CamShutter && p.CameraShutterKey != null)
                p.CameraShutterKey.Value = bind.Key;
        }

        private static void EndCapture()
        {
            Plugin.CapturingHotkey = false;
            Plugin.CaptureEndedFrame = Time.frameCount;
            _captureId = null;
            Action done = _captureDone;
            _captureDone = null;
            if (done != null)
                done();
        }

        private static bool IsIgnored(KeyCode key, bool modifiersAreMods)
        {
            if (key == KeyCode.None || key == KeyCode.Escape)
                return true;
            int code = (int)key;
            if (code >= (int)KeyCode.Mouse0 && code <= (int)KeyCode.Mouse6)
                return true;
            if (!modifiersAreMods)
                return false;
            switch (key)
            {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.LeftWindows:
                case KeyCode.RightWindows:
                case KeyCode.AltGr:
                    return true;
                default:
                    return false;
            }
        }

        private static KeyCode ParseKey(string val, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(val))
                return fallback;
            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), val, true);
            }
            catch
            {
                return fallback;
            }
        }

        private static int IndexOf(string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;
            for (int i = 0; i < Binds.Count; i++)
            {
                if (Binds[i] != null && string.Equals(Binds[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static void Sort()
        {
            Binds.Sort(Compare);
        }

        private static int Compare(PiPhoneKeybind a, PiPhoneKeybind b)
        {
            int g = string.Compare(a != null ? a.Group : string.Empty, b != null ? b.Group : string.Empty, StringComparison.OrdinalIgnoreCase);
            if (g != 0)
                return g;
            int o = (a != null ? a.SortOrder : 0).CompareTo(b != null ? b.SortOrder : 0);
            if (o != 0)
                return o;
            return string.Compare(a != null ? a.Label : string.Empty, b != null ? b.Label : string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null)
                handler();
        }
    }
}
