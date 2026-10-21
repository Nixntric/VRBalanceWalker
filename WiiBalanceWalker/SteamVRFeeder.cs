using System;
using System.Net.Sockets;

namespace WiiBalanceWalker
{
    // Sends the board's lean, twist and button to the "wbw" SteamVR driver (steamvr_driver\src\driver_wbw.cpp)
    // over localhost UDP. The driver zeroes its inputs if packets stop, so nothing needs sending on shutdown.
    class SteamVRFeeder
    {
        public const int Port = 26790;

        UdpClient udp    = new UdpClient();
        byte[]    packet = new byte[17] { (byte)'W', (byte)'B', (byte)'W', (byte)'1', 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public void Send(double x, double y, double turn, bool button)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(Clamp(x)),    0, packet, 4,  4);
            Buffer.BlockCopy(BitConverter.GetBytes(Clamp(y)),    0, packet, 8,  4);
            Buffer.BlockCopy(BitConverter.GetBytes(Clamp(turn)), 0, packet, 12, 4);
            packet[16] = (byte)(button ? 1 : 0);

            try { udp.Send(packet, packet.Length, "127.0.0.1", Port); }
            catch (SocketException) { }     // nothing listening yet (SteamVR not running) is fine
        }

        static float Clamp(double v)
        {
            if (double.IsNaN(v)) return 0f;
            return (float)Math.Max(-1.0, Math.Min(1.0, v));
        }
    }
}
