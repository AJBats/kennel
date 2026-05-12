using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using UevrLauncher.Models;

namespace UevrLauncher.Services
{
    // Locates the Steam install and enumerates all installed games across every
    // library folder the user has configured. Pure read-only — never touches
    // Steam state.
    public static class SteamLibrary
    {
        // Returns the Steam install root (e.g. "C:\Program Files (x86)\Steam"),
        // or null if Steam isn't installed for the current user.
        public static string FindSteamPath()
        {
            // HKCU is preferred — it reflects the install for the user actually
            // logged in. HKLM is a fallback for system-wide installs.
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                var v = key?.GetValue("SteamPath") as string;
                if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) return v;
            }
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
            {
                var v = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) return v;
            }
            using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
            {
                var v = key?.GetValue("InstallPath") as string;
                if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) return v;
            }
            return null;
        }

        // Read libraryfolders.vdf and return each library root path. Always
        // includes the main Steam install dir as the first library since
        // libraryfolders.vdf is the authoritative list.
        public static IList<string> GetLibraryRoots(string steamPath)
        {
            var roots = new List<string>();
            var lfPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(lfPath)) return roots;

            var root = VdfParser.Parse(File.ReadAllText(lfPath));
            var lf = root.GetBlock("libraryfolders");
            if (lf == null) return roots;

            foreach (var entry in lf.Entries)
            {
                if (!(entry.Value is VdfBlock lib)) continue;
                if (!lib.TryGetString("path", out var p) || string.IsNullOrEmpty(p)) continue;
                if (Directory.Exists(p)) roots.Add(p);
            }
            return roots;
        }

        // Enumerate every installed game across every library. Skips manifests
        // that can't be parsed or don't carry the minimum fields.
        public static IList<SteamGame> GetInstalledGames()
        {
            var games = new List<SteamGame>();
            var steamPath = FindSteamPath();
            if (steamPath == null) return games;

            foreach (var libRoot in GetLibraryRoots(steamPath))
            {
                var steamapps = Path.Combine(libRoot, "steamapps");
                if (!Directory.Exists(steamapps)) continue;

                foreach (var manifest in Directory.GetFiles(steamapps, "appmanifest_*.acf"))
                {
                    SteamGame game = TryParseManifest(manifest, libRoot);
                    if (game != null) games.Add(game);
                }
            }
            return games;
        }

        private static SteamGame TryParseManifest(string manifestPath, string libRoot)
        {
            try
            {
                var root = VdfParser.Parse(File.ReadAllText(manifestPath));
                var app = root.GetBlock("AppState");
                if (app == null) return null;

                if (!app.TryGetString("appid", out var appId) || string.IsNullOrEmpty(appId)) return null;
                if (!app.TryGetString("name", out var name) || string.IsNullOrEmpty(name)) return null;
                if (!app.TryGetString("installdir", out var installDir) || string.IsNullOrEmpty(installDir)) return null;

                var installPath = Path.Combine(libRoot, "steamapps", "common", installDir);

                return new SteamGame
                {
                    AppId = appId,
                    Name = name,
                    InstallDir = installDir,
                    InstallPath = installPath,
                    LibraryRoot = libRoot,
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
