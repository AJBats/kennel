using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UevrLauncher.Models;

namespace UevrLauncher.Services
{
    // Renders the .bat + .vbs pair from embedded templates and reads them back.
    //
    // Each game gets two files in the wrappers dir:
    //   <basename>_smart_wrap.bat   ← the actual smart launcher
    //   <basename>_smart_wrap.vbs   ← invisible cmd-window wrapper for Steam
    //
    // We can re-parse the .bat to recover game name, exe path, and delay because
    // the template format is fixed and self-documenting. The basename↔appid link
    // is the only piece that has to live in config.json.
    public static class WrapperIo
    {
        public const string BatSuffix = "_smart_wrap.bat";
        public const string VbsSuffix = "_smart_wrap.vbs";

        // ----- Write -----

        public const string UevrBuildRelease = "Release";
        public const string UevrBuildNightly = "Nightly";
        public const string UevrBuildCustom = "Custom";

        public static void Write(
            string basename,
            string gameName,
            string gameExePath,
            int delaySeconds,
            string chihuahuaExePath,
            string uevrBuild,        // "Release", "Nightly", or "Custom"
            bool manualInjection,
            string uevrInjectorExePath,  // path to UEVRInjector.exe matching uevrBuild (only used when manualInjection)
            string customUevrDir,        // user-supplied UEVR install dir; only set when uevrBuild == "Custom"
            string wrappersDir)
        {
            ValidateBasename(basename);
            Directory.CreateDirectory(wrappersDir);

            // Defensive normalize: chihuahua only accepts Release/Nightly.
            // Custom is Kennel's extension and always implies Manual.
            string normalizedBuild;
            if (string.Equals(uevrBuild, UevrBuildCustom, StringComparison.OrdinalIgnoreCase))
                normalizedBuild = UevrBuildCustom;
            else if (string.Equals(uevrBuild, UevrBuildNightly, StringComparison.OrdinalIgnoreCase))
                normalizedBuild = UevrBuildNightly;
            else
                normalizedBuild = UevrBuildRelease;

            // Custom path means Custom build; force-clear otherwise so the
            // metadata can't lie.
            if (normalizedBuild != UevrBuildCustom) customUevrDir = null;

            string batPath = BatPathFor(basename, wrappersDir);
            string vbsPath = VbsPathFor(basename, wrappersDir);

            string vrBlock = manualInjection
                ? BuildManualVrBlock(uevrInjectorExePath)
                : BuildChihuahuaVrBlock(chihuahuaExePath, gameExePath, delaySeconds, normalizedBuild);

            string bat = LoadTemplate("smart_wrap.bat.tmpl")
                .Replace("{{GameName}}", gameName ?? basename)
                .Replace("{{GameExe}}", gameExePath ?? "")
                .Replace("{{Delay}}", delaySeconds.ToString())
                .Replace("{{UevrBuild}}", normalizedBuild)
                .Replace("{{Manual}}", manualInjection ? "true" : "false")
                .Replace("{{CustomUevrDir}}", customUevrDir ?? "")
                .Replace("{{VrBlock}}", vrBlock);

            string vbs = LoadTemplate("smart_wrap.vbs.tmpl")
                .Replace("{{BatFileName}}", Path.GetFileName(batPath))
                .Replace("{{BatPath}}", batPath);

            File.WriteAllText(batPath, NormalizeCrlf(bat));
            File.WriteAllText(vbsPath, NormalizeCrlf(vbs));
        }

        // chihuahua auto-injection block. Falls through to :no_chihuahua if the
        // exe is missing; chihuahua handles UEVR DLL download + injection.
        //
        // chihuahua's CLI only accepts Release or Nightly for --uevr-build.
        // "Custom" is a Kennel-side concept that means "chihuahua is co-located
        // with user-supplied DLLs in their custom dir, no managed update
        // path." For chihuahua's purposes, that's effectively Release (the
        // flag is a no-op when uevr.version is missing — the AND condition in
        // chihuahua's update logic short-circuits).
        private static string BuildChihuahuaVrBlock(
            string chihuahuaExePath, string gameExePath, int delaySeconds, string uevrBuild)
        {
            string chihuahuaFlag = string.Equals(uevrBuild, UevrBuildNightly, StringComparison.OrdinalIgnoreCase)
                ? UevrBuildNightly
                : UevrBuildRelease;
            return string.Join("\n", new[]
            {
                "if not exist \"" + chihuahuaExePath + "\" goto :no_chihuahua",
                "REM --- VR mode: chihuahua launches the game and injects UEVR ---",
                "\"" + chihuahuaExePath + "\" ^",
                " \"" + gameExePath + "\" ^",
                " --delay " + delaySeconds + " ^",
                " --runtime OpenXR ^",
                " --uevr-build " + chihuahuaFlag,
                "exit /b %ERRORLEVEL%",
            });
        }

