using System;
using UnityEngine;

namespace Crispberry_PiPhone
{
    internal static class PhoneNotify
    {
        internal static string LastTextThreadId;
        internal static string LastTextName;

        public static void ClearLastText()
        {
            LastTextThreadId = null;
            LastTextName = null;
        }

        public static void Post(string appId, string title, string body)
        {
            Post(appId, title, body, true, true);
        }

        public static void Quiet(string title, string body)
        {
            PhoneMenu.ShowBanner(title, body);
        }

        public static void Post(string appId, string title, string body, bool persist, bool sound)
        {
            if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
                return;
            PiPhoneApp app;
            if (!string.IsNullOrEmpty(appId) && PiPhoneApi.TryGetApp(appId, out app) && app != null && !app.PostsNotices)
                return;
            if (!string.IsNullOrEmpty(appId) && !PhoneTheme.AppNoticesOn(appId))
                return;
            if (persist)
                PhoneStore.AddNotice(title ?? "Notice", body ?? string.Empty, appId ?? string.Empty);
            if (PhoneMenu.IsOpen)
                PhoneMenu.ShowBanner(title, body);
            if (sound)
                PlayAlert(false, () => PhoneSounds.PlayApp(appId));
        }

        public static void IncomingText(string fromId, string fromName, string preview)
        {
            PhoneContacts.See(fromId, fromName);
            string name = PhoneContacts.Display(fromId, fromName);
            string body = preview ?? string.Empty;
            LastTextThreadId = fromId;
            LastTextName = name;
            if (!PhoneTheme.AppNoticesOn(BuiltinApps.MessagesId))
                return;
            PhoneStore.AddNotice("Message", name, BuiltinApps.MessagesId);
            if (PhoneMenu.IsOpen)
                PhoneMenu.ShowBanner("Message", name);
            else
                AlertHud.ShowText(fromId, name, body);
            PlayAlert(false, () => PhoneSounds.PlayTextFor(fromId));
        }

        public static void IncomingCall(string fromId, string fromName)
        {
            PhoneContacts.See(fromId, fromName);
            fromName = PhoneContacts.Display(fromId, fromName);
            if (PhoneTheme.AppNoticesOn(BuiltinApps.PhoneId))
                PhoneStore.AddNotice("Incoming call", fromName ?? "Scout", BuiltinApps.PhoneId);
            if (PhoneMenu.IsOpen)
                PhoneMenu.ShowBanner("Incoming call", fromName);
            else
                AlertHud.ShowCall();
            PlayAlert(true, () => PhoneSounds.PlayRingtoneFor(fromId));
        }

        private static void PlayAlert(bool call, Action ring)
        {
            if (PhoneTheme.DoNotDisturb || PhoneTheme.RingerMode == 2)
                return;
            if (PhoneTheme.RingerMode == 1)
            {
                PhoneSounds.PlayVibrate(call);
                return;
            }
            if (ring != null)
                ring();
        }
    }
}
