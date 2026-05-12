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

        public static void Write(
            string basename,
            string gameName,
            string gameExePath,
            int delaySeconds,
            string chihuahuaExePath,
            string uevrBuild,        // "Release" or "Nightly"
            string wrappersDir)
        {
            ValidateBasename(basename);
            Directory.CreateDirectory(wrappersDir);

            // Defensive normalize: chihuahua only accepts these two literals.
            string normalizedBuild =
                string.Equals(uevrBuild, UevrBuildNightly, StringComparison.OrdinalIgnoreCase)
                    ? UevrBuildNightly
                    : UevrBuildRelease;

            string batPath = BatPathFor(basename, wrappersDir);
            string vbsPath = VbsPathFor(basename, wrappersDir);

            string bat = LoadTemplate("smart_wrap.bat.tmpl")
                .Replace("{{GameName}}", gameName ?? basename)
                .Replace("{{ChihuahuaExe}}", chihuahuaExePath)
                .Replace("{{GameExe}}", gameExePath)
                .Replace("{{Delay}}", delaySeconds.ToString())
                .Replace("{{UevrBuild}}", normalizedBuild);

            string vbs = LoadTemplate("smart_wrap.vbs.tmpl")
                .Replace("{{BatFileName}}", Path.GetFileName(batPath))
                .Replace("{{BatPath}}", batPath);

            File.WriteAllText(batPath, NormalizeCrlf(bat));
            File.WriteAllText(vbsPath, NormalizeCrlf(vbs));
        }

        // ----- Read -----

        public static WrapperInfo Read(string basename, string wrappersDir)
        {
            string batPath = BatPathFor(basename, wrappersDir);
            string vbsPath = VbsPathFor(basename, wrappersDir);
            if (!File.Exists(batPath)) return null;

            string bat = File.ReadAllText(batPath);

            string gameName = MatchOrNull(bat, @"^REM Smart launcher for (.+) \(Steam library entry\)\.\s*$");

            // The VR block looks like:
            //   "<chihuahua>" ^
            //    "<game.exe>" ^
            //    --delay <N> ^
            //    --runtime OpenXR
            // We pull the game exe by anchoring on the line that comes between
            // the chihuahua call and the --delay line.
            string gameExe = MatchOrNull(bat,
                @"^\s+""([^""]+\.exe)""\s+\^\s*\r?\n\s+--delay\s+\d+",
                RegexOptions.Multiline);

            string delayStr = MatchOrNull(bat, @"--delay\s+(\d+)");
            int delay = int.TryParse(delayStr, out var d) ? d : 0;

            // --uevr-build was added later; old wrappers won't have it. Treat
            // absence as Release since that matches chihuahua's own default.
            string uevrBuild = MatchOrNull(bat, @"--uevr-build\s+(\w+)") ?? UevrBuildRelease;
            if (!string.Equals(uevrBuild, UevrBuildNightly, StringComparison.OrdinalIgnoreCase))
                uevrBuild = UevrBuildRelease;

            return new WrapperInfo
            {
                Basename = basename,
                GameName = gameName,
                GameExePath = gameExe,
                DelaySeconds = delay,
                UevrBuild = uevrBuild,
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
