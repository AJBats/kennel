namespace UevrLauncher.Models
{
    public sealed class WrapperInfo
    {
        public string Basename { get; set; }
        public string AppId { get; set; }
        public string GameName { get; set; }
        public string GameExePath { get; set; }
        public int DelaySeconds { get; set; }
        public string BatPath { get; set; }
        public string VbsPath { get; set; }
    }
}
