using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

enum InstallationLoader { None, Standard, Legacy, Both }
enum InstallationSeverity { Info, Warning, Error }

sealed class InstallationFinding
{
    internal InstallationFinding(InstallationSeverity severity, string path, string message)
    {
        Severity = severity;
        Path = path;
        Message = message;
    }

    public InstallationSeverity Severity { get; }
    public string Path { get; }
    public string Message { get; }
}

sealed class InstallationReport
{
    internal InstallationReport(string gameDirectory, InstallationLoader loader, IReadOnlyList<InstallationFinding> findings)
    {
        GameDirectory = gameDirectory;
        Loader = loader;
        Findings = findings;
    }

    public string GameDirectory { get; }
    public InstallationLoader Loader { get; }
    public IReadOnlyList<InstallationFinding> Findings { get; }
}

static class InstallationDiagnostics
{
    const string StandardLoader = "RDR2Coop.asi";
    const string StandardDll = "RDR2Coop/RDR2Coop.dll";
    const string LegacyLoader = "ScriptHookRDRDotNet.asi";
    const string LegacyDll = "scripts/RDR2Coop.dll";
    const string Settings = "scripts/RDR2Coop.ini";
    const string FxrPath = "RDR2Coop/runtime/host/fxr";
    const string FrameworkPath = "RDR2Coop/runtime/shared/Microsoft.NETCore.App";
    const int LogTailLimit = 64 * 1024;

    static readonly string[] StandardFiles =
    {
        StandardLoader, StandardDll,
        "RDR2Coop/RDR2Coop.Api.dll",
        "RDR2Coop/RDR2Coop.Core.dll",
        "RDR2Coop/RDR2Coop.Core.runtimeconfig.json",
        "RDR2Coop/RDR2Coop.Core.deps.json",
        "RDR2Coop/ScriptHookRDRNetAPI.dll",
        "RDR2Coop/System.Windows.Forms.dll",
        "RDR2Coop/System.Diagnostics.EventLog.dll",
        "RDR2Coop/System.Diagnostics.EventLog.Messages.dll",
        "RDR2Coop/System.Security.Permissions.dll",
        "RDR2Coop/System.Windows.Extensions.dll",
    };

    static readonly string[] CoreRuntimeFiles =
    {
        "Microsoft.NETCore.App.deps.json", "Microsoft.NETCore.App.runtimeconfig.json",
        "coreclr.dll", "hostpolicy.dll", "clrjit.dll", "System.Private.CoreLib.dll",
    };

    static readonly string[] LogFiles =
    {
        "scripts/RDR2Coop.Host.log",
        "scripts/RDR2Coop.Core.log",
        "scripts/RDR2Coop.log",
        "ScriptHookRDR2.log",
        "ScriptHookRDRDotNet.log",
    };

