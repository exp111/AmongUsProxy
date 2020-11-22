using AmongUsProxy.Extensions;
using Impostor.Api.Net.Messages;
using Impostor.Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmongUsProxy
{
	public class PlayerInfo
	{
		public string Name = "";
		public byte Color;
		public uint Hat;
		public uint Pet;
		public uint Skin;
		public bool Disconnected;
		public bool IsImpostor;
		public bool IsDead;

		public PlayerInfo()
		{

		}

		public PlayerInfo(IMessageReader reader)
		{
			Name = reader.ReadString();
			Color = reader.ReadByte();
			Hat = reader.ReadPackedUInt32();
			Pet = reader.ReadPackedUInt32();
			Skin = reader.ReadPackedUInt32();
			var flags = reader.ReadByte();
			Disconnected = (flags & 1) != 0;
			IsImpostor = (flags & 2) != 0;
			IsDead = (flags & 4) != 0;
			var capacity = reader.ReadByte();
			for (var i = 0; i < capacity; i++)
			{
				var id = reader.ReadPackedUInt32();
				//var typeId = reader.ReadByte(); // idk if this is written
				var complete = reader.ReadBoolean();
			}
		}
	}

	public class PlayerControl
	{
		public byte PlayerId;
		public PlayerInfo Info = new PlayerInfo();

		public void SetName(string name)
		{
			Info.Name = name;
		}
	}

	class Handler
	{
		// NetId to PlayerControl
		private static readonly Dictionary<uint, PlayerControl> Players = new Dictionary<uint, PlayerControl>();

		// InnerNetClient_HandleMessage
		public static void HandleToClient(string source, IMessageReader packet)
		{
			var tagName = Enum.GetName(typeof(MessageFlags), packet.Tag) ?? "Unknown";
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine($"{source,-15} To Client: {packet.Tag,-2} {tagName}");

			switch ((MessageFlags)packet.Tag)
			{
				case MessageFlags.Redirect:
				case MessageFlags.ReselectServer:
					// packet.Position = packet.Length;
					break;
				case MessageFlags.HostGame:
					Console.WriteLine($"- GameCode        {packet.ReadInt32()}");
					break;
				case MessageFlags.GameData:
					HandleGameData(false, packet);
					break;
				case MessageFlags.GameDataTo:
					HandleGameData(true, packet);
					// packet.Position = packet.Length;
					break;
				case MessageFlags.JoinedGame:
					Console.WriteLine($"- GameCode        {packet.ReadInt32()}");
					Console.WriteLine($"- ClientId        {packet.ReadInt32()}");
					Console.WriteLine($"- HostId          {packet.ReadInt32()}");
					var playerCount = packet.ReadPackedUInt32();
					Console.WriteLine($"- PlayerCount     {playerCount}");
					for (var i = 0; i < playerCount; i++)
					{
						var playerId = packet.ReadPackedUInt32();
						Console.WriteLine($"-     PlayerId    {playerId}");
						// do we need those PlayerIds?
						/*if (!Clients.ContainsKey(playerId))
						{
							Clients[playerId] = new Client();
						}*/
					}
					break;
				case MessageFlags.AlterGame:
					Console.WriteLine($"- GameCode        {packet.ReadInt32()}");
					Console.WriteLine($"- Flag            {packet.ReadSByte()}");
					Console.WriteLine($"- Value           {packet.ReadBoolean()}");
					break;
			}
		}

		// InnerNetServer_HandleMessage
		public static void HandleToServer(string source, IMessageReader packet)
		{
			var tagName = Enum.GetName(typeof(MessageFlags), packet.Tag) ?? "Unknown";
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"{source,-15} To Server: {packet.Tag,-2} {tagName}");

			switch ((MessageFlags)packet.Tag)
			{
				case MessageFlags.HostGame:
					Console.WriteLine($"- GameInfo Length {packet.ReadBytesAndSize().Length}");
					break;
				case MessageFlags.JoinGame:
					Console.WriteLine($"- GameCode        {packet.ReadInt32()}");
					Console.WriteLine($"- MapPurchase     {packet.ReadPackedUInt32()}");
					break;
				case MessageFlags.GameData:
					HandleGameData(false, packet);
					break;
				case MessageFlags.GameDataTo:
					HandleGameData(true, packet);
					//Console.WriteLine("- GameCode        " + packet.ReadInt32());                    // packet.Position = packet.Length;
					break;
			}
		}

		// InnerNetClient_HandleGameData
		public static void HandleGameData(bool toPlayer, IMessageReader packet)
		{
			Console.WriteLine($" - Game Code        {packet.ReadInt32()}");
			if (toPlayer)
			{
				Console.WriteLine($" - Target       {packet.ReadPackedInt32()}");
			}

			//Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToArray()));
			// InnerNetClient_HandleGameDataInner
			while (packet.Position < packet.Length)
			{
				using var reader = packet.ReadMessage();
				Console.WriteLine($" - Tag          {reader.Tag}");
				switch ((GameDataTag)reader.Tag)
				{
					case GameDataTag.DataFlag:
						//TODO: maybe do smth with Data?
						break;
					case GameDataTag.RPCFlag:
						var netID = reader.ReadPackedUInt32();
						HandleRPC(netID, reader);
						break;
					case GameDataTag.SpawnFlag:
						// add to Players or smth
						var prefabID = reader.ReadPackedUInt32();
						Console.WriteLine($"Prefab {prefabID}");
						var clientID = reader.ReadPackedUInt32();
						Console.WriteLine($"ClientID {clientID}");
						var flags = reader.ReadByte();
						Console.WriteLine($"SpawnFlags {flags}");
						var componentCount = reader.ReadPackedUInt32();
						Console.WriteLine($"Comp Count {componentCount}");
						for (var i = 0; i < componentCount; i++)
						{
							var netId = reader.ReadPackedUInt32();
							Console.WriteLine($"NetID {netId}");
							using var objReader = reader.ReadMessage();
							if (objReader.Length < 0)
								continue;

							// Deserialize
							switch (prefabID)
							{
								case 3: // InnerGameData
									{
										var num = objReader.ReadPackedInt32();
										for (var j = 0; j < num; j++)
										{
											var playerId = objReader.ReadByte();
											var playerInfo = new PlayerInfo(objReader);
											var player = Players.EnsureKey(netId);
											player.Info = playerInfo;
											player.PlayerId = playerId;
											Console.WriteLine($"Player {playerId}: {playerInfo.Name}");
										}
										break;
									}
								case 4: // InnerPlayerControl
									{
										var isNew = reader.ReadBoolean();
										var playerId = reader.ReadByte();
										var player = Players.EnsureKey(netId);
										player.PlayerId = playerId;
										break;
									}
								default:
									Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
										GetRange(objReader.Offset, objReader.Length).ToArray()));
									break;
							}
						}
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
			Console.WriteLine($" - NetID        {GetPlayerName(netID)}");
			var callID = packet.ReadByte();
			Console.WriteLine($" - CallID       {callID}");
			switch ((RPCCallID)callID)
			{
				case RPCCallID.SetName:
					{
						var name = packet.ReadString();
						var player = Players.EnsureKey(netID);
						Console.WriteLine($"{GetPlayerName(netID)} set name to {name}");
						player.SetName(name);
						break;
					}
				case RPCCallID.Chat:
					Console.WriteLine($"Message from {GetPlayerName(netID)}: {packet.ReadString()}");
					break;
				case RPCCallID.MurderPlayer:
					{
						// var target = ReadNetObject<PlayerControl>
						// ReadNetObject:
						// - var netId = packet.ReadPackedUInt32();
						var target = packet.ReadPackedUInt32();
						Console.WriteLine($"{GetPlayerName(netID)} killed {GetPlayerName(target)}");
						Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
								GetRange(packet.Offset, packet.Length).ToArray()));
						break;
					}
				case RPCCallID.EnterVent:
					{
						var id = packet.ReadPackedUInt32();
						Console.WriteLine($"{GetPlayerName(netID)} vented into {id}");
						break;
					}
				case RPCCallID.ExitVent:
					{
						var id = packet.ReadPackedUInt32();
						Console.WriteLine($"{GetPlayerName(netID)} vented out of {id}");
						break;
					}
				case RPCCallID.PlayerInfo:
					{
						while (packet.Position < packet.Length)
						{
							using var reader = packet.ReadMessage();
							var playerId = reader.Tag;
							var player = Players.Where(p => p.Value.PlayerId == playerId).FirstOrDefault().Value;
							if (player != null)
							{
								player.Info = new PlayerInfo(reader);
							}
							else
							{
								Console.WriteLine($"Player {playerId} not found");
							}
						}
						break;
					}
				default:
					//Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
					//		GetRange(packet.Offset, packet.Length).ToArray()));
					break;
			}
		}

		public static string GetPlayerName(uint netID)
		{
			if (Players.TryGetValue(netID, out var player) && !string.IsNullOrEmpty(player.Info.Name))
			{
				return $"{player.Info.Name} ({netID})";
			}
			return $"({netID})";
		}
	}
}
