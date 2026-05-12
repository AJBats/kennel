using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace UevrLauncher.Services
{
    // Two-tier on-disk state:
    //
    //   %LOCALAPPDATA%\Kennel\install.json   ← bootstrap; just points at the
    //                                          user-chosen data root
    //   <DataRoot>\config.json               ← actual config (chihuahua
    //                                          version, basename↔appid map)
    //   <DataRoot>\wrappers\                 ← generated .bat + .vbs pairs
    //   <DataRoot>\chihuahua\                ← managed chihuahua install
    //
    // First run: bootstrap missing → GUI asks user where to put data → writes
    // bootstrap → initializes <DataRoot> subdirs.

    public sealed class InstallBootstrap
    {
        public int SchemaVersion { get; set; }
        public string DataRoot { get; set; }
    }

    public sealed class ChihuahuaState
    {
        public string Tag { get; set; }            // e.g. "v0.6.0", or null if not installed
        public string InstalledAt { get; set; }    // ISO-8601 UTC
    }

    public sealed class WrapperRegistry
    {
        public string Basename { get; set; }
        public string AppId { get; set; }
    }

    public sealed class AppConfig
    {
        public int SchemaVersion { get; set; } = 1;
        public ChihuahuaState Chihuahua { get; set; } = new ChihuahuaState();
        public List<WrapperRegistry> Wrappers { get; set; } = new List<WrapperRegistry>();
    }

    public static class ConfigStore
    {
        private const int BootstrapSchema = 1;

        public static string BootstrapDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kennel");

        public static string BootstrapPath =>
            Path.Combine(BootstrapDir, "install.json");

        // Two presets we offer the user on first run. Documents is user-visible;
        // LocalAppData is hidden but tidy. GUI shows both as radio choices.
        public static string DefaultDocumentsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Kennel");

        public static string DefaultLocalAppDataRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kennel", "Data");

        // ----- Paths derived from a data root -----

        public static string ConfigJson(string dataRoot) => Path.Combine(dataRoot, "config.json");
        public static string WrappersDir(string dataRoot) => Path.Combine(dataRoot, "wrappers");
        public static string ChihuahuaDir(string dataRoot) => Path.Combine(dataRoot, "chihuahua");
        public static string ChihuahuaBackupDir(string dataRoot) => Path.Combine(dataRoot, "chihuahua.bak");

        // ----- Bootstrap -----

        // Returns null if first run (no bootstrap yet).
        public static InstallBootstrap LoadBootstrap()
        {
            if (!File.Exists(BootstrapPath)) return null;
            var json = File.ReadAllText(BootstrapPath);
            var b = new JavaScriptSerializer().Deserialize<InstallBootstrap>(json);
            return b;
        }

        public static void SaveBootstrap(InstallBootstrap b)
        {
            Directory.CreateDirectory(BootstrapDir);
            b.SchemaVersion = BootstrapSchema;
            var json = new JavaScriptSerializer().Serialize(b);
            WriteAllTextAtomic(BootstrapPath, Prettify(json));
        }

        // Create <DataRoot> and its subdirs. Safe to call repeatedly.
        public static void InitializeDataRoot(string dataRoot)
        {
            Directory.CreateDirectory(dataRoot);
            Directory.CreateDirectory(WrappersDir(dataRoot));
            Directory.CreateDirectory(ChihuahuaDir(dataRoot));
        }

        // ----- Config -----

        public static AppConfig LoadConfig(string dataRoot)
        {
            var path = ConfigJson(dataRoot);
            if (!File.Exists(path)) return new AppConfig();
            var json = File.ReadAllText(path);
            var cfg = new JavaScriptSerializer().Deserialize<AppConfig>(json) ?? new AppConfig();
            if (cfg.Chihuahua == null) cfg.Chihuahua = new ChihuahuaState();
            if (cfg.Wrappers == null) cfg.Wrappers = new List<WrapperRegistry>();
            return cfg;
        }

        public static void SaveConfig(string dataRoot, AppConfig cfg)
        {
            Directory.CreateDirectory(dataRoot);
            var json = new JavaScriptSerializer().Serialize(cfg);
            WriteAllTextAtomic(ConfigJson(dataRoot), Prettify(json));
        }

        // ----- Helpers -----

        // JavaScriptSerializer emits compact one-line JSON. Pretty-print so the
        // file is hand-editable in an emergency.
        private static string Prettify(string json)
        {
            var sb = new System.Text.StringBuilder(json.Length + 64);
            int depth = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                    if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inString = true; sb.Append(c); break;
                    case '{': case '[':
                        sb.Append(c).Append('\n');
                        depth++;
                        sb.Append(new string(' ', depth * 2));
                        break;
                    case '}': case ']':
                        sb.Append('\n');
                        depth--;
                        sb.Append(new string(' ', depth * 2));
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c).Append('\n');
                        sb.Append(new string(' ', depth * 2));
                        break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // Write then rename so a crash mid-write never leaves a half-file behind.
        private static void WriteAllTextAtomic(string path, string contents)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, contents);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
