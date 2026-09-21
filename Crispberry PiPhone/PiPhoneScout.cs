using System;
using System.Collections.Generic;
using UnityEngine;
using PhotonPlayer = Photon.Realtime.Player;

namespace Crispberry_PiPhone
{
    /// <summary>
    /// Snapshot of a scout's identity and character state.
    /// <see cref="Id"/> is the same string Messages uses for that player.
    /// </summary>
    public sealed class PiPhoneScout
    {
        public string Id;
        public string Name;
        public int ActorNumber;
        public bool IsLocal;
        public bool IsDead;
        public bool IsGhost;
        public bool IsPassedOut;
        public bool IsFullyPassedOut;
        public bool IsFullyConscious;
        /// <summary>0–1 fade into a pass-out. 1 is down.</summary>
        public float PassOutValue;
        /// <summary>
        /// PEAK 0–1 death bar while fully passed out. They die at 1.
        /// Usual bleed is about 60s; last scout alive is about 10s; holding give-up is faster.
        /// </summary>
        public float DeathTimer;
        /// <summary>1 − <see cref="DeathTimer"/>. Use with a bar; 0 means they are about to die or already dead.</summary>
        public float DeathLeft;
        /// <summary>Estimate in seconds at the usual 60s bleed. 0 if not fully passed out or already dead.</summary>
        public float DeathSecondsLeft;
        /// <summary>Seconds since they died. 0 if still alive.</summary>
        public float TimeSinceDied;
        public bool IsInFog;
        public bool IsInWater;
        public bool IsCarried;
        public string CarrierId;
        public string CarrierName;
        public int CarrierActor;
        public bool IsCarrying;
        public string CarriedId;
        public string CarriedName;
        public int CarriedActor;
        public Vector3 Position;
        public Vector3 VoicePosition;
    }

    internal static class ScoutQuery
    {
        internal static PiPhoneScout Local()
        {
            try
            {
                Character local = Character.localCharacter;
                if (local != null)
                    return FromCharacter(local);
            }
            catch
            {
            }
            return null;
        }

        internal static bool LocalIsDead()
        {
            PiPhoneScout scout = Local();
            return scout != null && scout.IsDead;
        }

        internal static bool LocalIsGhost()
        {
            PiPhoneScout scout = Local();
            return scout != null && scout.IsGhost;
        }

        internal static PiPhoneScout[] All(bool othersOnly)
        {
            var list = new List<PiPhoneScout>();
            try
            {
                List<Character> all = Character.AllCharacters;
                if (all == null)
                    return list.ToArray();
                for (int i = 0; i < all.Count; i++)
                {
                    Character character = all[i];
                    if (character == null)
                        continue;
                    try
                    {
                        if (character.isBot)
                            continue;
                    }
                    catch
                    {
                    }
                    PiPhoneScout scout = FromCharacter(character);
                    if (scout == null)
                        continue;
                    if (othersOnly && scout.IsLocal)
                        continue;
                    list.Add(scout);
                }
            }
            catch
            {
            }
            return list.ToArray();
        }

