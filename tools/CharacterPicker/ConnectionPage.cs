using System;
using System.Drawing;
using System.Windows.Forms;
using RDR2Coop.Net;

sealed class ConnectionPage : TabPage
{
    readonly ComboBox _mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, AccessibleName = "Connection mode" };
    readonly TextBox _name = new TextBox();
    readonly TextBox _guestServer = new TextBox(), _guestPort = new TextBox(), _guestPassword = new TextBox { UseSystemPasswordChar = true };
    readonly TextBox _hostRoom = new TextBox(), _hostPort = new TextBox(), _hostPassword = new TextBox { UseSystemPasswordChar = true };
    readonly CheckBox _guestPreferHost = new CheckBox { Text = "Prefer my story in this room", AutoSize = true };
    readonly CheckBox _hostPreferHost = new CheckBox { Text = "Prefer my story in this room", AutoSize = true };
    readonly CheckBox _publicRoom = new CheckBox { Text = "List my room online", AutoSize = true };
    readonly CheckBox _upnp = new CheckBox { Text = "Ask the router to forward the port (UPnP)", AutoSize = true };
    readonly GroupBox _guestGroup, _hostGroup;

    public bool Hosting => _mode.SelectedIndex == 1;
    public event EventHandler RoleChanged;

    public ConnectionPage() : base("Connection")
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };
        var fields = CreateLayout();
        _mode.Items.AddRange(new object[] { "Guest — join a room", "Host — start a room" });
        AddRow(fields, "Mode", _mode);
        AddRow(fields, "Your player name", _name);

        var guest = CreateLayout();
        AddRow(guest, "Server address or room name", _guestServer);
        AddRow(guest, "Server port", _guestPort);
        AddRow(guest, "Room password", _guestPassword);
        AddRow(guest, "", _guestPreferHost);
        AddNote(guest, "Leave the address blank to join from the in-game co-op menu.");
        _guestGroup = Group("Guest connection", guest);

        var host = CreateLayout();
        AddRow(host, "Room name", _hostRoom);
        AddRow(host, "Host port", _hostPort);
        AddRow(host, "Room password", _hostPassword);
        AddRow(host, "", _hostPreferHost);
        AddRow(host, "", _publicRoom);
        AddRow(host, "", _upnp);
        AddNote(host, "A public room shares your address in the online list. Players still need the password to join.");
        _hostGroup = Group("Host connection", host);

        AddWide(fields, _guestGroup);
        AddWide(fields, _hostGroup);
        AddNote(fields, "Save settings, then restart RDR2 to use the selected mode.");
        scroll.Controls.Add(fields);
        Controls.Add(scroll);

        _mode.SelectedIndexChanged += (_, __) =>
        {
            _guestGroup.Visible = !Hosting;
            _hostGroup.Visible = Hosting;
            RoleChanged?.Invoke(this, EventArgs.Empty);
        };
        _mode.SelectedIndex = 0;
    }

    public void LoadSettings(Ini ini)
    {
        var profile = ConnectionProfiles.Load(ini);
        _mode.SelectedIndex = profile.Hosting ? 1 : 0;
        _name.Text = profile.Name;
        _guestServer.Text = profile.GuestServer;
        _guestPort.Text = profile.GuestPort;
        _guestPassword.Text = profile.GuestPassword;
        _guestPreferHost.Checked = profile.GuestPreferHost;
        _hostRoom.Text = profile.RoomName;
        _hostPort.Text = profile.HostPort;
        _hostPassword.Text = profile.HostPassword;
        _hostPreferHost.Checked = profile.HostPreferHost;
        _publicRoom.Checked = profile.Public;
        _upnp.Checked = profile.UPnP;
    }

    public ConnectionProfiles Read() =>
        new ConnectionProfiles
        {
            Hosting = Hosting,
            Name = _name.Text.Trim(),
            GuestServer = _guestServer.Text.Trim(),
            GuestPort = Port(_guestPort.Text, "Guest server port"),
            GuestPassword = _guestPassword.Text.Trim(),
            GuestPreferHost = _guestPreferHost.Checked,
            RoomName = _hostRoom.Text.Trim(),
            HostPort = Port(_hostPort.Text, "Host port"),
            HostPassword = Hosting && string.IsNullOrWhiteSpace(_hostPassword.Text)
                ? throw new ArgumentException("Enter a room password to host.")
                : _hostPassword.Text.Trim(),
            HostPreferHost = _hostPreferHost.Checked,
            Public = _publicRoom.Checked,
            UPnP = _upnp.Checked,
        };

    static string Port(string text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) return ConnectionProfiles.DefaultPort;
        if (!int.TryParse(text, out int port) || port < 1 || port > 65535)
            throw new ArgumentException(name + " must be between 1 and 65535.");
        return port.ToString();
    }

    static GroupBox Group(string title, Control contents)
    {
        var group = new GroupBox { Text = title, AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(9), Margin = new Padding(3, 10, 3, 0) };
        group.Controls.Add(contents);
        return group;
    }

    internal static TableLayoutPanel CreateLayout()
    {
        var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    internal static void AddRow(TableLayoutPanel layout, string title, Control field)
    {
        int row = layout.RowCount++;
        if (title.Length > 0)
        {
            layout.Controls.Add(new Label { Text = title, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            field.AccessibleName = title;
        }
        field.Anchor = field is TextBox ? AnchorStyles.Left | AnchorStyles.Right : AnchorStyles.Left;
        layout.Controls.Add(field, 1, row);
    }

    internal static void AddWide(TableLayoutPanel layout, Control control)
    {
        int row = layout.RowCount++;
        layout.Controls.Add(control, 0, row);
        layout.SetColumnSpan(control, 2);
    }

    internal static void AddNote(TableLayoutPanel layout, string text) =>
        AddWide(layout, new Label { Text = text, AutoSize = true, MaximumSize = new Size(580, 0), Margin = new Padding(3, 9, 3, 3) });
}
