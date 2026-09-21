using Photon.Pun;
using System;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    internal static class BuiltinApps
    {
        internal const string PhoneId = "pip.phone";
        internal const string MessagesId = "pip.messages";
        internal const string VoicemailId = "pip.voicemail";
        internal const string NotesId = "pip.notes";
        internal const string CameraId = "pip.camera";
        internal const string ClosetId = "pip.closet";
        internal const string PhotosId = "pip.photos";
        internal const string VoiceMemosId = "pip.voicememos";
        internal const string SnakeId = "pip.snake";
        internal const string Game2048Id = "pip.2048";
        internal const string MinesId = "pip.mines";
        internal const string SimonId = "pip.simon";
        internal const string TetrisId = "pip.tetris";
        internal const string BreakoutId = "pip.breakout";
        internal const string Connect4Id = "pip.connect4";
        internal const string SudokuId = "pip.sudoku";
        internal const string SolitaireId = "pip.solitaire";
        internal const string SoundsId = "pip.sounds";
        internal const string MakeNotiId = "pip.makenoti";
        internal const string SettingsId = "pip.settings";
        internal const string StoreId = "pip.store";

        internal static void Register()
        {
            DialerApp.Register();
            MessagesApp.Register();
            VoicemailApp.Register();
            NotesApp.Register();
            CameraApp.Register();
            ClosetApp.Register();
            PhotosApp.Register();
            VoiceMemosApp.Register();
            SoundsApp.Register();
            MakeNotiApp.Register();
            AppStoreApp.Register();
            SnakeApp.Register();
            Game2048App.Register();
            MinesweeperApp.Register();
            SimonApp.Register();
            TetrisApp.Register();
            BreakoutApp.Register();
            Connect4App.Register();
            SudokuApp.Register();
            SolitaireApp.Register();
            SettingsApp.Register();
            PhoneNumbers.RegisterBuiltins();
            ApplyBuiltinFlags();
            PhoneIcons.BindBuiltins();
            PhoneStore.DefaultsReady = true;
            PhoneStore.EnsureInstalledDefaults();
            PhoneStore.EnsureHomeDefaults();
            PhoneMenu.OnAppsChanged();
        }

        private static void ApplyBuiltinFlags()
        {
            string[] pre = { PhoneId, MessagesId, VoicemailId, NotesId, CameraId, ClosetId, PhotosId, VoiceMemosId, SoundsId, MakeNotiId, SnakeId, Connect4Id, SudokuId, SolitaireId, SettingsId, StoreId };
            string[] extra = { Game2048Id, MinesId, SimonId, TetrisId, BreakoutId };
            string[] games = { SnakeId, Game2048Id, MinesId, SimonId, TetrisId, BreakoutId, Connect4Id, SudokuId, SolitaireId };
            string[] sticky = { PhoneId, MessagesId, SettingsId, StoreId };
            var apps = PiPhoneApi.Apps;
            for (int i = 0; i < apps.Count; i++)
            {
                PiPhoneApp app = apps[i];
                if (app == null || string.IsNullOrEmpty(app.Id))
                    continue;
                if (ContainsId(games, app.Id))
                    app.PostsNotices = false;
                if (ContainsId(sticky, app.Id))
                    app.Sticky = true;
                if (ContainsId(extra, app.Id))
                {
                    app.Preinstalled = false;
                    app.ShowOnHome = false;
                    app.ListedInStore = true;
                }
                else if (ContainsId(pre, app.Id))
                {
                    app.Preinstalled = true;
                    if (app.Id != StoreId)
                        app.ListedInStore = true;
                }
            }
        }

        private static bool ContainsId(string[] ids, string id)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        internal static string LocalId()
        {
            try
            {
                if (PhotonNetwork.LocalPlayer != null)
                    return PlayerId(PhotonNetwork.LocalPlayer);
            }
            catch
            {
            }
            return "local";
        }

        internal static string LocalName()
        {
            try
            {
                if (PhotonNetwork.LocalPlayer != null)
                    return PlayerName(PhotonNetwork.LocalPlayer);
            }
            catch
            {
            }
            return "You";
        }

        internal static string PlayerId(PhotonPlayer player)
        {
            if (player == null)
                return "unknown";
            if (!string.IsNullOrEmpty(player.UserId))
                return player.UserId;
            return "actor:" + player.ActorNumber;
        }

        internal static string PlayerName(PhotonPlayer player)
        {
            if (player == null)
                return "Unknown";
            if (string.IsNullOrEmpty(player.NickName))
                return "Scout " + player.ActorNumber;
            return player.NickName;
        }

        internal static string ContactName(PhotonPlayer player)
        {
            if (player == null)
                return "Scout";
            string id = PlayerId(player);
            string real = PlayerName(player);
            PhoneContacts.See(id, real);
            return PhoneContacts.Display(id, real);
        }

        internal static PhotonPlayer[] RoomPlayers()
        {
            try
            {
                if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null)
                    return PhotonNetwork.PlayerList;
            }
            catch
            {
            }
            return new PhotonPlayer[0];
        }

        internal static PhotonPlayer[] OtherPlayers()
        {
            PhotonPlayer[] all = RoomPlayers();
            var list = new System.Collections.Generic.List<PhotonPlayer>(all.Length);
            int me = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ActorNumber != me)
                    list.Add(all[i]);
            }
            return list.ToArray();
        }

        internal static PhotonPlayer FindByActor(int actor)
        {
            PhotonPlayer[] all = RoomPlayers();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ActorNumber == actor)
                    return all[i];
            }
            return null;
        }

        internal static PhotonPlayer FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            PhotonPlayer[] all = RoomPlayers();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && PlayerId(all[i]) == id)
                    return all[i];
            }
            return null;
        }
    }
}
