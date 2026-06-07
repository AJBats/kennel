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
        // or "Nightly" (tracks praydog/UEVR main branch builds). "Custom" is
        // a Kennel-specific extension: the wrapper points at a user-supplied
        // UEVR install (presumably a locally-built fork) and goes through
        // that dir's UEVRInjector.exe directly — chihuahua is bypassed.
        public string UevrBuild { get; set; }
        // Absolute path to the user's custom UEVR install dir. Only set when
        // UevrBuild == "Custom"; otherwise null.
        public string CustomUevrDir { get; set; }
        // When true, the wrapper does NOT auto-inject via chihuahua. Instead
        // it starts the UEVRInjector.exe frontend (if not already running) and
        // launches the game flat — the user injects manually from the frontend
        // once the game is up. Mutually exclusive with DelaySeconds (which is
        // a chihuahua-only knob).
        public bool ManualInjection { get; set; }
        public string BatPath { get; set; }
        public string VbsPath { get; set; }
    }
}
