using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Crispberry_PiPhone
{
    internal static class PhotosApp
    {
        private static Session _live;

        internal static void Register()
        {
            PiPhoneApi.RegisterApp(new PiPhoneApp
            {
                Id = BuiltinApps.PhotosId,
                DisplayName = "Photos",
                IconGlyph = "P",
                IconBackground = PhoneUi.PhotosIcon,
                SortOrder = 26,
                ShowOnHome = true,
                OnOpen = host => { _live = new Session(host); _live.ShowFolders(); },
                OnClose = () =>
                {
                    if (_live != null)
                        _live.StopPlayback();
                    _live = null;
                },
                OnOrientation = () => { if (_live != null) _live.Relayout(); }
            });
        }

        internal static bool TryGoBack()
        {
            if (_live == null)
                return false;
            return _live.GoBack();
        }

        private sealed class Session
        {
            private readonly IPiPhoneHost _host;
            private string _page = "folders";
            private bool _busy;
            private bool _selecting;
            private readonly HashSet<string> _picked = new HashSet<string>();
            private float _galleryNorm = 1f;
            private ScrollRect _galleryScroll;
            private Coroutine _play;
            private GameObject _playerHost;

            public Session(IPiPhoneHost host)
            {
                _host = host;
            }

            public void StopPlayback()
            {
                if (_playerHost != null)
                {
                    PhoneVideo.Stop(_playerHost);
                    _playerHost = null;
                }
                _play = null;
            }

            public void Relayout()
            {
                if (_page == "view")
                    return;
                ShowGallery();
            }

            public bool GoBack()
            {
                if (_selecting)
                {
                    _selecting = false;
                    _picked.Clear();
                    ShowGallery();
                    return true;
                }
                if (_page == "folders" || _page == "gallery")
                    return false;
                if (_page == "view")
                {
                    ShowGallery();
                    return true;
                }
                ShowGallery();
                return true;
            }

            private string _folder = "photos";

            public void ShowFolders()
            {
                if (string.IsNullOrEmpty(_folder))
                    _folder = "photos";
                ShowGallery();
            }

            private void ShowGallery()
            {
                if (_galleryScroll != null)
                    _galleryNorm = _galleryScroll.verticalNormalizedPosition;
                _page = "gallery";
                Clear();
                _host.SetTitle("Gallery");
                var tabs = PhoneUi.CreateImage(_host.Content, "Tabs", PhoneUi.Rounded(14), PhoneUi.Surface);
                PhoneUi.Size(tabs.gameObject, 36f);
                var tabRow = PhoneUi.AddHorizontal(tabs.gameObject, 4f);
                tabRow.padding = new RectOffset(4, 4, 3, 3);
                tabRow.childForceExpandWidth = false;
                Chip(tabs.transform, "Photos", "photos", "photo_library");
                Chip(tabs.transform, "Videos", "videos", "videocam");
                Chip(tabs.transform, "Files", "downloads", "folder");

                var tools = new GameObject("Tools", typeof(RectTransform));
                tools.transform.SetParent(_host.Content, false);
                PhoneUi.Size(tools, 36f);
                PhoneUi.AddHorizontal(tools, 6f);
                PhoneUi.CreateIconChip(tools.transform, "Trash", PhoneIcons.Material("delete"), PhoneTrash.Reveal, false, new Vector2(36f, 32f));
                var spacer = new GameObject("Pad", typeof(RectTransform));
                spacer.transform.SetParent(tools.transform, false);
                spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
                if (_selecting)
                {
                    PhoneUi.MaterialChip(tools.transform, "select_all", "All", SelectAllVisible, new Vector2(36f, 32f));
                    PhoneUi.MaterialChip(tools.transform, "close", "Cancel", () =>
                    {
                        _selecting = false;
                        _picked.Clear();
                        ShowGallery();
                    }, new Vector2(36f, 32f));
                    PhoneUi.CreateIconChip(tools.transform, "Delete", PhoneIcons.Material("delete"), DeletePicked, false, new Vector2(36f, 32f));
                }
                else
                {
                    PhoneUi.MaterialChip(tools.transform, "select", "Select", () =>
                    {
                        _selecting = true;
                        _picked.Clear();
                        ShowGallery();
                    }, new Vector2(36f, 32f));
                }

                if (_folder == "downloads")
                {
                    TMP_InputField input = PhoneUi.CreateInput(_host.Content, "Paste image, GIF, or video link", 2000);
                    var row = new GameObject("LinkRow", typeof(RectTransform));
                    row.transform.SetParent(_host.Content, false);
                    PhoneUi.Size(row, 36f);
                    PhoneUi.AddHorizontal(row, 6f);
                    PhoneUi.CreateButton(row.transform, "Paste", () =>
                    {
                        try
                        {
                            string clip = GUIUtility.systemCopyBuffer;
                            if (!string.IsNullOrEmpty(clip) && input != null)
                                input.text = clip.Trim();
                        }
                        catch
                        {
                        }
                    }, new Vector2(80f, 32f));
                    PhoneUi.MaterialChip(row.transform, "save", "Save", () => StartDownload(input), new Vector2(36f, 32f));
                }

                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                _galleryScroll = scroll;
                MakeGrid(content);
                int n = 0;
                if (_folder == "downloads")
                {
                    for (int i = 0; i < PhoneStore.Downloads.Count; i++)
                    {
                        DownloadItem item = PhoneStore.Downloads[i];
                        if (item == null || PhoneStore.IsAudioFile(item.File))
                            continue;
                        string path = PhoneStore.DownloadPath(item.File);
                        string wall = "downloads/" + item.File;
                        AddThumb(content, path, ExtBadge(item.File), "d:" + item.Id, () => ShowViewer(null, wall));
                        n++;
                    }
                }
                else
                {
                    bool videos = _folder == "videos";
                    for (int i = 0; i < PhoneStore.Photos.Count; i++)
                    {
                        PhotoItem item = PhoneStore.Photos[i];
                        if (item == null || item.Video != videos)
                            continue;
                        string path = item.Video ? PhoneStore.VideoThumbPath(item) : PhoneStore.PhotoPath(item.File);
                        PhotoItem captured = item;
                        AddThumb(content, path, item.Video ? "VID" : null, "p:" + item.Id, () => ShowViewer(captured, null));
                        n++;
                    }
                }
                if (n == 0)
                    EmptyHint();
                else
                    RestoreGalleryScroll();
            }

            private void RestoreGalleryScroll()
            {
                if (_galleryScroll == null)
                    return;
                Canvas.ForceUpdateCanvases();
                _galleryScroll.verticalNormalizedPosition = _galleryNorm;
                _host.StartHostCoroutine(RestoreGalleryScrollNext());
            }

            private IEnumerator RestoreGalleryScrollNext()
            {
                yield return null;
                if (_galleryScroll != null)
                    _galleryScroll.verticalNormalizedPosition = _galleryNorm;
            }

            private void SelectAllVisible()
            {
                if (_folder == "downloads")
                {
                    for (int i = 0; i < PhoneStore.Downloads.Count; i++)
                    {
                        DownloadItem item = PhoneStore.Downloads[i];
                        if (item == null || PhoneStore.IsAudioFile(item.File))
                            continue;
                        _picked.Add("d:" + item.Id);
                    }
                }
                else
                {
                    bool videos = _folder == "videos";
                    for (int i = 0; i < PhoneStore.Photos.Count; i++)
                    {
                        PhotoItem item = PhoneStore.Photos[i];
                        if (item == null || item.Video != videos)
                            continue;
                        _picked.Add("p:" + item.Id);
                    }
                }
                ShowGallery();
            }

            private void Chip(Transform parent, string label, string folder, string icon)
            {
                bool on = _folder == folder;
                PhoneUi.CreateIconChip(parent, label, PhoneIcons.Material(icon), () =>
                {
                    _folder = folder;
                    _galleryNorm = 1f;
                    ShowGallery();
                }, on, new Vector2(36f, 32f));
            }

            private static string ExtBadge(string file)
            {
                string ext = Path.GetExtension(file ?? string.Empty);
                if (string.IsNullOrEmpty(ext))
                    return null;
                return ext.TrimStart('.').ToUpperInvariant();
            }

            private void ShowCamera()
            {
                _folder = "photos";
                ShowGallery();
            }

            private void ShowDownloads()
            {
                _folder = "downloads";
                ShowGallery();
            }

            private void EmptyHint()
            {
                var empty = PhoneUi.CreateLabel(_host.Content, "Empty", "Nothing here yet.", 15f, FontStyles.Normal, TextAlignmentOptions.Center);
                empty.color = PhoneUi.TextDim;
                PhoneUi.Wrap(empty);
                PhoneUi.Size(empty.gameObject, 36f);
            }

            private void StartDownload(TMP_InputField input)
            {
                if (_busy)
                    return;
                string url = input != null ? input.text : string.Empty;
                if (string.IsNullOrEmpty(url))
                {
                    try { url = GUIUtility.systemCopyBuffer; } catch { }
                    if (input != null && !string.IsNullOrEmpty(url))
                        input.text = url.Trim();
                }
                _busy = true;
                _host.ShowToast("Downloading...");
                _host.StartHostCoroutine(PhoneImages.Download(url, (bytes, ext, error) =>
                {
                    _busy = false;
                    if (!string.IsNullOrEmpty(error) || bytes == null)
                    {
                        _host.ShowToast(string.IsNullOrEmpty(error) ? "Download failed." : error);
                        ShowDownloads();
                        return;
                    }
                    PhoneStore.AddDownload(bytes, ext, url);
                    _host.ShowToast("Saved to Downloads.");
                    ShowDownloads();
                }));
            }

            private void FillPhotoGrid()
            {
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                MakeGrid(content);
                for (int i = 0; i < PhoneStore.Photos.Count; i++)
                {
                    PhotoItem item = PhoneStore.Photos[i];
                    if (item == null)
                        continue;
                    string path = item.Video
                        ? PhoneStore.VideoThumbPath(item)
                        : PhoneStore.PhotoPath(item.File);
                    PhotoItem captured = item;
                    AddThumb(content, path, item.Video ? "VID" : null, "p:" + item.Id, () => ShowViewer(captured, null));
                }
            }

            private void FillDownloadGrid()
            {
                ScrollRect scroll = PhoneUi.CreateScrollView(_host.Content, out RectTransform content);
                var le = scroll.gameObject.AddComponent<LayoutElement>();
                le.flexibleHeight = 1f;
                MakeGrid(content);
                for (int i = 0; i < PhoneStore.Downloads.Count; i++)
                {
                    DownloadItem item = PhoneStore.Downloads[i];
                    if (item == null || PhoneStore.IsAudioFile(item.File))
                        continue;
                    string path = PhoneStore.DownloadPath(item.File);
                    string wall = "downloads/" + item.File;
                    string badge = PhoneVideo.IsVideoPath(path) ? "VID" : ExtBadge(item.File);
                    AddThumb(content, path, badge, "d:" + item.Id, () => ShowViewer(null, wall));
                }
            }

            private static void MakeGrid(RectTransform content)
            {
                PhoneUi.ApplyMediaGrid(content);
            }

            private void DeletePicked()
            {
                if (_picked.Count == 0)
                {
                    _host.ShowToast("Select items first.");
                    return;
                }
                string[] keys = new string[_picked.Count];
                _picked.CopyTo(keys);
                int n = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    string key = keys[i];
                    if (string.IsNullOrEmpty(key) || key.Length < 3)
                        continue;
                    if (key.StartsWith("p:", StringComparison.Ordinal))
                    {
                        PhoneStore.DeletePhoto(key.Substring(2));
                        n++;
                    }
                    else if (key.StartsWith("d:", StringComparison.Ordinal))
                    {
                        PhoneStore.DeleteDownload(key.Substring(2));
                        n++;
                    }
                }
                _picked.Clear();
                _selecting = false;
                _host.ShowToast(n == 1 ? "Moved to Trash." : n + " items moved to Trash.");
                ShowGallery();
            }

            private void AddThumb(Transform parent, string path, string badge, string key, UnityEngine.Events.UnityAction onClick)
            {
                bool picked = _selecting && !string.IsNullOrEmpty(key) && _picked.Contains(key);
                var thumb = PhoneUi.CreateImage(parent, "T", PhoneUi.Rounded(10), picked ? PhoneUi.Accent : PhoneUi.Surface);
                Texture2D tex = PhoneImages.LoadFile(path);
                if (tex != null)
                {
                    var rawGo = new GameObject("Raw", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    rawGo.transform.SetParent(thumb, false);
                    PhoneUi.Stretch(rawGo.GetComponent<RectTransform>(), 4f, 4f);
                    var raw = rawGo.GetComponent<RawImage>();
                    raw.texture = tex;
                    raw.color = Color.white;
                    raw.raycastTarget = false;
                    PhoneUi.FitContained(raw, tex);
                }
                else
                {
                    var label = PhoneUi.CreateLabel(thumb, "Miss", badge ?? "?", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    PhoneUi.Stretch(label.rectTransform, 4f, 4f);
                }
                if (!string.IsNullOrEmpty(badge) && tex != null)
                {
                    var tag = PhoneUi.CreateLabel(thumb, "Badge", badge, 11f, FontStyles.Normal, TextAlignmentOptions.BottomRight);
                    PhoneUi.Stretch(tag.rectTransform, 6f, 4f);
                }
                if (picked)
                {
                    var mark = PhoneUi.CreateLabel(thumb, "Pick", "✓", 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                    PhoneUi.Stretch(mark.rectTransform, 6f, 4f);
                    mark.color = new Color(0.10f, 0.11f, 0.12f, 1f);
                }
                var btn = thumb.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                if (_selecting && !string.IsNullOrEmpty(key))
                {
                    string captured = key;
                    btn.onClick.AddListener(() =>
                    {
                        if (_picked.Contains(captured))
                            _picked.Remove(captured);
                        else
                            _picked.Add(captured);
                        ShowGallery();
                    });
                }
                else if (onClick != null)
                    btn.onClick.AddListener(onClick);
            }

            private void ShowViewer(PhotoItem photo, string downloadWall)
            {
                _page = "view";
                Clear();
                _host.SetTitle(photo != null && photo.Video ? "Video" : (PhoneVideo.IsVideoPath(PhoneStore.MediaPath(downloadWall)) ? "Video" : "Photo"));
                PhoneUi.MaterialChip(_host.Content, "arrow_back", "Back", () => GoBack(), new Vector2(36f, 32f));

                var wrap = new GameObject("ViewWrap", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                wrap.transform.SetParent(_host.Content, false);
                var wrapImg = wrap.GetComponent<Image>();
                wrapImg.sprite = PhoneUi.White();
                wrapImg.color = new Color(0f, 0f, 0f, 1f);
                wrapImg.raycastTarget = false;
                var wrapLe = wrap.AddComponent<LayoutElement>();
                wrapLe.flexibleHeight = 1f;
                wrapLe.minHeight = 220f;

                var preview = new GameObject("View", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                preview.transform.SetParent(wrap.transform, false);
                PhoneUi.Stretch(preview.GetComponent<RectTransform>(), 8f, 8f);
                var raw = preview.GetComponent<RawImage>();
                raw.color = Color.white;
                raw.raycastTarget = false;

                string stillPath = photo != null
                    ? (photo.Video ? PhoneStore.VideoThumbPath(photo) : PhoneStore.PhotoPath(photo.File))
                    : PhoneStore.MediaPath(downloadWall);
                Texture2D tex = PhoneImages.LoadFile(stillPath);
                if (tex != null)
                {
                    raw.texture = tex;
                    PhoneUi.FitContained(raw, tex);
                }
                else if ((photo == null || !photo.Video) && !PhoneVideo.IsVideoPath(stillPath))
                {
                    var miss = PhoneUi.CreateLabel(_host.Content, "Miss", "Couldn't load this file.", 14f, FontStyles.Normal, TextAlignmentOptions.Center);
                    miss.color = PhoneUi.TextDim;
                    PhoneUi.Size(miss.gameObject, 32f);
                }

                if (photo != null && photo.Video)
                {
                    if (PhoneStore.IsLegacyFlipbook(photo) && photo.Frames > 1)
                        _play = _host.StartHostCoroutine(PlayFlipbook(photo, raw));
                    else
                    {
                        string vid = PhoneStore.VideoFilePath(photo);
                        _playerHost = preview;
                        _play = _host.StartHostCoroutine(PhoneVideo.PlayFile(preview, raw, vid));
                    }
                }
                else if (!string.IsNullOrEmpty(downloadWall))
                {
                    string gifPath = PhoneStore.MediaPath(downloadWall);
                    if (PhoneVideo.IsVideoPath(gifPath))
                    {
                        _playerHost = preview;
                        _play = _host.StartHostCoroutine(PhoneVideo.PlayFile(preview, raw, gifPath));
                    }
                    else if (!string.IsNullOrEmpty(gifPath) && gifPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) && File.Exists(gifPath))
                    {
                        PhoneGif.Clip clip = PhoneGif.Decode(File.ReadAllBytes(gifPath), 80);
                        if (clip != null && clip.Frames != null && clip.Frames.Length > 1)
                            _play = _host.StartHostCoroutine(PlayGif(clip, raw));
                    }
                }

                string wall = downloadWall;
                if (photo != null && !photo.Video)
                    wall = "photos/" + photo.File;
                string reveal = stillPath;
                if (photo != null && photo.Video)
                    reveal = PhoneStore.VideoFilePath(photo);
                if (!string.IsNullOrEmpty(reveal))
                {
                    string capturedPath = reveal;
                    PhoneUi.CreateButton(_host.Content, "Show in folder", () => PhoneStore.RevealFile(capturedPath), new Vector2(220f, 40f));
                }
                if (!string.IsNullOrEmpty(wall) && (photo == null || !photo.Video))
                {
                    PhoneUi.CreateButton(_host.Content, "Set as wallpaper", () =>
                    {
                        PhoneTheme.SetWallpaper(wall);
                        _host.ShowToast("Wallpaper set.");
                    }, new Vector2(220f, 40f));
                    if (!string.IsNullOrEmpty(PhoneTheme.WallpaperFile))
                    {
                        PhoneUi.CreateButton(_host.Content, "Use default background", () =>
                        {
                            PhoneTheme.SetWallpaper(string.Empty);
                            _host.ShowToast("Default background restored.");
                        }, new Vector2(220f, 40f));
                    }
                }
                if (photo != null)
                {
                    PhoneUi.CreateIconChip(_host.Content, "Delete", PhoneIcons.Material("delete"), () =>
                    {
                        PhoneVideo.Stop(preview);
                        PhoneStore.DeletePhoto(photo.Id);
                        _host.ShowToast("Moved to Trash.");
                        ShowGallery();
                    }, false, new Vector2(40f, 40f));
                }
                else if (!string.IsNullOrEmpty(downloadWall))
                {
                    DownloadItem found = null;
                    for (int i = 0; i < PhoneStore.Downloads.Count; i++)
                    {
                        if (PhoneStore.Downloads[i] != null && ("downloads/" + PhoneStore.Downloads[i].File) == downloadWall)
                            found = PhoneStore.Downloads[i];
                    }
                    if (found != null)
                    {
                        DownloadItem captured = found;
                        PhoneUi.CreateIconChip(_host.Content, "Delete", PhoneIcons.Material("delete"), () =>
                        {
                            PhoneStore.DeleteDownload(captured.Id);
                            _host.ShowToast("Moved to Trash.");
                            ShowDownloads();
                        }, false, new Vector2(40f, 40f));
                    }
                }
            }

            private IEnumerator PlayGif(PhoneGif.Clip clip, RawImage raw)
            {
                int i = 0;
                while (raw != null && clip != null && clip.Frames != null && clip.Frames.Length > 0)
                {
                    if (clip.Frames[i] != null)
                    {
                        raw.texture = clip.Frames[i];
                        PhoneUi.FitContained(raw, clip.Frames[i]);
                    }
                    float wait = clip.Delays != null && i < clip.Delays.Length ? clip.Delays[i] : 0.1f;
                    if (wait < 0.04f)
                        wait = 0.04f;
                    i++;
                    if (i >= clip.Frames.Length)
                        i = 0;
                    yield return new WaitForSecondsRealtime(wait);
                }
            }

            private IEnumerator PlayFlipbook(PhotoItem photo, RawImage raw)
            {
                float wait = 1f / Mathf.Max(4f, photo.Fps);
                int i = 0;
                while (raw != null && photo != null)
                {
                    Texture2D frame = PhoneImages.LoadFile(PhoneStore.VideoFramePath(photo.Id, i));
                    if (frame != null)
                    {
                        raw.texture = frame;
                        PhoneUi.FitContained(raw, frame);
                    }
                    i++;
                    if (i >= photo.Frames)
                        i = 0;
                    yield return new WaitForSecondsRealtime(wait);
                }
            }

            private void Clear()
            {
                StopPlayback();
                for (int i = _host.Content.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(_host.Content.GetChild(i).gameObject);
            }
        }
    }
}
