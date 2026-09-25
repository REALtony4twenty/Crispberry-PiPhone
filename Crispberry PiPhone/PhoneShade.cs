using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Quick-settings (shade) toolbar. Built-in tiles plus extras from
    /// <see cref="PiPhoneApi.RegisterShadeButton"/>. Players hide tiles in Settings.
    /// </summary>
    internal static class PhoneShade
    {
        internal const string LockId = "pip.shade.lock";
        internal const string SizeId = "pip.shade.size";
        internal const string DndId = "pip.shade.dnd";
        internal const string RingerId = "pip.shade.ringer";
        internal const string RotateId = "pip.shade.rotate";
        internal const string HomeId = "pip.shade.home";
        internal const string NavId = "pip.shade.nav";
        internal const string SettingsId = "pip.shade.settings";
        internal const string CastId = "pip.shade.cast";
        internal const string AlertPlaceId = "pip.shade.alertpos";

        internal const float Chip = 36f;
        internal const float MediaChip = 32f;

        internal static bool SizeOpen;

        private static readonly List<PiPhoneShadeButton> Buttons = new List<PiPhoneShadeButton>(12);
        private static Sprite _lockOn;
        private static Sprite _lockOff;
        private static Sprite _sizeIcon;
        private static Sprite _dndIcon;
        private static Sprite _homeIcon;
        private static Sprite _rotateIcon;
        private static Sprite _ringIcon;
        private static Sprite _vibIcon;
        private static Sprite _silentIcon;
        private static Sprite _playIcon;
        private static Sprite _pauseIcon;
        private static Sprite _nextIcon;
        private static Sprite _removeIcon;
        private static Sprite _uninstallIcon;

        internal static void Register(PiPhoneShadeButton button)
        {
            if (button == null || string.IsNullOrEmpty(button.Id))
                return;
            int existing = IndexOf(button.Id);
            if (existing >= 0)
                Buttons[existing] = button;
            else
                Buttons.Add(button);
            Sort();
        }

        internal static bool Unregister(string id)
        {
            int i = IndexOf(id);
            if (i < 0)
                return false;
            Buttons.RemoveAt(i);
            return true;
        }

        internal static PiPhoneShadeButton[] All()
        {
            Sort();
            return Buttons.ToArray();
        }

        internal static bool Visible(PiPhoneShadeButton button)
        {
            if (button == null)
                return false;
            if (button.Available != null)
            {
                try
                {
                    if (!button.Available())
                        return false;
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Shade button '" + button.Id + "' Available: " + ex.Message);
                    return false;
                }
            }
            if (!button.CanHide)
                return true;
            return PhoneTheme.ShadeButtonOn(button.Id, button.DefaultVisible);
        }

        internal static Sprite LockIcon(bool locked)
        {
            EnsureIcons();
            return locked ? _lockOn : _lockOff;
        }

        internal static Sprite SizeIcon()
        {
            EnsureIcons();
            return _sizeIcon;
        }

        internal static Sprite DndIcon()
        {
            EnsureIcons();
            return _dndIcon;
        }

        internal static Sprite HomeIcon()
        {
            EnsureIcons();
            return _homeIcon;
        }

        internal static Sprite RotateIcon()
        {
            EnsureIcons();
            return _rotateIcon;
        }

        internal static Sprite RingerIcon(int mode)
        {
            EnsureIcons();
            if (mode == 1)
                return _vibIcon;
            if (mode == 2)
                return _silentIcon;
            return _ringIcon;
        }

        internal static Sprite PlayIcon(bool playing)
        {
            EnsureIcons();
            return playing ? _pauseIcon : _playIcon;
        }

        internal static Sprite NextIcon()
        {
            EnsureIcons();
            return _nextIcon;
        }

        internal static Sprite RemoveIcon()
        {
            EnsureIcons();
            return _removeIcon;
        }

        internal static Sprite UninstallIcon()
        {
            EnsureIcons();
            return _uninstallIcon;
        }

        internal static int LastRows = 1;

        internal static void DrawTiles(Transform parent)
        {
            Sort();
            float limit = PhoneMenu.ToolbarInnerWidth();
            if (limit < Chip + 8f)
                limit = Chip + 8f;
            var wrap = new GameObject("Tiles", typeof(RectTransform));
            wrap.transform.SetParent(parent, false);
            var column = PhoneUi.AddVertical(wrap, 6f, new RectOffset(0, 0, 0, 0));
            column.childAlignment = TextAnchor.UpperLeft;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            Transform row = null;
            float x = 0f;
            int shown = 0;
            int rows = 0;
            for (int i = 0; i < Buttons.Count; i++)
            {
                PiPhoneShadeButton b = Buttons[i];
                if (!Visible(b))
                    continue;
                float w = b.UseText ? 76f : Chip;
                if (row == null || (x > 0f && x + w > limit))
                {
                    var rowGo = new GameObject("Row", typeof(RectTransform));
                    rowGo.transform.SetParent(wrap.transform, false);
                    PhoneUi.AddHorizontal(rowGo, 6f);
                    var h = rowGo.GetComponent<HorizontalLayoutGroup>();
                    h.childForceExpandWidth = false;
                    h.childForceExpandHeight = false;
                    h.childAlignment = TextAnchor.MiddleLeft;
                    h.childControlWidth = true;
                    h.childControlHeight = true;
                    PhoneUi.Size(rowGo, Chip + 2f);
                    row = rowGo.transform;
                    x = 0f;
                    rows++;
                }
                DrawChip(row, b);
                x += w + 6f;
                shown++;
            }
            LastRows = Mathf.Max(1, rows);
            PhoneUi.Size(wrap, LastRows * (Chip + 2f) + Mathf.Max(0, LastRows - 1) * 6f);
            if (shown == 0)
                PhoneUi.Size(wrap, 8f);
        }

        private static void DrawChip(Transform parent, PiPhoneShadeButton button)
        {
            bool lit = false;
            if (button.IsActive != null)
            {
                try
                {
                    lit = button.IsActive();
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Shade button '" + button.Id + "' IsActive: " + ex.Message);
                }
            }
            Sprite icon = button.UseText ? null : ResolveIcon(button);
            string glyph = ResolveGlyph(button);
            if (button.UseText && string.IsNullOrEmpty(glyph))
                glyph = button.Label;
            string id = button.Id;
            Vector2 size = button.UseText ? new Vector2(76f, Chip) : new Vector2(Chip, Chip);
            Button chip = PhoneUi.CreateIconChip(parent, glyph, icon, () => Click(id), lit, size);
            string tip = string.IsNullOrEmpty(button.Tooltip) ? button.Label : button.Tooltip;
            PhoneUi.SetTooltip(chip.gameObject, tip);
        }

        private static void Click(string id)
        {
            PiPhoneShadeButton button = Find(id);
            if (button == null || button.OnClick == null)
                return;
            try
            {
                button.OnClick();
            }
            catch (Exception ex)
            {
                Plugin.LogError("Shade button '" + id + "': " + ex.Message);
            }
        }

        private static Sprite ResolveIcon(PiPhoneShadeButton button)
        {
            if (button.IconFn != null)
            {
                try
                {
                    Sprite s = button.IconFn();
                    if (s != null)
                        return s;
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Shade button '" + button.Id + "' Icon: " + ex.Message);
                }
            }
            return button.Icon;
        }

        private static string ResolveGlyph(PiPhoneShadeButton button)
        {
            if (button.GlyphFn != null)
            {
                try
                {
                    string g = button.GlyphFn();
                    if (!string.IsNullOrEmpty(g))
                        return g;
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Shade button '" + button.Id + "' Glyph: " + ex.Message);
                }
            }
            return button.Glyph;
        }

        private static PiPhoneShadeButton Find(string id)
        {
            int i = IndexOf(id);
            return i < 0 ? null : Buttons[i];
        }

        private static int IndexOf(string id)
        {
            for (int i = 0; i < Buttons.Count; i++)
            {
                if (Buttons[i] != null && string.Equals(Buttons[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static void Sort()
        {
            Buttons.Sort(Compare);
        }

        private static int Compare(PiPhoneShadeButton a, PiPhoneShadeButton b)
        {
            int ao = a != null ? a.SortOrder : 0;
            int bo = b != null ? b.SortOrder : 0;
            if (ao != bo)
                return ao.CompareTo(bo);
            string an = a != null ? a.Id : string.Empty;
            string bn = b != null ? b.Id : string.Empty;
            return string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
        }

        internal static void ForgetIcons()
        {
            _lockOn = null;
        }

        private static void EnsureIcons()
        {
            if (_lockOn != null)
                return;
            _lockOn = PhoneIcons.Material("lock");
            _lockOff = PhoneIcons.Material("lock_open");
            _sizeIcon = PhoneIcons.Material("aspect_ratio");
            _dndIcon = PhoneIcons.Material("dnd");
            _homeIcon = PhoneIcons.Material("home");
            _rotateIcon = PhoneIcons.Material("screen_rotation");
            _ringIcon = PhoneIcons.Material("volume_up");
            _silentIcon = PhoneIcons.Material("volume_off");
            _vibIcon = PhoneIcons.Material("vibration");
            _playIcon = PhoneIcons.Material("play");
            _pauseIcon = PhoneIcons.Material("pause");
            _nextIcon = PhoneIcons.Material("skip_next");
            _removeIcon = PhoneIcons.Material("remove_circle");
            _uninstallIcon = PhoneIcons.Material("delete");
        }
    }
}

