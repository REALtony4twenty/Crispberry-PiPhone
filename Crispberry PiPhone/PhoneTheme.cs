using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class PhoneTheme
    {
        public static event Action Changed;

        public const float MinBrightness = 0.10f;

        public static float Brightness = 1f;
        public static float RingVolume = 0.7f;
        public static float MusicVolume = 0.7f;
        public static bool HideDock;
        public static float PhoneScale = 1f;
        public static float PhoneScaleLand = 1.2f;

        /// <summary>While a mod has overridden placement, size and position changes stay in memory.</summary>
        internal static bool HoldSavedPlacement;
        public static float PhonePosX;
        public static float PhonePosY;
        public static float PhonePosLandX;
        public static float PhonePosLandY;
        public static int RingerMode;
        public static bool DoNotDisturb;
        public static bool AutoAnswer;
        public static bool Clock24Hour;
        public static bool UsePeakTime;
        public static bool HideDate;
        public static string WallpaperFile = string.Empty;
        public static int SnakeHigh;
        public static int High2048;
        public static int HighMines;
        public static float HighMinesTime;
        public static float AlertX;
        public static float AlertY = 28f;
        public static int HighSimon;
        public static int HighTetris;
        public static int HighBreakout;
        public static int HighSudoku;
        public static int HighSolitaire;
        public static KeyCode SnakeUp = KeyCode.W;
        public static KeyCode SnakeDown = KeyCode.S;
        public static KeyCode SnakeLeft = KeyCode.A;
        public static KeyCode SnakeRight = KeyCode.D;
        public static string MutedApps = string.Empty;
        public static string RingtoneId = string.Empty;
        public static string TextToneId = string.Empty;
        public static string NotifyToneId = string.Empty;
        public static bool PositionLocked;
        public static string HiddenShadeIds = string.Empty;
        public static string ShownShadeIds = string.Empty;
        /// <summary>Info logs. Leave true while testing; set this default false for release.</summary>
        public static bool WriteLogs = true;
        public static bool NavRotateButton = true;
        public static bool NavBarOn = true;
        public static bool FilledIcons = true;
        public static float NavHandleX;
        /// <summary>0 bottom, 1 left, 2 right. The top edge stays clear of the status bar.</summary>
        public static int NavEdge;

        public static Color CaseColor = new Color(0.07f, 0.07f, 0.08f, 1f);
        public static Color ClockColor = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color ButtonFillColor = new Color(0.20f, 0.22f, 0.26f, 1f);
        public static Color ButtonFontColor = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color ScreenColor = new Color(0.08f, 0.10f, 0.13f, 1f);
        public static Color SurfaceColor = new Color(0.16f, 0.18f, 0.21f, 0.96f);
        public static Color NavColor = new Color(0.10f, 0.11f, 0.13f, 1f);
        public static Color NavButtonColor = new Color(0.20f, 0.22f, 0.26f, 1f);
        public static Color NavIconColor = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color TextColor = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static Color AccentColor = new Color(0.24f, 0.86f, 0.52f, 1f);
        public static Color IconColor = Color.white;
        public static float FontScale = 1f;
        public static int ButtonRadius = 18;
        public static int IconRadius = 20;
        public static string Language = string.Empty;

        public static readonly Color DefaultCase = new Color(0.07f, 0.07f, 0.08f, 1f);
        public static readonly Color DefaultClock = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static readonly Color DefaultButtonFill = new Color(0.20f, 0.22f, 0.26f, 1f);
        public static readonly Color DefaultButtonFont = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static readonly Color DefaultScreen = new Color(0.08f, 0.10f, 0.13f, 1f);
        public static readonly Color DefaultSurface = new Color(0.16f, 0.18f, 0.21f, 0.96f);
        public static readonly Color DefaultNav = new Color(0.10f, 0.11f, 0.13f, 1f);
        public static readonly Color DefaultNavButton = new Color(0.20f, 0.22f, 0.26f, 1f);
        public static readonly Color DefaultNavIcon = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static readonly Color DefaultText = new Color(0.96f, 0.97f, 0.98f, 1f);
        public static readonly Color DefaultAccent = new Color(0.24f, 0.86f, 0.52f, 1f);
        public static readonly Color DefaultIcon = Color.white;
        public const float DefaultFontScale = 1f;
        public const int DefaultButtonRadius = 18;
        public const int DefaultIconRadius = 20;

        public static void Load()
        {
            string path = Path.Combine(PhoneStore.RootDir, "theme.txt");
            if (!File.Exists(path))
            {
                Apply();
                return;
            }
            try
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    int n;
                    float f;
                    int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                    float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
                    if (key == "brightness") Brightness = ClampBright(f);
                    else if (key == "bright" && val.IndexOf('.') >= 0) Brightness = Mathf.Clamp01(f);
                    else if (key == "ringf") RingVolume = Mathf.Clamp01(f);
                    else if (key == "ring" && val.IndexOf('.') >= 0) RingVolume = Mathf.Clamp01(f);
                    else if (key == "ring") RingVolume = Mathf.Clamp01(n / 10f);
                    else if (key == "musicvol") MusicVolume = Mathf.Clamp01(f);
                    else if (key == "hidedock") HideDock = n != 0 || val == "true";
                    else if (key == "clock24") Clock24Hour = n != 0 || val == "true";
                    else if (key == "peak") UsePeakTime = n != 0 || val == "true";
                    else if (key == "hidedate") HideDate = n != 0 || val == "true";
                    else if (key == "wallpaper") WallpaperFile = val ?? string.Empty;
                    else if (key == "casec") CaseColor = ParseColor(val, CaseColor);
                    else if (key == "clockc") ClockColor = ParseColor(val, ClockColor);
                    else if (key == "btnfill") ButtonFillColor = ParseColor(val, ButtonFillColor);
                    else if (key == "btnfont") ButtonFontColor = ParseColor(val, ButtonFontColor);
                    else if (key == "screenc") ScreenColor = ParseColor(val, ScreenColor);
                    else if (key == "surfacec") SurfaceColor = ParseColor(val, SurfaceColor);
                    else if (key == "navc") NavColor = ParseColor(val, NavColor);
                    else if (key == "navbtn") NavButtonColor = ParseColor(val, NavButtonColor);
                    else if (key == "navicon") NavIconColor = ParseColor(val, NavIconColor);
                    else if (key == "textc") TextColor = ParseColor(val, TextColor);
                    else if (key == "accentc") AccentColor = ParseColor(val, AccentColor);
                    else if (key == "iconc") IconColor = ParseColor(val, IconColor);
                    else if (key == "fontscale") FontScale = Mathf.Clamp(f, 0.7f, 1.6f);
                    else if (key == "btnradius") ButtonRadius = Clamp(n, 0, 28);
                    else if (key == "iconradius") IconRadius = Clamp(n, 0, 28);
                    else if (key == "lang") Language = val ?? string.Empty;
                    else if (key == "case" && val.IndexOf(',') < 0) CaseColor = PaletteColor(n, CaseColor);
                    else if (key == "font" && val.IndexOf(',') < 0) ClockColor = PaletteColor(n, ClockColor);
                    else if (key == "button" && val.IndexOf(',') < 0) ButtonFillColor = PaletteColor(n, ButtonFillColor);
                    else if (key == "scale") PhoneScale = Mathf.Clamp(f, 0.55f, 1.35f);
                    else if (key == "scaleland") PhoneScaleLand = Mathf.Clamp(f, 0.55f, 1.8f);
                    else if (key == "posx") PhonePosX = f;
                    else if (key == "posy") PhonePosY = f;
                    else if (key == "poslx") PhonePosLandX = f;
                    else if (key == "posly") PhonePosLandY = f;
                    else if (key == "ringer") RingerMode = Clamp(n, 0, 2);
                    else if (key == "dnd") DoNotDisturb = n != 0 || val == "true";
                    else if (key == "autoanswer") AutoAnswer = n != 0 || val == "true";
                    else if (key == "snakehigh") SnakeHigh = n < 0 ? 0 : n;
                    else if (key == "high2048") High2048 = n < 0 ? 0 : n;
                    else if (key == "highmines") HighMines = n < 0 ? 0 : n;
                    else if (key == "highminestime") HighMinesTime = f < 0f ? 0f : f;
                    else if (key == "alertx") AlertX = f;
                    else if (key == "alerty") AlertY = f;
                    else if (key == "highsimon") HighSimon = n < 0 ? 0 : n;
                    else if (key == "hightetris") HighTetris = n < 0 ? 0 : n;
                    else if (key == "highbreak") HighBreakout = n < 0 ? 0 : n;
                    else if (key == "highsudoku") HighSudoku = n < 0 ? 0 : n;
                    else if (key == "highsol") HighSolitaire = n < 0 ? 0 : n;
                    else if (key == "snakeu") SnakeUp = ParseKey(val, KeyCode.W);
                    else if (key == "snaked") SnakeDown = ParseKey(val, KeyCode.S);
                    else if (key == "snakel") SnakeLeft = ParseKey(val, KeyCode.A);
                    else if (key == "snaker") SnakeRight = ParseKey(val, KeyCode.D);
                    else if (key == "muted") MutedApps = val ?? string.Empty;
                    else if (key == "ringtone") RingtoneId = val ?? string.Empty;
                    else if (key == "texttone") TextToneId = val ?? string.Empty;
                    else if (key == "notifytone") NotifyToneId = val ?? string.Empty;
                    else if (key == "poslock") PositionLocked = n != 0 || val == "true";
                    else if (key == "shadeoff") HiddenShadeIds = val ?? string.Empty;
                    else if (key == "shadeon") ShownShadeIds = val ?? string.Empty;
                    else if (key == "logs") WriteLogs = n != 0 || val == "true";
                    else if (key == "navrot") NavRotateButton = n != 0 || val == "true";
                    else if (key == "navon") NavBarOn = n != 0 || val == "true";
                    else if (key == "filled") FilledIcons = n != 0 || val == "true";
                    else if (key == "navhx") NavHandleX = f;
                    else if (key == "navedge") NavEdge = n == 1 || n == 2 ? n : 0;
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Theme load failed: " + ex.Message);
            }
            Apply();
        }

        public static void Save()
        {
            try
            {
                PhoneStore.EnsureDir();
                File.WriteAllText(Path.Combine(PhoneStore.RootDir, "theme.txt"),
                    "brightness=" + Brightness.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "ringf=" + RingVolume.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "musicvol=" + MusicVolume.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "hidedock=" + (HideDock ? "1" : "0") + "\n"
                    + "clock24=" + (Clock24Hour ? "1" : "0") + "\n"
                    + "peak=" + (UsePeakTime ? "1" : "0") + "\n"
                    + "hidedate=" + (HideDate ? "1" : "0") + "\n"
                    + "wallpaper=" + (WallpaperFile ?? string.Empty) + "\n"
                    + "casec=" + Fmt(CaseColor) + "\n"
                    + "clockc=" + Fmt(ClockColor) + "\n"
                    + "btnfill=" + Fmt(ButtonFillColor) + "\n"
                    + "btnfont=" + Fmt(ButtonFontColor) + "\n"
                    + "screenc=" + Fmt(ScreenColor) + "\n"
                    + "surfacec=" + Fmt(SurfaceColor) + "\n"
                    + "navc=" + Fmt(NavColor) + "\n"
                    + "navbtn=" + Fmt(NavButtonColor) + "\n"
                    + "navicon=" + Fmt(NavIconColor) + "\n"
                    + "textc=" + Fmt(TextColor) + "\n"
                    + "accentc=" + Fmt(AccentColor) + "\n"
                    + "iconc=" + Fmt(IconColor) + "\n"
                    + "fontscale=" + FontScale.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "btnradius=" + ButtonRadius + "\n"
                    + "iconradius=" + IconRadius + "\n"
                    + "lang=" + (Language ?? string.Empty) + "\n"
                    + "scale=" + PhoneScale.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "scaleland=" + PhoneScaleLand.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "posx=" + PhonePosX.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "posy=" + PhonePosY.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "poslx=" + PhonePosLandX.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "posly=" + PhonePosLandY.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "ringer=" + RingerMode + "\n"
                    + "dnd=" + (DoNotDisturb ? "1" : "0") + "\n"
                    + "autoanswer=" + (AutoAnswer ? "1" : "0") + "\n"
                    + "snakehigh=" + SnakeHigh + "\n"
                    + "high2048=" + High2048 + "\n"
                    + "highmines=" + HighMines + "\n"
                    + "highminestime=" + HighMinesTime.ToString("0.###", CultureInfo.InvariantCulture) + "\n"
                    + "alertx=" + AlertX.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "alerty=" + AlertY.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "highsimon=" + HighSimon + "\n"
                    + "hightetris=" + HighTetris + "\n"
                    + "highbreak=" + HighBreakout + "\n"
                    + "highsudoku=" + HighSudoku + "\n"
                    + "highsol=" + HighSolitaire + "\n"
                    + "snakeu=" + SnakeUp + "\n"
                    + "snaked=" + SnakeDown + "\n"
                    + "snakel=" + SnakeLeft + "\n"
                    + "snaker=" + SnakeRight + "\n"
                    + "muted=" + (MutedApps ?? string.Empty) + "\n"
                    + "ringtone=" + (RingtoneId ?? string.Empty) + "\n"
                    + "texttone=" + (TextToneId ?? string.Empty) + "\n"
                    + "notifytone=" + (NotifyToneId ?? string.Empty) + "\n"
                    + "poslock=" + (PositionLocked ? "1" : "0") + "\n"
                    + "shadeoff=" + (HiddenShadeIds ?? string.Empty) + "\n"
                    + "shadeon=" + (ShownShadeIds ?? string.Empty) + "\n"
                    + "logs=" + (WriteLogs ? "1" : "0") + "\n"
                    + "navrot=" + (NavRotateButton ? "1" : "0") + "\n"
                    + "navon=" + (NavBarOn ? "1" : "0") + "\n"
                    + "filled=" + (FilledIcons ? "1" : "0") + "\n"
                    + "navhx=" + NavHandleX.ToString("0.#", CultureInfo.InvariantCulture) + "\n"
                    + "navedge=" + NavEdge + "\n");
            }
            catch (Exception ex)
            {
                Plugin.LogError("Theme save failed: " + ex.Message);
            }
        }

        public static void SetBrightness(float value)
        {
            Brightness = ClampBright(value);
            Apply();
            Save();
            PhoneMenu.RefreshLiveChrome();
        }

        public static void SetPhoneScale(float value)
        {
            PhoneScale = Mathf.Clamp(value, 0.55f, 1.35f);
            if (!HoldSavedPlacement)
                Save();
            PhoneMenu.RefreshLiveChrome();
        }

        public static void SetPhoneScaleLand(float value)
        {
            PhoneScaleLand = Mathf.Clamp(value, 0.55f, 1.8f);
            if (!HoldSavedPlacement)
                Save();
            PhoneMenu.RefreshLiveChrome();
        }

        public static void SetPhonePos(float x, float y)
        {
            PhonePosX = x;
            PhonePosY = y;
            if (!HoldSavedPlacement)
                Save();
        }

        public static void SetPhonePosLand(float x, float y)
        {
            PhonePosLandX = x;
            PhonePosLandY = y;
            if (!HoldSavedPlacement)
                Save();
        }

        public static void SetAutoAnswer(bool on)
        {
            AutoAnswer = on;
            Commit();
        }

        public static void CycleRinger()
        {
            RingerMode = (RingerMode + 1) % 3;
            Commit();
        }

        public static void SetDoNotDisturb(bool on)
        {
            DoNotDisturb = on;
            Commit();
        }

        public static void SetPositionLocked(bool on)
        {
            PositionLocked = on;
            Save();
        }

        public static void SetWriteLogs(bool on)
        {
            WriteLogs = on;
            Save();
        }

        public static bool ShadeButtonOn(string id, bool defaultOn)
        {
            if (string.IsNullOrEmpty(id))
                return defaultOn;
            if (InCsv(HiddenShadeIds, id))
                return false;
            if (InCsv(ShownShadeIds, id))
                return true;
            return defaultOn;
        }

        public static void SetShadeButtonOn(string id, bool on)
        {
            if (string.IsNullOrEmpty(id))
                return;
            HiddenShadeIds = CsvWithout(HiddenShadeIds, id);
            ShownShadeIds = CsvWithout(ShownShadeIds, id);
            if (on)
                ShownShadeIds = CsvAdd(ShownShadeIds, id);
            else
                HiddenShadeIds = CsvAdd(HiddenShadeIds, id);
            Commit();
        }

        private static bool InCsv(string csv, string id)
        {
            if (string.IsNullOrEmpty(csv) || string.IsNullOrEmpty(id))
                return false;
            return ("," + csv + ",").IndexOf("," + id + ",", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string CsvWithout(string csv, string id)
        {
            var list = new System.Collections.Generic.List<string>();
            string[] parts = (csv ?? string.Empty).Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && !string.Equals(parts[i], id, StringComparison.OrdinalIgnoreCase))
                    list.Add(parts[i]);
            }
            return string.Join(",", list.ToArray());
        }

        private static string CsvAdd(string csv, string id)
        {
            if (string.IsNullOrEmpty(id))
                return csv ?? string.Empty;
            if (InCsv(csv, id))
                return csv ?? string.Empty;
            if (string.IsNullOrEmpty(csv))
                return id;
            return csv + "," + id;
        }

        public static string RingerLabel()
        {
            if (RingerMode == 1) return "Vibrate";
            if (RingerMode == 2) return "Silent";
            return "Ringer";
        }

        public static float ClampBright(float value)
        {
            if (value < MinBrightness) return MinBrightness;
            if (value > 1f) return 1f;
            return value;
        }

        public static void SetRingVolume(float value)
        {
            RingVolume = Mathf.Clamp01(value);
            VoiceIo.ApplyVolume();
            Save();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            MusicPlayer.ApplyVolume();
            Save();
        }

        public static void SetNavRotateButton(bool on)
        {
            NavRotateButton = on;
            Commit();
        }

        public static void SetNavBar(bool on)
        {
            NavBarOn = on;
            Commit();
        }

        public static void SetFilledIcons(bool filled)
        {
            FilledIcons = filled;
            PhoneShade.ForgetIcons();
            Commit();
        }

        public static void RememberNavHandle(float x)
        {
            NavHandleX = x;
            Save();
        }

        public static void RememberNavPlace(int edge, float along)
        {
            NavEdge = edge == 1 || edge == 2 ? edge : 0;
            NavHandleX = along;
            Save();
        }

        public static void SetHideDock(bool hide)
        {
            HideDock = hide;
            Commit();
            PhoneMenu.OnAppsChanged();
        }

        public static void SetCaseColor(Color color)
        {
            CaseColor = color;
            Commit();
        }

        public static void SetClockColor(Color color)
        {
            ClockColor = color;
            Commit();
        }

        public static void SetButtonFill(Color color)
        {
            ButtonFillColor = color;
            Commit();
        }

        public static void SetButtonFont(Color color)
        {
            ButtonFontColor = color;
            Commit();
        }

        public static void SetScreenColor(Color color)
        {
            ScreenColor = color;
            Commit();
        }

        public static void SetSurfaceColor(Color color)
        {
            SurfaceColor = color;
            Commit();
        }

        public static void SetNavColor(Color color)
        {
            NavColor = color;
            Commit();
        }

        public static void SetNavButtonColor(Color color)
        {
            NavButtonColor = color;
            Commit();
        }

        public static void SetNavIconColor(Color color)
        {
            NavIconColor = color;
            Commit();
        }

        public static void SetTextColor(Color color)
        {
            TextColor = color;
            Commit();
        }

        public static void SetAccentColor(Color color)
        {
            AccentColor = color;
            Commit();
        }

        public static void SetIconColor(Color color)
        {
            IconColor = color;
            Commit();
        }

        public static void SetFontScale(float scale)
        {
            FontScale = Mathf.Clamp(scale, 0.7f, 1.6f);
            Commit();
        }

        public static void SetButtonRadius(int radius)
        {
            ButtonRadius = Clamp(radius, 0, 28);
            Commit();
        }

        public static void SetIconRadius(int radius)
        {
            IconRadius = Clamp(radius, 0, 28);
            Commit();
        }

        public static void ResetLook()
        {
            ScreenColor = DefaultScreen;
            SurfaceColor = DefaultSurface;
            NavColor = DefaultNav;
            NavButtonColor = DefaultNavButton;
            NavIconColor = DefaultNavIcon;
            TextColor = DefaultText;
            AccentColor = DefaultAccent;
            IconColor = DefaultIcon;
            FontScale = DefaultFontScale;
            FilledIcons = true;
            PhoneShade.ForgetIcons();
            Commit();
        }

        public static void ResetButtons()
        {
            ButtonFillColor = DefaultButtonFill;
            ButtonFontColor = DefaultButtonFont;
            ButtonRadius = DefaultButtonRadius;
            IconRadius = DefaultIconRadius;
            Commit();
        }

        public static void ResetCase()
        {
            CaseColor = DefaultCase;
            Commit();
        }

        public static void ResetClockColor()
        {
            ClockColor = DefaultClock;
            Commit();
        }

        public static void SetClock24Hour(bool on)
        {
            Clock24Hour = on;
            Commit();
        }

        public static void SetPeakTime(bool on)
        {
            UsePeakTime = on;
            Commit();
        }

        public static void SetHideDate(bool on)
        {
            HideDate = on;
            Commit();
        }

        public static bool AppNoticesOn(string appId)
        {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(MutedApps))
                return true;
            string needle = "," + appId + ",";
            return ("," + MutedApps + ",").IndexOf(needle, StringComparison.Ordinal) < 0;
        }

        public static void SetAppNotices(string appId, bool on)
        {
            if (string.IsNullOrEmpty(appId))
                return;
            var list = new System.Collections.Generic.List<string>();
            string[] parts = (MutedApps ?? string.Empty).Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && parts[i] != appId)
                    list.Add(parts[i]);
            }
            if (!on)
                list.Add(appId);
            MutedApps = string.Join(",", list.ToArray());
            Commit();
        }

        public static void SetTone(string kind, string soundId)
        {
            if (kind == "ringtone") RingtoneId = soundId ?? string.Empty;
            else if (kind == "text") TextToneId = soundId ?? string.Empty;
            else NotifyToneId = soundId ?? string.Empty;
            Commit();
        }

        public static string ToneName(string id)
        {
            SoundItem s = PhoneStore.FindSound(id);
            return s != null ? s.Name : "Default";
        }

        public static void SetWallpaper(string relativePath)
        {
            WallpaperFile = relativePath ?? string.Empty;
            Commit();
        }

        public static void Commit()
        {
            Apply();
            Save();
            Action handler = Changed;
            if (handler != null)
                handler();
        }

        public static void Apply()
        {
            Brightness = ClampBright(Brightness);
            PhoneUi.Bezel = CaseColor;
            PhoneUi.ClockText = ClockColor;
            PhoneUi.ButtonText = ButtonFontColor;
            PhoneUi.SurfaceAlt = ButtonFillColor;
            PhoneUi.Screen = ScreenColor;
            PhoneUi.Surface = SurfaceColor;
            PhoneUi.Nav = NavColor;
            PhoneUi.Text = TextColor;
            PhoneUi.Accent = AccentColor;
            PhoneUi.TextDim = Color.Lerp(TextColor, ScreenColor, 0.35f);
        }

        public static string FormatStatusClock()
        {
            string time = FormatTimeOnly();
            if (UsePeakTime || HideDate)
                return time;
            DateTime now = DateTime.Now;
            return time + "  " + now.ToString("ddd, MMM d", CultureInfo.InvariantCulture);
        }

        public static string FormatTimeOnly()
        {
            if (UsePeakTime)
                return FormatPeakTime(Clock24Hour);
            DateTime now = DateTime.Now;
            if (Clock24Hour)
                return now.ToString("HH:mm", CultureInfo.InvariantCulture);
            return now.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        public static string FormatPeakTime(bool clock24)
        {
            float tod = 12f;
            try
            {
                if (DayNightManager.instance != null)
                    tod = DayNightManager.instance.timeOfDay;
            }
            catch
            {
            }
            tod %= 24f;
            if (tod < 0f)
                tod += 24f;
            int hours = Mathf.FloorToInt(tod);
            int minutes = Mathf.FloorToInt((tod - hours) * 60f);
            if (minutes >= 60)
            {
                minutes = 0;
                hours = (hours + 1) % 24;
            }
            if (clock24)
                return hours.ToString("00") + ":" + minutes.ToString("00");
            string amPm = hours >= 12 ? "PM" : "AM";
            int h12 = hours % 12;
            if (h12 == 0)
                h12 = 12;
            return h12 + ":" + minutes.ToString("00") + " " + amPm;
        }

        private static Color PaletteColor(int index, Color fallback)
        {
            Color[] palette =
            {
                new Color(0.07f, 0.07f, 0.08f),
                new Color(0.92f, 0.93f, 0.95f),
                new Color(0.72f, 0.16f, 0.18f),
                new Color(0.16f, 0.38f, 0.78f),
                new Color(0.78f, 0.62f, 0.18f),
                new Color(0.82f, 0.38f, 0.55f),
                new Color(0.12f, 0.55f, 0.52f),
                new Color(0.42f, 0.44f, 0.48f)
            };
            if (index < 0 || index >= palette.Length)
                return fallback;
            return palette[index];
        }

        private static Color ParseColor(string s, Color fallback)
        {
            if (string.IsNullOrEmpty(s))
                return fallback;
            string[] p = s.Split(',');
            if (p.Length < 3)
                return fallback;
            float r, g, b, a = 1f;
            if (!float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out r))
                return fallback;
            if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out g))
                return fallback;
            if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b))
                return fallback;
            if (p.Length >= 4)
                float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a);
            return new Color(r, g, b, a);
        }

        private static string Fmt(Color c)
        {
            return c.r.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + c.g.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + c.b.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + c.a.ToString("0.###", CultureInfo.InvariantCulture);
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

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