        internal static PiPhoneScout[] Where(Func<PiPhoneScout, bool> match)
        {
            PiPhoneScout[] all = All(false);
            if (match == null)
                return all;
            var list = new List<PiPhoneScout>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && match(all[i]))
                    list.Add(all[i]);
            }
            return list.ToArray();
        }

        internal static bool TryById(string id, out PiPhoneScout scout)
        {
            scout = null;
            if (string.IsNullOrEmpty(id))
                return false;
            PiPhoneScout[] all = All(false);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].Id, id, StringComparison.Ordinal))
                {
                    scout = all[i];
                    return true;
                }
            }
            return false;
        }

        internal static bool TryByActor(int actor, out PiPhoneScout scout)
        {
            scout = null;
            if (actor <= 0)
                return false;
            PiPhoneScout[] all = All(false);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].ActorNumber == actor)
                {
                    scout = all[i];
                    return true;
                }
            }
            return false;
        }

        private static PiPhoneScout FromCharacter(Character character)
        {
            if (character == null)
                return null;
            var scout = new PiPhoneScout();
            PhotonPlayer owner = null;
            try
            {
                if (character.photonView != null)
                    owner = character.photonView.Owner;
            }
            catch
            {
            }
            if (owner != null)
            {
                scout.Id = BuiltinApps.PlayerId(owner);
                scout.Name = BuiltinApps.PlayerName(owner);
                scout.ActorNumber = owner.ActorNumber;
                scout.IsLocal = owner.IsLocal;
            }
            else
            {
                scout.Id = character.IsLocal ? BuiltinApps.LocalId() : "unknown";
                scout.Name = character.IsLocal ? BuiltinApps.LocalName() : "Scout";
                scout.IsLocal = character.IsLocal;
            }
            try
            {
                if (character.IsLocal)
                    scout.IsLocal = true;
            }
            catch
            {
            }
            try
            {
                CharacterData data = character.data;
                if (data != null)
                {
                    scout.IsDead = data.dead;
                    scout.IsFullyPassedOut = data.fullyPassedOut;
                    scout.IsPassedOut = data.passedOut || data.fullyPassedOut;
                    scout.IsFullyConscious = data.fullyConscious;
                    scout.PassOutValue = data.passOutValue;
                    scout.DeathTimer = Mathf.Clamp01(data.deathTimer);
                    scout.DeathLeft = scout.IsDead ? 0f : Mathf.Clamp01(1f - scout.DeathTimer);
                    if (scout.IsFullyPassedOut && !scout.IsDead)
                        scout.DeathSecondsLeft = scout.DeathLeft * 60f;
                    if (data.dead)
                        scout.TimeSinceDied = data.sinceDied;
                    scout.IsInFog = data.isInFog;
                    try
                    {
                        scout.IsInWater = data.isInWater;
                    }
                    catch
                    {
                    }
                    scout.IsCarried = data.isCarried || data.carrier != null;
                    FillPeer(data.carrier, out scout.CarrierId, out scout.CarrierName, out scout.CarrierActor);
                    scout.IsCarrying = data.IsCarryingCharacter || data.carriedPlayer != null;
                    FillPeer(data.carriedPlayer, out scout.CarriedId, out scout.CarriedName, out scout.CarriedActor);
                }
            }
            catch
            {
            }
            try
            {
                scout.IsGhost = character.IsGhost;
            }
            catch
            {
            }
            scout.Position = WorldPos(character);
            scout.VoicePosition = VoicePos(character);
            return scout;
        }

        private static Vector3 WorldPos(Character character)
        {
            try
            {
                if (character.Ghost != null)
                    return character.Ghost.transform.position;
            }
            catch
            {
            }
            try
            {
                if (character.refs != null && character.refs.head != null)
                    return character.refs.head.transform.position;
            }
            catch
            {
            }
            try
            {
                return character.Center;
            }
            catch
            {
            }
            return Vector3.zero;
        }

        private static Vector3 VoicePos(Character character)
        {
            try
            {
                CharacterVoiceHandler handler = character.GetComponentInChildren<CharacterVoiceHandler>();
                if (handler != null && handler.m_transformProvider != null)
                    return handler.m_transformProvider.GetVoicePosition(false);
            }
            catch
            {
            }
            return WorldPos(character);
        }

        private static void FillPeer(Character other, out string id, out string name, out int actor)
        {
            id = null;
            name = null;
            actor = 0;
            if (other == null)
                return;
            actor = ActorOf(other);
            PhotonPlayer owner = null;
            try
            {
                if (other.photonView != null)
                    owner = other.photonView.Owner;
            }
            catch
            {
            }
            if (owner != null)
            {
                id = BuiltinApps.PlayerId(owner);
                name = BuiltinApps.PlayerName(owner);
                actor = owner.ActorNumber;
                return;
            }
            id = other.IsLocal ? BuiltinApps.LocalId() : "unknown";
            name = other.IsLocal ? BuiltinApps.LocalName() : "Scout";
        }

        internal static int ActorOf(Character character)
        {
            if (character == null)
                return 0;
            try
            {
                if (character.photonView != null && character.photonView.Owner != null)
                    return character.photonView.Owner.ActorNumber;
            }
            catch
            {
            }
            return 0;
        }

        internal static CharacterVoiceHandler HandlerOf(Character character)
        {
            if (character == null)
                return null;
            try
            {
                return character.GetComponentInChildren<CharacterVoiceHandler>();
            }
            catch
            {
                return null;
            }
        }

        internal static CharacterVoiceHandler HandlerById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            try
            {
                List<Character> all = Character.AllCharacters;
                if (all == null)
                    return null;
                for (int i = 0; i < all.Count; i++)
                {
                    Character character = all[i];
                    if (character == null)
                        continue;
                    PhotonPlayer owner = null;
                    try
                    {
                        if (character.photonView != null)
                            owner = character.photonView.Owner;
                    }
                    catch
                    {
                    }
                    if (owner == null)
                        continue;
                    if (string.Equals(BuiltinApps.PlayerId(owner), id, StringComparison.Ordinal))
                        return HandlerOf(character);
                }
            }
            catch
            {
            }
            return null;
        }

        internal static CharacterVoiceHandler HandlerByActor(int actor)
        {
            if (actor <= 0)
                return null;
            try
            {
                List<Character> all = Character.AllCharacters;
                if (all == null)
                    return null;
                for (int i = 0; i < all.Count; i++)
                {
                    Character character = all[i];
                    if (ActorOf(character) == actor)
                        return HandlerOf(character);
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