        // Manual injection block: ensure the UEVR frontend is running (start it
        // if not), then launch the game flat. The user injects from the
        // frontend after the game is up.
        //
        // UEVRInjector.exe requires admin privileges (its manifest requests
        // requireAdministrator), so we elevate via PowerShell's RunAs verb.
        // That triggers the standard UAC prompt; once the user accepts,
        // UEVRInjector launches elevated in parallel with the game.
        private static string BuildManualVrBlock(string uevrInjectorExePath)
        {
            return string.Join("\n", new[]
            {
                "REM --- Manual injection mode: bring up the UEVR frontend and launch flat ---",
                "if not exist \"" + uevrInjectorExePath + "\" goto :no_chihuahua",
                "tasklist /FI \"IMAGENAME eq UEVRInjector.exe\" 2>NUL | find /I \"UEVRInjector.exe\" >NUL",
                "if \"%ERRORLEVEL%\"==\"0\" goto :manual_run",
                "powershell -NoProfile -Command \"Start-Process -FilePath '" + uevrInjectorExePath + "' -Verb RunAs\"",
                ":manual_run",
                "%*",
                "exit /b %ERRORLEVEL%",
            });
        }

        // ----- Read -----

        public static WrapperInfo Read(string basename, string wrappersDir)
        {
            string batPath = BatPathFor(basename, wrappersDir);
            string vbsPath = VbsPathFor(basename, wrappersDir);
            if (!File.Exists(batPath)) return null;

            string bat = File.ReadAllText(batPath);

            string gameName = MatchOrNull(bat, @"^REM Smart launcher for (.+) \(Steam library entry\)\.\s*$");

            // All wrapper state lives in REM comments at the top so reads are
            // unambiguous across both auto and manual modes. We also keep the
            // chihuahua command line legacy regex as a fallback so wrappers
            // written by older versions of Kennel still parse correctly.
            //
            // Patterns deliberately don't anchor with $: in multiline mode $
            // matches before \n but NOT before \r, and our files are CRLF.
            // The capture groups are bounded enough on their own.
            string gameExe = MatchOrNull(bat, @"^REM Kennel-GameExe=([^\r\n]+)", RegexOptions.Multiline);
            string delayStr = MatchOrNull(bat, @"^REM Kennel-Delay=(\d+)", RegexOptions.Multiline);
            string uevrBuild = MatchOrNull(bat, @"^REM Kennel-UevrBuild=(\w+)", RegexOptions.Multiline);
            string manualStr = MatchOrNull(bat, @"^REM Kennel-Manual=(true|false)", RegexOptions.Multiline);
            string customUevrDir = MatchOrNull(bat, @"^REM Kennel-CustomUevrDir=([^\r\n]*)", RegexOptions.Multiline);
            if (string.IsNullOrEmpty(customUevrDir)) customUevrDir = null;

            // Legacy fallback — pre-metadata wrappers had values only in the
            // chihuahua command line.
            if (gameExe == null)
            {
                gameExe = MatchOrNull(bat,
                    @"^\s+""([^""]+\.exe)""\s+\^\s*\r?\n\s+--delay\s+\d+",
                    RegexOptions.Multiline);
            }
            if (delayStr == null)
            {
                delayStr = MatchOrNull(bat, @"--delay\s+(\d+)");
            }
            if (uevrBuild == null)
            {
                uevrBuild = MatchOrNull(bat, @"--uevr-build\s+(\w+)");
            }

            int delay = int.TryParse(delayStr, out var d) ? d : 0;

            // Normalize uevrBuild to one of the three known values.
            if (string.Equals(uevrBuild, UevrBuildCustom, StringComparison.OrdinalIgnoreCase))
                uevrBuild = UevrBuildCustom;
            else if (string.Equals(uevrBuild, UevrBuildNightly, StringComparison.OrdinalIgnoreCase))
                uevrBuild = UevrBuildNightly;
            else
                uevrBuild = UevrBuildRelease;

            bool manualInjection = string.Equals(manualStr, "true", StringComparison.OrdinalIgnoreCase);

            // Invariant: Manual without Custom ⇒ Release (only the tagged
            // praydog release ships a UEVRInjector frontend). Custom can be
            // either auto-inject (chihuahua + custom DLLs) or manual (custom
            // UEVRInjector) — no forced coupling.
            if (manualInjection && uevrBuild != UevrBuildCustom)
                uevrBuild = UevrBuildRelease;

            // CustomUevrDir is only meaningful when Custom; drop stale values.
            if (uevrBuild != UevrBuildCustom) customUevrDir = null;

            return new WrapperInfo
            {
                Basename = basename,
                GameName = gameName,
                GameExePath = gameExe,
                DelaySeconds = delay,
                UevrBuild = uevrBuild,
                ManualInjection = manualInjection,
                CustomUevrDir = customUevrDir,
                BatPath = batPath,
                VbsPath = File.Exists(vbsPath) ? vbsPath : null,
            };
        }

