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
