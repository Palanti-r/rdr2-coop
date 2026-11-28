using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security;

namespace RDR2Coop.Net
{
    // Local connection diagnostics. Callers supply status codes and counters, never packet data or addresses.
    sealed class NetworkLog
    {
        public const string FileName = "RDR2Coop.Network.log";
        internal const int MaxBytes = 256 * 1024;
        static readonly object Gate = new object();
        readonly string _directory, _path;
        readonly string _session = Guid.NewGuid().ToString("N").Substring(0, 8);

        public NetworkLog(string directory)
        {
            _directory = directory;
            _path = Path.Combine(directory, FileName);
        }

        public void Write(string fields)
        {
            string line = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " session=" + _session + " " + fields;
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(_directory);
                    var file = new FileInfo(_path);
                    if (file.Exists && file.Length >= MaxBytes)
                    {
                        string previous = _path + ".previous";
                        File.Delete(previous);
                        File.Move(_path, previous);
                    }
                    File.AppendAllText(_path, line + Environment.NewLine);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (SecurityException) { }
        }

        public string[] Recent()
        {
            lock (Gate)
            {
                try { return File.Exists(_path) ? File.ReadAllLines(_path) : new string[0]; }
                catch (IOException) { return new string[0]; }
                catch (UnauthorizedAccessException) { return new string[0]; }
            }
        }

        public string Save(string status)
        {
            lock (Gate)
            {
                string root = Path.GetDirectoryName(_directory);
                string name = "RDR2Coop.Debug_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".log";
                string previous = _path + ".previous";
                using (var writer = new StreamWriter(Path.Combine(root, name)))
                {
                    writer.WriteLine(status);
                    if (File.Exists(previous)) writer.Write(File.ReadAllText(previous));
                    if (File.Exists(_path)) writer.Write(File.ReadAllText(_path));
                }
                return name;
            }
        }

        public static string ErrorFields(Exception error)
        {
            if (error is SocketException socket) return "socket_error=" + socket.SocketErrorCode;
            if (error is WebException web)
            {
                string status = "web_error=" + web.Status;
                return web.Response is HttpWebResponse response && web.Status == WebExceptionStatus.ProtocolError
                    ? status + " http_status=" + (int)response.StatusCode
                    : status;
            }
            return "error_type=" + error.GetType().Name;
        }
    }
}
