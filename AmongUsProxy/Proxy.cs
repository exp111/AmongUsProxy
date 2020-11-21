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
                using (var filter = communicator.CreateFilter("udp and port 22023"))
                {
                    communicator.SetFilter(filter);
                }

                communicator.ReceivePackets(0, PacketHandler);
            }
            Console.WriteLine("Listening...");
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
                        HandleToServer(ipSrc, message);
                    }
                    else
                    {
                        HandleToClient(ipSrc, message);
                    }

                    if (message.Position < message.Length)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("- Did not consume all bytes.");
                    }
                }
            }
        }

        private static void HandleToClient(string source, IMessageReader packet)
        {
            var tagName = Enum.GetName(typeof(MessageFlags), packet.Tag) ?? "Unknown";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{source,-15} To Client: {packet.Tag,-2} {tagName}");

            switch (packet.Tag)
            {
                case MessageFlags.Redirect:
                case MessageFlags.ReselectServer:
                    // packet.Position = packet.Length;
                    break;
                case MessageFlags.HostGame:
                    Console.WriteLine("- GameCode        " + packet.ReadInt32());
                    break;
                case MessageFlags.GameData:
                case MessageFlags.GameDataTo:
                    Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToArray().Take(packet.Length).ToArray()));
                    // packet.Position = packet.Length;
                    break;
                case MessageFlags.JoinedGame:
                    Console.WriteLine("- GameCode        " + packet.ReadInt32());
                    Console.WriteLine("- PlayerId        " + packet.ReadInt32());
                    Console.WriteLine("- Host            " + packet.ReadInt32());
                    var playerCount = packet.ReadPackedInt32();
                    Console.WriteLine("- PlayerCount     " + playerCount);
                    for (var i = 0; i < playerCount; i++)
                    {
                        Console.WriteLine("-     PlayerId    " + packet.ReadPackedInt32());
                    }
                    break;
                case MessageFlags.AlterGame:
                    Console.WriteLine("- GameCode        " + packet.ReadInt32());
                    Console.WriteLine("- Flag            " + packet.ReadSByte());
                    Console.WriteLine("- Value           " + packet.ReadBoolean());
                    break;
            }
        }

        private static void HandleToServer(string source, IMessageReader packet)
        {
            var tagName = Enum.GetName(typeof(MessageFlags), packet.Tag) ?? "Unknown";
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"{source,-15} To Server: {packet.Tag,-2} {tagName}");

            switch (packet.Tag)
            {
                case MessageFlags.HostGame:
                    Console.WriteLine("- GameInfo length " + packet.ReadBytesAndSize().Length);
                    break;
                case MessageFlags.JoinGame:
                    Console.WriteLine("- GameCode        " + packet.ReadInt32());
                    Console.WriteLine("- Unknown         " + packet.ReadByte());
                    break;
                case MessageFlags.GameData:
                case MessageFlags.GameDataTo:
                    Console.WriteLine("- GameCode        " + packet.ReadInt32());
                    Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToArray().Take(packet.Length).ToArray()));
                    // packet.Position = packet.Length;
                    break;
            }
        }
    }
}
