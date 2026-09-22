using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// A dialer number other mods (or PiPhone) can handle. Register with
    /// <see cref="PiPhoneApi.RegisterNumber"/>. Return true from <see cref="OnCall"/>
    /// if you handled the call.
    /// </summary>
    public sealed class PiPhoneNumber
    {
        /// <summary>Digits only, e.g. "911".</summary>
        public string Number;

        /// <summary>Optional toast / recents label.</summary>
        public string Label;

        /// <summary>Return true if this number was handled.</summary>
        public Func<IPiPhoneHost, string, bool> OnCall;
    }

    internal static class PhoneNumbers
    {
        private static readonly List<PiPhoneNumber> Handlers = new List<PiPhoneNumber>();
        private static Scoutmaster _forced;
        private static float _forcedUntil;

        public static void Register(PiPhoneNumber number)
        {
            if (number == null || string.IsNullOrEmpty(number.Number) || number.OnCall == null)
                return;
            string digits = Digits(number.Number);
            if (string.IsNullOrEmpty(digits))
                return;
            number.Number = digits;
            for (int i = 0; i < Handlers.Count; i++)
            {
                if (Handlers[i] != null && Handlers[i].Number == digits)
                {
                    Handlers[i] = number;
                    return;
                }
            }
            Handlers.Add(number);
        }

        public static bool Unregister(string number)
        {
            string digits = Digits(number);
            if (string.IsNullOrEmpty(digits))
                return false;
            for (int i = Handlers.Count - 1; i >= 0; i--)
            {
                if (Handlers[i] != null && Handlers[i].Number == digits)
                {
                    Handlers.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public static bool TryCall(IPiPhoneHost host, string number)
        {
            string digits = Digits(number);
            if (string.IsNullOrEmpty(digits))
                return false;
            for (int i = 0; i < Handlers.Count; i++)
            {
                PiPhoneNumber n = Handlers[i];
                if (n == null || n.OnCall == null || n.Number != digits)
                    continue;
                try
                {
                    if (n.OnCall(host, digits))
                        return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError("Dial '" + digits + "' failed: " + ex.Message);
                }
            }
            return false;
        }

        internal static void RegisterBuiltins()
        {
            string[] rescue = { "911", "112", "999", "4357" };
            for (int i = 0; i < rescue.Length; i++)
            {
                string digits = rescue[i];
                Register(new PiPhoneNumber
                {
                    Number = digits,
                    Label = "Rescue",
                    OnCall = CallScoutmaster
                });
            }
        }

        private static bool _rescueAck;
        private static bool _rescueOk;
        private static string _rescueMsg = string.Empty;

        private static bool CallScoutmaster(IPiPhoneHost host, string digits)
        {
            if (host != null)
                host.StartHostCoroutine(CallScoutmasterRoutine(host, digits));
            return true;
        }

        private static IEnumerator CallScoutmasterRoutine(IPiPhoneHost host, string digits)
        {
            Character ghostCheck = Character.localCharacter;
            if (ghostCheck != null && ghostCheck.IsGhost)
            {
                if (host != null)
                    host.ShowToast("You're already beyond rescue!");
                yield break;
            }
            if (host != null)
                host.ShowToast("Calling Rescue...");
            yield return new WaitForSecondsRealtime(0.8f);
            Character local = Character.localCharacter;
            if (local == null || local.photonView == null)
            {
                if (host != null)
                    host.ShowToast("Join a game first.");
                yield break;
            }
            if (!PhotonNetwork.InRoom)
            {
                if (host != null)
                    host.ShowToast("No signal.");
                yield break;
            }
            const float chase = 60f;
            if (PhotonNetwork.IsMasterClient)
            {
                string err;
                if (RunRescueOnHost(local, chase, out err))
                {
                    StartChaseVideo(chase);
                    if (host != null)
                        host.ShowToast(CountLivingScouts() < 2
                            ? "Scoutmaster is coming for you. (solo)"
                            : "Scoutmaster is coming for you.");
                }
                else if (host != null)
                    host.ShowToast(err);
                yield break;
            }
            Photon.Realtime.Player master = PhotonNetwork.MasterClient;
            if (master == null)
            {
                if (host != null)
                    host.ShowToast("No expedition leader.");
                yield break;
            }
            _rescueAck = false;
            _rescueOk = false;
            _rescueMsg = string.Empty;
            PhoneNet.SendRescue(master.ActorNumber, local.photonView.ViewID, chase);
            float until = Time.unscaledTime + 2.5f;
            while (!_rescueAck && Time.unscaledTime < until)
                yield return null;
            if (!_rescueAck)
            {
                if (host != null)
                    host.ShowToast("Rescue needs the expedition leader to have PiPhone.");
                yield break;
            }
            if (host != null && !string.IsNullOrEmpty(_rescueMsg))
                host.ShowToast(_rescueMsg);
        }

        internal static void OnRescueRequest(ExitGames.Client.Photon.EventData photonEvent, object[] data)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;
            int reply = 0;
            if (photonEvent != null)
                reply = photonEvent.Sender;
            if (data == null || data.Length < 5)
            {
                if (reply > 0)
                    PhoneNet.SendRescueResult(reply, false, "Bad Rescue packet.");
                return;
            }
            int viewId = Convert.ToInt32(data[3]);
            float seconds = 60f;
            try { seconds = Convert.ToSingle(data[4]); } catch { }
            Character target = CharacterFromView(viewId);
            string err;
            bool ok = RunRescueOnHost(target, seconds, out err);
            if (reply > 0)
                PhoneNet.SendRescueResult(reply, ok, ok
                    ? (CountLivingScouts() < 2 ? "Scoutmaster is coming for you. (solo)" : "Scoutmaster is coming for you.")
                    : err);
            if (ok && reply > 0)
                PhoneNet.SendRescueChase(reply, seconds);
        }

        internal static void OnRescueResult(object[] data)
        {
            _rescueAck = true;
            _rescueOk = data != null && data.Length > 3 && data[3] is bool && (bool)data[3];
            _rescueMsg = data != null && data.Length > 4 ? data[4] as string : string.Empty;
        }

        internal static void OnRescueChase(object[] data)
        {
            float seconds = 60f;
            if (data != null && data.Length > 3)
            {
                try { seconds = Convert.ToSingle(data[3]); } catch { }
            }
            StartChaseVideo(seconds);
        }

        private static bool RunRescueOnHost(Character target, float seconds, out string error)
        {
            error = string.Empty;
            if (target == null)
            {
                error = "Couldn't find that scout.";
                return false;
            }
            Scoutmaster sm;
            if (!Scoutmaster.GetPrimaryScoutmaster(out sm) || sm == null)
            {
                TrySpawnScoutmaster();
                Scoutmaster.GetPrimaryScoutmaster(out sm);
            }
            if (sm == null)
            {
                error = "Scoutmaster isn't here.";
                return false;
            }
            if (sm.preventSpawning)
            {
                error = "Scoutmaster isn't on this ascent.";
                return false;
            }
            _forced = sm;
            _forcedUntil = Time.time + 3600f;
            try
            {
                if (sm.currentTarget == target)
                    sm.SetCurrentTarget(null);
                sm.SetCurrentTarget(target, seconds);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Rescue SetCurrentTarget failed: " + ex.Message);
                error = "Scoutmaster didn't take the call.";
                return false;
            }
            Plugin.LogInfo("Rescue targeted view " + (target.photonView != null ? target.photonView.ViewID : 0)
                + " living=" + CountLivingScouts() + " height=" + target.Center.y.ToString("0") + ".");
            return true;
        }

        private static void StartChaseVideo(float seconds)
        {
            PhoneMenu.Open();
            Character prey = Character.localCharacter;
            Character hunter = CallVideo.FindScoutmasterBody();
            CallVideo.StartChase(hunter, prey, seconds);
        }

        private static Character CharacterFromView(int viewId)
        {
            if (viewId <= 0)
                return null;
            try
            {
                PhotonView view = PhotonView.Find(viewId);
                if (view != null)
                    return view.GetComponent<Character>();
            }
            catch
            {
            }
            return null;
        }

        internal static bool IsForcedChase(Scoutmaster sm)
        {
            return sm != null && sm == _forced && Time.time < _forcedUntil;
        }

        public static void RequestDismiss()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                DismissScoutmaster();
                return;
            }
            Photon.Realtime.Player master = PhotonNetwork.MasterClient;
            if (master != null)
                PhoneNet.SendRescueEnd(master.ActorNumber);
        }

        internal static void OnRescueEnd()
        {
            if (PhotonNetwork.IsMasterClient)
                DismissScoutmaster();
        }

        private static void DismissScoutmaster()
        {
            _forcedUntil = 0f;
            Scoutmaster sm;
            if (!Scoutmaster.GetPrimaryScoutmaster(out sm) || sm == null)
                return;
            try
            {
                sm.SetCurrentTarget(null);
            }
            catch
            {
            }
            try
            {
                sm.discovered = null;
            }
            catch
            {
            }
            try
            {
                PhotonView view = sm.view != null ? sm.view : sm.GetComponent<PhotonView>();
                if (view != null)
                {
                    view.RPC("WarpPlayerRPC", RpcTarget.All, new Vector3(0f, 0f, 5000f), false);
                    view.RPC("StopClimbingRpc", RpcTarget.All, 0f);
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Scoutmaster dismiss warp failed: " + ex.Message);
            }
            Plugin.LogInfo("Rescue sent Scoutmaster away.");
        }

        private static int CountLivingScouts()
        {
            int n = 0;
            List<Character> all = Character.AllCharacters;
            if (all == null)
                return 0;
            for (int i = 0; i < all.Count; i++)
            {
                Character c = all[i];
                if (c == null || c.isBot || c.data == null)
                    continue;
                if (!c.data.dead && !c.data.fullyPassedOut)
                    n++;
            }
            return n;
        }

        private static void TrySpawnScoutmaster()
        {
            try
            {
                ScoutmasterSpawner spawner = UnityEngine.Object.FindFirstObjectByType<ScoutmasterSpawner>(FindObjectsInactive.Include);
                if (spawner != null)
                    spawner.SpawnScoutmaster();
            }
            catch (Exception ex)
            {
                Plugin.LogError("Scoutmaster spawn failed: " + ex.Message);
            }
        }

        internal static string Digits(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;
            var sb = new System.Text.StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (char.IsDigit(raw[i]))
                    sb.Append(raw[i]);
            }
            return sb.ToString();
        }
    }

    [HarmonyPatch(typeof(Scoutmaster), "VerifyTarget")]
    internal static class Patch_KeepForcedScoutmaster
    {
        private static bool Prefix(Scoutmaster __instance)
        {
            return !PhoneNumbers.IsForcedChase(__instance);
        }
    }
}
