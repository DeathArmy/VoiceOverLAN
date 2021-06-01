using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace VoiceOverLAN
{
    public class VoIPListener
    {
        public (string ip, string code) UdpResponse { get; set; }
        private int port = 11001;
        public VoIPListener()
        {
        }

        public void SendCode(string code, string ip)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var ipAddress = IPAddress.Parse(ip);

            var sendbuf = Encoding.UTF8.GetBytes($"{code}");
            var ipEndPoint = new IPEndPoint(ipAddress, port);

            socket.SendTo(sendbuf, ipEndPoint);
        }
    }
}