        public static IList<WrapperInfo> List(string wrappersDir)
        {
            var result = new List<WrapperInfo>();
            if (!Directory.Exists(wrappersDir)) return result;

            foreach (var f in Directory.GetFiles(wrappersDir, "*" + BatSuffix))
            {
                var name = Path.GetFileName(f);
                var basename = name.Substring(0, name.Length - BatSuffix.Length);
                var info = Read(basename, wrappersDir);
                if (info != null) result.Add(info);
            }
            return result;
        }

        // ----- Delete -----

        public static void Delete(string basename, string wrappersDir)
        {
            ValidateBasename(basename);
            var bat = BatPathFor(basename, wrappersDir);
            var vbs = VbsPathFor(basename, wrappersDir);
            if (File.Exists(bat)) File.Delete(bat);
            if (File.Exists(vbs)) File.Delete(vbs);
        }

        // ----- Steam launch options string -----

        // The exact value to put in Steam's per-app LaunchOptions. Steam will
        // append %command% in place of the literal "%command%" when invoking.
        public static string BuildSteamLaunchOptions(string basename, string wrappersDir)
        {
            string vbs = VbsPathFor(basename, wrappersDir);
            // Match the literal path the original hand-authored wrappers used so
            // a re-save of an existing wrapper produces a byte-identical Steam
            // LaunchOptions string.
            string wscript = Path.Combine(Environment.SystemDirectory, "wscript.exe");
            return $"\"{wscript}\" \"{vbs}\" %command%";
        }

        // ----- Helpers -----

        public static string BatPathFor(string basename, string wrappersDir)
            => Path.Combine(wrappersDir, basename + BatSuffix);

        public static string VbsPathFor(string basename, string wrappersDir)
            => Path.Combine(wrappersDir, basename + VbsSuffix);

        private static void ValidateBasename(string basename)
        {
            if (string.IsNullOrEmpty(basename))
                throw new ArgumentException("basename must be non-empty");
            if (!Regex.IsMatch(basename, @"^[a-zA-Z0-9_-]+$"))
                throw new ArgumentException(
                    "basename must contain only letters, digits, underscore, hyphen");
        }

        private static string LoadTemplate(string name)
        {
            // Resource names depend on RootNamespace (which isn't always the
            // assembly name). Match by suffix so a rename later won't break us.
            var asm = Assembly.GetExecutingAssembly();
            var match = Array.Find(
                asm.GetManifestResourceNames(),
                n => n.EndsWith("." + name, StringComparison.Ordinal));
            if (match == null)
            {
                throw new InvalidOperationException(
                    "Embedded template not found: " + name +
                    "; embedded resources are: " +
                    string.Join(", ", asm.GetManifestResourceNames()));
            }
            using (var stream = asm.GetManifestResourceStream(match))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static string MatchOrNull(string text, string pattern, RegexOptions opts = RegexOptions.None)
        {
            var m = Regex.Match(text, pattern, opts | RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        // Batch and VBScript both expect CRLF; ensure we don't accidentally
        // ship LF-only files if the build host normalizes.
        private static string NormalizeCrlf(string s)
        {
            s = s.Replace("\r\n", "\n");
            return s.Replace("\n", "\r\n");
        }
    }
}