    public static InstallationReport Check(string gameDirectory)
    {
        var findings = new List<InstallationFinding>();
        if (!TryDirectory(gameDirectory, out string directory))
        {
            findings.Add(new InstallationFinding(InstallationSeverity.Error, gameDirectory ?? "",
                "Choose the game folder containing RDR2.exe."));
            return new InstallationReport(gameDirectory ?? "", InstallationLoader.None, findings.AsReadOnly());
        }

        Require(directory, "RDR2.exe", findings);
        bool standard = HasStandardLoader(directory);
        bool legacyHook = File.Exists(Under(directory, LegacyLoader));
        bool legacyMod = File.Exists(Under(directory, LegacyDll));
        bool legacy = standard ? legacyMod && legacyHook : legacyMod || legacyHook;
        var loader = standard
            ? (legacy ? InstallationLoader.Both : InstallationLoader.Standard)
            : (legacy ? InstallationLoader.Legacy : InstallationLoader.None);

        Require(directory, "ScriptHookRDR2.dll", findings);
        if (!HasContents(Under(directory, "dinput8.dll")))
        {
            if (HasContents(Under(directory, "version.dll")))
                findings.Add(new InstallationFinding(InstallationSeverity.Warning, "version.dll",
                    "The usual ASI loader dinput8.dll is missing. Confirm that version.dll is an ASI loader."));
            else
                Require(directory, "dinput8.dll", findings);
        }

        if (loader == InstallationLoader.None)
            findings.Add(new InstallationFinding(InstallationSeverity.Error, StandardLoader,
                "No installed co-op loader found. Run RDR2CoopSetup.exe from the full Windows download."));

        if (standard && legacyMod && legacyHook)
            findings.Add(new InstallationFinding(InstallationSeverity.Warning, LegacyDll,
                "Both loaders are installed. The standard loader defers to this legacy copy. Run the installer to switch to the standard loader."));
        else if (standard && legacyMod)
            findings.Add(new InstallationFinding(InstallationSeverity.Warning, LegacyDll,
                "A leftover legacy copy is present. The standard loader ignores it while ScriptHookRDRDotNet.asi is disabled."));
        else if (standard && legacyHook)
            findings.Add(new InstallationFinding(InstallationSeverity.Warning, LegacyLoader,
                "The legacy loader is active alongside the standard loader. This can prevent compatibility with scripts such as enRage."));

        if (standard)
        {
            foreach (string file in StandardFiles) Require(directory, file, findings);
            CheckRuntime(directory, findings);
            if (File.Exists(Under(directory, "scripts/NemesisMod.dll")))
            {
                foreach (string name in new[] { "patch.txt", "NemesisMod.coop.patch" })
                {
                    string patch = "RDR2Coop/enrage-coop-patch/" + name;
                    if (!HasContents(Under(directory, patch)))
                        findings.Add(new InstallationFinding(InstallationSeverity.Warning, patch,
                            "The optional enRage co-op patch is incomplete. enRage can run without the co-op link."));
                }
            }
        }

        if (legacy)
        {
            Require(directory, LegacyLoader, findings);
            Require(directory, LegacyDll, findings);
            Require(directory, "ScriptHookRDRNetAPI.dll", findings);
        }

        CheckSettings(directory, standard && !legacy, findings);
        if (!findings.Any(finding => finding.Severity == InstallationSeverity.Error))
            findings.Add(new InstallationFinding(InstallationSeverity.Info, "",
                "The expected co-op files are present. Start Story Mode and check the logs if the mod still does not load."));

        return new InstallationReport(directory, loader, findings.AsReadOnly());
    }

    public static InstallationReport CheckIni(string iniPath) => Check(GameDirectoryForIni(iniPath));

