using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WiiBalanceWalker
{
    // Streams the raw load sensors to browser games (BoardSkate) over a localhost WebSocket, at the board's own
    // report rate rather than the 20 Hz UI timer. One JSON text frame per report:
    //   {"w":kg,"tl":kg,"tr":kg,"bl":kg,"br":kg,"a":0|1}
    // Nothing is listening unless a page connects, and a client that stalls is dropped so the Wiimote thread never waits.
    class GameFeed
    {
        public const int Port = 26791;

        readonly List<Socket> clients = new List<Socket>();
        TcpListener listener;

        public void Start()
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, Port);
                listener.Start();
                var t = new Thread(AcceptLoop) { IsBackground = true, Name = "GameFeed" };
                t.Start();
            }
            catch (SocketException ex) { BalanceWalker.LogError("GameFeed start", ex); }   // port taken: just no feed
        }

        void AcceptLoop()
        {
            while (true)
            {
                Socket s;
                try { s = listener.AcceptSocket(); }
                catch { return; }
                ThreadPool.QueueUserWorkItem(_ => Handshake(s));
            }
        }

        void Handshake(Socket s)
        {
            try
            {
                s.ReceiveTimeout = 3000;
                s.SendTimeout    = 200;
                s.NoDelay        = true;

                var buf = new byte[4096];
                var len = 0;
                while (len < buf.Length)
                {
                    var n = s.Receive(buf, len, buf.Length - len, SocketFlags.None);
                    if (n <= 0) { s.Close(); return; }
                    len += n;
                    if (Encoding.ASCII.GetString(buf, 0, len).Contains("\r\n\r\n")) break;
                }

                string key = null;
                foreach (var line in Encoding.ASCII.GetString(buf, 0, len).Split(new[] { "\r\n" }, StringSplitOptions.None))
                    if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                        key = line.Substring(18).Trim();

                if (key == null)
                {
                    // Plain HTTP visit: say what this is instead of hanging.
                    var body = "WiiBalanceWalker game feed (WebSocket). Open BoardSkate to use it.";
                    s.Send(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n" + body));
                    s.Close();
                    return;
                }

                string accept;
                using (var sha = SHA1.Create())
                    accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                s.Send(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n"));
                lock (clients) clients.Add(s);
            }
            catch { try { s.Close(); } catch { } }
        }

        public bool HasClients { get { lock (clients) return clients.Count > 0; } }

        public void Send(float w, float tl, float tr, float bl, float br, bool button)
        {
            if (!HasClients) return;

            var json = string.Format(CultureInfo.InvariantCulture, "{{\"w\":{0:0.00},\"tl\":{1:0.00},\"tr\":{2:0.00},\"bl\":{3:0.00},\"br\":{4:0.00},\"a\":{5}}}",
                                     w, tl, tr, bl, br, button ? 1 : 0);
            var payload = Encoding.ASCII.GetBytes(json);
            var frame = new byte[payload.Length + 2];       // payloads stay well under 126 bytes
            frame[0] = 0x81;                                // FIN + text
            frame[1] = (byte)payload.Length;
            Buffer.BlockCopy(payload, 0, frame, 2, payload.Length);

            lock (clients)
            {
                for (int i = clients.Count - 1; i >= 0; i--)
                {
                    var c = clients[i];
                    try
                    {
                        // Anything the page sends (only a close frame) means it's going away.
                        if (c.Available > 0 || (c.Poll(0, SelectMode.SelectRead) && c.Available == 0)) throw new SocketException();
                        c.Send(frame);
                    }
                    catch
                    {
                        try { c.Close(); } catch { }
                        clients.RemoveAt(i);
                    }
                }
            }
        }
    }
}
