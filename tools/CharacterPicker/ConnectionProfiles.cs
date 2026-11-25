using System;
using System.Collections.Generic;
using RDR2Coop.Net;

sealed class ConnectionProfiles
{
    public const string DefaultPort = "27460";
    public bool Hosting, Public, UPnP, GuestPreferHost, HostPreferHost;
    public string Name, GuestServer, GuestPort, GuestPassword, HostPort, HostPassword, RoomName;

    public static ConnectionProfiles Load(Ini ini)
    {
        bool host = Yes(ini.Get("Coop", "RunServer", "No"));
        string guestSection = host ? "GuestConnection" : "Coop";
        return new ConnectionProfiles
        {
            Hosting = host,
            Name = ini.Get("Coop", "Name", Environment.UserName),
            GuestServer = ini.Get(guestSection, "Server", ""),
            GuestPort = ini.Get(guestSection, "Port", DefaultPort),
            GuestPassword = ini.Get(guestSection, "Password", ""),
            GuestPreferHost = Yes(ini.Get(guestSection, "PreferHost", "No")),
            HostPort = ini.Get("Coop", host ? "Port" : "HostPort", DefaultPort),
            HostPassword = ini.Get("Coop", host ? "Password" : "HostPassword", ""),
            HostPreferHost = host ? Yes(ini.Get("Coop", "PreferHost", "No")) : Yes(ini.Get("HostConnection", "PreferHost", "Yes")),
            RoomName = ini.Get("Coop", "RoomName", ""),
            Public = Yes(ini.Get("Coop", "Public", "No")),
            UPnP = !ini.Get("Coop", "UPnP", "Yes").StartsWith("n", StringComparison.OrdinalIgnoreCase),
        };
    }

    public IDictionary<string, IDictionary<string, string>> ToSections() =>
        new Dictionary<string, IDictionary<string, string>>
        {
            ["Coop"] = new Dictionary<string, string>
            {
                ["Name"] = Name,
                ["RunServer"] = Flag(Hosting),
                ["Server"] = Hosting ? "127.0.0.1" : GuestServer,
                ["Port"] = Hosting ? HostPort : GuestPort,
                ["Password"] = Hosting ? HostPassword : GuestPassword,
                ["PreferHost"] = Flag(Hosting ? HostPreferHost : GuestPreferHost),
                ["HostPort"] = HostPort,
                ["HostPassword"] = HostPassword,
                ["RoomName"] = RoomName,
                ["Public"] = Flag(Public),
                ["UPnP"] = Flag(UPnP),
            },
            ["GuestConnection"] = new Dictionary<string, string>
            {
                ["Server"] = GuestServer,
                ["Port"] = GuestPort,
                ["Password"] = GuestPassword,
                ["PreferHost"] = Flag(GuestPreferHost),
            },
            ["HostConnection"] = new Dictionary<string, string> { ["PreferHost"] = Flag(HostPreferHost) },
        };

    static bool Yes(string value) => value.StartsWith("y", StringComparison.OrdinalIgnoreCase);
    static string Flag(bool value) => value ? "Yes" : "No";
}
