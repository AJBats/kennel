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
        //
        // Twin install layout: separate dirs for Release-mode and Nightly-mode,
        // each with its own chihuahua.exe and its own UEVR DLL slot. Each
        // chihuahua keeps its own uevr.version in sync with the mode it serves,
        // so toggling a wrapper's mode no longer forces chihuahua to re-fetch
        // 12 MB of UEVR DLLs — each mode is permanently cached.

        public static string ChihuahuaDir(string dataRoot, string uevrBuild)
        {
            string sub = string.Equals(uevrBuild, WrapperIo.UevrBuildNightly, StringComparison.OrdinalIgnoreCase)
                ? "chihuahua-nightly"
                : "chihuahua-release";
            return Path.Combine(dataRoot, sub);
        }

        public static string ChihuahuaBackupDir(string dataRoot, string uevrBuild)
            => ChihuahuaDir(dataRoot, uevrBuild) + ".bak";

        public static string ChihuahuaExePath(string dataRoot, string uevrBuild)
            => Path.Combine(ChihuahuaDir(dataRoot, uevrBuild), "chihuahua.exe");

        // praydog's UEVR frontend, dropped into each mode's chihuahua dir
        // alongside the runtime DLLs (which chihuahua manages itself).
        public static string UevrInjectorExePath(string dataRoot, string uevrBuild)
            => Path.Combine(ChihuahuaDir(dataRoot, uevrBuild), "UEVRInjector.exe");

        public const string PraydogRepoOwner = "praydog";
        public const string PraydogRepoName = "UEVR";
        public const string PraydogAssetName = "UEVR.zip";

        // True only when BOTH modes are installed. We always install/uninstall
        // them as a pair so the GUI never has to expose "Nightly is missing"
        // as a state.
        public static bool IsInstalled(string dataRoot)
            => File.Exists(ChihuahuaExePath(dataRoot, WrapperIo.UevrBuildRelease))
               && File.Exists(ChihuahuaExePath(dataRoot, WrapperIo.UevrBuildNightly));

        public static bool HasBackup(string dataRoot)
            => File.Exists(Path.Combine(ChihuahuaBackupDir(dataRoot, WrapperIo.UevrBuildRelease), "chihuahua.exe"))
               || File.Exists(Path.Combine(ChihuahuaBackupDir(dataRoot, WrapperIo.UevrBuildNightly), "chihuahua.exe"));

        // ----- One-time migration from old single-chihuahua layout -----

        // True when the data root has the pre-Option-B layout: a single
        // <dataRoot>\chihuahua\ dir, and the mode-specific dirs don't exist.
        public static bool NeedsMigration(string dataRoot)
        {
            var oldDir = Path.Combine(dataRoot, "chihuahua");
            return File.Exists(Path.Combine(oldDir, "chihuahua.exe"))
                && !File.Exists(ChihuahuaExePath(dataRoot, WrapperIo.UevrBuildRelease))
                && !File.Exists(ChihuahuaExePath(dataRoot, WrapperIo.UevrBuildNightly));
        }

        // Split the old <dataRoot>\chihuahua\ into mode dirs:
        //   - the existing dir (which has UEVR DLLs for whichever mode it ran
        //     in) becomes the dir for that mode
        //   - the other mode gets a bare chihuahua.exe (+ its sidecars) so
        //     it's ready to bootstrap UEVR DLLs on its first launch
        // Detects the current mode by reading uevr.version: starts with
        // "nightly" → Nightly, otherwise Release.
        public static void Migrate(string dataRoot)
        {
            var oldDir = Path.Combine(dataRoot, "chihuahua");
            if (!File.Exists(Path.Combine(oldDir, "chihuahua.exe"))) return;

            string mode = WrapperIo.UevrBuildRelease;
            var verPath = Path.Combine(oldDir, "uevr.version");
            if (File.Exists(verPath))
            {
                var ver = File.ReadAllText(verPath).Trim();
                if (ver.StartsWith("nightly", StringComparison.OrdinalIgnoreCase))
                    mode = WrapperIo.UevrBuildNightly;
            }

            var existingDir = ChihuahuaDir(dataRoot, mode);
            var otherDir = ChihuahuaDir(dataRoot, mode == WrapperIo.UevrBuildRelease
                ? WrapperIo.UevrBuildNightly
                : WrapperIo.UevrBuildRelease);

            Directory.Move(oldDir, existingDir);

            // Bootstrap the other mode with just the chihuahua binaries — its
            // UEVR DLLs will be fetched on its first game launch.
            Directory.CreateDirectory(otherDir);
            foreach (var f in new[] { "chihuahua.exe", "chihuahua.pdb", "rai-pal-manifest.json" })
            {
                var src = Path.Combine(existingDir, f);
                if (File.Exists(src)) File.Copy(src, Path.Combine(otherDir, f));
            }

            // The old .bak (if any) belonged to the same mode; rename to match.
            var oldBak = Path.Combine(dataRoot, "chihuahua.bak");
            if (Directory.Exists(oldBak))
            {
                var newBak = ChihuahuaBackupDir(dataRoot, mode);
                if (Directory.Exists(newBak)) Directory.Delete(newBak, true);
                Directory.Move(oldBak, newBak);
            }
        }

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

            string releaseDir = ChihuahuaDir(dataRoot, WrapperIo.UevrBuildRelease);
            string nightlyDir = ChihuahuaDir(dataRoot, WrapperIo.UevrBuildNightly);
            string releaseBak = ChihuahuaBackupDir(dataRoot, WrapperIo.UevrBuildRelease);
            string nightlyBak = ChihuahuaBackupDir(dataRoot, WrapperIo.UevrBuildNightly);
            string tmpZip = Path.Combine(dataRoot, "chihuahua-download.zip.tmp");
            string releaseStage = Path.Combine(dataRoot, "chihuahua-release.stage.tmp");
            string nightlyStage = Path.Combine(dataRoot, "chihuahua-nightly.stage.tmp");

            // Clean any leftover staging from a previous failed install.
            if (File.Exists(tmpZip)) File.Delete(tmpZip);
            if (Directory.Exists(releaseStage)) Directory.Delete(releaseStage, true);
            if (Directory.Exists(nightlyStage)) Directory.Delete(nightlyStage, true);

            try
            {
                DownloadWithProgress(release.AssetUrl, tmpZip, release.AssetSizeBytes, onProgress);

                long downloadedSize = new FileInfo(tmpZip).Length;
                if (downloadedSize != release.AssetSizeBytes)
                {
                    throw new InvalidDataException(
                        $"Download size mismatch: expected {release.AssetSizeBytes} bytes, " +
                        $"got {downloadedSize}. Refusing to extract.");
                }

                // Extract once into the Release stage; copy to the Nightly
                // stage. We can't extract twice from the same zip stream
                // efficiently, and File.Copy is faster than a second
                // ZipFile.OpenRead pass anyway.
                Directory.CreateDirectory(releaseStage);
                SafeExtractZip(tmpZip, releaseStage);
                if (!File.Exists(Path.Combine(releaseStage, "chihuahua.exe")))
                {
                    throw new InvalidDataException(
                        "Downloaded zip did not contain chihuahua.exe at its root.");
                }
                CopyDirectory(releaseStage, nightlyStage);

                // Atomic-ish swap, both modes. Order matters so a crash mid-swap
                // leaves recoverable state.
                if (Directory.Exists(releaseBak)) Directory.Delete(releaseBak, true);
                if (Directory.Exists(nightlyBak)) Directory.Delete(nightlyBak, true);
                if (Directory.Exists(releaseDir)) Directory.Move(releaseDir, releaseBak);
                if (Directory.Exists(nightlyDir)) Directory.Move(nightlyDir, nightlyBak);
                Directory.Move(releaseStage, releaseDir);
                Directory.Move(nightlyStage, nightlyDir);

                // Drop praydog's UEVRInjector.exe into both mode dirs so
                // Manual-injection wrappers have a frontend to launch. SHA-256
                // verified against praydog's published .sha256 sidecar.
                FetchUevrInjector(dataRoot, onProgress);

                var cfg = ConfigStore.LoadConfig(dataRoot);
                cfg.Chihuahua.Tag = release.Tag;
                cfg.Chihuahua.InstalledAt = DateTime.UtcNow.ToString("o");
                ConfigStore.SaveConfig(dataRoot, cfg);
            }
            finally
            {
                if (File.Exists(tmpZip))
                {
                    try { File.Delete(tmpZip); } catch { /* best-effort */ }
                }
                foreach (var s in new[] { releaseStage, nightlyStage })
                {
                    if (Directory.Exists(s))
                    {
                        try { Directory.Delete(s, true); } catch { /* best-effort */ }
                    }
                }
            }
        }

        // Recursive copy. .NET Framework has no built-in for this.
        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDirectory(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        // Swap chihuahua\ ↔ chihuahua.bak\. Lets the user roll back to the
        // previous install if a new chihuahua release breaks something.
        // Updates config.json only if we can recover a prior tag — otherwise
        // we just blank the tag field so an update prompt will fire again.
        public static void RestorePreviousVersion(string dataRoot)
        {
            if (!HasBackup(dataRoot))
                throw new InvalidOperationException("No backup to restore from.");

            // Both modes were installed as a pair, so they roll back as a pair.
            // We swap them in lockstep.
            foreach (var mode in new[] { WrapperIo.UevrBuildRelease, WrapperIo.UevrBuildNightly })
            {
                string chiDir = ChihuahuaDir(dataRoot, mode);
                string bakDir = ChihuahuaBackupDir(dataRoot, mode);
                string scratch = chiDir + ".swap.tmp";
                if (!Directory.Exists(bakDir)) continue;
                if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
                if (Directory.Exists(chiDir)) Directory.Move(chiDir, scratch);
                Directory.Move(bakDir, chiDir);
                if (Directory.Exists(scratch)) Directory.Move(scratch, bakDir);
            }

            var cfg = ConfigStore.LoadConfig(dataRoot);
            cfg.Chihuahua.Tag = null;
            cfg.Chihuahua.InstalledAt = null;
            ConfigStore.SaveConfig(dataRoot, cfg);
        }

        // ----- Internals -----

        // Download praydog/UEVR's latest release zip, verify its SHA-256
        // against the sidecar published in the same release, then extract
        // just UEVRInjector.exe into both mode dirs. The runtime DLLs are
        // intentionally NOT pulled from this zip — chihuahua owns those, and
        // for Nightly mode chihuahua will fetch a different (newer) set.
        //
        // Errors here are non-fatal for the chihuahua install: if praydog's
        // release is unreachable, the user can still use auto-inject; only
        // Manual-mode wrappers will fail at game-launch time (with the
        // existing :no_chihuahua msgbox).
        public static void FetchUevrInjector(string dataRoot, Action<long, long> onProgress = null)
        {
            EnsureTls12();

            string apiUrl = $"https://api.github.com/repos/{PraydogRepoOwner}/{PraydogRepoName}/releases/latest";
            string json;
            try { json = HttpGetString(apiUrl); }
            catch { return; }

            var ser = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024 };
            var rel = ser.Deserialize<GitHubRelease>(json);
            if (rel?.assets == null) return;

            GitHubAsset zipAsset = null;
            GitHubAsset shaAsset = null;
            foreach (var a in rel.assets)
            {
                if (string.Equals(a.name, PraydogAssetName, StringComparison.OrdinalIgnoreCase))
                    zipAsset = a;
                else if (string.Equals(a.name, PraydogAssetName + ".sha256", StringComparison.OrdinalIgnoreCase))
                    shaAsset = a;
            }
            if (zipAsset == null) return;

            string tmpZip = Path.Combine(dataRoot, "uevr-download.zip.tmp");
            string stageDir = Path.Combine(dataRoot, "uevr-stage.tmp");
            if (File.Exists(tmpZip)) File.Delete(tmpZip);
            if (Directory.Exists(stageDir)) Directory.Delete(stageDir, true);

            try
            {
                DownloadWithProgress(zipAsset.browser_download_url, tmpZip, zipAsset.size, onProgress);

                if (new FileInfo(tmpZip).Length != zipAsset.size) return;

                // Verify SHA-256 if praydog published one.
                if (shaAsset != null)
                {
                    string expected;
                    try { expected = HttpGetString(shaAsset.browser_download_url).Trim().Split(' ')[0]; }
                    catch { expected = null; }

                    if (!string.IsNullOrEmpty(expected))
                    {
                        string actual = ComputeSha256Hex(tmpZip);
                        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidDataException(
                                $"UEVR.zip SHA-256 mismatch: expected {expected}, got {actual}. Refusing to extract.");
                        }
                    }
                }

                Directory.CreateDirectory(stageDir);
                SafeExtractZip(tmpZip, stageDir);

                // Praydog's UEVR.zip contains the 1.05 frontend + matching
                // runtime DLLs. We populate ONLY the Release-mode chihuahua
                // dir — Nightly mode is owned by chihuahua's own fetcher (it
                // tracks praydog's pre-release nightly tags), and dropping
                // 1.05 DLLs into chihuahua-nightly\ would silently lie about
                // which UEVR version is actually loaded. Manual-injection
                // wrappers are now forced to use chihuahua-release\ (the only
                // mode with a published frontend), so this layout serves both
                // the auto-inject Release path and every Manual wrapper.
                if (FindFile(stageDir, "UEVRInjector.exe") == null) return;

                string releaseDir = ChihuahuaDir(dataRoot, WrapperIo.UevrBuildRelease);
                Directory.CreateDirectory(releaseDir);

                // Frontend: always overwrite. If praydog ships a new 1.x
                // release, we want the latest frontend in place.
                var frontendSrc = FindFile(stageDir, "UEVRInjector.exe");
                if (frontendSrc != null)
                    File.Copy(frontendSrc, Path.Combine(releaseDir, "UEVRInjector.exe"), overwrite: true);

                // Runtime DLLs + version stamp: copy only when missing so a
                // re-install doesn't revert a newer 1.x set fetched by
                // chihuahua's own update logic.
                string[] copyIfMissing =
                {
                    "UEVRBackend.dll",
                    "UEVRPluginNullifier.dll",
                    "openvr_api.dll",
                    "openxr_loader.dll",
                    "uevr.version",
                };
                foreach (var name in copyIfMissing)
                {
                    var dstFile = Path.Combine(releaseDir, name);
                    if (File.Exists(dstFile)) continue;
                    var src = FindFile(stageDir, name);
                    if (src != null) File.Copy(src, dstFile);
                }
            }
            finally
            {
                if (File.Exists(tmpZip)) { try { File.Delete(tmpZip); } catch { } }
                if (Directory.Exists(stageDir)) { try { Directory.Delete(stageDir, true); } catch { } }
            }
        }

        // Find a file by basename anywhere in a directory tree. praydog's
        // UEVR.zip lays everything out flat at the root in current releases,
        // but defending against future layout changes is cheap.
        private static string FindFile(string root, string fileName)
        {
            var hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            return hits.Length > 0 ? hits[0] : null;
        }

        private static string ComputeSha256Hex(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // Replacement for ZipFile.ExtractToDirectory that defends against
        // "zip-slip": malicious entries whose names resolve outside the
        // destination directory via `..\..\` or absolute paths. .NET Framework
        // 4.6.1+ has partial protection but it's been incomplete in the past
        // and won't catch every shape (e.g. mixed slash directions, junctions).
        // We do the check ourselves: resolve the would-be destination path,
        // compare its prefix against the canonicalized stage dir, refuse if it
        // doesn't match.
        private static void SafeExtractZip(string zipPath, string destDir)
        {
            string destFull = Path.GetFullPath(destDir);
            if (!destFull.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                destFull += Path.DirectorySeparatorChar;

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in archive.Entries)
                {
                    // Skip directory entries (FullName ends with '/'). Files
                    // inside them implicitly create the directory below.
                    if (string.IsNullOrEmpty(entry.Name)) continue;

                    string targetPath = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
                    if (!targetPath.StartsWith(destFull, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "Refusing zip entry '" + entry.FullName + "' — resolved path " +
                            "is outside the staging directory (zip-slip).");
                    }

                    var parent = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }
        }

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
