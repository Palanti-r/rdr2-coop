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
