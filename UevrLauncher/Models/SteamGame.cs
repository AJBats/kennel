namespace UevrLauncher.Models
{
    public sealed class SteamGame
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string InstallDir { get; set; }
        public string InstallPath { get; set; }
        public string LibraryRoot { get; set; }
    }
}
