using System;
using Steamworks;
using UnityEngine;

namespace ArenaShooter.Gameplay.Net
{
    /// <summary>
    /// Starts Steam (Steamworks.NET) and pumps its callbacks. Uses Valve's public test app ID 480
    /// ("Spacewar", from steam_appid.txt) so the game can use Steam invites and relays without a
    /// store listing (GDD 9.1). Everything offline keeps working if Steam isn't running.
    /// </summary>
    public class SteamService : MonoBehaviour
    {
        public static bool IsReady { get; private set; }
        public static string Status { get; private set; } = "Steam not started.";
        public static ulong LocalId => IsReady ? SteamUser.GetSteamID().m_SteamID : 0UL;
        public static string LocalName => IsReady ? SteamFriends.GetPersonaName() : "Player";

        private static SteamService _instance;

        /// <summary>Creates the service once (idempotent). Returns whether Steam is usable.</summary>
        public static bool EnsureStarted(GameObject host)
        {
            if (_instance == null) _instance = host.AddComponent<SteamService>();
            return IsReady;
        }

        private void Awake()
        {
            try
            {
                if (!Packsize.Test())
                {
                    Status = "Steamworks.NET was built for the wrong platform.";
                    return;
                }
                if (!DllCheck.Test())
                {
                    Status = "Steam API DLL is missing or the wrong version.";
                    return;
                }
                IsReady = SteamAPI.Init();
                Status = IsReady ? "Connected to Steam as " + SteamFriends.GetPersonaName() : "Steam isn't running (start Steam and restart the game).";
                if (IsReady) SteamNetworkingUtils.InitRelayNetworkAccess(); // warm up relay routes early
            }
            catch (DllNotFoundException)
            {
                IsReady = false;
                Status = "Steam API DLL not found.";
            }
            catch (Exception e)
            {
                IsReady = false;
                Status = "Steam failed to start: " + e.Message;
            }
            if (!IsReady) Debug.LogWarning("[Steam] " + Status);
        }

        private void Update()
        {
            if (IsReady) SteamAPI.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            if (!IsReady) return;
            SteamAPI.Shutdown();
            IsReady = false;
        }
    }
}
