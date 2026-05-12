namespace UevrLauncher.Models
{
    public sealed class WrapperInfo
    {
        public string Basename { get; set; }
        public string AppId { get; set; }
        public string GameName { get; set; }
        public string GameExePath { get; set; }
        public int DelaySeconds { get; set; }
        // chihuahua's --uevr-build value: "Release" (default, tracks UEVR 1.05)
        // or "Nightly" (tracks praydog/UEVR main branch builds).
        public string UevrBuild { get; set; }
        public string BatPath { get; set; }
        public string VbsPath { get; set; }
    }
}
