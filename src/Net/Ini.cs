// Reads RDR2Coop.ini. ScriptHookRDR2DotNet's ScriptSettings found the file but
// returned only defaults under Proton, so this small reader replaces it.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RDR2Coop.Net
{
    public sealed class Ini
    {
        readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static Ini Load(string path) => Parse(ReadText(path));

        public static Ini Parse(string text)
        {
            var ini = new Ini();
            string section = "";
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[' && line.EndsWith("]")) { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                ini._values[section + "." + line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            return ini;
        }

        public string Get(string section, string key, string fallback) =>
            _values.TryGetValue(section + "." + key, out var v) && v.Length > 0 ? v : fallback;

        public int Get(string section, string key, int fallback) =>
            int.TryParse(Get(section, key, ""), out int v) ? v : fallback;

        // Sets [Coop] key=value lines in the file, keeping its comments and other
        // settings (the same rules as the installers).
        public static void SetCoop(string path, IDictionary<string, string> values) => Set(path, "Coop", values);

        /// <summary>Writes keys in the requested section, keeping comments and other settings.</summary>
        public static void Set(string path, string section, IDictionary<string, string> values) =>
            Update(path, text => Apply(text, section, values));

        public static void SetSections(string path, IDictionary<string, IDictionary<string, string>> sections)
        {
            Update(path, text =>
            {
                foreach (var section in sections) text = Apply(text, section.Key, section.Value);
                return text;
            });
        }

        public static string Apply(string text, string section, IDictionary<string, string> values)
        {
            if (values.Count == 0) return text;
            var updates = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
            var missing = new HashSet<string>(updates.Keys, StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>(text.Split('\n'));
            string ending = text.Contains("\r\n") ? "\r" : "";
            int header = -1;
            bool inSection = false;
            for (int i = 0; i < lines.Count; i++)
            {
                string raw = lines[i];
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[' && line.EndsWith("]"))
                {
                    inSection = string.Equals(line.Substring(1, line.Length - 2).Trim(), section, StringComparison.OrdinalIgnoreCase);
                    if (inSection) header = i;
                    continue;
                }
                if (!inSection) continue;
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                string key = raw.Substring(0, eq).Trim();
                if (!updates.TryGetValue(key, out string value)) continue;
                lines[i] = raw.Substring(0, eq + 1) + value + (raw.EndsWith("\r") ? "\r" : "");
                missing.Remove(key);
            }

            var additions = new List<string>();
            foreach (var pair in updates)
                if (missing.Contains(pair.Key)) additions.Add(pair.Key + "=" + pair.Value + ending);
            if (additions.Count == 0) return string.Join("\n", lines);
            if (header < 0)
            {
                additions.Insert(0, "[" + section + "]" + ending);
                var block = string.Join("\n", additions) + "\n";
                if (text.Length == 0 || string.Equals(section, "Coop", StringComparison.OrdinalIgnoreCase)) return block + text;
                return text.TrimEnd('\r', '\n') + ending + "\n" + ending + "\n" + block;
            }

            if (header == lines.Count - 1)
            {
                lines[header] += ending;
                lines.Add("");
            }
            lines.InsertRange(header + 1, additions);
            return string.Join("\n", lines);
        }

        internal static string ReadText(string path)
        {
            try
            {
                using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)))
                    return reader.ReadToEnd();
            }
            catch (FileNotFoundException) { return ""; }
            catch (DirectoryNotFoundException) { return ""; }
        }

        static void Update(string path, Func<string, string> transform)
        {
            string fullPath = Path.GetFullPath(path);
            string mutexKey = Environment.OSVersion.Platform == PlatformID.Win32NT ? fullPath.ToUpperInvariant() : fullPath;
            string mutexName;
            using (var sha = SHA256.Create())
                mutexName = "RDR2Coop.Ini." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(mutexKey))).Replace("-", "");
            using (var mutex = new Mutex(false, mutexName))
            {
                try
                {
                    if (!mutex.WaitOne(2000)) throw new IOException("RDR2 Co-op settings are busy. Try again.");
                }
                catch (AbandonedMutexException) { } // The previous writer exited. The old settings file is still intact.
                try
                {
                    string previous = ReadText(fullPath);
                    string updated = transform(previous);
                    if (!string.Equals(previous, updated, StringComparison.Ordinal)) WriteAtomic(fullPath, updated);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        internal static void WriteAtomic(string path, string text)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, text);
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path)) { File.Replace(temporary, path, null); }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
