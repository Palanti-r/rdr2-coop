using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using RDR2Coop.Net;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CharacterPicker());
    }
}

sealed class CharacterPicker : Form
{
    const string IniName = "RDR2Coop.ini";
    const string PresetPrefix = "; GuestCharacter=";
    readonly TextBox _location = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, AccessibleName = "Settings file" };
    readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill, Multiline = true };
    readonly ConnectionPage _connection = new ConnectionPage();
    readonly DiagnosticsPage _diagnostics;
    readonly TabPage _characterPage;
    readonly SettingsPage[] _settings;
    readonly ComboBox _characters = new ComboBox
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, AccessibleName = "Guest character", MaxDropDownItems = 14
    };
    readonly Label _characterHelp = new Label { AutoSize = true, MaximumSize = new Size(520, 0), Margin = new Padding(3, 9, 3, 9) };
    readonly Label _status = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly Button _applyCharacter = new Button { Text = "Apply character", AutoSize = true };
    readonly Button _save = new Button { Text = "&Save settings", AutoSize = true, Enabled = false, Anchor = AnchorStyles.Right };
    Character _originalCustom;
    string _iniPath;

    public CharacterPicker()
    {
        Text = "RDR2 Co-op Settings";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(810, 560);
        MinimumSize = SizeFromClientSize(new Size(670, 460));

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 4 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var browse = new Button { Text = "&Browse…", AutoSize = true };
        browse.Click += (_, __) => Browse();
        _save.Click += (_, __) => SaveSettings();
        _applyCharacter.Click += (_, __) => SaveCharacter();
        _connection.RoleChanged += (_, __) => UpdateCharacterPage();

        grid.Controls.Add(new Label { Text = "Co-op settings file", AutoSize = true }, 0, 0);
        grid.Controls.Add(_location, 0, 1);
        grid.Controls.Add(browse, 1, 1);
        grid.Controls.Add(_tabs, 0, 2);
        grid.SetColumnSpan(_tabs, 2);
        grid.Controls.Add(_status, 0, 3);
        grid.Controls.Add(_save, 1, 3);
        Controls.Add(grid);

        string template;
        using (var reader = new StreamReader(typeof(CharacterPicker).Assembly.GetManifestResourceStream("CoopSettings.ini")))
            template = reader.ReadToEnd();
        LoadPresets(template);
        var supported = Setting.FromTemplate(template).Concat(Setting.Additional());
        _settings = supported.GroupBy(setting => setting.Section).Select(group =>
            new SettingsPage(TabTitle(group.Key), group)).ToArray();

        _characterPage = CreateCharacterPage();
        _diagnostics = new DiagnosticsPage(SelectGameFolder);
        _tabs.TabPages.Add(_connection);
        _tabs.TabPages.Add(_characterPage);
        _tabs.TabPages.Add(_diagnostics);
        _tabs.TabPages.AddRange(_settings);
        _tabs.SelectedIndexChanged += async (_, __) =>
        {
            if (_tabs.SelectedTab == _diagnostics) await _diagnostics.RefreshReport();
        };
        SetEditingEnabled(false);
        UpdateCharacterPage();

        string folder = AppDomain.CurrentDomain.BaseDirectory;
        if (File.Exists(Path.Combine(folder, "RDR2.exe")))
        {
            string path = InstallationDiagnostics.FindActiveIni(folder);
            if (path != null) OpenSettings(path);
            else SelectGameFolder(folder);
        }
        else
        {
            _tabs.SelectedTab = _diagnostics;
            _status.Text = "Choose the game folder or browse to its installed RDR2Coop.ini.";
        }
    }

    static string TabTitle(string section)
    {
        switch (section)
        {
            case "Coop": return "General";
            case "Keys": return "Key bindings";
            case "Net": return "Network";
            case "Testing": return "Advanced";
            default: return section;
        }
    }

    TabPage CreateCharacterPage()
    {
        var tab = new TabPage("Character") { Padding = new Padding(12) };
        var fields = ConnectionPage.CreateLayout();
        ConnectionPage.AddRow(fields, "Guest character", _characters);
        ConnectionPage.AddRow(fields, "", _applyCharacter);
        ConnectionPage.AddWide(fields, _characterHelp);
        tab.Controls.Add(fields);
        return tab;
    }

    void UpdateCharacterPage()
    {
        _characters.Enabled = _applyCharacter.Enabled = !_connection.Hosting;
        _characterHelp.Text = _connection.Hosting
            ? "Hosts play their story character. Use Appear As in General to change the model other players see."
            : "Apply your selection while playing as a guest on foot in free roam. To return to your original story character, restart RDR2 and load your original save.";
    }

    void LoadPresets(string template)
    {
        _characters.Items.Add(new Character("Story character (reload save)", ""));
        using (var reader = new StringReader(template))
        {
            string previous = "";
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(PresetPrefix, StringComparison.Ordinal))
                    _characters.Items.Add(new Character(previous, line.Substring(PresetPrefix.Length)));
                previous = line.StartsWith("; ", StringComparison.Ordinal) ? line.Substring(2) : "";
            }
        }
        _characters.SelectedIndex = 0;
    }

    async void Browse()
    {
        using (var dialog = new OpenFileDialog { FileName = IniName, Filter = "RDR2 Co-op settings|RDR2Coop.ini", CheckFileExists = true })
        {
            if (_iniPath != null) dialog.InitialDirectory = Path.GetDirectoryName(_iniPath);
            if (dialog.ShowDialog(this) == DialogResult.OK && OpenSettings(dialog.FileName) && _tabs.SelectedTab == _diagnostics)
                await _diagnostics.RefreshReport();
        }
    }

    async void SelectGameFolder(string folder)
    {
        string path = File.Exists(Path.Combine(folder, "RDR2.exe")) ? InstallationDiagnostics.FindActiveIni(folder) : null;
        if (path == null || !OpenSettings(path))
        {
            _iniPath = null;
            _location.Clear();
            SetEditingEnabled(false);
            _diagnostics.SetTarget(folder, null);
            if (path == null) _status.Text = "Choose an installed RDR2Coop.ini to edit settings or review the file checks.";
        }

        if (_tabs.SelectedTab == _diagnostics) await _diagnostics.RefreshReport();
        else _tabs.SelectedTab = _diagnostics;
    }

    void SetEditingEnabled(bool enabled)
    {
        _connection.Enabled = _characterPage.Enabled = _save.Enabled = enabled;
        foreach (var page in _settings) page.Enabled = enabled;
    }

    bool OpenSettings(string path)
    {
        if (!string.Equals(Path.GetFileName(path), IniName, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            _status.Text = "Choose an existing RDR2Coop.ini file.";
            return false;
        }

        try
        {
            string folder = InstallationDiagnostics.GameDirectoryForIni(path);
            if (folder == null || !File.Exists(Path.Combine(folder, "RDR2.exe")))
            {
                _status.Text = "That INI is outside the installed game folder. Choose the folder containing RDR2.exe.";
                return false;
            }
            string active = InstallationDiagnostics.FindActiveIni(folder);
            if (active == null)
            {
                _status.Text = "The loader cannot find this INI. Check the game folder in Diagnostics.";
                return false;
            }
            bool redirected = !string.Equals(Path.GetFullPath(path), active, StringComparison.OrdinalIgnoreCase);
            path = active;
            var ini = Ini.Load(path);
            string model = GuestCharacterConfig.Read(path, ini.Get("Coop", "GuestCharacter", ""));
            _connection.LoadSettings(ini);
            foreach (var settings in _settings) settings.LoadSettings(ini);
            if (_originalCustom != null) _characters.Items.Remove(_originalCustom);
            _originalCustom = null;
            var choice = _characters.Items.Cast<Character>().FirstOrDefault(item => string.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase));
            if (choice == null)
            {
                choice = _originalCustom = new Character(model, model);
                _characters.Items.Add(choice);
            }

            _iniPath = path;
            _location.Text = path;
            _characters.SelectedItem = choice;
            SetEditingEnabled(true);
            _diagnostics.SetTarget(folder, path);
            _status.Text = redirected ? "Opened the INI used by the active loader." : "Settings loaded.";
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            _status.Text = "Could not load settings. " + ex.Message;
            return false;
        }
    }

    void SaveSettings()
    {
        if (_iniPath == null) return;
        try
        {
            var profile = _connection.Read();
            var sections = profile.ToSections();
            foreach (var settings in _settings) settings.ExportChanges(sections);
            Ini.SetSections(_iniPath, sections);
            var updated = Ini.Load(_iniPath);
            foreach (var settings in _settings) settings.LoadSettings(updated);
            _status.Text = (profile.Hosting ? "Host" : "Guest") + " settings saved. Restart RDR2 to apply.";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            _status.Text = "Could not save settings. " + ex.Message;
        }
    }

    void SaveCharacter()
    {
        if (_iniPath == null || _connection.Hosting || !(_characters.SelectedItem is Character character)) return;
        try
        {
            GuestCharacterConfig.Save(_iniPath, character.Model);
            _status.Text = character.Model.Length == 0
                ? "Story character saved. Restart RDR2 and load your original save."
                : character.Name + " saved. Return to the game on foot in free roam.";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            _status.Text = "Could not save the character. " + ex.Message;
        }
    }

    sealed class Character
    {
        public readonly string Name, Model;
        public Character(string name, string model) { Name = name; Model = model; }
        public override string ToString() => Name;
    }
}
