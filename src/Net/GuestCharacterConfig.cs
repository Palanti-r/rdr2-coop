using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RDR2Coop.Net
{
    // The picker and mod share this small file so a character change cannot
    // overwrite server settings that the in-game browser is saving.
    public static class GuestCharacterConfig
    {
        public static string Read(string iniPath, string fallback)
        {
            string path = Path.ChangeExtension(iniPath, ".character");
            if (!File.Exists(path)) return fallback;
            try
            {
                using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)))
                    return reader.ReadToEnd().Trim();
            }
            catch (FileNotFoundException) { return fallback; }
        }

        public static void Save(string iniPath, string model)
        {
            if (model.Length > 0 && !Regex.IsMatch(model, @"\A[A-Za-z0-9_]{1,80}\z"))
                throw new ArgumentException("Enter a character model name using letters, numbers, or underscores.");

            Ini.WriteAtomic(Path.ChangeExtension(iniPath, ".character"), model + Environment.NewLine);
        }
    }
}
