using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Web.Script.Serialization;

namespace UevrLauncher.Services
{
    // Manages the chihuahua install: fetch latest release info from GitHub,
    // download the zip, extract atomically to <DataRoot>\chihuahua\, keep a
    // .bak rollback dir so the user can revert if a new build breaks things.
    //
    // Chihuahua ships as a single asset (chihuahua.zip) on every release
    // tag at github.com/keton/chihuahua. Their packaging has been stable
    // across 7+ releases so the asset-name lookup is just an exact match.
    //
    // We DO touch the network — only in GetLatestRelease() and Install().
    // Both are synchronous; callers should run on a background thread and
    // surface progress to the user via the progress callback.
    public static class ChihuahuaManager
    {
        public const string RepoOwner = "keton";
        public const string RepoName = "chihuahua";
        public const string ExpectedAssetName = "chihuahua.zip";

        public sealed class ReleaseInfo
        {
            public string Tag;            // e.g. "v0.4.1"
            public string PublishedAt;    // ISO-8601
            public string AssetUrl;
            public long AssetSizeBytes;
        }

        // ----- Paths -----

        public static string ChihuahuaDir(string dataRoot)
            => ConfigStore.ChihuahuaDir(dataRoot);

        public static string ChihuahuaBackupDir(string dataRoot)
            => ConfigStore.ChihuahuaBackupDir(dataRoot);

        public static string ChihuahuaExePath(string dataRoot)
            => Path.Combine(ChihuahuaDir(dataRoot), "chihuahua.exe");

        public static bool IsInstalled(string dataRoot)
            => File.Exists(ChihuahuaExePath(dataRoot));

        public static bool HasBackup(string dataRoot)
            => Directory.Exists(ChihuahuaBackupDir(dataRoot))
               && File.Exists(Path.Combine(ChihuahuaBackupDir(dataRoot), "chihuahua.exe"));

        // ----- Network -----

        // Hit GitHub API for the latest release. Throws on network/HTTP error.
        public static ReleaseInfo GetLatestRelease()
        {
            EnsureTls12();
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            string json = HttpGetString(url);
            return ParseRelease(json);
        }

        // Public for tests / future preview-tag support. Pass a specific tag
        // (e.g. "v0.4.0") to install something other than latest.
        public static ReleaseInfo GetReleaseByTag(string tag)
        {
            EnsureTls12();
            string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/tags/{tag}";
            string json = HttpGetString(url);
            return ParseRelease(json);
        }

        public static bool IsUpdateAvailable(string installedTag, ReleaseInfo latest)
        {
            if (latest == null) return false;
            if (string.IsNullOrEmpty(installedTag)) return true;
            return !string.Equals(installedTag, latest.Tag, StringComparison.Ordinal);
        }

        // ----- Install / Update -----

        // Download `release` and install it at <dataRoot>\chihuahua\.
        // If chihuahua is already installed, the previous install is moved to
        // <dataRoot>\chihuahua.bak\ (any prior .bak is discarded) so the user
        // has exactly one rollback target.
        //
        // onProgress is called as bytes arrive (bytesDownloaded, totalBytes).
        //
        // Failure modes are transactional: a partial download or extract is
        // cleaned up and any pre-existing chihuahua\ install is left intact.
        public static void Install(
            string dataRoot,
            ReleaseInfo release,
            Action<long, long> onProgress = null)
        {
            if (release == null) throw new ArgumentNullException(nameof(release));
            EnsureTls12();

            Directory.CreateDirectory(dataRoot);

            string chiDir = ChihuahuaDir(dataRoot);
            string bakDir = ChihuahuaBackupDir(dataRoot);
            string tmpZip = Path.Combine(dataRoot, "chihuahua-download.zip.tmp");
            string stageDir = Path.Combine(dataRoot, "chihuahua-stage.tmp");

            // Clean any leftover staging from a previous failed install.
            if (File.Exists(tmpZip)) File.Delete(tmpZip);
            if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true);

