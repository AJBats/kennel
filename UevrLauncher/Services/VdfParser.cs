using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UevrLauncher.Services
{
    // Minimal Valve KeyValues (VDF) parser/serializer.
    //
    // Handles the dialect Steam writes for libraryfolders.vdf, appmanifest_*.acf,
    // and userdata/<id>/config/localconfig.vdf:
    //   "key" "value"        — string pair
    //   "key" { ... }        — nested block
    //   // line comment
    //   C-style escapes inside double-quoted strings (\\ \" \n \t)
    //
    // Order is preserved on serialize so Steam doesn't see gratuitous reorders.
    // Conditional clauses like [$WIN32] are not used by the files we touch and
    // are not parsed — if encountered they'll cause a parse error rather than
    // silently dropping data.
    public sealed class VdfBlock
    {
        private readonly List<KeyValuePair<string, object>> _entries =
            new List<KeyValuePair<string, object>>();

        public IReadOnlyList<KeyValuePair<string, object>> Entries => _entries;

        public int Count => _entries.Count;

        public object this[string key]
        {
            get
            {
                int idx = IndexOf(key);
                return idx < 0 ? null : _entries[idx].Value;
            }
            set
            {
                int idx = IndexOf(key);
                if (idx < 0) _entries.Add(new KeyValuePair<string, object>(key, value));
                else _entries[idx] = new KeyValuePair<string, object>(_entries[idx].Key, value);
            }
        }

        public bool TryGetString(string key, out string value)
        {
            int idx = IndexOf(key);
            if (idx >= 0 && _entries[idx].Value is string s) { value = s; return true; }
            value = null; return false;
        }

        public bool TryGetBlock(string key, out VdfBlock block)
        {
            int idx = IndexOf(key);
            if (idx >= 0 && _entries[idx].Value is VdfBlock b) { block = b; return true; }
            block = null; return false;
        }

        public string GetString(params string[] path)
        {
            VdfBlock cur = this;
            for (int i = 0; i < path.Length - 1; i++)
            {
                if (!cur.TryGetBlock(path[i], out cur)) return null;
            }
            cur.TryGetString(path[path.Length - 1], out var v);
            return v;
        }

        public VdfBlock GetBlock(params string[] path)
        {
            VdfBlock cur = this;
            foreach (var seg in path)
            {
                if (!cur.TryGetBlock(seg, out cur)) return null;
            }
            return cur;
        }

        // Set or create a string at the given path, creating any intermediate
        // blocks that don't exist. Used for writing LaunchOptions into
        // localconfig.vdf where the per-appid block may not yet exist.
        public void SetStringAtPath(string[] path, string value)
        {
            VdfBlock cur = this;
            for (int i = 0; i < path.Length - 1; i++)
            {
                if (!cur.TryGetBlock(path[i], out var next))
                {
                    next = new VdfBlock();
                    cur[path[i]] = next;
                }
                cur = next;
            }
            cur[path[path.Length - 1]] = value;
        }

        public void Add(string key, object value)
        {
            _entries.Add(new KeyValuePair<string, object>(key, value));
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Key, key, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }

    public static class VdfParser
    {
        public static VdfBlock Parse(string text)
        {
            var p = new Parser(text);
            var root = new VdfBlock();
            p.SkipTrivia();
            while (!p.Eof)
            {
                ParseEntry(p, root);
                p.SkipTrivia();
            }
            return root;
        }

        public static string Serialize(VdfBlock root, bool useTabs = true)
        {
            var sb = new StringBuilder();
            string indent = useTabs ? "\t" : "    ";
            foreach (var entry in root.Entries) WriteEntry(sb, entry.Key, entry.Value, 0, indent);
            return sb.ToString();
        }

        private static void ParseEntry(Parser p, VdfBlock parent)
        {
            string key = p.ReadString();
            p.SkipTrivia();
            if (p.Peek() == '{')
            {
                p.Next(); // consume {
                var block = new VdfBlock();
                p.SkipTrivia();
                while (!p.Eof && p.Peek() != '}')
                {
                    ParseEntry(p, block);
                    p.SkipTrivia();
                }
                if (p.Eof) throw new InvalidDataException("VDF: unterminated block");
                p.Next(); // consume }
                parent.Add(key, block);
            }
            else
            {
                string value = p.ReadString();
                parent.Add(key, value);
            }
        }

        private static void WriteEntry(StringBuilder sb, string key, object value, int depth, string indent)
        {
            for (int i = 0; i < depth; i++) sb.Append(indent);
            sb.Append('"').Append(Escape(key)).Append('"');
            if (value is VdfBlock b)
            {
                sb.Append('\n');
                for (int i = 0; i < depth; i++) sb.Append(indent);
                sb.Append("{\n");
                foreach (var e in b.Entries) WriteEntry(sb, e.Key, e.Value, depth + 1, indent);
                for (int i = 0; i < depth; i++) sb.Append(indent);
                sb.Append("}\n");
            }
            else
            {
                string s = value as string ?? string.Empty;
                sb.Append("\t\t\"").Append(Escape(s)).Append("\"\n");
            }
        }

        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            public Parser(string s) { _s = s; _i = 0; }
            public bool Eof => _i >= _s.Length;
            public char Peek() => _s[_i];
            public char Next() => _s[_i++];

            public void SkipTrivia()
            {
                while (!Eof)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { _i++; continue; }
                    if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        while (!Eof && _s[_i] != '\n') _i++;
                        continue;
                    }
                    break;
                }
            }

            public string ReadString()
            {
                SkipTrivia();
                if (Eof) throw new InvalidDataException("VDF: unexpected EOF (expected string)");
                if (_s[_i] == '"') return ReadQuoted();
                return ReadBareword();
            }

            private string ReadQuoted()
            {
                _i++; // consume opening quote
                var sb = new StringBuilder();
                while (!Eof)
                {
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\' && !Eof)
                    {
                        char esc = _s[_i++];
                        switch (esc)
                        {
                            case '\\': sb.Append('\\'); break;
                            case '"': sb.Append('"'); break;
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            default: sb.Append('\\').Append(esc); break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                throw new InvalidDataException("VDF: unterminated string");
            }

            private string ReadBareword()
            {
                int start = _i;
                while (!Eof)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '{' || c == '}' || c == '"') break;
                    _i++;
                }
                return _s.Substring(start, _i - start);
            }
        }
    }
}
