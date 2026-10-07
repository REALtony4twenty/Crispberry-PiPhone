using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Instance;
        public static event Action Changed;

        public static bool HasTrack
        {
            get { return Instance != null && !string.IsNullOrEmpty(Instance._id); }
        }

        public static bool Playing
        {
            get { return Instance != null && Instance._playing && !Instance._paused; }
        }

        public static bool Paused
        {
            get { return Instance != null && Instance._paused; }
        }

        public static string CurrentId
        {
            get { return Instance != null ? Instance._id : string.Empty; }
        }

        public static string CurrentName
        {
            get { return Instance != null ? Instance._name : string.Empty; }
        }

        public static string NextName
        {
            get
            {
                if (Instance == null || Instance._queue.Count == 0)
                    return string.Empty;
                int i = (Instance._index + 1) % Instance._queue.Count;
                if (i == Instance._index)
                    return string.Empty;
                SoundItem item = PhoneStore.FindMusic(Instance._queue[i]) ?? PhoneStore.FindSound(Instance._queue[i]);
                return item != null ? item.Name : string.Empty;
            }
        }

        public static string UpcomingLine
        {
            get
            {
                if (Instance == null || Instance._queue.Count <= 1)
                    return "Nothing queued";
                var parts = new List<string>();
                int n = Mathf.Min(3, Instance._queue.Count - 1);
                for (int k = 1; k <= n; k++)
                {
                    int i = (Instance._index + k) % Instance._queue.Count;
                    SoundItem item = PhoneStore.FindMusic(Instance._queue[i]) ?? PhoneStore.FindSound(Instance._queue[i]);
                    if (item != null && !string.IsNullOrEmpty(item.Name))
                        parts.Add(item.Name);
                }
                return parts.Count == 0 ? "Nothing queued" : string.Join("  ·  ", parts.ToArray());
            }
        }

        private readonly List<string> _queue = new List<string>();
        private int _index;
        private string _id = string.Empty;
        private string _name = string.Empty;
        private bool _playing;
        private bool _paused;
        private bool _loading;
        private bool _callPaused;
        private Coroutine _load;

        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("PiP_Music");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<MusicPlayer>();
        }

        public static void PlayLibrary(string startId)
        {
            Ensure();
            Instance._callPaused = false;
            Instance.PlayQueue(PhoneStore.MusicIds(), startId);
        }

        public static void PlayPlaylist(PlaylistItem list, string startId)
        {
            Ensure();
            Instance._callPaused = false;
            var ids = new List<string>();
            if (list != null && list.TrackIds != null)
            {
                for (int i = 0; i < list.TrackIds.Count; i++)
                {
                    if (PhoneStore.FindMusic(list.TrackIds[i]) != null)
                        ids.Add(list.TrackIds[i]);
                }
            }
            Instance.PlayQueue(ids, startId);
        }

        public static void Toggle()
        {
            Ensure();
            Instance._callPaused = false;
            if (!Instance._playing && string.IsNullOrEmpty(Instance._id))
            {
                List<string> ids = PhoneStore.MusicIds();
                if (ids.Count == 0)
                {
                    PhoneMenu.Toast("Add songs in Audio first.");
                    return;
                }
                Instance.PlayQueue(ids, ids[0]);
                return;
            }
            if (Instance._paused || !Instance._playing)
                Instance.Resume();
            else
                Instance.Pause();
        }

        public static void Next()
        {
            Ensure();
            Instance._callPaused = false;
            Instance.Skip(1);
        }

        public static void Prev()
        {
            Ensure();
            Instance._callPaused = false;
            Instance.Skip(-1);
        }

        public static void PauseForCall()
        {
            if (Instance == null || !Playing)
                return;
            Instance.Pause();
            Instance._callPaused = true;
        }

        public static void ResumeAfterCall()
        {
            if (Instance == null || !Instance._callPaused)
                return;
            Instance._callPaused = false;
            if (Instance._paused)
                Instance.Resume();
        }

        private void PlayQueue(List<string> ids, string startId)
        {
            _queue.Clear();
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    if (!string.IsNullOrEmpty(ids[i]) && !_queue.Contains(ids[i]))
                        _queue.Add(ids[i]);
                }
            }
            _index = 0;
            if (!string.IsNullOrEmpty(startId))
            {
                int found = _queue.IndexOf(startId);
                if (found >= 0)
                    _index = found;
                else
                {
                    _queue.Insert(0, startId);
                    _index = 0;
                }
            }
            if (_queue.Count == 0)
            {
                PhoneMenu.Toast("No songs in that list.");
                return;
            }
            StartTrack(_queue[_index]);
        }

        private void StartTrack(string id)
        {
            SoundItem item = PhoneStore.FindMusic(id) ?? PhoneStore.FindSound(id);
            if (item == null)
            {
                Skip(1);
                return;
            }
            string path = PhoneStore.SoundPath(item.File);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                PhoneMenu.Toast("Missing file for " + item.Name + ".");
                Skip(1);
                return;
            }
            _id = item.Id;
            _name = item.Name;
            _playing = true;
            _paused = false;
            _loading = true;
            if (_load != null)
                StopCoroutine(_load);
            _load = StartCoroutine(LoadThenPlay(path));
            Raise();
        }

        private IEnumerator LoadThenPlay(string path)
        {
            object clip = null;
            yield return PhoneSounds.LoadClip(path, c => clip = c);
            _loading = false;
            _load = null;
            if (clip == null)
            {
                PhoneMenu.Toast("Couldn't play that song.");
                _playing = false;
                Raise();
                yield break;
            }
            PlayClip(clip);
        }

        private void PlayClip(object clip)
        {
            if (!PhoneAudio.PlayMusic(clip))
                return;
            _playing = true;
            _paused = false;
            Raise();
        }

        private void Pause()
        {
            if (!_playing)
                return;
            PhoneAudio.PauseMusic();
            _paused = true;
            Raise();
        }

        private void Resume()
        {
            PhoneAudio.ResumeMusic();
            _paused = false;
            _playing = true;
            Raise();
        }

        private void Skip(int delta)
        {
            if (_queue.Count == 0)
            {
                PlayLibrary(null);
                return;
            }
            _index = (_index + delta) % _queue.Count;
            if (_index < 0)
                _index += _queue.Count;
            StartTrack(_queue[_index]);
        }

        private void Update()
        {
            if (!_playing || _paused || _loading || !PhoneAudio.MusicReady)
                return;
            if (!PhoneAudio.MusicSounding && !PhoneAudio.MusicHeld)
                Skip(1);
        }

        private static void Raise()
        {
            Action handler = Changed;
            if (handler != null)
                handler();
            PhoneMenu.RefreshMusicBar();
        }
    }
}
