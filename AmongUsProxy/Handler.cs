using Impostor.Api.Net.Messages;
using Impostor.Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmongUsProxy
{
	public enum GameDataTag : byte
	{
		DataFlag = 1,
		RPCFlag = 2,
		Default = 3,
		F4 = 4,
		F5 = 5,
		F6 = 6,
		F7 = 7,
	}

	public enum RPCCallID : uint
	{
		CompleteTask = 0x1,
		SetInfected = 0x3,
		SetName = 0x6,
		SetSkin = 0xa,
		ReportBody = 0xb,
		Chat = 0xd,
		//SetScanner = 0xf,
		MurderPlayer = 0xf,
		SetPet = 0x11,
		CheckForEndVoting = 0x17,
	}

	class Handler
	{
		public static void HandleGameData(bool toPlayer, IMessageReader packet)
		{
			Console.WriteLine($" - Game Code        {packet.ReadInt32()}");
			if (toPlayer)
			{
				Console.WriteLine($" - Target       {packet.ReadPackedInt32()}");
			}

			//Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToArray()));
			while (packet.Position < packet.Length)
			{
				using var reader = packet.ReadMessage();
				Console.WriteLine($" - Tag          {reader.Tag}");
				switch ((GameDataTag)reader.Tag)
				{
					case GameDataTag.RPCFlag:
						var netID = reader.ReadPackedUInt32();
						HandleRPC(netID, reader);
						break;
					default:
						Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
							GetRange(reader.Offset, reader.Length).ToArray()));
						break;
				}
			}
		}

		public static void HandleRPC(uint netID, IMessageReader packet)
		{
			//TODO: get name from netid, default to netid if not yet seen
			Console.WriteLine($" - NetID        {netID}");
			var callID = packet.ReadByte();
			Console.WriteLine($" - CallID       {callID}");
			switch ((RPCCallID)callID)
			{
				case RPCCallID.Chat:
					Console.WriteLine($"Message from {netID}: {packet.ReadString()}");
					break;
				case RPCCallID.MurderPlayer:
					//TODO: read args
					Console.WriteLine($"{netID} killed someone");
					break;
				default:
					Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
							GetRange(packet.Offset, packet.Length).ToArray()));
					break;
			}
		}
	}
}
