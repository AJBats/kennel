using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace UevrLauncher.Services
{
    // Reads and writes per-app LaunchOptions in Steam's localconfig.vdf.
    //
    // The path we care about inside the file is:
    //   UserLocalConfigStore.Software.Valve.Steam.apps.<appid>.LaunchOptions
    //
    // Writing while Steam is running is unsafe: Steam holds its own in-memory
    // copy and rewrites localconfig.vdf on shutdown, clobbering anything we
    // change. Callers should check IsSteamRunning() first; SetLaunchOptions
    // throws if Steam is running rather than silently losing the write.
    //
    // Every write produces a timestamped .bak in the same directory so we have
    // a rollback if anything goes wrong.
    public static class SteamConfig
    {
        public sealed class SteamUser
        {
            public string SteamId3 { get; set; }      // numeric folder name
            public string LocalConfigPath { get; set; }
            public DateTime LastWriteUtc { get; set; }
        }

        // Enumerate every user that has a localconfig.vdf. Almost always one,
        // occasionally more (shared family PC). Sorted most-recent first.
        public static IList<SteamUser> ListUsers()
        {
            var users = new List<SteamUser>();
            var steam = SteamLibrary.FindSteamPath();
            if (steam == null) return users;
            var userdata = Path.Combine(steam, "userdata");
            if (!Directory.Exists(userdata)) return users;

            foreach (var dir in Directory.GetDirectories(userdata))
            {
                var local = Path.Combine(dir, "config", "localconfig.vdf");
                if (!File.Exists(local)) continue;
                users.Add(new SteamUser
                {
                    SteamId3 = Path.GetFileName(dir),
                    LocalConfigPath = local,
                    LastWriteUtc = File.GetLastWriteTimeUtc(local),
                });
            }
            return users.OrderByDescending(u => u.LastWriteUtc).ToList();
        }

        // Convenience: the most recently used user. Almost always what we want.
        public static SteamUser FindActiveUser()
        {
            var users = ListUsers();
            return users.Count > 0 ? users[0] : null;
        }

        public static bool IsSteamRunning()
        {
            // GetProcessesByName returns Process[] of native-handle-holding
            // objects; without disposing them we leak handles every call (and
            // this is polled every 3 seconds from the GUI). Dispose every one
            // before returning.
            Process[] procs;
            try { procs = Process.GetProcessesByName("steam"); }
            catch { return false; }
            try { return procs.Length > 0; }
            finally { foreach (var p in procs) p.Dispose(); }
        }

        // ----- Read -----

        public static string GetLaunchOptions(SteamUser user, string appId)
        {
            if (user == null || !File.Exists(user.LocalConfigPath)) return null;
            var root = VdfParser.Parse(File.ReadAllText(user.LocalConfigPath));
            return root.GetString(
                "UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId, "LaunchOptions");
        }

        // ----- Write -----

        public static void SetLaunchOptions(SteamUser user, string appId, string launchOptions)
        {
            EnsureSteamClosed();
            if (user == null) throw new InvalidOperationException("No Steam user found.");
            if (string.IsNullOrEmpty(appId)) throw new ArgumentException("appId required");

            var path = user.LocalConfigPath;
            var raw = File.ReadAllText(path);
            var root = VdfParser.Parse(raw);

            // Create intermediate blocks if Steam hasn't seen this app yet.
            root.SetStringAtPath(
                new[] { "UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId, "LaunchOptions" },
                launchOptions);

            BackupOnce(path);
            ConfigStore.WriteAllTextAtomic(path, VdfParser.Serialize(root));
        }

        // Remove the LaunchOptions entry for an app (e.g. when deleting a
        // wrapper). Leaves the rest of the app block intact.
        public static void ClearLaunchOptions(SteamUser user, string appId)
        {
            EnsureSteamClosed();
            if (user == null) return;

            var path = user.LocalConfigPath;
            var raw = File.ReadAllText(path);
            var root = VdfParser.Parse(raw);

            var app = root.GetBlock(
                "UserLocalConfigStore", "Software", "Valve", "Steam", "apps", appId);
            if (app == null) return;
            // Replace value-or-block in place; setting to empty string drops VR
            // mode but keeps the entry. Use empty string so Steam still treats
            // the app as configured (avoids a no-op write).
            if (app["LaunchOptions"] == null) return;
            app["LaunchOptions"] = string.Empty;

            BackupOnce(path);
            ConfigStore.WriteAllTextAtomic(path, VdfParser.Serialize(root));
        }

        // ----- Internals -----

        private static void EnsureSteamClosed()
        {
            if (IsSteamRunning())
            {
                throw new InvalidOperationException(
                    "Steam is running. Quit Steam before changing launch options — Steam " +
                    "rewrites localconfig.vdf on shutdown and would clobber any changes.");
            }
        }

        // Keep one timestamped backup per write so we always have a rollback
        // candidate, but cap the count so the directory doesn't grow forever.
        private const int MaxBackups = 10;

        private static void BackupOnce(string path)
        {
            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileName(path);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var bak = Path.Combine(dir, $"{name}.kennel-{stamp}.bak");
            File.Copy(path, bak, overwrite: false);

            // Prune older backups beyond the cap.
            var existing = Directory.GetFiles(dir, name + ".kennel-*.bak")
                .OrderByDescending(f => f)
                .Skip(MaxBackups);
            foreach (var f in existing)
            {
                try { File.Delete(f); } catch { /* best-effort */ }
            }
        }
    }
}
