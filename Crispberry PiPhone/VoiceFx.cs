using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Listener-side knobs on a remote scout's vanilla voice
    /// (low-pass, high-pass, reverb, echo). Same components PEAK already uses.
    /// </summary>
    public sealed class PiPhoneVoiceFilter
    {
        public bool LowPass;
        public float LowPassCutoff = 7500f;
        public float LowPassResonance = 1f;
        public bool HighPass;
        public float HighPassCutoff = 10f;
        public float HighPassResonance = 1f;
        public bool Reverb;
        public AudioReverbPreset ReverbPreset = AudioReverbPreset.Off;
        public bool Echo;
        public float EchoWet;
        public float EchoDry = 1f;
        public float EchoDelay = 10f;
        public float EchoDecay;

        /// <summary>Phone call, same plane: no cave/distance FX.</summary>
        public static PiPhoneVoiceFilter Dry()
        {
            return new PiPhoneVoiceFilter();
        }

        /// <summary>Living talking to a ghost, or a ghost talking to the living.</summary>
        public static PiPhoneVoiceFilter Realm()
        {
            return new PiPhoneVoiceFilter
            {
                LowPass = true,
                LowPassCutoff = 2800f,
                LowPassResonance = 1.05f,
                HighPass = true,
                HighPassCutoff = 900f,
                HighPassResonance = 1f,
                Reverb = true,
                ReverbPreset = AudioReverbPreset.Cave,
                Echo = true,
                EchoWet = 0.22f,
                EchoDry = 0.7f,
                EchoDelay = 280f,
                EchoDecay = 0.25f
            };
        }

        internal PiPhoneVoiceFilter Clone()
        {
            return new PiPhoneVoiceFilter
            {
                LowPass = LowPass,
                LowPassCutoff = LowPassCutoff,
                LowPassResonance = LowPassResonance,
                HighPass = HighPass,
                HighPassCutoff = HighPassCutoff,
                HighPassResonance = HighPassResonance,
                Reverb = Reverb,
                ReverbPreset = ReverbPreset,
                Echo = Echo,
                EchoWet = EchoWet,
                EchoDry = EchoDry,
                EchoDelay = EchoDelay,
                EchoDecay = EchoDecay
            };
        }
    }

    internal static class VoiceFx
    {
        private static readonly Dictionary<int, PiPhoneVoiceFilter> ApiHold = new Dictionary<int, PiPhoneVoiceFilter>();
        private static readonly Dictionary<int, PiPhoneVoiceFilter> CallLayer = new Dictionary<int, PiPhoneVoiceFilter>();
        private static readonly Dictionary<int, PiPhoneVoiceFilter> VanillaSnap = new Dictionary<int, PiPhoneVoiceFilter>();
        private static bool _logged;

        internal static bool TryGet(string id, out PiPhoneVoiceFilter filter)
        {
            filter = null;
            CharacterVoiceHandler handler = ScoutQuery.HandlerById(id);
            return TryRead(handler, out filter);
        }

        internal static bool TryGet(int actor, out PiPhoneVoiceFilter filter)
        {
            filter = null;
            CharacterVoiceHandler handler = ScoutQuery.HandlerByActor(actor);
            return TryRead(handler, out filter);
        }

        internal static bool SetHold(string id, PiPhoneVoiceFilter spec)
        {
            return SetHold(ScoutQuery.HandlerById(id), spec);
        }

        internal static bool SetHold(int actor, PiPhoneVoiceFilter spec)
        {
            return SetHold(ScoutQuery.HandlerByActor(actor), spec);
        }

        internal static bool ClearHold(string id)
        {
            return ClearHoldActor(ActorFromId(id));
        }

        internal static bool ClearHold(int actor)
        {
            return ClearHoldActor(actor);
        }

        internal static void ClearAllHolds()
        {
            var actors = new List<int>(ApiHold.Keys);
            ApiHold.Clear();
            for (int i = 0; i < actors.Count; i++)
                MaybeRestore(actors[i]);
        }

        internal static bool IsHeld(string id)
        {
            int actor = ActorFromId(id);
            return actor > 0 && ApiHold.ContainsKey(actor);
        }

        internal static void SetCallLayer(CharacterVoiceHandler handler, PiPhoneVoiceFilter spec)
        {
            int actor = ActorOf(handler);
            if (actor <= 0 || spec == null)
                return;
            RememberVanilla(actor, handler != null ? handler.voiceObscuranceFilter : null);
            CallLayer[actor] = spec.Clone();
            Write(handler != null ? handler.voiceObscuranceFilter : null, spec);
        }

        internal static void ClearCallLayer(CharacterVoiceHandler handler)
        {
            int actor = ActorOf(handler);
            if (actor <= 0)
                return;
            CallLayer.Remove(actor);
            MaybeRestore(actor);
        }

        internal static void ClearAllCallLayers()
        {
            var actors = new List<int>(CallLayer.Keys);
            CallLayer.Clear();
            for (int i = 0; i < actors.Count; i++)
                MaybeRestore(actors[i]);
        }

        internal static bool HasCallLayer(CharacterVoiceHandler handler)
        {
            int actor = ActorOf(handler);
            return actor > 0 && CallLayer.ContainsKey(actor);
        }

        private static bool SetHold(CharacterVoiceHandler handler, PiPhoneVoiceFilter spec)
        {
            int actor = ActorOf(handler);
            if (actor <= 0 || spec == null)
                return false;
            RememberVanilla(actor, handler.voiceObscuranceFilter);
            ApiHold[actor] = spec.Clone();
            Write(handler.voiceObscuranceFilter, spec);
            return true;
        }

        private static bool ClearHoldActor(int actor)
        {
            if (actor <= 0 || !ApiHold.Remove(actor))
                return false;
            MaybeRestore(actor);
            return true;
        }

        private static void RememberVanilla(int actor, VoiceObscuranceFilter fx)
        {
            if (actor <= 0 || fx == null || VanillaSnap.ContainsKey(actor))
                return;
            VanillaSnap[actor] = Read(fx);
        }

        private static void MaybeRestore(int actor)
        {
            if (actor <= 0)
                return;
            if (CallLayer.ContainsKey(actor) || ApiHold.ContainsKey(actor))
                return;
            CharacterVoiceHandler handler = ScoutQuery.HandlerByActor(actor);
            VoiceObscuranceFilter fx = handler != null ? handler.voiceObscuranceFilter : null;
            PiPhoneVoiceFilter snap;
            if (VanillaSnap.TryGetValue(actor, out snap))
            {
                Write(fx, snap);
                VanillaSnap.Remove(actor);
            }
            if (fx != null)
                fx.enabled = true;
        }

        private static bool TryRead(CharacterVoiceHandler handler, out PiPhoneVoiceFilter filter)
        {
            filter = null;
            if (handler == null || handler.voiceObscuranceFilter == null)
                return false;
            filter = Read(handler.voiceObscuranceFilter);
            return true;
        }

        private static PiPhoneVoiceFilter Read(VoiceObscuranceFilter fx)
        {
            var spec = new PiPhoneVoiceFilter();
            if (fx == null)
                return spec;
            AudioLowPassFilter low = fx.lowPass;
            if (low != null)
            {
                spec.LowPass = low.enabled;
                spec.LowPassCutoff = low.cutoffFrequency;
                spec.LowPassResonance = low.lowpassResonanceQ;
            }
            AudioHighPassFilter high = fx.highPass;
            if (high != null)
            {
                spec.HighPass = high.enabled;
                spec.HighPassCutoff = high.cutoffFrequency;
                spec.HighPassResonance = high.highpassResonanceQ;
            }
            AudioReverbFilter reverb = fx.reverb;
            if (reverb != null)
            {
                spec.Reverb = reverb.enabled;
                spec.ReverbPreset = reverb.reverbPreset;
            }
            AudioEchoFilter echo = fx.echo;
            if (echo != null)
            {
                spec.Echo = echo.enabled;
                spec.EchoWet = echo.wetMix;
                spec.EchoDry = echo.dryMix;
                spec.EchoDelay = echo.delay;
                spec.EchoDecay = echo.decayRatio;
            }
            return spec;
        }

        internal static void Write(VoiceObscuranceFilter fx, PiPhoneVoiceFilter spec)
        {
            if (fx == null || spec == null)
                return;
            try
            {
                AudioLowPassFilter low = fx.lowPass;
                if (low != null)
                {
                    low.enabled = spec.LowPass;
                    if (spec.LowPass)
                    {
                        low.cutoffFrequency = spec.LowPassCutoff;
                        low.lowpassResonanceQ = spec.LowPassResonance;
                    }
                }
                AudioHighPassFilter high = fx.highPass;
                if (high != null)
                {
                    high.enabled = spec.HighPass;
                    if (spec.HighPass)
                    {
                        high.cutoffFrequency = spec.HighPassCutoff;
                        high.highpassResonanceQ = spec.HighPassResonance;
                    }
                }
                AudioReverbFilter reverb = fx.reverb;
                if (reverb != null)
                {
                    reverb.enabled = spec.Reverb;
                    if (spec.Reverb)
                        reverb.reverbPreset = spec.ReverbPreset;
                }
                AudioEchoFilter echo = fx.echo;
                if (echo != null)
                {
                    echo.enabled = spec.Echo;
                    echo.wetMix = spec.Echo ? spec.EchoWet : 0f;
                    echo.dryMix = spec.Echo ? spec.EchoDry : 1f;
                    echo.delay = spec.Echo ? spec.EchoDelay : 10f;
                    echo.decayRatio = spec.Echo ? spec.EchoDecay : 0f;
                }
            }
            catch (Exception ex)
            {
                LogOnce("Voice filter write failed: " + ex.Message);
            }
        }

        private static bool TryOverride(VoiceObscuranceFilter fx, out PiPhoneVoiceFilter spec)
        {
            spec = null;
            if (fx == null || fx._voiceHandler == null)
                return false;
            int actor = ActorOf(fx._voiceHandler);
            if (actor <= 0)
                return false;
            if (CallLayer.TryGetValue(actor, out spec))
                return true;
            return ApiHold.TryGetValue(actor, out spec);
        }

        private static int ActorOf(CharacterVoiceHandler handler)
        {
            if (handler == null)
                return 0;
            return ScoutQuery.ActorOf(handler.m_character);
        }

        private static int ActorFromId(string id)
        {
            CharacterVoiceHandler handler = ScoutQuery.HandlerById(id);
            return ActorOf(handler);
        }

        private static void LogOnce(string message)
        {
            if (_logged)
                return;
            _logged = true;
            Plugin.LogError(message);
        }

        [HarmonyPatch(typeof(VoiceObscuranceFilter), "Update")]
        private static class Patch_VoiceFx
        {
            private static bool Prefix(VoiceObscuranceFilter __instance)
            {
                try
                {
                    PiPhoneVoiceFilter spec;
                    if (!TryOverride(__instance, out spec))
                        return true;
                    Write(__instance, spec);
                    return false;
                }
                catch (Exception ex)
                {
                    LogOnce("Voice filter patch failed: " + ex.Message);
                    return true;
                }
            }
        }
    }
}
