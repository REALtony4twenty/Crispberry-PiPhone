using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Built-in English plus extra packs from mods or lang/*.txt.
    /// Look up with <see cref="T(string, string)"/>. Missing keys fall back to English, then the caller string.
    /// </summary>
    internal static class PhoneLang
    {
        public const string English = "en";

        private static readonly Dictionary<string, PiPhoneLanguage> Packs = new Dictionary<string, PiPhoneLanguage>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> EnglishMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _ready;
        public static event Action Changed;

        public static string Code
        {
            get
            {
                Ensure();
                if (!string.IsNullOrEmpty(PhoneTheme.Language) && Packs.ContainsKey(PhoneTheme.Language))
                    return PhoneTheme.Language;
                return GuessSystem();
            }
        }

        public static void Ensure()
        {
            if (_ready)
                return;
            _ready = true;
            FillEnglish();
            Packs[English] = new PiPhoneLanguage
            {
                Code = English,
                Name = "English",
                Strings = new Dictionary<string, string>(EnglishMap, StringComparer.OrdinalIgnoreCase)
            };
            PackEs();
            PackFr();
            LoadFiles();
            WriteKeyList();
        }

        public static string T(string key)
        {
            return T(key, key);
        }

        public static string T(string key, string fallback)
        {
            Ensure();
            if (string.IsNullOrEmpty(key))
                return fallback ?? string.Empty;
            string text;
            PiPhoneLanguage pack;
            if (Packs.TryGetValue(Code, out pack) && pack != null && pack.Strings != null && pack.Strings.TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
                return text;
            if (EnglishMap.TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
                return text;
            return fallback ?? key;
        }

        public static string AppName(PiPhoneApp app)
        {
            if (app == null)
                return T("app", "App");
            return T("app." + app.Id, string.IsNullOrEmpty(app.DisplayName) ? app.Id : app.DisplayName);
        }

        public static void Register(PiPhoneLanguage pack)
        {
            Ensure();
            if (pack == null || string.IsNullOrEmpty(pack.Code))
                return;
            if (pack.Strings == null)
                pack.Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.Equals(pack.Code, English, StringComparison.OrdinalIgnoreCase))
            {
                foreach (KeyValuePair<string, string> kv in pack.Strings)
                {
                    if (!string.IsNullOrEmpty(kv.Key) && !string.IsNullOrEmpty(kv.Value))
                        EnglishMap[kv.Key] = kv.Value;
                }
                Packs[English].Strings = new Dictionary<string, string>(EnglishMap, StringComparer.OrdinalIgnoreCase);
            }
            else
                Packs[pack.Code] = pack;
            Action handler = Changed;
            if (handler != null)
                handler();
        }

        public static bool Set(string code)
        {
            Ensure();
            if (string.IsNullOrEmpty(code) || !Packs.ContainsKey(code))
                return false;
            PhoneTheme.Language = code;
            PhoneTheme.Commit();
            FireChanged();
            return true;
        }

        public static void UseSystem()
        {
            Ensure();
            PhoneTheme.Language = string.Empty;
            PhoneTheme.Commit();
            FireChanged();
        }

        private static void FireChanged()
        {
            Action handler = Changed;
            if (handler != null)
                handler();
        }

        public static PiPhoneLanguage[] All()
        {
            Ensure();
            var list = new List<PiPhoneLanguage>(Packs.Count);
            foreach (KeyValuePair<string, PiPhoneLanguage> kv in Packs)
            {
                if (kv.Value != null)
                    list.Add(kv.Value);
            }
            list.Sort((a, b) => string.Compare(a.Name ?? a.Code, b.Name ?? b.Code, StringComparison.OrdinalIgnoreCase));
            return list.ToArray();
        }

        public static string Dir
        {
            get { return Path.Combine(PhoneStore.RootDir, "lang"); }
        }

        private static string GuessSystem()
        {
            try
            {
                SystemLanguage sys = Application.systemLanguage;
                string code = FromUnity(sys);
                if (Packs.ContainsKey(code))
                    return code;
            }
            catch
            {
            }
            return English;
        }

        private static string FromUnity(SystemLanguage lang)
        {
            switch (lang)
            {
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh";
                case SystemLanguage.ChineseTraditional: return "zh-TW";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Swedish: return "sv";
                case SystemLanguage.Turkish: return "tr";
                default: return English;
            }
        }

        private static void LoadFiles()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string[] files = Directory.GetFiles(Dir, "*.txt");
                for (int i = 0; i < files.Length; i++)
                {
                    PiPhoneLanguage pack = ReadFile(files[i]);
                    if (pack != null)
                        Register(pack);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Lang load: " + ex.Message);
            }
        }

        private static PiPhoneLanguage ReadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            string code = Path.GetFileNameWithoutExtension(path);
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string name = code;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                if (key == "name")
                    name = val;
                else if (key == "code")
                    code = val;
                else
                    map[key] = val;
            }
            if (map.Count == 0 && string.Equals(code, English, StringComparison.OrdinalIgnoreCase))
                return null;
            return new PiPhoneLanguage { Code = code, Name = name, Strings = map };
        }

        private static void Add(string key, string english)
        {
            EnglishMap[key] = english;
        }

        private static void FillEnglish()
        {
            Add("play", "Play");
            Add("quit", "Quit");
            Add("menu", "Menu");
            Add("play_again", "Play Again");
            Add("back", "Back");
            Add("controls", "Controls");
            Add("next", "Next");
            Add("rotate", "Rotate");
            Add("drop", "Drop");
            Add("flag", "Flag");
            Add("open", "Open");
            Add("high_score", "High score");
            Add("game_over", "Game over");
            Add("best", "Best");
            Add("score", "Score");
            Add("look", "Look");
            Add("buttons", "Buttons");
            Add("language", "Language");
            Add("reset", "Reset");
            Add("reset_look", "Reset look to defaults");
            Add("reset_buttons", "Reset buttons to defaults");
            Add("system_language", "Use system language");
            Add("lang_hint", "Other mods can add packs with PiPhoneApi.RegisterLanguage, or drop key=value files in the lang folder.");
            Add("app.pip.phone", "Phone");
            Add("app.pip.messages", "Messages");
            Add("app.pip.voicemail", "Voicemail");
            Add("app.pip.notes", "Notes");
            Add("app.pip.camera", "Camera");
            Add("app.pip.closet", "Closet");
            Add("app.pip.photos", "Photos");
            Add("app.pip.voicememos", "Voice Memos");
            Add("app.pip.snake", "Snake");
            Add("app.pip.tetris", "Stacker");
            Add("app.pip.breakout", "Brick Break");
            Add("app.pip.simon", "Echo");
            Add("app.pip.connect4", "Four Across");
            Add("app.pip.mines", "Mines");
            Add("app.pip.2048", "2048");
            Add("app.pip.sounds", "Sounds");
            Add("app.pip.makenoti", "MakeNoti");
            Add("app.pip.settings", "Settings");
            Add("app.pip.store", "Apps");
            Add("app.pip.sudoku", "Sudoku");
            Add("app.pip.solitaire", "Solitaire");
            Add("hello", "HELLO!");
            Add("goodbye", "GOODBYE!");
            Add("battery", "Battery");
            Add("cannot_cast", "Cannot cast to this device");
            Add("no_cast_screen", "No screen nearby");
            Add("screen_cast", "Screen cast");
            Add("cast_on", "On");
        }

        private static void PackEs()
        {
            Pack("es", "Español",
                "play", "Jugar",
                "quit", "Salir",
                "menu", "Menú",
                "play_again", "Otra vez",
                "back", "Atrás",
                "controls", "Controles",
                "next", "Siguiente",
                "rotate", "Girar",
                "drop", "Soltar",
                "flag", "Bandera",
                "open", "Abrir",
                "high_score", "Mejor",
                "game_over", "Fin",
                "best", "Mejor",
                "score", "Puntos",
                "look", "Aspecto",
                "buttons", "Botones",
                "language", "Idioma",
                "reset", "Restablecer",
                "reset_look", "Restablecer aspecto",
                "reset_buttons", "Restablecer botones",
                "system_language", "Idioma del sistema",
                "app.pip.snake", "Serpiente",
                "app.pip.tetris", "Apilador",
                "app.pip.breakout", "Ladrillos",
                "app.pip.simon", "Eco",
                "app.pip.connect4", "Cuatro",
                "app.pip.mines", "Minas",
                "app.pip.store", "Apps",
                "app.pip.settings", "Ajustes",
                "app.pip.phone", "Teléfono",
                "app.pip.messages", "Mensajes",
                "hello", "¡HOLA!",
                "goodbye", "¡ADIÓS!",
                "battery", "Batería",
                "cannot_cast", "No se puede transmitir a este dispositivo",
                "no_cast_screen", "No hay una pantalla cerca",
                "screen_cast", "Transmitir",
                "cast_on", "Activo");
        }

        private static void PackFr()
        {
            Pack("fr", "Français",
                "play", "Jouer",
                "quit", "Quitter",
                "menu", "Menu",
                "play_again", "Rejouer",
                "back", "Retour",
                "controls", "Commandes",
                "next", "Suivant",
                "rotate", "Tourner",
                "drop", "Lâcher",
                "flag", "Drapeau",
                "open", "Ouvrir",
                "high_score", "Record",
                "game_over", "Terminé",
                "best", "Record",
                "score", "Score",
                "look", "Apparence",
                "buttons", "Boutons",
                "language", "Langue",
                "reset", "Réinitialiser",
                "reset_look", "Réinitialiser l'apparence",
                "reset_buttons", "Réinitialiser les boutons",
                "system_language", "Langue du système",
                "app.pip.snake", "Serpent",
                "app.pip.tetris", "Stacker",
                "app.pip.breakout", "Briques",
                "app.pip.simon", "Écho",
                "app.pip.connect4", "Quatre",
                "app.pip.mines", "Mines",
                "app.pip.store", "Apps",
                "app.pip.settings", "Réglages",
                "app.pip.phone", "Téléphone",
                "app.pip.messages", "Messages",
                "hello", "BONJOUR !",
                "goodbye", "AU REVOIR !",
                "battery", "Batterie",
                "cannot_cast", "Impossible de diffuser sur cet appareil",
                "no_cast_screen", "Aucun écran à proximité",
                "screen_cast", "Diffusion",
                "cast_on", "Actif");
        }

        private static void Pack(string code, string name, params string[] kv)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i + 1 < kv.Length; i += 2)
            {
                if (!string.IsNullOrEmpty(kv[i]) && kv[i + 1] != null)
                    map[kv[i]] = kv[i + 1];
            }
            Packs[code] = new PiPhoneLanguage { Code = code, Name = name, Strings = map };
        }

        private static void WriteKeyList()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, "_keys.txt");
                var lines = new List<string>();
                lines.Add("# PiPhone language packs: drop es.txt, de.txt, … here.");
                lines.Add("# name=Español");
                lines.Add("# code=es");
                lines.Add("# play=Jugar");
                lines.Add("# Mods can also call PiPhoneApi.RegisterLanguage.");
                lines.Add("# Keys (English defaults):");
                var keys = new List<string>(EnglishMap.Keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < keys.Count; i++)
                    lines.Add(keys[i] + "=" + EnglishMap[keys[i]]);
                File.WriteAllLines(path, lines.ToArray());
            }
            catch
            {
            }
        }
    }

    public sealed class PiPhoneLanguage
    {
        public string Code;
        public string Name;
        public Dictionary<string, string> Strings;
    }
}
