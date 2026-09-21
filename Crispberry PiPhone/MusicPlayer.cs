using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        private static readonly Type SourceType = Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");

        private Component _source;
        private readonly List<string> _queue = new List<string>();
        private int _index;
        private string _id = string.Empty;
        private string _name = string.Empty;
        private bool _playing;
        private bool _paused;
        private bool _loading;
        private float _duck = 1f;
        private float _duckUntil;
        private float _duckTarget = 1f;
        private Coroutine _load;
        private float _lastVol = -1f;

        public static void Ensure()
        {
            if (Instance != null)
                return;
            var go = new GameObject("PiP_Music");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<MusicPlayer>();
            Instance.BuildSource();
        }

        public static void PlayLibrary(string startId)
        {
            Ensure();
            Instance.PlayQueue(PhoneStore.MusicIds(), startId);
        }

        public static void PlayPlaylist(PlaylistItem list, string startId)
        {
            Ensure();
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
            Instance.Skip(1);
        }

        public static void Prev()
        {
            Ensure();
            Instance.Skip(-1);
        }

        public static void DuckFor(float seconds)
        {
            if (!PhoneTones.DuckMusic)
                return;
            Ensure();
            if (!Instance._playing || Instance._paused)
                return;
            Instance._duckUntil = Time.unscaledTime + Mathf.Max(0.4f, seconds);
            Instance._duckTarget = 0.08f;
        }

        public static void ApplyVolume()
        {
            if (Instance != null)
                Instance.WriteVolume();
        }

        private void BuildSource()
        {
            if (SourceType == null)
                return;
            _source = gameObject.AddComponent(SourceType);
            TrySet("playOnAwake", false);
            TrySet("loop", false);
            TrySet("spatialBlend", 0f);
            TrySet("ignoreListenerPause", true);
            TrySet("ignoreListenerVolume", true);
            TrySet("bypassEffects", true);
            TrySet("bypassListenerEffects", true);
            WriteVolume();
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
            if (_source == null || clip == null)
                return;
            try
            {
                SourceType.GetMethod("Stop", Type.EmptyTypes).Invoke(_source, null);
                SourceType.GetProperty("clip").SetValue(_source, clip, null);
                SourceType.GetProperty("time").SetValue(_source, 0f, null);
                WriteVolume();
                SourceType.GetMethod("Play", Type.EmptyTypes).Invoke(_source, null);
                _playing = true;
                _paused = false;
                Raise();
            }
            catch (Exception ex)
            {
                Plugin.LogError("Music play failed: " + ex.Message);
            }
        }

        private void Pause()
        {
            if (_source == null || !_playing)
                return;
            try { SourceType.GetMethod("Pause", Type.EmptyTypes).Invoke(_source, null); } catch { }
            _paused = true;
            Raise();
        }

        private void Resume()
        {
            if (_source == null)
                return;
            try { SourceType.GetMethod("UnPause", Type.EmptyTypes).Invoke(_source, null); } catch { }
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
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime < _duckUntil)
                _duckTarget = 0.08f;
            else
                _duckTarget = 1f;
            if (_playing || Mathf.Abs(_duck - _duckTarget) > 0.001f)
            {
                _duck = Mathf.MoveTowards(_duck, _duckTarget, dt / 0.28f);
                WriteVolume();
            }

            if (!_playing || _paused || _loading || _source == null)
                return;
            bool on = false;
            try
            {
                object v = SourceType.GetProperty("isPlaying").GetValue(_source, null);
                on = v is bool && (bool)v;
            }
            catch
            {
            }
            if (!on && _duckTarget > 0.9f)
                Skip(1);
        }

        private void WriteVolume()
        {
            if (_source == null)
                return;
            float v = PhoneTheme.MusicVolume * _duck;
            if (Mathf.Abs(v - _lastVol) < 0.002f)
                return;
            _lastVol = v;
            TrySet("volume", v);
        }

        private void TrySet(string name, object value)
        {
            try
            {
                PropertyInfo p = SourceType.GetProperty(name);
                if (p != null)
                    p.SetValue(_source, value, null);
            }
            catch
            {
            }
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