    public static string GameDirectoryForIni(string iniPath)
    {
        string directory;
        try { directory = Path.GetDirectoryName(Path.GetFullPath(iniPath)); }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
        {
            return null;
        }

        // The INI can be under scripts or beside the managed DLL.
        for (int depth = 0; depth < 2 && directory != null && !File.Exists(Under(directory, "RDR2.exe")); depth++)
        {
            string name = Path.GetFileName(directory);
            if (!string.Equals(name, "scripts", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(name, "RDR2Coop", StringComparison.OrdinalIgnoreCase)) break;
            directory = Path.GetDirectoryName(directory);
        }

        return directory;
    }

    public static string FindActiveIni(string gameDirectory)
    {
        if (!TryDirectory(gameDirectory, out string directory)) return null;
        bool activeLegacy = File.Exists(Under(directory, LegacyLoader)) && File.Exists(Under(directory, LegacyDll));
        string relative = SettingsPath(directory, HasStandardLoader(directory) && !activeLegacy);
        return relative == null ? null : Under(directory, relative);
    }

    public static string ReadRecentLog(string gameDirectory, int maxLines = 40)
    {
        if (!TryDirectory(gameDirectory, out string directory))
            return "Choose the game folder containing RDR2.exe to view logs.";

        int limit = Math.Max(1, Math.Min(200, maxLines));
        var result = new StringBuilder();
        bool hasCoopLog = false;
        foreach (string log in LogFiles)
        {
            string path = Under(directory, log);
            if (!File.Exists(path)) continue;
            if (log.StartsWith("scripts/", StringComparison.Ordinal)) hasCoopLog = true;
            if (result.Length > 0) result.AppendLine().AppendLine();
            result.Append(log);
            try
            {
                string modified = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss");
                result.Append(" (last modified ").Append(modified).AppendLine(")");
                string tail = ReadTail(path, limit);
                result.Append(tail.Length == 0 ? "This log is empty." : tail);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                result.AppendLine().Append("This log could not be read. It may have moved or be locked by the game.");
            }
        }

        if (result.Length == 0) return "No RDR2 Co-op or ScriptHook logs were found. Start Story Mode to generate them.";
        if (!hasCoopLog)
            result.Insert(0, "No RDR2 Co-op logs were found. Check the installation if they still do not appear after starting Story Mode."
                + Environment.NewLine + Environment.NewLine);
        return result.ToString();
    }

    static void CheckRuntime(string directory, List<InstallationFinding> findings)
    {
        string fxr = Under(directory, FxrPath);
        if (Directory.Exists(fxr))
        {
            // The native loader selects the last folder by name, including a partial newer copy.
            string selected = Subdirectories(fxr, FxrPath, findings).OrderBy(name => name, StringComparer.Ordinal).LastOrDefault();
            if (selected != null)
                Require(directory, FxrPath + "/" + selected + "/hostfxr.dll", findings);
            else
                Missing(FxrPath, findings, "No hostfxr version folder found. Reinstall the bundled .NET runtime.");
        }
        else
            Missing(FxrPath, findings, "The bundled .NET hostfxr folder is missing.");

        string shared = Under(directory, FrameworkPath);
        string version = Directory.Exists(shared)
            ? Subdirectories(shared, FrameworkPath, findings)
                .Where(name => Version.TryParse(name, out Version parsed) && parsed.Major == 9)
                .OrderBy(name => new Version(name)).LastOrDefault()
            : null;
        if (version == null)
        {
            Missing(FrameworkPath, findings, "No .NET 9 shared framework found. Reinstall the bundled runtime.");
            return;
        }

        string prefix = FrameworkPath + "/" + version + "/";
        if (version == DiagnosticsManifest.RuntimeVersion)
        {
            foreach (string file in DiagnosticsManifest.RuntimeFiles) Require(directory, prefix + file, findings);
        }
        else
        {
            foreach (string file in CoreRuntimeFiles) Require(directory, prefix + file, findings);
            findings.Add(new InstallationFinding(InstallationSeverity.Info, prefix.TrimEnd('/'),
                "This installation uses a different .NET 9 patch. The core runtime files were checked."));
        }
    }

    static void CheckSettings(string directory, bool standard, List<InstallationFinding> findings)
    {
        string selected = SettingsPath(directory, standard);
        if (selected == null)
            Require(directory, Settings, findings);
        else
        {
            Require(directory, selected, findings);
            if (selected != Settings)
                findings.Add(new InstallationFinding(InstallationSeverity.Warning, selected,
                    "The mod loads this settings file before scripts/RDR2Coop.ini. Select it to edit the active settings."));
        }
    }

    static string SettingsPath(string directory, bool standard)
    {
        string[] paths = standard
            ? new[] { "RDR2Coop/scripts/RDR2Coop.ini", "RDR2Coop/RDR2Coop.ini", Settings }
            : new[] { "scripts/scripts/RDR2Coop.ini", Settings };
        return paths.FirstOrDefault(path => File.Exists(Under(directory, path)));
    }

    static bool HasStandardLoader(string directory) =>
        File.Exists(Under(directory, StandardLoader)) || Directory.Exists(Under(directory, "RDR2Coop"));

    static string[] Subdirectories(string path, string relative, List<InstallationFinding> findings)
    {
        try
        {
            return Directory.GetDirectories(path).Select(Path.GetFileName).Where(name => !name.StartsWith(".")).ToArray();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            findings.Add(new InstallationFinding(InstallationSeverity.Error, relative,
                "Could not read this runtime folder. Check its permissions and contents."));
            return Array.Empty<string>();
        }
    }

    static void Require(string directory, string relative, List<InstallationFinding> findings)
    {
        if (HasContents(Under(directory, relative))) return;
        Missing(relative, findings, "Required file is missing, empty, or unreadable.");
    }

    static void Missing(string relative, List<InstallationFinding> findings, string message)
    {
        findings.Add(new InstallationFinding(InstallationSeverity.Error, relative, message));
    }

    static string Under(string directory, string relative) =>
        Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));

    static bool TryDirectory(string value, out string directory)
    {
        directory = null;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { directory = Path.GetFullPath(value); }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
        {
            return false;
        }
        return Directory.Exists(directory);
    }

    static bool HasContents(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists && file.Length > 0;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            return false;
        }
    }

    static string ReadTail(string path, int limit)
    {
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            long length = file.Length;
            int count = (int)Math.Min(length, LogTailLimit);
            bool truncated = length > count;
            file.Seek(length - count, SeekOrigin.Begin);
            var bytes = new byte[count];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = file.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) break;
                offset += read;
            }

            using (var memory = new MemoryStream(bytes, 0, offset))
            using (var reader = new StreamReader(memory, Encoding.UTF8, !truncated))
            {
                string first = truncated ? reader.ReadLine() : null;
                if (truncated && reader.EndOfStream) return first ?? "";
                var lines = new Queue<string>();
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (lines.Count == limit) lines.Dequeue();
                    lines.Enqueue(line);
                }
                return string.Join(Environment.NewLine, lines);
            }
        }
    }
}
