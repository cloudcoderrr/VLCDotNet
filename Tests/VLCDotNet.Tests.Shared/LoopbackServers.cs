using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VLCDotNet.Tests.Shared
{
    // Ephemeral, loopback-only (127.0.0.1) media servers used to exercise
    // libvlc's native network transport (http / ftp access modules) against a
    // real server with no external dependency. They serve files straight from
    // the local test-media directory and bind to an OS-assigned port, so many
    // instances can run in parallel across the test matrix without collisions.
    //
    // SECURITY: both bind to IPAddress.Loopback only and are read-only; they
    // exist purely for in-process test traffic and must never be exposed.

    /// <summary>
    /// Minimal read-only HTTP/1.1 server. Supports HEAD, GET and single
    /// <c>Range: bytes=</c> requests (206 Partial Content) so seek-over-HTTP and
    /// the adaptive/HLS byte-range fetch path can be verified.
    /// </summary>
    public sealed class LoopbackHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _root;
        private readonly Thread _thread;
        private volatile bool _running;

        public LoopbackHttpServer(string root)
        {
            _root = Path.GetFullPath(root);
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "loopback-http" };
            _thread.Start();
        }

        public int Port { get; }

        public string BaseUrl => "http://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + "/";

        public string UrlFor(string fileName) => BaseUrl + Uri.EscapeDataString(fileName);

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; }
                var worker = new Thread(() => { try { Handle(client); } catch { /* best effort */ } })
                {
                    IsBackground = true,
                };
                worker.Start();
            }
        }

        private void Handle(TcpClient client)
        {
            using (client)
            using (NetworkStream net = client.GetStream())
            {
                client.ReceiveTimeout = 15000;
                client.SendTimeout = 15000;

                string? requestLine = ReadRequest(net, out string? rangeHeader);
                if (requestLine == null)
                {
                    return;
                }

                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2)
                {
                    WriteStatus(net, 400, "Bad Request");
                    return;
                }

                string method = parts[0];
                string rawTarget = parts[1];
                string rel = Uri.UnescapeDataString(rawTarget.TrimStart('/'));
                string full = Path.GetFullPath(Path.Combine(_root, rel));

                // Reject path traversal outside the served root.
                if (!full.StartsWith(_root, StringComparison.Ordinal) || !File.Exists(full))
                {
                    WriteStatus(net, 404, "Not Found");
                    return;
                }

                var info = new FileInfo(full);
                long total = info.Length;
                long start = 0;
                long end = total - 1;
                bool partial = false;

                if (rangeHeader != null && TryParseRange(rangeHeader, total, out start, out end))
                {
                    partial = true;
                }

                long length = end - start + 1;
                var head = new StringBuilder();
                head.Append(partial ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n");
                head.Append("Content-Type: ").Append(ContentType(full)).Append("\r\n");
                head.Append("Accept-Ranges: bytes\r\n");
                head.Append("Content-Length: ").Append(length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                if (partial)
                {
                    head.Append("Content-Range: bytes ")
                        .Append(start.ToString(CultureInfo.InvariantCulture)).Append('-')
                        .Append(end.ToString(CultureInfo.InvariantCulture)).Append('/')
                        .Append(total.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
                }
                head.Append("Connection: close\r\n\r\n");

                byte[] headerBytes = Encoding.ASCII.GetBytes(head.ToString());
                net.Write(headerBytes, 0, headerBytes.Length);

                if (method == "HEAD")
                {
                    net.Flush();
                    return;
                }

                using (var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fs.Seek(start, SeekOrigin.Begin);
                    byte[] buffer = new byte[64 * 1024];
                    long remaining = length;
                    while (remaining > 0)
                    {
                        int want = (int)Math.Min(buffer.Length, remaining);
                        int read = fs.Read(buffer, 0, want);
                        if (read <= 0)
                        {
                            break;
                        }
                        net.Write(buffer, 0, read);
                        remaining -= read;
                    }
                    net.Flush();
                }
            }
        }

        private static string? ReadRequest(NetworkStream net, out string? rangeHeader)
        {
            rangeHeader = null;
            var raw = new MemoryStream();
            byte[] one = new byte[1];
            int consecutive = 0;
            // Read until the blank line that terminates the request headers.
            while (raw.Length < 16384)
            {
                int n;
                try { n = net.Read(one, 0, 1); }
                catch { return null; }
                if (n <= 0)
                {
                    break;
                }
                raw.WriteByte(one[0]);
                if (one[0] == (byte)'\n')
                {
                    consecutive++;
                    if (consecutive >= 2)
                    {
                        break;
                    }
                }
                else if (one[0] != (byte)'\r')
                {
                    consecutive = 0;
                }
            }

            string text = Encoding.ASCII.GetString(raw.ToArray());
            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0 || lines[0].Length == 0)
            {
                return null;
            }
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                {
                    rangeHeader = lines[i].Substring("Range:".Length).Trim();
                }
            }
            return lines[0];
        }

        private static bool TryParseRange(string header, long total, out long start, out long end)
        {
            start = 0;
            end = total - 1;
            // "bytes=start-end" | "bytes=start-" | "bytes=-suffix"
            int eq = header.IndexOf('=');
            if (eq < 0)
            {
                return false;
            }
            string spec = header.Substring(eq + 1).Trim();
            int dash = spec.IndexOf('-');
            if (dash < 0)
            {
                return false;
            }
            string a = spec.Substring(0, dash);
            string b = spec.Substring(dash + 1);
            if (a.Length == 0)
            {
                if (!long.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out long suffix))
                {
                    return false;
                }
                start = Math.Max(0, total - suffix);
                end = total - 1;
                return true;
            }
            if (!long.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
            {
                return false;
            }
            if (b.Length > 0 && long.TryParse(b, NumberStyles.Integer, CultureInfo.InvariantCulture, out long e))
            {
                end = Math.Min(e, total - 1);
            }
            return start <= end;
        }

        private static string ContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".mp4": case ".mov": case ".m4v": return "video/mp4";
                case ".ts": return "video/mp2t";
                case ".webm": return "video/webm";
                case ".mkv": return "video/x-matroska";
                case ".avi": return "video/x-msvideo";
                case ".mp3": return "audio/mpeg";
                case ".flac": return "audio/flac";
                case ".ogg": return "audio/ogg";
                case ".wav": return "audio/wav";
                case ".m4a": return "audio/mp4";
                default: return "application/octet-stream";
            }
        }

        private static void WriteStatus(NetworkStream net, int code, string reason)
        {
            string s = "HTTP/1.1 " + code.ToString(CultureInfo.InvariantCulture) + " " + reason
                + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            byte[] b = Encoding.ASCII.GetBytes(s);
            try { net.Write(b, 0, b.Length); } catch { /* ignore */ }
        }

        public void Dispose()
        {
            _running = false;
            try { _listener.Stop(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Minimal read-only FTP server (RFC 959 subset: USER/PASS/SYST/FEAT/PWD/
    /// TYPE/CWD/SIZE/REST/PASV/EPSV/RETR/QUIT) bound to loopback. Enough to drive
    /// libvlc's <c>ftp</c> access module through a real download, including the
    /// REST-based seek the demuxer issues.
    /// </summary>
    public sealed class LoopbackFtpServer : IDisposable
    {
        private readonly TcpListener _control;
        private readonly string _root;
        private readonly Thread _thread;
        private volatile bool _running;

        public LoopbackFtpServer(string root)
        {
            _root = Path.GetFullPath(root);
            _control = new TcpListener(IPAddress.Loopback, 0);
            _control.Start();
            Port = ((IPEndPoint)_control.LocalEndpoint).Port;
            _running = true;
            _thread = new Thread(AcceptLoop) { IsBackground = true, Name = "loopback-ftp" };
            _thread.Start();
        }

        public int Port { get; }

        public string UrlFor(string fileName) =>
            "ftp://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture) + "/" + Uri.EscapeDataString(fileName);

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _control.AcceptTcpClient(); }
                catch { break; }
                var worker = new Thread(() => { try { Session(client); } catch { /* best effort */ } })
                {
                    IsBackground = true,
                };
                worker.Start();
            }
        }

        private void Session(TcpClient client)
        {
            using (client)
            using (NetworkStream ctrl = client.GetStream())
            {
                client.ReceiveTimeout = 20000;
                client.SendTimeout = 20000;
                Send(ctrl, "220 VLCDotNet loopback FTP\r\n");

                TcpListener? dataListener = null;
                long restOffset = 0;

                var reader = new StreamReader(ctrl, Encoding.ASCII, false, 1024, leaveOpen: true);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    int sp = line.IndexOf(' ');
                    string cmd = (sp < 0 ? line : line.Substring(0, sp)).ToUpperInvariant();
                    string arg = sp < 0 ? string.Empty : line.Substring(sp + 1).Trim();

                    switch (cmd)
                    {
                        case "USER": Send(ctrl, "331 need password\r\n"); break;
                        case "PASS": Send(ctrl, "230 logged in\r\n"); break;
                        case "SYST": Send(ctrl, "215 UNIX Type: L8\r\n"); break;
                        case "FEAT": Send(ctrl, "211-Features\r\n REST STREAM\r\n SIZE\r\n211 End\r\n"); break;
                        case "PWD": Send(ctrl, "257 \"/\"\r\n"); break;
                        case "TYPE": Send(ctrl, "200 type set\r\n"); break;
                        case "CWD": Send(ctrl, "250 ok\r\n"); break;
                        case "OPTS": Send(ctrl, "200 ok\r\n"); break;
                        case "NOOP": Send(ctrl, "200 ok\r\n"); break;
                        case "REST":
                            long.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out restOffset);
                            Send(ctrl, "350 restarting at " + restOffset.ToString(CultureInfo.InvariantCulture) + "\r\n");
                            break;
                        case "SIZE":
                        {
                            string? f = Resolve(arg);
                            if (f != null && File.Exists(f))
                            {
                                Send(ctrl, "213 " + new FileInfo(f).Length.ToString(CultureInfo.InvariantCulture) + "\r\n");
                            }
                            else
                            {
                                Send(ctrl, "550 not found\r\n");
                            }
                            break;
                        }
                        case "PASV":
                        {
                            dataListener?.Stop();
                            dataListener = new TcpListener(IPAddress.Loopback, 0);
                            dataListener.Start();
                            int p = ((IPEndPoint)dataListener.LocalEndpoint).Port;
                            Send(ctrl, string.Format(CultureInfo.InvariantCulture,
                                "227 Entering Passive Mode (127,0,0,1,{0},{1})\r\n", p / 256, p % 256));
                            break;
                        }
                        case "EPSV":
                        {
                            dataListener?.Stop();
                            dataListener = new TcpListener(IPAddress.Loopback, 0);
                            dataListener.Start();
                            int p = ((IPEndPoint)dataListener.LocalEndpoint).Port;
                            Send(ctrl, "229 Entering Extended Passive Mode (|||" + p.ToString(CultureInfo.InvariantCulture) + "|)\r\n");
                            break;
                        }
                        case "RETR":
                        {
                            string? f = Resolve(arg);
                            if (f == null || !File.Exists(f) || dataListener == null)
                            {
                                Send(ctrl, "550 cannot open\r\n");
                                restOffset = 0;
                                break;
                            }
                            Send(ctrl, "150 opening data connection\r\n");
                            try
                            {
                                using (TcpClient data = dataListener.AcceptTcpClient())
                                using (NetworkStream dnet = data.GetStream())
                                using (var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    if (restOffset > 0 && restOffset < fs.Length)
                                    {
                                        fs.Seek(restOffset, SeekOrigin.Begin);
                                    }
                                    byte[] buffer = new byte[64 * 1024];
                                    int read;
                                    while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        dnet.Write(buffer, 0, read);
                                    }
                                    dnet.Flush();
                                }
                                Send(ctrl, "226 transfer complete\r\n");
                            }
                            catch
                            {
                                Send(ctrl, "426 transfer aborted\r\n");
                            }
                            finally
                            {
                                restOffset = 0;
                                dataListener.Stop();
                                dataListener = null;
                            }
                            break;
                        }
                        case "ABOR": Send(ctrl, "226 abort ok\r\n"); break;
                        case "QUIT": Send(ctrl, "221 bye\r\n"); return;
                        default: Send(ctrl, "500 unknown\r\n"); break;
                    }
                }

                dataListener?.Stop();
            }
        }

        private string? Resolve(string arg)
        {
            string rel = Uri.UnescapeDataString(arg).TrimStart('/');
            string full = Path.GetFullPath(Path.Combine(_root, rel));
            return full.StartsWith(_root, StringComparison.Ordinal) ? full : null;
        }

        private static void Send(NetworkStream s, string text)
        {
            byte[] b = Encoding.ASCII.GetBytes(text);
            s.Write(b, 0, b.Length);
            s.Flush();
        }

        public void Dispose()
        {
            _running = false;
            try { _control.Stop(); } catch { /* ignore */ }
        }
    }
}
