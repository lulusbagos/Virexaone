using UnityEngine.Networking;

namespace Virexa.FMS
{
    public static class FMSApiSession
    {
        public const string LocalDispatcherKey = "astha-local-dispatcher-key-2026-09";
        private static string googleIdToken;

        public static bool IsSignedIn => !string.IsNullOrEmpty(googleIdToken);

        public static void SetGoogleIdToken(string token)
        {
            googleIdToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        }

        public static void Logout()
        {
            googleIdToken = null;
        }

        public static void Authorize(UnityWebRequest request)
        {
            if (request != null && IsSignedIn)
                request.SetRequestHeader("Authorization", "Bearer " + googleIdToken);
        }

        public static void AuthorizeDispatcher(UnityWebRequest request)
        {
            if (request == null) return;
            Authorize(request);
            request.SetRequestHeader("X-FMS-Dispatcher-Key", LocalDispatcherKey);
        }
    }
}
