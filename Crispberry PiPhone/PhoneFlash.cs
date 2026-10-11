using System.Collections.Generic;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class PhoneFlash
    {
        private static readonly Color WhiteLed = new Color(1f, 0.97f, 0.92f, 1f);
        private static readonly Color WarmLed = new Color(1f, 0.72f, 0.42f, 1f);
        private static readonly Color CoolLed = new Color(0.75f, 0.86f, 1f, 1f);
        private static readonly Color Peach = new Color(0.86f, 0.67f, 0.52f, 1f);

        private static Pump _pump;
        private static Light _local;
        private static Light _spill;
        private static Light _camera;
        private static float _burstUntil;
        private static bool _burstFront;
        private static bool _videoHold;
        private static Color _skin = Peach;
        private static float _skinAt = -10f;
        private static bool _sentOn;
        private static bool _sentCam;
        private static float _sentBright = -1f;
        private static float _sentFocus = -1f;
        private static int _sentColor = -1;
        private static float _nextBeat;
        private static readonly Dictionary<int, Lamp> Remote = new Dictionary<int, Lamp>();

        public static bool Lit
        {
            get { return PhoneTheme.FlashOn || _videoHold || Time.unscaledTime < _burstUntil; }
        }

        public static void Ensure()
        {
            if (_pump != null)
                return;
            var go = new GameObject("PiPhoneFlash");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _pump = go.AddComponent<Pump>();
        }

        public static void Toggle()
        {
            PhoneTheme.SetFlashOn(!PhoneTheme.FlashOn);
        }

        public static void Burst(bool front)
        {
            _burstFront = front;
            _burstUntil = Time.unscaledTime + 0.28f;
            ApplyLocal(Character.localCharacter);
            Push();
        }

        public static void HoldVideo(bool on)
        {
            _videoHold = on;
            ApplyLocal(Character.localCharacter);
            Push();
        }

        public static void Push()
        {
            _sentBright = -1f;
            Send(true);
        }

        public static void Hear(int actor, bool on, float bright, byte r, byte g, byte b, bool cam, float camPower, float focus, float camRange)
        {
            if (actor <= 0)
                return;
            Lamp lamp = RemoteLamp(actor);
            if (lamp.Spot != null)
            {
                lamp.Spot.enabled = on;
                if (on)
                    PaintSpot(lamp.Spot, new Color(r / 255f, g / 255f, b / 255f, 1f), bright, focus);
                else
                    SyncBeam(lamp.Spot, false, Color.white, 0f, 0f);
            }
            if (lamp.Spill != null)
                lamp.Spill.enabled = false;
            if (lamp.Flood != null)
            {
                lamp.Flood.enabled = cam;
                if (cam)
                    PaintFlood(lamp.Flood, camPower > 0.2f ? camPower : 4f, camRange > 1f ? camRange : 8f, camRange < 6f ? 100f : 130f);
            }
        }

        private static void Tick()
        {
            ApplyLocal(Character.localCharacter);
            PlaceRemotes();
            if (Time.unscaledTime >= _nextBeat)
            {
                _nextBeat = Time.unscaledTime + 1.5f;
                Send(false);
            }
        }

        private static void ApplyLocal(Character character)
        {
            bool flash = PhoneTheme.FlashOn;
            bool cam = Time.unscaledTime < _burstUntil || _videoHold;
            Light spot = LocalLight();
            Light spill = SpillLight();
            Light flood = CameraLight();
            if (spot != null)
            {
                spot.enabled = flash;
                if (flash)
                {
                    PaintSpot(spot, Current(), PhoneTheme.FlashBright, PhoneTheme.FlashFocus);
                    Place(spot.transform, character);
                }
                else
                    SyncBeam(spot, false, Color.white, 0f, 0f);
            }
            if (spill != null)
                spill.enabled = false;
            if (flood != null)
            {
                flood.enabled = cam;
                if (cam)
                {
                    bool photo = Time.unscaledTime < _burstUntil && !_videoHold;
                    bool front = _videoHold ? CameraApp.FacingFront : _burstFront;
                    float intensity;
                    float range;
                    CamLevels(front, photo, out intensity, out range);
                    PaintFlood(flood, intensity, range, front ? 100f : 130f);
                    PlaceFlood(flood.transform, character);
                }
            }
        }

        private static void PaintSpot(Light spot, Color color, float bright, float focus)
        {
            float t = Mathf.Clamp01(bright);
            float beam = Mathf.Clamp01(focus);
            float reach = Mathf.Lerp(20f, 56f, beam) * Mathf.Lerp(0.45f, 1f, t);
            float angle = Mathf.Lerp(120f, 8f, beam);
            spot.type = LightType.Spot;
            spot.color = color;
            spot.shadows = LightShadows.None;
            spot.renderMode = LightRenderMode.ForcePixel;
            spot.intensity = Mathf.Lerp(0.7f, 5.5f, t) * Mathf.Lerp(0.85f, 1.35f, beam);
            spot.range = reach;
            spot.spotAngle = angle;
            spot.innerSpotAngle = Mathf.Max(2f, angle * 0.65f);
            PaintHalo(spot, color, t, beam, reach, angle);
            SyncBeam(spot, true, color, t, beam);
        }

        private static void PaintHalo(Light spot, Color color, float bright, float focus, float reach, float angle)
        {
            Light halo = HaloLight(spot);
            if (halo == null)
                return;
            halo.type = LightType.Spot;
            halo.color = color;
            halo.shadows = LightShadows.None;
            halo.renderMode = LightRenderMode.ForcePixel;
            halo.spotAngle = Mathf.Min(160f, angle + Mathf.Lerp(42f, 7f, focus));
            halo.innerSpotAngle = Mathf.Max(2f, angle * 0.9f);
            halo.range = reach * Mathf.Lerp(0.5f, 0.16f, focus);
            halo.intensity = spot.intensity * Mathf.Lerp(0.42f, 0.05f, focus) * Mathf.Lerp(0.65f, 1f, bright);
            halo.enabled = true;
        }

        private static Light HaloLight(Light spot)
        {
            Transform child = spot.transform.Find("PiPhoneFlashHalo");
            if (child != null)
            {
                Light found = child.GetComponent<Light>();
                if (found != null)
                    return found;
            }
            var go = new GameObject("PiPhoneFlashHalo");
            go.transform.SetParent(spot.transform, false);
            Light halo = go.AddComponent<Light>();
            halo.type = LightType.Spot;
            halo.shadows = LightShadows.None;
            halo.enabled = false;
            return halo;
        }

        private static void HideHalo(Light spot)
        {
            if (spot == null)
                return;
            Transform child = spot.transform.Find("PiPhoneFlashHalo");
            if (child == null)
                return;
            Light halo = child.GetComponent<Light>();
            if (halo != null)
                halo.enabled = false;
        }

        private static void PaintSpill(Light spill, Color color, float bright, float focus)
        {
            float t = Mathf.Clamp01(bright);
            float beam = Mathf.Clamp01(focus);
            spill.type = LightType.Point;
            spill.color = color;
            spill.shadows = LightShadows.None;
            spill.renderMode = LightRenderMode.ForcePixel;
            spill.intensity = Mathf.Lerp(0.25f, 1.3f, t) * Mathf.Lerp(1f, 0.22f, beam);
            spill.range = Mathf.Lerp(2.2f, 4.5f, t) * Mathf.Lerp(1f, 0.5f, beam);
        }

        private static void CamLevels(bool front, bool photo, out float intensity, out float range)
        {
            if (front && photo)
            {
                intensity = 4f;
                range = 5.5f;
            }
            else if (front)
            {
                intensity = 2.2f;
                range = 4.2f;
            }
            else if (photo)
            {
                intensity = 9f;
                range = 12f;
            }
            else
            {
                intensity = 5.5f;
                range = 9f;
            }
        }

        private static void PaintFlood(Light flood, float intensity, float range, float angle)
        {
            flood.type = LightType.Spot;
            flood.color = Color.white;
            flood.intensity = intensity;
            flood.range = range;
            flood.spotAngle = angle;
            flood.innerSpotAngle = Mathf.Max(8f, angle * 0.72f);
            flood.shadows = LightShadows.None;
            flood.renderMode = LightRenderMode.ForcePixel;
        }

        internal static void FollowFrame()
        {
            if (Time.unscaledTime >= _burstUntil && !_videoHold)
                return;
            ApplyLocal(Character.localCharacter);
        }

        private static void Send(bool reliable)
        {
            bool on = PhoneTheme.FlashOn;
            bool cam = Time.unscaledTime < _burstUntil || _videoHold;
            float bright = PhoneTheme.FlashBright;
            float focus = PhoneTheme.FlashFocus;
            Color color = on ? Current() : WhiteLed;
            float camPower = 0f;
            float camRange = 0f;
            if (cam)
            {
                bool photo = Time.unscaledTime < _burstUntil && !_videoHold;
                bool front = _videoHold ? CameraApp.FacingFront : _burstFront;
                CamLevels(front, photo, out camPower, out camRange);
            }
            int packed = ((int)(color.r * 255f) << 16) | ((int)(color.g * 255f) << 8) | (int)(color.b * 255f);
            if (!reliable && on == _sentOn && cam == _sentCam && Mathf.Abs(bright - _sentBright) < 0.02f && Mathf.Abs(focus - _sentFocus) < 0.02f && packed == _sentColor)
                return;
            _sentOn = on;
            _sentCam = cam;
            _sentBright = bright;
            _sentFocus = focus;
            _sentColor = packed;
            PhoneNet.SendFlash(on, bright, (byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), cam, camPower, focus, camRange, reliable || !on && !cam);
        }

        private static Color Current()
        {
            switch (PhoneTheme.FlashMode)
            {
                case 1: return WarmLed;
                case 2: return CoolLed;
                case 3: return Skin();
                case 4: return PhoneTheme.FlashCustom;
                default: return WhiteLed;
            }
        }

        private static Color Skin()
        {
            if (Time.unscaledTime - _skinAt < 2f)
                return _skin;
            _skinAt = Time.unscaledTime;
            _skin = SampleSkin();
            return _skin;
        }

        private static Color SampleSkin()
        {
            Character character;
            try
            {
                character = Character.localCharacter;
            }
            catch
            {
                return Peach;
            }
            if (character == null)
                return Peach;
            try
            {
                if (character.refs != null && character.refs.customization != null)
                {
                    Color tint = character.refs.customization.PlayerColor;
                    tint.a = 1f;
                    if (tint.maxColorComponent > 0.05f)
                        return tint;
                }
            }
            catch
            {
            }
            return Peach;
        }

        private static Light LocalLight()
        {
            if (_local != null)
                return _local;
            var go = new GameObject("PiPhoneFlashLocal");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _local = go.AddComponent<Light>();
            _local.type = LightType.Spot;
            _local.shadows = LightShadows.None;
            _local.spotAngle = 55f;
            _local.enabled = false;
            return _local;
        }

        private static Light SpillLight()
        {
            if (_spill != null)
                return _spill;
            var go = new GameObject("PiPhoneFlashSpill");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _spill = go.AddComponent<Light>();
            _spill.type = LightType.Point;
            _spill.shadows = LightShadows.None;
            _spill.renderMode = LightRenderMode.ForcePixel;
            _spill.range = 4f;
            _spill.enabled = false;
            return _spill;
        }

        private static Light CameraLight()
        {
            if (_camera != null)
                return _camera;
            var go = new GameObject("PiPhoneCameraLight");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _camera = go.AddComponent<Light>();
            _camera.type = LightType.Spot;
            _camera.shadows = LightShadows.None;
            _camera.renderMode = LightRenderMode.ForcePixel;
            _camera.color = Color.white;
            _camera.range = 12f;
            _camera.spotAngle = 130f;
            _camera.enabled = false;
            return _camera;
        }

        private static Lamp RemoteLamp(int actor)
        {
            Lamp lamp;
            if (Remote.TryGetValue(actor, out lamp) && lamp != null && lamp.Spot != null)
                return lamp;
            lamp = new Lamp();
            lamp.Spot = MakeRemote("PiPhoneFlashRemote", 55f);
            lamp.Spill = MakeRemote("PiPhoneFlashSpill", 4f);
            lamp.Spill.type = LightType.Point;
            lamp.Flood = MakeRemote("PiPhoneCameraRemote", 130f);
            lamp.Flood.type = LightType.Spot;
            lamp.Flood.color = Color.white;
            lamp.Flood.range = 12f;
            Remote[actor] = lamp;
            return lamp;
        }

        private static Light MakeRemote(string name, float angle)
        {
            var go = new GameObject(name);
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.shadows = LightShadows.None;
            light.spotAngle = angle;
            light.enabled = false;
            return light;
        }

        private sealed class Lamp
        {
            public Light Spot;
            public Light Spill;
            public Light Flood;
        }

        private static void PlaceRemotes()
        {
            if (Remote.Count == 0)
                return;
            Character[] all = null;
            try
            {
                all = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }
            catch
            {
            }
            var gone = new List<int>();
            foreach (KeyValuePair<int, Lamp> pair in Remote)
            {
                Lamp lamp = pair.Value;
                if (lamp == null || (lamp.Spot == null && lamp.Flood == null && lamp.Spill == null))
                {
                    gone.Add(pair.Key);
                    continue;
                }
                Character character = FindActor(all, pair.Key);
                if (character == null)
                {
                    if (lamp.Spot != null)
                        lamp.Spot.enabled = false;
                    if (lamp.Spill != null)
                        lamp.Spill.enabled = false;
                    if (lamp.Flood != null)
                        lamp.Flood.enabled = false;
                    continue;
                }
                if (lamp.Spot != null)
                    Place(lamp.Spot.transform, character);
                if (lamp.Spill != null)
                    Place(lamp.Spill.transform, character);
                if (lamp.Flood != null)
                    Place(lamp.Flood.transform, character);
            }
            for (int i = 0; i < gone.Count; i++)
                Remote.Remove(gone[i]);
        }

        private static Character FindActor(Character[] all, int actor)
        {
            if (all == null)
                return null;
            for (int i = 0; i < all.Length; i++)
            {
                Character character = all[i];
                if (character == null)
                    continue;
                try
                {
                    if (character.photonView != null && character.photonView.Owner != null && character.photonView.Owner.ActorNumber == actor)
                        return character;
                }
                catch
                {
                }
            }
            return null;
        }

        private static void Place(Transform light, Character character)
        {
            if (light == null || character == null)
                return;
            Transform anchor = character.transform;
            try
            {
                if (character.refs != null && character.refs.head != null)
                    anchor = character.refs.head.transform;
            }
            catch
            {
            }
            Vector3 aim = anchor.forward;
            try
            {
                if (character.data != null && character.data.lookDirection.sqrMagnitude > 0.01f)
                    aim = character.data.lookDirection;
            }
            catch
            {
            }
            aim.Normalize();
            light.position = anchor.position + aim * 0.18f;
            light.rotation = Quaternion.LookRotation(aim, Vector3.up);
        }

        private static void PlaceFlood(Transform light, Character character)
        {
            Vector3 pos;
            Quaternion rot;
            float fov;
            bool front;
            if (CameraApp.TryWorldPose(out pos, out rot, out fov, out front))
            {
                light.position = pos + rot * Vector3.forward * 0.4f;
                light.rotation = rot;
                return;
            }
            Place(light, character);
        }

        private static void SyncBeam(Light spot, bool on, Color color, float bright, float focus)
        {
            if (spot == null)
                return;
            Shaft shaft = spot.GetComponent<Shaft>();
            if (!on)
            {
                HideHalo(spot);
                if (shaft != null)
                    shaft.SetVisible(false);
                return;
            }
            if (shaft == null)
                shaft = spot.gameObject.AddComponent<Shaft>();
            shaft.SetVisible(true);
            shaft.Shape(color, spot.range, spot.spotAngle, bright, focus);
        }

        private sealed class Shaft : MonoBehaviour
        {
            private Mesh _mesh;
            private MeshRenderer _renderer;
            private float _range = -1f;
            private float _angle = -1f;
            private float _focus = -1f;
            private float _bright = -1f;
            private Color _color;
            private static Material _mat;

            public void SetVisible(bool on)
            {
                if (_renderer != null)
                    _renderer.enabled = on;
            }

            public void Shape(Color color, float range, float angle, float bright, float focus)
            {
                Ensure();
                if (_renderer == null)
                    return;
                _renderer.enabled = true;
                if (Mathf.Abs(range - _range) < 0.25f && Mathf.Abs(angle - _angle) < 0.5f && Mathf.Abs(focus - _focus) < 0.02f && Mathf.Abs(bright - _bright) < 0.02f && color == _color)
                    return;
                _range = range;
                _angle = angle;
                _focus = focus;
                _bright = bright;
                _color = color;
                Build(color, range, angle, bright, focus);
            }

            private void Ensure()
            {
                if (_renderer != null)
                    return;
                Material mat = BeamMaterial();
                if (mat == null)
                    return;
                _mesh = new Mesh();
                _mesh.name = "PiPhoneBeam";
                var filter = gameObject.GetComponent<MeshFilter>();
                if (filter == null)
                    filter = gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = _mesh;
                _renderer = gameObject.GetComponent<MeshRenderer>();
                if (_renderer == null)
                    _renderer = gameObject.AddComponent<MeshRenderer>();
                _renderer.sharedMaterial = mat;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }

            private static Material BeamMaterial()
            {
                if (_mat != null)
                    return _mat;
                Shader shader = Shader.Find("Particles/Additive");
                if (shader == null)
                    shader = Shader.Find("Legacy Shaders/Particles/Additive");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    return null;
                _mat = new Material(shader);
                _mat.SetInt("_Cull", 0);
                _mat.renderQueue = 3000;
                return _mat;
            }

            private void Build(Color color, float range, float angle, float bright, float focus)
            {
                const int rings = 12;
                const int sides = 12;
                int verts = (rings + 1) * sides;
                var pos = new Vector3[verts];
                var col = new Color[verts];
                var tri = new int[rings * sides * 6];
                float length = Mathf.Max(0.4f, range);
                float endR = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad) * length;
                float strength = Mathf.Lerp(0.22f, 0.9f, bright) * Mathf.Lerp(0.45f, 1.15f, focus);
                int v = 0;
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    float radius = Mathf.Max(0.01f, endR * t);
                    float fade = Mathf.Lerp(1f - Mathf.Clamp01(t / 0.34f), Mathf.Lerp(1f, 0.08f, t * t), focus);
                    Color tint = color * (fade * strength);
                    tint.a = fade;
                    for (int s = 0; s < sides; s++)
                    {
                        float a = (s / (float)sides) * Mathf.PI * 2f;
                        pos[v] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, t * length);
                        col[v] = tint;
                        v++;
                    }
                }
                int n = 0;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < sides; s++)
                    {
                        int i0 = r * sides + s;
                        int i1 = r * sides + (s + 1) % sides;
                        int i2 = i0 + sides;
                        int i3 = i1 + sides;
                        tri[n++] = i0;
                        tri[n++] = i2;
                        tri[n++] = i1;
                        tri[n++] = i1;
                        tri[n++] = i2;
                        tri[n++] = i3;
                    }
                }
                _mesh.Clear();
                _mesh.vertices = pos;
                _mesh.colors = col;
                _mesh.triangles = tri;
                _mesh.RecalculateBounds();
            }
        }

        private sealed class Pump : MonoBehaviour
        {
            private void Update()
            {
                Tick();
            }
        }
    }
}
