using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RDR2Coop.Net;

sealed class DiagnosticsPage : TabPage
{
    readonly TextBox _folder = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, AccessibleName = "Game folder" };
    readonly Label _summary = new Label { Dock = DockStyle.Fill, AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
    readonly Button _refresh = new Button { Text = "Check files and logs", AutoSize = true, Enabled = false };
    readonly Button _debug = new Button { Text = "Enable debug logging", AutoSize = true, Enabled = false };
    readonly Button _copy = new Button { Text = "Copy report", AutoSize = true, Enabled = false };
    readonly DataGridView _files = new DataGridView
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AccessibleName = "Installation checks",
    };
    readonly TextBox _logs = new TextBox
    {
        Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = false,
        ScrollBars = ScrollBars.Both, AccessibleName = "Recent RDR2 logs",
    };
    string _gameDirectory, _iniPath, _reportText;
    int _refreshId;

    public DiagnosticsPage(Action<string> selectGameFolder) : base("Diagnostics")
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var choose = new Button { Text = "Choose game folder…", AutoSize = true };
        choose.Click += (_, __) =>
        {
            using (var dialog = new FolderBrowserDialog { Description = "Choose the folder containing RDR2.exe.", ShowNewFolderButton = false })
            {
                if (_gameDirectory != null) dialog.SelectedPath = _gameDirectory;
                if (dialog.ShowDialog(this) == DialogResult.OK) selectGameFolder(dialog.SelectedPath);
            }
        };
        _refresh.Click += async (_, __) => await RefreshReport();
        _debug.Click += (_, __) => ToggleDebug();
        _copy.Click += (_, __) => CopyReport();

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        toolbar.Controls.AddRange(new Control[] { _refresh, _debug, _copy });
        _files.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Result", FillWeight = 19, MinimumWidth = 70 });
        _files.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File or check", FillWeight = 47 });
        _files.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", FillWeight = 44 });
        var details = new TabControl { Dock = DockStyle.Fill };
        var files = new TabPage("File checks");
        files.Controls.Add(_files);
        var logs = new TabPage("Recent logs");
        logs.Controls.Add(_logs);
        details.TabPages.Add(files);
        details.TabPages.Add(logs);

        layout.Controls.Add(_folder, 0, 0);
        layout.Controls.Add(choose, 1, 0);
        layout.Controls.Add(toolbar, 0, 1);
        layout.SetColumnSpan(toolbar, 2);
        layout.Controls.Add(_summary, 0, 2);
        layout.SetColumnSpan(_summary, 2);
        layout.Controls.Add(details, 0, 3);
        layout.SetColumnSpan(details, 2);
        Controls.Add(layout);
        _summary.Text = "Choose the game folder to check the installation and read its logs.";
    }

    public void SetTarget(string gameDirectory, string iniPath)
    {
        _refreshId++;
        _gameDirectory = gameDirectory;
        _iniPath = iniPath;
        _folder.Text = gameDirectory;
        _files.Rows.Clear();
        _logs.Clear();
        _reportText = null;
        _copy.Enabled = false;
        _refresh.Enabled = true;
        _debug.Enabled = iniPath != null;
        _summary.Text = "Check the files and logs for this game folder.";
        if (_debug.Enabled) SetDebugLabel();
        else _debug.Text = "Enable debug logging";
    }

    public async Task RefreshReport()
    {
        string directory = _gameDirectory;
        if (directory == null) return;
        int request = ++_refreshId;
        _refresh.Enabled = false;
        _copy.Enabled = false;
        _summary.Text = "Checking the installation and reading logs…";
        try
        {
            var result = await Task.Run(() => new
            {
                Report = InstallationDiagnostics.Check(directory),
                Logs = InstallationDiagnostics.ReadRecentLog(directory),
            });
            if (request != _refreshId || IsDisposed) return;
            ShowReport(result.Report, result.Logs);
            if (_debug.Enabled) SetDebugLabel();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            if (request == _refreshId && !IsDisposed) _summary.Text = "Could not check this installation. " + ex.Message;
        }
        finally
        {
            if (request == _refreshId && !IsDisposed) _refresh.Enabled = true;
        }
    }

    void ShowReport(InstallationReport report, string logs)
    {
        _files.Rows.Clear();
        var reportText = new StringBuilder().AppendLine("Game folder: " + report.GameDirectory).AppendLine("Loader: " + report.Loader);
        foreach (var finding in report.Findings.OrderByDescending(item => item.Severity))
        {
            int row = _files.Rows.Add(finding.Severity.ToString(), finding.Path, finding.Message);
            _files.Rows[row].Cells[1].ToolTipText = finding.Path;
            _files.Rows[row].Cells[2].ToolTipText = finding.Message;
            reportText.AppendLine($"{finding.Severity} | {finding.Path} | {finding.Message}");
        }
        int errors = report.Findings.Count(finding => finding.Severity == InstallationSeverity.Error);
        int warnings = report.Findings.Count(finding => finding.Severity == InstallationSeverity.Warning);
        _summary.Text = $"{report.Loader} loader · {errors} errors · {warnings} warnings";
        _logs.Text = logs;
        _reportText = reportText.AppendLine().Append(logs).ToString();
        _copy.Enabled = true;
    }

    bool DebugEnabled() => Ini.Load(_iniPath).Get("Testing", "DebugMode", "No").Equals("Yes", StringComparison.OrdinalIgnoreCase);

    void SetDebugLabel()
    {
        try { _debug.Text = DebugEnabled() ? "Disable debug logging" : "Enable debug logging"; }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _summary.Text = "Could not read debug settings. " + ex.Message;
        }
    }

    void ToggleDebug()
    {
        if (_iniPath == null) return;
        try
        {
            if (!File.Exists(_iniPath)) throw new FileNotFoundException("The installed RDR2Coop.ini is missing.");
            bool enabled = !DebugEnabled();
            Ini.Set(_iniPath, "Testing", new Dictionary<string, string> { ["DebugMode"] = enabled ? "Yes" : "No" });
            _debug.Text = enabled ? "Disable debug logging" : "Enable debug logging";
            _summary.Text = enabled
                ? "Restart RDR2, try the menu shortcut, then check the recent logs here."
                : "Debug logging is off. Restart RDR2 to apply.";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _summary.Text = "Could not change debug logging. " + ex.Message;
        }
    }

    void CopyReport()
    {
        if (_reportText == null) return;
        try
        {
            Clipboard.SetText(_reportText);
            _summary.Text = "Report copied.";
        }
        catch (System.Runtime.InteropServices.ExternalException ex)
        {
            _summary.Text = "Could not copy the report. " + ex.Message;
        }
    }
}
