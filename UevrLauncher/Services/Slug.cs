using System.Text;
using System.Text.RegularExpressions;

namespace UevrLauncher.Services
{
    // Turn a free-form game name into a safe wrapper basename.
    //   "South of Midnight"          -> "south-of-midnight"
    //   "ACE COMBAT™7: SKIES UNKNOWN" -> "ace-combat-7-skies-unknown"
    //   "Baldur's Gate 3"            -> "baldurs-gate-3"
    public static class Slug
    {
        public static string FromGameName(string name, int maxLength = 40)
        {
            if (string.IsNullOrEmpty(name)) return "wrapper";

            var sb = new StringBuilder(name.Length);
            foreach (char c in name.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) sb.Append(c);
                else if (c == '-' || c == ' ' || c == '_' || c == ':' || c == '\'' || c == '.') sb.Append('-');
                // Drop anything else (™, &, ?, etc).
            }
            string s = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
            if (s.Length == 0) return "wrapper";
            if (s.Length > maxLength) s = s.Substring(0, maxLength).TrimEnd('-');
            return s;
        }
    }
}
