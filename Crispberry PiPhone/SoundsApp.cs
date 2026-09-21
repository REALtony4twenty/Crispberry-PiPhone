using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class SoundsApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.SoundsId,
                DisplayName = "Audio",
                IconGlyph = "A",
                IconBackground = new Color(0.72f, 0.32f, 0.48f, 1f),
                SortOrder = 55,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.ShowPlayer(); },
                OnClose = () => { _live = null; },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static bool TryGoBack()
        {
            return _live != null && _live.GoBack();
        }

        internal static void RefreshIfOpen()
        {
            if (_live != null)
                _live.RefreshPlayerIfOpen();
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _page = "player";
            private PlaylistItem _list;
            private TextMeshProUGUI _nowTitle;
            private TextMeshProUGUI _playLabel;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public bool GoBack()
            {
                if (_page == "player")
                    return false;
                if (_page == "plist")
                {
                    ShowLists();
                    return true;
                }
                ShowPlayer();
                return true;
            }

            public void RefreshPlayerIfOpen()
            {
                if (_page == "player")
                    ShowPlayer();
            }

            public void Relayout()
            {
                if (_page == "player")
                    ShowPlayer();
                else if (_page == "library")
                    ShowLibrary();
                else if (_page == "lists")
                    ShowLists();
                else if (_page == "plist" && _list != null)
                    ShowPlaylist(_list);
            }

            public void ShowPlayer()
            {
                _page = "player";
                Clear();
                _host.SetTitle("Audio");

                string name = MusicPlayer.HasTrack ? MusicPlayer.CurrentName : "Nothing playing";
                bool wide = _host.IsLandscape;
                Transform artParent = _host.Content;
                Transform infoParent = _host.Content;
                if (wide)
                {
                    RectTransform left;
                    RectTransform right;
                    PhoneUi.CreateSplit(_host.Content, out left, out right);
                    artParent = left;
                    infoParent = right;
                }

                _nowTitle = PhoneUi.CreateLabel(artParent, "Now", name, wide ? 18f : 20f, FontStyles.Normal, TextAlignmentOptions.Center);
                PhoneUi.Wrap(_nowTitle);
                PhoneUi.Size(_nowTitle.gameObject, wide ? 28f : 48f);

                float artSize = wide ? 96f : 180f;
                var art = PhoneUi.CreateImage(artParent, "Art", PhoneUi.Rounded(24), new Color(0.62f, 0.24f, 0.40f, 1f));
                PhoneUi.Size(art.gameObject, artSize, artSize);
                var glyph = PhoneUi.CreateLabel(art, "G", "♪", wide ? 36f : 64f, FontStyles.Normal, TextAlignmentOptions.Center);
                glyph.color = Color.white;

                var controls = new GameObject("Ctl", typeof(RectTransform));
                controls.transform.SetParent(wide ? infoParent : artParent, false);
                PhoneUi.Size(controls, 52f);
                PhoneUi.AddHorizontal(controls, 10f);
                var h = controls.GetComponent<HorizontalLayoutGroup>();
                h.childAlignment = TextAnchor.MiddleCenter;
                h.childForceExpandWidth = false;
                PhoneUi.CreateButton(controls.transform, "|<", MusicPlayer.Prev, new Vector2(56f, 44f));
                var play = PhoneUi.CreateIconChip(controls.transform, MusicPlayer.Playing ? "■" : ">", PhoneIcons.Material(MusicPlayer.Playing ? "stop" : "play"), null, false, new Vector2(56f, 44f));
                play.onClick.AddListener(() =>
                {
                    MusicPlayer.Toggle();
                    PaintPlay(play);
                });
                _playLabel = play.GetComponentInChildren<TextMeshProUGUI>(true);
                PhoneUi.CreateButton(controls.transform, ">|", MusicPlayer.Next, new Vector2(56f, 44f));

                PhoneUi.CreateSliderRow(infoParent, "Music", 0f, 1f, PhoneTheme.MusicVolume, v =>
                {
                    PhoneTheme.SetMusicVolume(v);
                    MusicPlayer.ApplyVolume();
                });

                if (wide)
                {
                    var nextTitle = PhoneUi.CreateLabel(infoParent, "UpNextT", "Up next", 14f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    nextTitle.color = PhoneUi.TextDim;
                    PhoneUi.Size(nextTitle.gameObject, 22f);
                    var next = PhoneUi.CreateLabel(infoParent, "UpNext", MusicPlayer.UpcomingLine, 16f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    PhoneUi.Wrap(next);
                    PhoneUi.Size(next.gameObject, 56f);
                }

                PhoneUi.CreateButton(infoParent, "Library", ShowLibrary, new Vector2(280f, 40f));
                PhoneUi.CreateButton(infoParent, "Playlists", ShowLists, new Vector2(280f, 40f));
            }

            private static void PaintPlay(Button play)
            {
                if (play == null)
                    return;
                Transform art = play.transform.Find("I");
                if (art != null)
                {
                    var img = art.GetComponent<Image>();
                    if (img != null)
                    {
                        Sprite sprite = PhoneIcons.Material(MusicPlayer.Playing ? "stop" : "play");
                        if (sprite != null)
                            img.sprite = sprite;
                    }
                }
                var tmp = play.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = MusicPlayer.Playing ? "■" : ">";
            }

            private void ShowLibrary()
            {
                _page = "library";
                Clear();
                _host.SetTitle("Library");
                PhoneUi.CreateButton(_host.Content, "Add song", AddMusic, new Vector2(220f, 40f));
                FillSoundRows(PhoneStore.MusicTracks(), id =>
                {
                    MusicPlayer.PlayLibrary(id);
                    ShowPlayer();
                });
            }

            private void ShowLists()
            {
                _page = "lists";
                _list = null;
                Clear();
                _host.SetTitle("Playlists");
                PhoneUi.CreateButton(_host.Content, "New playlist", () =>
                {
                    PlaylistItem created = PhoneStore.AddPlaylist("Playlist " + (PhoneStore.Playlists.Count + 1));
                    ShowPlaylist(created);
                }, new Vector2(220f, 40f));
                if (PhoneStore.Playlists.Count == 0)
                {
                    Empty("No playlists yet.");
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                for (int i = 0; i < PhoneStore.Playlists.Count; i++)
                {
                    PlaylistItem p = PhoneStore.Playlists[i];
                    if (p == null)
                        continue;
                    PlaylistItem captured = p;
                    var row = new GameObject("P", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, captured.Name, () => ShowPlaylist(captured), new Vector2(200f, 36f));
                    PhoneUi.CreateButton(row.transform, "Del", () =>
                    {
                        PhoneStore.DeletePlaylist(captured.Id);
                        ShowLists();
                    }, new Vector2(56f, 36f));
                }
            }

            private void ShowPlaylist(PlaylistItem list)
            {
                _page = "plist";
                _list = list;
                Clear();
                _host.SetTitle(list.Name);
                PhoneUi.CreateButton(_host.Content, "Play", () =>
                {
                    if (list.TrackIds.Count == 0)
                    {
                        _host.ShowToast("Add songs first.");
                        return;
                    }
                    MusicPlayer.PlayPlaylist(list, list.TrackIds[0]);
                    ShowPlayer();
                }, new Vector2(160f, 40f));
                PhoneUi.CreateButton(_host.Content, "Add from library", () => ShowAddToList(list), new Vector2(240f, 40f));
                if (list.TrackIds.Count == 0)
                {
                    Empty("Empty playlist.");
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                for (int i = 0; i < list.TrackIds.Count; i++)
                {
                    SoundItem s = PhoneStore.FindMusic(list.TrackIds[i]);
                    if (s == null)
                        continue;
                    SoundItem captured = s;
                    var row = new GameObject("T", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, captured.Name, () =>
                    {
                        MusicPlayer.PlayPlaylist(list, captured.Id);
                        ShowPlayer();
                    }, new Vector2(200f, 36f));
                    PhoneUi.CreateButton(row.transform, "X", () =>
                    {
                        PhoneStore.RemoveFromPlaylist(list.Id, captured.Id);
                        ShowPlaylist(list);
                    }, new Vector2(40f, 36f));
                }
            }

            private void ShowAddToList(PlaylistItem list)
            {
                _page = "plist";
                Clear();
                _host.SetTitle("Add songs");
                FillSoundRows(PhoneStore.MusicTracks(), id =>
                {
                    PhoneStore.AddToPlaylist(list.Id, id);
                    _host.ShowToast("Added.");
                    ShowPlaylist(list);
                });
            }

            private void FillSoundRows(List<SoundItem> items, System.Action<string> onPick)
            {
                if (items == null || items.Count == 0)
                {
                    Empty("Library is empty.");
                    return;
                }
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
                if (_host.IsLandscape)
                {
                    var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(380f, 40f);
                    grid.spacing = new Vector2(8f, 6f);
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 2;
                    grid.padding = new RectOffset(4, 4, 4, 4);
                    grid.childAlignment = TextAnchor.UpperLeft;
                }
                else
                    PhoneUi.AddVertical(content.gameObject, 6f, new RectOffset(4, 4, 4, 4));
                PhoneUi.FitVertical(content.gameObject);
                for (int i = 0; i < items.Count; i++)
                {
                    SoundItem s = items[i];
                    if (s == null)
                        continue;
                    SoundItem captured = s;
                    var row = new GameObject("S", typeof(RectTransform));
                    row.transform.SetParent(content, false);
                    PhoneUi.Size(row, 40f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, captured.Name, () => onPick(captured.Id), new Vector2(200f, 36f));
                    PhoneUi.CreateButton(row.transform, "Del", () =>
                    {
                        PhoneStore.DeleteSound(captured.Id);
                        ShowLibrary();
                    }, new Vector2(56f, 36f));
                }
            }

            private void AddMusic()
            {
                _host.StartHostCoroutine(AddRoutine());
            }

            private IEnumerator AddRoutine()
            {
                _host.ShowToast("Pick a file. Stay in the lobby.");
                string path = null;
                yield return PhoneSounds.PickAudioFile(p => path = p);
                if (string.IsNullOrEmpty(path))
                {
                    _host.ShowToast("No file selected.");
                    yield break;
                }
                SoundItem item = PhoneStore.AddSound(path, false, 0f);
                _host.ShowToast(item != null ? "Added " + item.Name + "." : "Couldn't add that file.");
                ShowLibrary();
            }

            private void Empty(string text)
            {
                var empty = PhoneUi.CreateLabel(_host.Content, "Empty", text, 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                empty.color = PhoneUi.TextDim;
                PhoneUi.Size(empty.gameObject, 32f);
            }

            private void Clear()
            {
                _nowTitle = null;
                _playLabel = null;
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
