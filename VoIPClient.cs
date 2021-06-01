using System;
using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace VoiceOverLAN
{
    public class VoIPClient
    {
        private int port = 11000;
        private BufferedWaveProvider BufferedWaveProvider { get; set; }
        private WaveInEvent WaveIn { get; set; }
        private WaveOutEvent WaveOut { get; set; }

        public VoIPClient()
        {
        }

        public void ListenVOIP()
        {
            var udpClient = new UdpClient(port);
            var ipEndPoint = new IPEndPoint(IPAddress.Any, port);
            
            BufferedWaveProvider = new BufferedWaveProvider(new WaveFormat(8000, 1));

            WaveOut = new WaveOutEvent();

            WaveOut.Init(BufferedWaveProvider);
            WaveOut.Play();

            try
            {
                while (true)
                {
                    var bytes = udpClient.Receive(ref ipEndPoint);
                    BufferedWaveProvider.AddSamples(bytes, 0, bytes.Length);
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                udpClient.Close();
            }
        }
        public void SendVOIP(string recipientIp)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var ipAddress = IPAddress.Parse(recipientIp);
            var ipEndPoint = new IPEndPoint(ipAddress, port);

            WaveIn = new WaveInEvent();

            WaveIn.DataAvailable += (o, arg) =>
            {
                socket.SendTo(arg.Buffer, ipEndPoint);
            };

            WaveIn.StartRecording();
        }

        public void ChangeVolume(float value)
        {
            WaveOut.Volume = value;
        }
    }
}
