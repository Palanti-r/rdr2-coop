// RunServer=Yes: this game runs the co-op server (Net/Relay.cs) and, with
// UPnP=Yes, asks the router to open its port so friends can join over the
// internet; with Public=Yes it also lists the room in the online server
// browser. Router and list talk happen on a background thread; the game picks
// up what to tell the player with TakeNote() from its tick.
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RDR2Coop.Net;

namespace RDR2Coop.Mod
{
    sealed class BuiltInServer : IDisposable
    {
        const int RenewMs = 30 * 60 * 1000, AnnounceMs = 60 * 1000;

        readonly RelayHost _host;
        readonly int _port;
        readonly string _roomName, _listServer;   // _listServer null: not listed online
        readonly Action<string> _networkLog;   // the sink is also used by the relay and background worker
        readonly ManualResetEvent _stop = new ManualResetEvent(false);
        volatile Upnp _upnp;
        volatile string _note;

        public string Status { get; private set; }
        public string ListedAs { get; private set; }   // our address in the online list, once listed
        public int Port => _port;

        BuiltInServer(RelayHost host, int port, bool upnp, string roomName, string listServer, Action<string> networkLog)
        {
            _host = host;
            _port = port;
            _roomName = roomName;
            _listServer = listServer;
            _networkLog = networkLog;
            Status = $"server '{roomName}' on UDP {port}";
            if (!upnp) Note($"Co-op server running (UDP {port}). UPnP is off: forward UDP {port} to this PC for friends over the internet.");
            if (upnp || listServer != null) new Thread(() => Background(upnp)) { IsBackground = true, Name = "RDR2Coop.ServerNet" }.Start();
        }

        // null (and a note saying why) when it can't run.
        public static BuiltInServer Start(int port, string password, bool upnp, string roomName, string listServer, out string why,
            Action<string> networkLog = null)
        {
            why = null;
            Action<string> trace = networkLog == null ? null : new Action<string>(line =>
            {
                try { networkLog(line); }
                catch (Exception) { }   // Diagnostics cannot take down the listener or the hosting worker.
            });
            if (string.IsNullOrEmpty(password))
            {
                trace?.Invoke("component=host event=bind_skipped reason=missing_password");
                why = "Co-op: set a Password in RDR2Coop.ini to run the server";
                return null;
            }
            trace?.Invoke($"component=host event=bind_start port={port} upnp={(upnp ? "on" : "off")} listing={(listServer != null ? "on" : "off")}");
            try
            {
                var host = new RelayHost(port, password, l => CoopScript.Log("server: " + l), roomName, trace);
                trace?.Invoke($"component=host event=bind_success port={host.Port}");
                return new BuiltInServer(host, port, upnp, roomName, listServer, trace);
            }
            catch (SocketException e)
            {
                trace?.Invoke($"component=host event=bind_failed port={port} " + NetworkLog.ErrorFields(e));
                why = $"Co-op: can't run the server, UDP {port} is in use ({e.SocketErrorCode}); joining whatever runs there";
                return null;
            }
            catch (Exception e)
            {
                trace?.Invoke($"component=host event=bind_failed port={port} " + NetworkLog.ErrorFields(e));
                throw;
            }
        }

        public string TakeNote() => Interlocked.Exchange(ref _note, null);

        void Note(string text)
        {
            CoopScript.Log(text);
            _note = text;
        }

        void Background(bool upnp)
        {
            try
            {
                if (upnp) OpenPort();
                long nextRenew = Environment.TickCount + RenewMs;
                string lastError = "";
                bool? announced = null;
                do
                {
                    if (_listServer != null && !(_upnp != null && Shared(_upnp.PublicIp)))   // a shared (CGNAT) address can't be joined: not listed (OpenPort said why)
                    {
                        string err = RoomList.Announce(_listServer, _roomName, _port, _host.PlayerCount, 4, _upnp?.PublicIp, out string listedAs);
                        if (listedAs != null) ListedAs = listedAs;
                        if (err != null && err != lastError) Note("Co-op: your room couldn't be listed online: " + err);
                        else if (err == null && lastError != null) CoopScript.Log("listed online as '" + _roomName + "'");
                        bool success = err == null;
                        if (announced != success)
                            _networkLog?.Invoke(success ? "component=host event=announce_success" : "component=host event=announce_failed");
                        announced = success;
                        lastError = err;
                    }
                    if (_upnp != null && Environment.TickCount - nextRenew >= 0)
                    {
                        string err = _upnp.Open(_port);
                        _networkLog?.Invoke(err == null ? "component=host event=upnp_renew_success" : "component=host event=upnp_renew_failed");
                        nextRenew = Environment.TickCount + RenewMs;
                    }
                }
                while (!_stop.WaitOne(_listServer != null ? AnnounceMs : RenewMs));
            }
            catch (Exception e)
            {
                CoopScript.Log("server network: " + e.Message);
                _networkLog?.Invoke("component=host event=worker_failed " + NetworkLog.ErrorFields(e));
            }
        }

        void OpenPort()
        {
            _networkLog?.Invoke("component=host event=upnp_discovery_start");
            var u = Upnp.Find();
            CoopScript.Log(Upnp.LastSearch);
            if (u == null)
            {
                Status += ", no UPnP router";
                _networkLog?.Invoke("component=host event=upnp_discovery_failed reason=no_router");
                Note($"Co-op server running. No UPnP router answered: forward UDP {_port} to this PC for friends over the internet (same network: they use this PC's address).");
                return;
            }
            string err = u.Open(_port);
            if (err != null)
            {
                Status += ", UPnP refused";
                _networkLog?.Invoke("component=host event=upnp_mapping_failed");
                Note($"Co-op server running. The router refused to open UDP {_port} ({err}): forward it to {u.LocalIp} by hand.");
                return;
            }
            _upnp = u;
            if (Shared(u.PublicIp))
            {
                Status += $", port open but {u.PublicIp} is shared";
                _networkLog?.Invoke("component=host event=upnp_mapping_success address_scope=shared_or_private");
                if (_listServer != null) _networkLog?.Invoke("component=host event=announce_skipped reason=shared_or_private");
                Note($"Co-op server running, but your internet address ({u.PublicIp}) is shared by your provider (CGNAT), so friends can't reach it and it isn't listed online. Start a free dedicated server in the Discord's #find-a-partner, or use Tailscale or ZeroTier.");
            }
            else
            {
                Status += $", friends join {u.PublicIp}:{_port}";
                _networkLog?.Invoke(IPAddress.TryParse(u.PublicIp, out var ip) && ip.AddressFamily == AddressFamily.InterNetwork
                    ? "component=host event=upnp_mapping_success address_scope=external"
                    : "component=host event=upnp_mapping_success address_scope=unknown");
                Note($"Co-op server open to the internet: friends join {u.PublicIp} port {_port}");
            }
        }

        static bool Shared(string ip)
        {
            if (!IPAddress.TryParse(ip ?? "", out var a) || a.AddressFamily != AddressFamily.InterNetwork) return false;
            var b = a.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] < 32) || (b[0] == 192 && b[1] == 168) || (b[0] == 100 && b[1] >= 64 && b[1] < 128);
        }

        public void Dispose()
        {
            _stop.Set();
            _host.Dispose();
            _networkLog?.Invoke("component=host event=listener_stopped");
            try { _upnp?.Close(_port); } catch (Exception) { }
            if (_listServer != null) RoomList.Remove(_listServer, _port);
        }
    }
}
