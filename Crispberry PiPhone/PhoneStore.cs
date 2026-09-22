using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal sealed class NoteItem
    {
        public string Id;
        public string Title;
        public string Body;
        public long UpdatedMs;
    }

    internal sealed class ChatMessage
    {
        public string Id;
        public string ThreadId;
        public string FromId;
        public string FromName;
        public string Text;
        public string AudioFile;
        public string MediaFile;
        public string MediaKind;
        public long UnixMs;
        public bool VoiceMail;
    }

    internal sealed class PlaylistItem
    {
        public string Id;
        public string Name;
        public readonly List<string> TrackIds = new List<string>();
    }

    internal sealed class CallLogItem
    {
        public string Id;
        public string OtherId;
        public string OtherName;
        public bool Outgoing;
        public bool Missed;
        public bool Group;
        public long UnixMs;
    }

    internal sealed class PhotoItem
    {
        public string Id;
        public string File;
        public bool Front;
        public long UnixMs;
        public bool Video;
        public int Frames;
        public float Fps = 10f;
    }

    internal sealed class NoticeItem
    {
        public string Id;
        public string Title;
        public string Body;
        public string AppId;
        public long UnixMs;
        public bool Seen;
    }

    internal sealed class DownloadItem
    {
        public string Id;
        public string File;
        public string Url;
        public long UnixMs;
    }

    internal sealed class VoiceMemoItem
    {
        public string Id;
        public string File;
        public long UnixMs;
    }

    internal sealed class SoundItem
    {
        public string Id;
        public string File;
        public string Name;
        public long UnixMs;
        public bool IsAlert;
    }

    internal static class PhoneStore
    {
        public static readonly List<NoteItem> Notes = new List<NoteItem>();
        public static readonly List<ChatMessage> Messages = new List<ChatMessage>();
        public static readonly List<CallLogItem> Calls = new List<CallLogItem>();
        public static readonly List<PhotoItem> Photos = new List<PhotoItem>();
        public static readonly List<DownloadItem> Downloads = new List<DownloadItem>();
        public static readonly List<VoiceMemoItem> Memos = new List<VoiceMemoItem>();
        public static readonly List<NoticeItem> Notices = new List<NoticeItem>();
        public static readonly List<string> HomeIds = new List<string>();
        public static readonly List<string> DockIds = new List<string>();
        public static readonly List<string> InstalledIds = new List<string>();
        public static readonly List<SoundItem> Sounds = new List<SoundItem>();
        public static readonly List<PlaylistItem> Playlists = new List<PlaylistItem>();
        internal static bool DefaultsReady;
        private static int _unseenCached = -1;

        public static string RootDir
        {
            get { return Path.Combine(Path.GetDirectoryName(Paths.ConfigPath) ?? Paths.BepInExRootPath, "CrispberryPiPhone"); }
        }

        public static string AudioDir
        {
            get { return Path.Combine(RootDir, "audio"); }
        }

        public static string PhotoDir
        {
            get { return Path.Combine(RootDir, "photos"); }
        }

        public static string DownloadDir
        {
            get { return Path.Combine(RootDir, "downloads"); }
        }

        public static string SoundsDir
        {
            get { return Path.Combine(RootDir, "sounds"); }
        }

        public static string AlertsDir
        {
            get { return Path.Combine(RootDir, "alerts"); }
        }

        public static string ChatDir
        {
            get { return Path.Combine(RootDir, "chat"); }
        }

        public static string TrashDir
        {
            get { return PhoneTrash.TrashDir; }
        }

        public static void EnsureDir()
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(AudioDir);
            Directory.CreateDirectory(PhotoDir);
            Directory.CreateDirectory(DownloadDir);
            Directory.CreateDirectory(SoundsDir);
            Directory.CreateDirectory(AlertsDir);
            Directory.CreateDirectory(ChatDir);
            Directory.CreateDirectory(TrashDir);
        }

        public static void Load()
        {
            EnsureDir();
            Notes.Clear();
            Messages.Clear();
            Calls.Clear();
            Photos.Clear();
            Downloads.Clear();
            Memos.Clear();
            LoadNotes();
            LoadMessages();
            LoadCalls();
            LoadPhotos();
            LoadDownloads();
            LoadMemos();
            LoadNotices();
            LoadLayout();
            LoadInstalled();
            LoadSounds();
            ImportLooseAlerts();
            LoadPlaylists();
            MigrateOldMemos();
        }

        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static long NowMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public static NoteItem AddNote(string title, string body)
        {
            var note = new NoteItem
            {
                Id = NewId(),
                Title = title ?? "Note",
                Body = body ?? string.Empty,
                UpdatedMs = NowMs()
            };
            Notes.Insert(0, note);
            SaveNotes();
            return note;
        }

        public static void UpdateNote(NoteItem note)
        {
            if (note == null)
                return;
            note.UpdatedMs = NowMs();
            SaveNotes();
        }

        public static void DeleteNote(string id)
        {
            Notes.RemoveAll(n => n != null && n.Id == id);
            SaveNotes();
        }

        public static void AddMessage(ChatMessage msg)
        {
            if (msg == null || string.IsNullOrEmpty(msg.Id))
                return;
            for (int i = 0; i < Messages.Count; i++)
            {
                if (Messages[i] != null && Messages[i].Id == msg.Id)
                    return;
            }
            Messages.Add(msg);
            SaveMessages();
        }

        public static void DeleteMessages(string threadId, List<string> ids)
        {
            if (string.IsNullOrEmpty(threadId))
                return;
            bool all = ids == null || ids.Count == 0;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                ChatMessage m = Messages[i];
                if (m == null || m.ThreadId != threadId || m.VoiceMail)
                    continue;
                if (!all && (ids == null || !ids.Contains(m.Id)))
                    continue;
                Messages.RemoveAt(i);
            }
            SaveMessages();
        }

        public static void DeleteThread(string threadId)
        {
            DeleteMessages(threadId, null);
        }

        public static List<ChatMessage> Thread(string threadId)
        {
            var list = new List<ChatMessage>();
            for (int i = 0; i < Messages.Count; i++)
            {
                ChatMessage m = Messages[i];
                if (m != null && m.ThreadId == threadId && !m.VoiceMail)
                    list.Add(m);
            }
            return list;
        }

        public static List<ChatMessage> Voicemails()
        {
            var list = new List<ChatMessage>();
            for (int i = 0; i < Messages.Count; i++)
            {
                ChatMessage m = Messages[i];
                if (m != null && m.VoiceMail && !IsOldMemo(m))
                    list.Add(m);
            }
            return list;
        }

        public static List<string> ThreadIds()
        {
            var ids = new List<string>();
            for (int i = 0; i < Messages.Count; i++)
            {
                ChatMessage m = Messages[i];
                if (m == null || m.VoiceMail || string.IsNullOrEmpty(m.ThreadId))
                    continue;
                if (!ids.Contains(m.ThreadId))
                    ids.Add(m.ThreadId);
            }
            return ids;
        }

        public static string ThreadTitle(string threadId)
        {
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                ChatMessage m = Messages[i];
                if (m == null || m.ThreadId != threadId)
                    continue;
                if (!string.IsNullOrEmpty(m.FromName) && m.FromId != BuiltinApps.LocalId())
                    return m.FromName;
            }
            Photon.Realtime.Player p = BuiltinApps.FindById(threadId);
            if (p != null)
                return BuiltinApps.ContactName(p);
            return PhoneContacts.Display(threadId, "Chat");
        }

        public static void AddCall(CallLogItem item)
        {
            if (item == null)
                return;
            Calls.Insert(0, item);
            SaveCalls();
        }

        public static PhotoItem AddPhoto(byte[] png, bool front)
        {
            EnsureDir();
            string id = NewId();
            string file = id + ".png";
            File.WriteAllBytes(Path.Combine(PhotoDir, file), png ?? new byte[0]);
            var item = new PhotoItem { Id = id, File = file, Front = front, UnixMs = NowMs() };
            Photos.Insert(0, item);
            SavePhotos();
            return item;
        }

        public static PhotoItem AddRecordedVideo(string id, bool front, int frames, string outputPath)
        {
            if (string.IsNullOrEmpty(id))
                id = NewId();
            string file = id + ".mp4";
            if (!string.IsNullOrEmpty(outputPath))
                file = Path.GetFileName(outputPath);
            var item = new PhotoItem
            {
                Id = id,
                File = file,
                Front = front,
                UnixMs = NowMs(),
                Video = true,
                Frames = frames,
                Fps = PhoneVideo.Fps
            };
            Photos.Insert(0, item);
            SavePhotos();
            return item;
        }

        public static bool IsLegacyFlipbook(PhotoItem item)
        {
            return item != null && item.Video && !string.IsNullOrEmpty(item.File)
                && (item.File.IndexOf('/') >= 0 || item.File.IndexOf('\\') >= 0);
        }

        public static string VideoPath(string id)
        {
            return Path.Combine(PhotoDir, id + ".avi");
        }

        public static string VideoFilePath(PhotoItem item)
        {
            if (item == null)
                return null;
            if (!string.IsNullOrEmpty(item.File) && (item.File.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || item.File.EndsWith(".avi", StringComparison.OrdinalIgnoreCase)))
                return Path.Combine(PhotoDir, item.File);
            string mp4 = VideoPath(item.Id);
            if (File.Exists(mp4))
                return mp4;
            string avi = Path.Combine(PhotoDir, item.Id + ".avi");
            if (File.Exists(avi))
                return avi;
            return mp4;
        }

        public static string VideoPosterPath(string id)
        {
            return Path.Combine(PhotoDir, id + ".jpg");
        }

        public static string VideoThumbPath(PhotoItem item)
        {
            if (item == null)
                return null;
            string poster = VideoPosterPath(item.Id);
            if (File.Exists(poster))
                return poster;
            if (IsLegacyFlipbook(item))
                return VideoFramePath(item.Id, 0);
            return poster;
        }

        public static string VideoDir(string id)
        {
            return Path.Combine(PhotoDir, id);
        }

        public static string VideoFramePath(string id, int frame)
        {
            return Path.Combine(PhotoDir, id, frame.ToString("0000") + ".png");
        }

        public static void DeleteMemo(string id)
        {
            for (int i = Memos.Count - 1; i >= 0; i--)
            {
                VoiceMemoItem m = Memos[i];
                if (m == null || m.Id != id)
                    continue;
                TryDelete(AudioPath(m.File));
                Memos.RemoveAt(i);
            }
            SaveMemos();
        }

        public static void DeletePhoto(string id)
        {
            for (int i = Photos.Count - 1; i >= 0; i--)
            {
                PhotoItem p = Photos[i];
                if (p == null || p.Id != id)
                    continue;
                TryDelete(PhotoPath(p.File));
                TryDelete(VideoPath(p.Id));
                TryDelete(Path.Combine(PhotoDir, p.Id + ".avi"));
                TryDelete(Path.Combine(PhotoDir, p.Id + ".wav"));
                TryDelete(VideoPosterPath(p.Id));
                TryDelete(VideoDir(p.Id));
                Photos.RemoveAt(i);
            }
            SavePhotos();
        }

        public static void DeleteDownload(string id)
        {
            for (int i = Downloads.Count - 1; i >= 0; i--)
            {
                DownloadItem d = Downloads[i];
                if (d == null || d.Id != id)
                    continue;
                TryDelete(DownloadPath(d.File));
                Downloads.RemoveAt(i);
            }
            SaveDownloads();
        }

        public static string SoundPath(string file)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            if (file.StartsWith("alerts/", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("alerts\\", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(RootDir, file.Replace('/', Path.DirectorySeparatorChar));
            return Path.Combine(SoundsDir, file);
        }

        public static SoundItem FindSound(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < Sounds.Count; i++)
            {
                if (Sounds[i] != null && Sounds[i].Id == id)
                    return Sounds[i];
            }
            return null;
        }

        public static SoundItem FindMusic(string id)
        {
            SoundItem s = FindSound(id);
            return s != null && !s.IsAlert ? s : null;
        }

        public static List<SoundItem> MusicTracks()
        {
            return FilterSounds(false);
        }

        public static List<SoundItem> AlertTones()
        {
            return FilterSounds(true);
        }

        public static List<string> MusicIds()
        {
            List<SoundItem> tracks = MusicTracks();
            var ids = new List<string>(tracks.Count);
            for (int i = 0; i < tracks.Count; i++)
            {
                if (tracks[i] != null)
                    ids.Add(tracks[i].Id);
            }
            return ids;
        }

        private static List<SoundItem> FilterSounds(bool alerts)
        {
            var list = new List<SoundItem>();
            for (int i = 0; i < Sounds.Count; i++)
            {
                if (Sounds[i] != null && Sounds[i].IsAlert == alerts)
                    list.Add(Sounds[i]);
            }
            return list;
        }

        public static SoundItem AddSound(string sourcePath)
        {
            return AddSound(sourcePath, false, 0f);
        }

        public const long MusicMaxBytes = 96L * 1024 * 1024;

        public static SoundItem AddSound(string sourcePath, bool alert, float maxSeconds)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return null;
            if (!alert)
            {
                try
                {
                    if (new FileInfo(sourcePath).Length > MusicMaxBytes)
                    {
                        PhoneMenu.Toast("That song is too large (max 96 MB).");
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }
            EnsureDir();
            string id = NewId();
            string ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(ext))
                ext = ".wav";
            ext = ext.ToLowerInvariant();
            string file;
            string dest;
            if (!alert)
            {
                file = id + ext;
                dest = Path.Combine(SoundsDir, file);
                File.Copy(sourcePath, dest, true);
                if (ext == ".wav" || ext == ".wave")
                    EnsurePlayableWav(dest);
            }
            else
            {
                file = id + ".wav";
                dest = Path.Combine(AlertsDir, file);
                byte[] decoded = null;
                try { decoded = PhoneVideo.DecodeAudioToWav(sourcePath); } catch { }
                if (decoded != null && decoded.Length > 64)
                    File.WriteAllBytes(dest, decoded);
                else if (sourcePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    File.Copy(sourcePath, dest, true);
                else
                {
                    file = id + ext;
                    dest = Path.Combine(AlertsDir, file);
                    File.Copy(sourcePath, dest, true);
                    Plugin.LogError("Could not convert " + ext + " to WAV. Playback may fail.");
                }
                if (maxSeconds > 0f)
                    TrimFile(dest, maxSeconds);
            }
            var item = new SoundItem
            {
                Id = id,
                File = alert ? "alerts/" + Path.GetFileName(file) : Path.GetFileName(file),
                Name = Path.GetFileNameWithoutExtension(sourcePath),
                UnixMs = NowMs(),
                IsAlert = alert
            };
            Sounds.Insert(0, item);
            SaveSounds();
            return item;
        }

        private static void EnsurePlayableWav(string dest)
        {
            try
            {
                if (string.IsNullOrEmpty(dest) || !File.Exists(dest))
                    return;
                byte[] raw = File.ReadAllBytes(dest);
                if (VoiceIo.FromWav(raw) != null)
                    return;
                byte[] decoded = null;
                try { decoded = PhoneVideo.DecodeAudioToWav(dest); } catch { }
                if (decoded != null && decoded.Length > 64)
                    File.WriteAllBytes(dest, decoded);
            }
            catch
            {
            }
        }

        public static SoundItem AddAlertWav(byte[] wav, string name)
        {
            if (wav == null || wav.Length < 64)
                return null;
            EnsureDir();
            string id = NewId();
            string file = id + ".wav";
            File.WriteAllBytes(Path.Combine(AlertsDir, file), wav);
            var item = new SoundItem
            {
                Id = id,
                File = "alerts/" + file,
                Name = string.IsNullOrEmpty(name) ? "Clip" : name,
                UnixMs = NowMs(),
                IsAlert = true
            };
            Sounds.Insert(0, item);
            SaveSounds();
            return item;
        }

        public static void ImportLooseAlerts()
        {
            EnsureDir();
            if (!Directory.Exists(AlertsDir))
                return;
            string[] files = Directory.GetFiles(AlertsDir);
            bool added = false;
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name) || name.StartsWith("."))
                    continue;
                string rel = "alerts/" + name;
                if (FindByFile(rel) != null)
                    continue;
                Sounds.Insert(0, new SoundItem
                {
                    Id = "file:" + Path.GetFileNameWithoutExtension(name),
                    File = rel,
                    Name = Path.GetFileNameWithoutExtension(name),
                    UnixMs = NowMs(),
                    IsAlert = true
                });
                added = true;
            }
            if (added)
                SaveSounds();
        }

        private static SoundItem FindByFile(string relative)
        {
            for (int i = 0; i < Sounds.Count; i++)
            {
                if (Sounds[i] != null && string.Equals(Sounds[i].File, relative, StringComparison.OrdinalIgnoreCase))
                    return Sounds[i];
            }
            return null;
        }

        private static void TrimFile(string path, float maxSeconds)
        {
            try
            {
                if (!File.Exists(path) || !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    return;
                object clip = VoiceIo.FromWav(File.ReadAllBytes(path));
                if (clip == null || VoiceIo.ClipSeconds(clip) <= maxSeconds)
                    return;
                object sliced = VoiceIo.Slice(clip, 0f, maxSeconds);
                byte[] wav = VoiceIo.ToWav(sliced);
                if (wav != null && wav.Length > 64)
                    File.WriteAllBytes(path, wav);
            }
            catch
            {
            }
        }

        public static void DeleteSound(string id)
        {
            for (int i = Sounds.Count - 1; i >= 0; i--)
            {
                SoundItem s = Sounds[i];
                if (s == null || s.Id != id)
                    continue;
                TryDelete(SoundPath(s.File));
                Sounds.RemoveAt(i);
            }
            SaveSounds();
        }

        private static void TryDelete(string path)
        {
            PhoneTrash.Recycle(path);
        }

        public static bool IsAudioFile(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName))
                return false;
            string ext = Path.GetExtension(pathOrName);
            return ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".aac", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".flac", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".wma", StringComparison.OrdinalIgnoreCase);
        }

        public static string SaveChatMedia(byte[] data, string ext)
        {
            EnsureDir();
            if (string.IsNullOrEmpty(ext))
                ext = ".bin";
            if (!ext.StartsWith("."))
                ext = "." + ext;
            string file = NewId() + ext;
            File.WriteAllBytes(Path.Combine(ChatDir, file), data ?? new byte[0]);
            return "chat/" + file;
        }

        public static void AddNotice(string title, string body, string appId)
        {
            Notices.Insert(0, new NoticeItem
            {
                Id = NewId(),
                Title = title ?? "Notice",
                Body = body ?? string.Empty,
                AppId = appId ?? string.Empty,
                UnixMs = NowMs(),
                Seen = false
            });
            while (Notices.Count > 40)
                Notices.RemoveAt(Notices.Count - 1);
            _unseenCached = -1;
            SaveNotices();
        }

        public static int UnseenNoticeCount()
        {
            if (_unseenCached >= 0)
                return _unseenCached;
            int n = 0;
            for (int i = 0; i < Notices.Count; i++)
            {
                if (Notices[i] != null && !Notices[i].Seen)
                    n++;
            }
            _unseenCached = n;
            return n;
        }

        public static void DeleteNotice(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            bool changed = false;
            for (int i = Notices.Count - 1; i >= 0; i--)
            {
                if (Notices[i] == null || Notices[i].Id != id)
                    continue;
                Notices.RemoveAt(i);
                changed = true;
            }
            if (changed)
            {
                _unseenCached = -1;
                SaveNotices();
            }
        }

        public static void DismissNotices(string appId, string match)
        {
            bool changed = false;
            for (int i = Notices.Count - 1; i >= 0; i--)
            {
                NoticeItem n = Notices[i];
                if (n == null)
                    continue;
                if (!string.IsNullOrEmpty(appId) && !string.Equals(n.AppId, appId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrEmpty(match)
                    && (n.Title ?? string.Empty).IndexOf(match, StringComparison.OrdinalIgnoreCase) < 0
                    && (n.Body ?? string.Empty).IndexOf(match, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Notices.RemoveAt(i);
                changed = true;
            }
            if (changed)
            {
                _unseenCached = -1;
                SaveNotices();
            }
        }

        public static void DeleteVoicemail(string id)
        {
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                ChatMessage m = Messages[i];
                if (m == null || !m.VoiceMail || m.Id != id)
                    continue;
                TryDelete(AudioPath(m.AudioFile));
                Messages.RemoveAt(i);
            }
            SaveMessages();
        }

        public static void MarkNoticesSeen()
        {
            bool changed = false;
            for (int i = 0; i < Notices.Count; i++)
            {
                if (Notices[i] == null || Notices[i].Seen)
                    continue;
                Notices[i].Seen = true;
                changed = true;
            }
            if (changed)
            {
                _unseenCached = 0;
                SaveNotices();
            }
        }

        public static bool IsInstalled(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            PiPhoneApp app;
            if (PiPhoneApi.TryGetApp(id, out app) && app != null && app.Sticky)
                return true;
            return InstalledIds.Contains(id);
        }

        public static bool Install(string id)
        {
            if (string.IsNullOrEmpty(id) || InstalledIds.Contains(id))
                return false;
            PiPhoneApp app;
            if (!PiPhoneApi.TryGetApp(id, out app) || app == null)
                return false;
            InstalledIds.Add(id);
            SaveInstalled();
            if (app.ShowOnHome)
                AddToHome(id);
            return true;
        }

        public static bool Uninstall(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            PiPhoneApp app;
            if (PiPhoneApi.TryGetApp(id, out app) && app != null && app.Sticky)
                return false;
            bool removed = InstalledIds.Remove(id);
            HomeIds.Remove(id);
            DockIds.Remove(id);
            if (removed)
            {
                SaveInstalled();
                SaveLayout();
            }
            return removed;
        }

        public static void EnsureInstalledDefaults()
        {
            bool hadFile = File.Exists(Path.Combine(RootDir, "installed.txt"));
            var apps = PiPhoneApi.Apps;
            if (!hadFile && InstalledIds.Count == 0)
            {
                for (int i = 0; i < apps.Count; i++)
                {
                    if (apps[i] == null || string.IsNullOrEmpty(apps[i].Id))
                        continue;
                    if (apps[i].Preinstalled || apps[i].Sticky)
                    {
                        if (!InstalledIds.Contains(apps[i].Id))
                            InstalledIds.Add(apps[i].Id);
                    }
                }
            }
            for (int i = 0; i < apps.Count; i++)
            {
                if (apps[i] != null && apps[i].Sticky && !InstalledIds.Contains(apps[i].Id))
                    InstalledIds.Add(apps[i].Id);
            }
            string migrate = Path.Combine(RootDir, "makenoti.migrated");
            if (!File.Exists(migrate))
            {
                if (!InstalledIds.Contains(BuiltinApps.MakeNotiId))
                    InstalledIds.Add(BuiltinApps.MakeNotiId);
                try { File.WriteAllText(migrate, "1"); } catch { }
            }
            SaveInstalled();
            PruneUninstalledShortcuts();
        }

        public static void PruneUninstalledShortcuts()
        {
            bool changed = false;
            for (int i = HomeIds.Count - 1; i >= 0; i--)
            {
                if (!IsInstalled(HomeIds[i]))
                {
                    HomeIds.RemoveAt(i);
                    changed = true;
                }
            }
            for (int i = DockIds.Count - 1; i >= 0; i--)
            {
                if (!IsInstalled(DockIds[i]))
                {
                    DockIds.RemoveAt(i);
                    changed = true;
                }
            }
            if (changed)
                SaveLayout();
        }

        public static bool IsOnHome(string id)
        {
            return !string.IsNullOrEmpty(id) && HomeIds.Contains(id);
        }

        public static bool AddToHome(string id)
        {
            if (string.IsNullOrEmpty(id) || !IsInstalled(id) || HomeIds.Contains(id))
                return false;
            HomeIds.Add(id);
            SaveLayout();
            return true;
        }

        public static bool RemoveFromHome(string id)
        {
            if (string.IsNullOrEmpty(id) || !HomeIds.Remove(id))
                return false;
            SaveLayout();
            return true;
        }

        public static void EnsureHomeDefaults()
        {
            if (!DefaultsReady)
                return;
            EnsureInstalledDefaults();
            PruneUninstalledShortcuts();
            if (HomeIds.Count > 0)
            {
                bool homeChanged = false;
                if (IsInstalled(BuiltinApps.StoreId) && !HomeIds.Contains(BuiltinApps.StoreId))
                {
                    HomeIds.Add(BuiltinApps.StoreId);
                    homeChanged = true;
                }
                if (homeChanged)
                    SaveLayout();
                return;
            }
            var apps = PiPhoneApi.Apps;
            for (int i = 0; i < apps.Count; i++)
            {
                if (apps[i] == null || !IsInstalled(apps[i].Id))
                    continue;
                if (apps[i].ShowOnHome)
                    HomeIds.Add(apps[i].Id);
                if (apps[i].ShowOnDock && DockIds.Count < 4)
                    DockIds.Add(apps[i].Id);
            }
            SaveLayout();
        }

        public static void ToggleHome(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            if (HomeIds.Contains(id))
                HomeIds.Remove(id);
            else
                HomeIds.Add(id);
            SaveLayout();
        }

        public static void MoveHome(string id, int delta)
        {
            SwapIn(HomeIds, id, delta);
            SaveLayout();
        }

        public static void MoveDock(string id, int delta)
        {
            SwapIn(DockIds, id, delta);
            SaveLayout();
        }

        public static void RemoveDock(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;
            DockIds.Remove(id);
            SaveLayout();
        }

        public static bool IsOnDock(string id)
        {
            return !string.IsNullOrEmpty(id) && DockIds.Contains(id);
        }

        public static bool AddDock(string id)
        {
            if (string.IsNullOrEmpty(id) || !IsInstalled(id) || DockIds.Contains(id))
                return false;
            if (DockIds.Count >= 4)
                return false;
            DockIds.Add(id);
            SaveLayout();
            return true;
        }

        public static PlaylistItem FindPlaylist(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < Playlists.Count; i++)
            {
                if (Playlists[i] != null && Playlists[i].Id == id)
                    return Playlists[i];
            }
            return null;
        }

        public static PlaylistItem AddPlaylist(string name)
        {
            var item = new PlaylistItem
            {
                Id = NewId(),
                Name = string.IsNullOrEmpty(name) ? "Playlist" : name
            };
            Playlists.Insert(0, item);
            SavePlaylists();
            return item;
        }

        public static void DeletePlaylist(string id)
        {
            Playlists.RemoveAll(p => p != null && p.Id == id);
            SavePlaylists();
        }

        public static void AddToPlaylist(string listId, string trackId)
        {
            PlaylistItem p = FindPlaylist(listId);
            if (p == null || string.IsNullOrEmpty(trackId) || p.TrackIds.Contains(trackId))
                return;
            p.TrackIds.Add(trackId);
            SavePlaylists();
        }

        public static void RemoveFromPlaylist(string listId, string trackId)
        {
            PlaylistItem p = FindPlaylist(listId);
            if (p == null)
                return;
            p.TrackIds.Remove(trackId);
            SavePlaylists();
        }

        private static void SwapIn(List<string> list, string id, int delta)
        {
            int i = list.IndexOf(id);
            if (i < 0)
                return;
            int j = i + delta;
            if (j < 0 || j >= list.Count)
                return;
            string tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }

        public static void OpenFolder(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir))
                    return;
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Plugin.LogError("Open folder failed: " + ex.Message);
            }
        }

        public static void RevealFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return;
                if (File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "/select,\"" + path + "\"",
                        UseShellExecute = true
                    });
                    return;
                }
                OpenFolder(Path.GetDirectoryName(path));
            }
            catch (Exception ex)
            {
                Plugin.LogError("Reveal file failed: " + ex.Message);
            }
        }

        public static string SaveAudio(string id, byte[] wav)
        {
            EnsureDir();
            string file = id + ".wav";
            File.WriteAllBytes(Path.Combine(AudioDir, file), wav ?? new byte[0]);
            return file;
        }

        public static string AudioPath(string file)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            return Path.Combine(AudioDir, file);
        }

        public static string PhotoPath(string file)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            return Path.Combine(PhotoDir, file);
        }

        public static string DownloadPath(string file)
        {
            if (string.IsNullOrEmpty(file))
                return null;
            return Path.Combine(DownloadDir, file);
        }

        public static string MediaPath(string relative)
        {
            if (string.IsNullOrEmpty(relative))
                return null;
            return Path.Combine(RootDir, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        public static DownloadItem AddDownload(byte[] data, string ext, string url)
        {
            EnsureDir();
            string id = NewId();
            if (string.IsNullOrEmpty(ext))
                ext = ".png";
            if (!ext.StartsWith("."))
                ext = "." + ext;
            string file = id + ext;
            File.WriteAllBytes(Path.Combine(DownloadDir, file), data ?? new byte[0]);
            var item = new DownloadItem { Id = id, File = file, Url = url ?? string.Empty, UnixMs = NowMs() };
            Downloads.Insert(0, item);
            SaveDownloads();
            return item;
        }

        public static VoiceMemoItem AddMemo(byte[] wav)
        {
            string id = NewId();
            string file = SaveAudio(id, wav);
            var item = new VoiceMemoItem { Id = id, File = file, UnixMs = NowMs() };
            Memos.Insert(0, item);
            SaveMemos();
            return item;
        }

        private static void LoadNotes()
        {
            string path = Path.Combine(RootDir, "notes.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 4);
                if (p.Length < 4)
                    continue;
                long ms;
                long.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Notes.Add(new NoteItem { Id = p[0], Title = Unescape(p[1]), Body = Unescape(p[2]), UpdatedMs = ms });
            }
        }

        private static void SaveNotes()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Notes.Count; i++)
            {
                NoteItem n = Notes[i];
                if (n == null)
                    continue;
                sb.Append(n.Id).Append('|').Append(Escape(n.Title)).Append('|').Append(Escape(n.Body)).Append('|').Append(n.UpdatedMs).Append('\n');
            }
            Write("notes.txt", sb.ToString());
        }

        private static void LoadMessages()
        {
            string path = Path.Combine(RootDir, "messages.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 11);
                if (p.Length < 9)
                    continue;
                long ms;
                long.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Messages.Add(new ChatMessage
                {
                    Id = p[0],
                    ThreadId = Unescape(p[1]),
                    FromId = Unescape(p[2]),
                    FromName = Unescape(p[3]),
                    Text = Unescape(p[4]),
                    AudioFile = Unescape(p[5]),
                    UnixMs = ms,
                    VoiceMail = p[7] == "1",
                    MediaFile = p.Length > 9 ? Unescape(p[9]) : string.Empty,
                    MediaKind = p.Length > 10 ? Unescape(p[10]) : string.Empty
                });
            }
        }

        private static void SaveMessages()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Messages.Count; i++)
            {
                ChatMessage m = Messages[i];
                if (m == null)
                    continue;
                sb.Append(m.Id).Append('|')
                    .Append(Escape(m.ThreadId)).Append('|')
                    .Append(Escape(m.FromId)).Append('|')
                    .Append(Escape(m.FromName)).Append('|')
                    .Append(Escape(m.Text)).Append('|')
                    .Append(Escape(m.AudioFile)).Append('|')
                    .Append(m.UnixMs).Append('|')
                    .Append(m.VoiceMail ? "1" : "0").Append('|')
                    .Append(Escape(m.MediaFile)).Append('|')
                    .Append(Escape(m.MediaKind)).Append('\n');
            }
            Write("messages.txt", sb.ToString());
        }

        private static void LoadCalls()
        {
            string path = Path.Combine(RootDir, "calls.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 7);
                if (p.Length < 7)
                    continue;
                long ms;
                long.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Calls.Add(new CallLogItem
                {
                    Id = p[0],
                    OtherId = Unescape(p[1]),
                    OtherName = Unescape(p[2]),
                    Outgoing = p[3] == "1",
                    Missed = p[4] == "1",
                    Group = p[5] == "1",
                    UnixMs = ms
                });
            }
        }

        private static void SaveCalls()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Calls.Count; i++)
            {
                CallLogItem c = Calls[i];
                if (c == null)
                    continue;
                sb.Append(c.Id).Append('|')
                    .Append(Escape(c.OtherId)).Append('|')
                    .Append(Escape(c.OtherName)).Append('|')
                    .Append(c.Outgoing ? "1" : "0").Append('|')
                    .Append(c.Missed ? "1" : "0").Append('|')
                    .Append(c.Group ? "1" : "0").Append('|')
                    .Append(c.UnixMs).Append('\n');
            }
            Write("calls.txt", sb.ToString());
        }

        private static void LoadPhotos()
        {
            string path = Path.Combine(RootDir, "photos.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 7);
                if (p.Length < 4)
                    continue;
                long ms;
                long.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                int frames = 0;
                float fps = 10f;
                if (p.Length > 5)
                    int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out frames);
                if (p.Length > 6)
                    float.TryParse(p[6], NumberStyles.Float, CultureInfo.InvariantCulture, out fps);
                Photos.Add(new PhotoItem
                {
                    Id = p[0],
                    File = Unescape(p[1]),
                    Front = p[2] == "1",
                    UnixMs = ms,
                    Video = p.Length > 4 && p[4] == "1",
                    Frames = frames,
                    Fps = fps > 0f ? fps : 10f
                });
            }
        }

        internal static void SavePhotos()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Photos.Count; i++)
            {
                PhotoItem p = Photos[i];
                if (p == null)
                    continue;
                sb.Append(p.Id).Append('|').Append(Escape(p.File)).Append('|').Append(p.Front ? "1" : "0").Append('|').Append(p.UnixMs)
                    .Append('|').Append(p.Video ? "1" : "0").Append('|').Append(p.Frames).Append('|')
                    .Append(p.Fps.ToString("0.#", CultureInfo.InvariantCulture)).Append('\n');
            }
            Write("photos.txt", sb.ToString());
        }

        private static void LoadDownloads()
        {
            string path = Path.Combine(RootDir, "downloads.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 4);
                if (p.Length < 4)
                    continue;
                long ms;
                long.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Downloads.Add(new DownloadItem { Id = p[0], File = Unescape(p[1]), Url = Unescape(p[2]), UnixMs = ms });
            }
        }

        private static void SaveDownloads()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Downloads.Count; i++)
            {
                DownloadItem d = Downloads[i];
                if (d == null)
                    continue;
                sb.Append(d.Id).Append('|').Append(Escape(d.File)).Append('|').Append(Escape(d.Url)).Append('|').Append(d.UnixMs).Append('\n');
            }
            Write("downloads.txt", sb.ToString());
        }

        private static void LoadMemos()
        {
            string path = Path.Combine(RootDir, "memos.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 3);
                if (p.Length < 3)
                    continue;
                long ms;
                long.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Memos.Add(new VoiceMemoItem { Id = p[0], File = Unescape(p[1]), UnixMs = ms });
            }
        }

        private static void SaveMemos()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Memos.Count; i++)
            {
                VoiceMemoItem m = Memos[i];
                if (m == null)
                    continue;
                sb.Append(m.Id).Append('|').Append(Escape(m.File)).Append('|').Append(m.UnixMs).Append('\n');
            }
            Write("memos.txt", sb.ToString());
        }

        private static void MigrateOldMemos()
        {
            bool changed = false;
            for (int i = Messages.Count - 1; i >= 0; i--)
            {
                ChatMessage m = Messages[i];
                if (!IsOldMemo(m))
                    continue;
                Memos.Insert(0, new VoiceMemoItem { Id = m.Id, File = m.AudioFile, UnixMs = m.UnixMs });
                Messages.RemoveAt(i);
                changed = true;
            }
            if (changed)
            {
                SaveMemos();
                SaveMessages();
            }
        }

        private static bool IsOldMemo(ChatMessage m)
        {
            return m != null && m.VoiceMail && string.Equals(m.FromName, "Memo", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadNotices()
        {
            _unseenCached = -1;
            string path = Path.Combine(RootDir, "notices.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 6);
                if (p.Length < 6)
                    continue;
                long ms;
                long.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Notices.Add(new NoticeItem
                {
                    Id = p[0],
                    Title = Unescape(p[1]),
                    Body = Unescape(p[2]),
                    AppId = Unescape(p[3]),
                    UnixMs = ms,
                    Seen = p[5] == "1"
                });
            }
        }

        private static void SaveNotices()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Notices.Count; i++)
            {
                NoticeItem n = Notices[i];
                if (n == null)
                    continue;
                sb.Append(n.Id).Append('|').Append(Escape(n.Title)).Append('|').Append(Escape(n.Body)).Append('|')
                    .Append(Escape(n.AppId)).Append('|').Append(n.UnixMs).Append('|').Append(n.Seen ? "1" : "0").Append('\n');
            }
            Write("notices.txt", sb.ToString());
        }

        private static void LoadInstalled()
        {
            InstalledIds.Clear();
            string path = Path.Combine(RootDir, "installed.txt");
            if (!File.Exists(path))
                return;
            string raw = File.ReadAllText(path);
            string[] ids = raw.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i].Trim();
                if (!string.IsNullOrEmpty(id) && !InstalledIds.Contains(id))
                    InstalledIds.Add(id);
            }
        }

        internal static void SaveInstalled()
        {
            Write("installed.txt", string.Join(",", InstalledIds.ToArray()) + "\n");
        }

        private static void LoadLayout()
        {
            string path = Path.Combine(RootDir, "layout.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = line.Substring(0, eq);
                string[] ids = line.Substring(eq + 1).Split(',');
                if (key == "home")
                {
                    HomeIds.Clear();
                    for (int j = 0; j < ids.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(ids[j]))
                            HomeIds.Add(ids[j]);
                    }
                }
                else if (key == "dock")
                {
                    DockIds.Clear();
                    for (int j = 0; j < ids.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(ids[j]))
                            DockIds.Add(ids[j]);
                    }
                }
            }
        }

        private static void LoadSounds()
        {
            Sounds.Clear();
            string path = Path.Combine(RootDir, "sounds.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 5);
                if (p.Length < 3)
                    continue;
                long ms;
                long.TryParse(p.Length > 3 ? p[3] : "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out ms);
                Sounds.Add(new SoundItem
                {
                    Id = p[0],
                    File = Unescape(p[1]),
                    Name = Unescape(p[2]),
                    UnixMs = ms,
                    IsAlert = p.Length > 4 && p[4] == "1"
                });
            }
        }

        private static void SaveSounds()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Sounds.Count; i++)
            {
                SoundItem s = Sounds[i];
                if (s == null)
                    continue;
                sb.Append(s.Id).Append('|').Append(Escape(s.File)).Append('|').Append(Escape(s.Name)).Append('|')
                    .Append(s.UnixMs).Append('|').Append(s.IsAlert ? "1" : "0").Append('\n');
            }
            Write("sounds.txt", sb.ToString());
        }

        private static void LoadPlaylists()
        {
            Playlists.Clear();
            string path = Path.Combine(RootDir, "playlists.txt");
            if (!File.Exists(path))
                return;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] p = Split(lines[i], 3);
                if (p.Length < 2)
                    continue;
                var item = new PlaylistItem { Id = p[0], Name = Unescape(p[1]) };
                if (p.Length > 2)
                {
                    string[] ids = p[2].Split(',');
                    for (int j = 0; j < ids.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(ids[j]))
                            item.TrackIds.Add(ids[j]);
                    }
                }
                Playlists.Add(item);
            }
        }

        private static void SavePlaylists()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < Playlists.Count; i++)
            {
                PlaylistItem p = Playlists[i];
                if (p == null)
                    continue;
                sb.Append(p.Id).Append('|').Append(Escape(p.Name)).Append('|')
                    .Append(string.Join(",", p.TrackIds.ToArray())).Append('\n');
            }
            Write("playlists.txt", sb.ToString());
        }

        internal static void SaveLayout()
        {
            Write("layout.txt", "home=" + string.Join(",", HomeIds.ToArray()) + "\ndock=" + string.Join(",", DockIds.ToArray()) + "\n");
        }

        private static void Write(string file, string contents)
        {
            try
            {
                EnsureDir();
                File.WriteAllText(Path.Combine(RootDir, file), contents ?? string.Empty);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Store write " + file + " failed: " + ex.Message);
            }
        }

        internal static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("\\", "\\\\").Replace("|", "\\p").Replace("\r", string.Empty).Replace("\n", "\\n");
        }

        internal static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;
            return s.Replace("\\n", "\n").Replace("\\p", "|").Replace("\\\\", "\\");
        }

        private static string[] Split(string line, int parts)
        {
            if (string.IsNullOrEmpty(line))
                return new string[0];
            var list = new List<string>(parts);
            int start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] != '|')
                    continue;
                list.Add(line.Substring(start, i - start));
                start = i + 1;
                if (list.Count == parts - 1)
                    break;
            }
            list.Add(start <= line.Length ? line.Substring(start) : string.Empty);
            return list.ToArray();
        }
    }
}