            try
            {
                DownloadWithProgress(release.AssetUrl, tmpZip, release.AssetSizeBytes, onProgress);

                Directory.CreateDirectory(stageDir);
                ZipFile.ExtractToDirectory(tmpZip, stageDir);

                if (!File.Exists(Path.Combine(stageDir, "chihuahua.exe")))
                {
                    throw new InvalidDataException(
                        "Downloaded zip did not contain chihuahua.exe at its root.");
                }

                // Atomic swap. Order matters so a crash mid-swap is recoverable:
                //   1. discard old .bak (if any)
                //   2. rename existing chihuahua\ → .bak (only if chihuahua\ exists)
                //   3. rename stage\ → chihuahua\
                if (Directory.Exists(bakDir)) Directory.Delete(bakDir, true);
                if (Directory.Exists(chiDir)) Directory.Move(chiDir, bakDir);
                Directory.Move(stageDir, chiDir);

                // Record the install in config.json
                var cfg = ConfigStore.LoadConfig(dataRoot);
                cfg.Chihuahua.Tag = release.Tag;
                cfg.Chihuahua.InstalledAt = DateTime.UtcNow.ToString("o");
                ConfigStore.SaveConfig(dataRoot, cfg);
            }
            finally
            {
                // Clean up staging zip; staging dir is gone (renamed) on success
                // or cleaned up on failure.
                if (File.Exists(tmpZip))
                {
                    try { File.Delete(tmpZip); } catch { /* best-effort */ }
                }
                if (Directory.Exists(stageDir))
                {
                    try { Directory.Delete(stageDir, true); } catch { /* best-effort */ }
                }
            }
        }

        // Swap chihuahua\ ↔ chihuahua.bak\. Lets the user roll back to the
        // previous install if a new chihuahua release breaks something.
        // Updates config.json only if we can recover a prior tag — otherwise
        // we just blank the tag field so an update prompt will fire again.
        public static void RestorePreviousVersion(string dataRoot)
        {
            string chiDir = ChihuahuaDir(dataRoot);
            string bakDir = ChihuahuaBackupDir(dataRoot);
            string scratch = chiDir + ".swap.tmp";

            if (!HasBackup(dataRoot))
                throw new InvalidOperationException("No backup to restore from.");

            if (Directory.Exists(scratch)) Directory.Delete(scratch, true);

            if (Directory.Exists(chiDir)) Directory.Move(chiDir, scratch);
            Directory.Move(bakDir, chiDir);
            if (Directory.Exists(scratch)) Directory.Move(scratch, bakDir);

            // We don't know the prior tag (we only stored the current one), so
            // blank the field. Next "Check for updates" will offer the latest.
            var cfg = ConfigStore.LoadConfig(dataRoot);
            cfg.Chihuahua.Tag = null;
            cfg.Chihuahua.InstalledAt = null;
            ConfigStore.SaveConfig(dataRoot, cfg);
        }

        // ----- Internals -----

        private static void EnsureTls12()
        {
            // GitHub requires TLS 1.2+. net48 defaults are friendly but be
            // explicit so we don't depend on the framework version.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        private static string HttpGetString(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "Kennel/0.1 (+https://github.com/AJBats/kennel)";
            req.Accept = "application/vnd.github+json";
            req.Timeout = 30_000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var stream = resp.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static void DownloadWithProgress(
            string url, string destPath, long expectedSize, Action<long, long> onProgress)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.UserAgent = "Kennel/0.1 (+https://github.com/AJBats/kennel)";
            req.Timeout = 60_000;
            req.ReadWriteTimeout = 120_000;
            req.AllowAutoRedirect = true;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(destPath))
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : expectedSize;
                var buf = new byte[81920];
                long copied = 0;
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    copied += n;
                    onProgress?.Invoke(copied, total);
                }
            }
        }

        private static ReleaseInfo ParseRelease(string json)
        {
            var ser = new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
            var rel = ser.Deserialize<GitHubRelease>(json);
            if (rel == null) throw new InvalidDataException("Unexpected GitHub release payload.");

            GitHubAsset asset = null;
            if (rel.assets != null)
            {
                foreach (var a in rel.assets)
                {
                    if (string.Equals(a.name, ExpectedAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        asset = a;
                        break;
                    }
                }
            }
            if (asset == null)
                throw new InvalidDataException($"Release {rel.tag_name} has no asset named {ExpectedAssetName}.");

            return new ReleaseInfo
            {
                Tag = rel.tag_name,
                PublishedAt = rel.published_at,
                AssetUrl = asset.browser_download_url,
                AssetSizeBytes = asset.size,
            };
        }

        // GitHub release JSON shapes. Property names match the API verbatim.
        private sealed class GitHubRelease
        {
            public string tag_name { get; set; }
            public string name { get; set; }
            public string published_at { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public List<GitHubAsset> assets { get; set; }
        }

        private sealed class GitHubAsset
        {
            public string name { get; set; }
            public long size { get; set; }
            public string browser_download_url { get; set; }
            public string content_type { get; set; }
        }
    }
}
