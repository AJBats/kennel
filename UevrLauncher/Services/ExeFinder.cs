using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace UevrLauncher.Services
{
    // Heuristically finds the main game .exe under a Steam install directory.
    //
    // Most UE5 games live at <Install>\<Project>\Binaries\Win64\<Name>-Shipping.exe,
    // but plenty of UE4 games (e.g. Ace Combat 7) ship a custom launcher exe at
    // the install root instead. The finder walks the install tree, filters out
    // obvious non-game exes (crash reporters, redistributables, engine tooling),
    // and ranks what's left so the GUI can pre-fill the best guess with a
    // "Browse..." escape hatch.
    public static class ExeFinder
    {
        public sealed class ExeCandidate
        {
            public string Path;
            public long SizeBytes;
            public int Confidence;   // 0..100, higher = more likely the right exe
            public string Reason;    // human-readable for tooltip / debug
        }

        // Directory *names* (case-insensitive) that we never descend into.
        private static readonly HashSet<string> PrunedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "_CommonRedist", "CommonRedist", "Redist",
            "DirectX", "VC_redist", "VCRedist", "dotnet", "DotNet",
            "GamingServices", "GamingRepair",
            "EpicGamesLauncher", "EOSOverlay", "EOSSDK",
            "Engine",  // UE engine binaries — almost never the right pick
            "DXSETUP",
        };

        // Filename substrings (case-insensitive) that disqualify an exe.
        private static readonly string[] BadNameFragments =
        {
            "crashreport", "crashpad", "unrealcefsubprocess", "consoleapp",
            "setup", "installer", "unins", "vc_redist", "dxsetup", "dotnetfx",
            "epicgames", "epicwebhelper", "eosoverlay", "easyanticheat",
            "battleye", "redist",
        };

        public static IList<ExeCandidate> FindCandidates(string installPath)
        {
            var results = new List<ExeCandidate>();
            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                return results;

            var exes = new List<string>();
            WalkExes(installPath, exes, 0);

            foreach (var exe in exes)
            {
                var name = Path.GetFileName(exe);
                if (IsBadName(name)) continue;

                var rel = exe.Length > installPath.Length
                    ? exe.Substring(installPath.Length).TrimStart('\\', '/')
                    : exe;

                long size;
                try { size = new FileInfo(exe).Length; } catch { continue; }

                int score = 0;
                var reasons = new List<string>();

                // Strongest signal: lives under a Binaries\Win64 folder.
                string relSep = rel.Replace('/', '\\');
                if (relSep.IndexOf(@"\Binaries\Win64\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 50;
                    reasons.Add("Binaries\\Win64");
                }
                else if (!relSep.Contains("\\"))
                {
                    // .exe sitting at the install root — UE4 launcher pattern (AC7).
                    score += 25;
                    reasons.Add("install root");
                }

                // UE5 shipping builds.
                if (name.IndexOf("-Shipping", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 25;
                    reasons.Add("Shipping build");
                }

                // Size: real game exes are usually big.
                if (size >= 100L * 1024 * 1024) { score += 25; reasons.Add(">=100MB"); }
                else if (size >= 10L * 1024 * 1024) { score += 10; reasons.Add(">=10MB"); }
                else if (size >= 1L * 1024 * 1024) { score += 5; }

                results.Add(new ExeCandidate
                {
                    Path = exe,
                    SizeBytes = size,
                    Confidence = score,
                    Reason = string.Join(", ", reasons),
                });
            }

            return results
                .OrderByDescending(c => c.Confidence)
                .ThenByDescending(c => c.SizeBytes)
                .ToList();
        }

        public static ExeCandidate BestGuess(string installPath)
        {
            var list = FindCandidates(installPath);
            return list.Count > 0 ? list[0] : null;
        }

        private static void WalkExes(string dir, List<string> result, int depth)
        {
            if (depth > 10) return; // sanity cap on pathological trees
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*.exe"))
                    result.Add(f);
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (PrunedDirs.Contains(name)) continue;
                    WalkExes(sub, result, depth + 1);
                }
            }
            catch
            {
                // Permission/IO errors — skip and keep walking siblings.
            }
        }

        private static bool IsBadName(string filename)
        {
            string lower = filename.ToLowerInvariant();
            foreach (var bad in BadNameFragments)
            {
                if (lower.IndexOf(bad, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }
    }
}
