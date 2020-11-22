using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using Impostor.Hazel;
using Impostor.Api.Net.Messages;
using Impostor.Hazel.Udp;
using Impostor.Hazel.Extensions;
using System.Diagnostics;

namespace AmongUsProxy
{
    internal static class Program
    {
        // Also this probably needs to be read from a config?
        private const string DeviceName = "Realtek";

        private static IServiceProvider _serviceProvider;
        private static ObjectPool<MessageReader> _readerPool;

        private static void Main()
        {
            var services = new ServiceCollection();
            services.AddHazel();

            _serviceProvider = services.BuildServiceProvider();
            _readerPool = _serviceProvider.GetRequiredService<ObjectPool<MessageReader>>();

            var devices = LivePacketDevice.AllLocalMachine;
            if (devices.Count == 0)
            {
                Console.WriteLine("No interfaces found! Make sure WinPcap/Npcap is installed.");
                return;
            }

            var device = devices.FirstOrDefault(x => x.Description.Contains(DeviceName));
            if (device == null)
            {
                Console.WriteLine("Unable to find configured device.");
                return;
            }

            using (var communicator = device.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 1000))
            {
                // Best we can do?
                //FIXME: sometimes we use another port
                //InnerNetClient_SetEndpoint sets port (mostly 22023)
                //in ServerManager_TrackServerFailure a other port
                using (var filter = communicator.CreateFilter("udp and port 22023"))
                {
                    communicator.SetFilter(filter);
                }

                communicator.ReceivePackets(0, PacketHandler);
            }
        }

        private static void PacketHandler(Packet packet)
        {
            var ip = packet.Ethernet.IpV4;
            var ipSrc = ip.Source.ToString();
            var udp = ip.Udp;

            using (var stream = udp.Payload.ToMemoryStream())
            {
                using var reader = _readerPool.Get();

                reader.Update(stream.ToArray());

                var option = reader.Buffer[0];
                if (option == (byte)MessageType.Reliable)
                {
                    reader.Seek(reader.Position + 3);
                }
                else if (option == (byte)UdpSendOption.Acknowledgement ||
                         option == (byte)UdpSendOption.Ping ||
                         option == (byte)UdpSendOption.Hello ||
                         option == (byte)UdpSendOption.Disconnect)
                {
                    return;
                }
                else
                {
                    reader.Seek(reader.Position + 1);
                }

                // This is kinda shite
                var isSent = ipSrc.StartsWith("192.");

                while (true)
                {
                    if (reader.Position >= reader.Length)
                    {
                        break;
                    }

                    using var message = reader.ReadMessage();
                    if (isSent)
                    {
                        Handler.HandleToServer(ipSrc, message);
                    }
                    else
                    {
                        Handler.HandleToClient(ipSrc, message);
                    }

                    if (message.Position < message.Length)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Debug.WriteLine("- Did not consume all bytes.");
                    }
                }
            }
        }
    }
}
