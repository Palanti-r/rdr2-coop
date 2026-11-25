using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using RDR2Coop.Net;

sealed class SettingsPage : TabPage
{
    readonly DataGridView _grid = new DataGridView
    {
        Dock = DockStyle.Fill,
        EditMode = DataGridViewEditMode.EditOnEnter,
        RowHeadersVisible = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = false,
        AutoGenerateColumns = false,
    };
    readonly Label _description = new Label { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(3, 6, 3, 0) };
    readonly Setting[] _settings;
    Ini _ini;

    public SettingsPage(string title, IEnumerable<Setting> settings) : base(title)
    {
        _settings = settings.ToArray();
        Padding = new Padding(8);
        _grid.AccessibleName = title + " settings";
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Setting", ReadOnly = true, FillWeight = 60, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", FillWeight = 40, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grid.SelectionChanged += (_, __) =>
        {
            var selected = _grid.CurrentRow?.Tag as Setting;
            _description.Text = selected == null ? "" : selected.Description;
        };
        Controls.Add(_grid);
        Controls.Add(_description);
    }

    public void LoadSettings(Ini ini)
    {
        _ini = ini;
        _grid.Rows.Clear();
        foreach (var setting in _settings)
        {
            string value = ini.Get(setting.Section, setting.Key, setting.Default);
            var row = _grid.Rows[_grid.Rows.Add(setting.Label, value)];
            row.Tag = setting;
            row.Cells[0].ToolTipText = "[" + setting.Section + "] " + setting.Key;
            string[] choices = setting.Choices();
            if (choices.Length == 0) continue;
            var cell = new DataGridViewComboBoxCell { FlatStyle = FlatStyle.Flat };
            cell.Items.AddRange(choices);
            if (!cell.Items.Contains(value)) cell.Items.Add(value);
            row.Cells[1] = cell;
            cell.Value = value;
        }
        _description.Text = _grid.CurrentRow?.Tag is Setting first ? first.Description : "";
    }

    public void ExportChanges(IDictionary<string, IDictionary<string, string>> sections)
    {
        if (!_grid.EndEdit()) throw new ArgumentException("Finish editing the " + Text + " settings first.");
        foreach (DataGridViewRow row in _grid.Rows)
        {
            var setting = (Setting)row.Tag;
            string value = Convert.ToString(row.Cells[1].Value).Trim();
            if (value == _ini.Get(setting.Section, setting.Key, setting.Default)) continue;
            if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new ArgumentException(setting.Label + " must fit on one line.");
            if (!sections.TryGetValue(setting.Section, out var section))
                sections[setting.Section] = section = new Dictionary<string, string>();
            section[setting.Key] = value;
        }
    }
}
