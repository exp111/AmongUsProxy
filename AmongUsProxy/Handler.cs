using AmongUsProxy.Extensions;
using Impostor.Api.Net.Messages;
using Impostor.Hazel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
			Debug.WriteLine($"{source,-15} To Client: {packet.Tag,-2} {tagName}");

			switch ((MessageFlags)packet.Tag)
			{
				case MessageFlags.Redirect:
					{
						long address = packet.ReadUInt32();
						uint port = packet.ReadUInt16();
						Debug.WriteLine($"- IP              {new IPAddress(address)}");
						Debug.WriteLine($"- Port            {port}");
						break;
					}
				case MessageFlags.ReselectServer:
					{
						Debug.WriteLine($"Byte: {packet.ReadByte()}");
						var count = packet.ReadPackedUInt32();
						Debug.WriteLine($"ServerInfo Count: {count}");
						for (var i = 0; i < count; i++)
						{
							var reader = packet.ReadMessage();
							// ServerInfo Deserialize():
							var name = reader.ReadString();
							Debug.WriteLine($"Name: {name}");
							long address = reader.ReadUInt32();
							Debug.WriteLine($"IP: {new IPAddress(address)}");
							var port = reader.ReadUInt16();
							Debug.WriteLine($"Port: {port}");
							Debug.WriteLine($"Start: {reader.ReadPackedUInt32()}");
						}
						break;
					}
				case MessageFlags.HostGame:
					Debug.WriteLine($"- GameCode        {packet.ReadInt32()}");
					break;
				case MessageFlags.GameData:
					HandleGameData(false, packet);
					break;
				case MessageFlags.GameDataTo:
					HandleGameData(true, packet);
					// packet.Position = packet.Length;
					break;
				case MessageFlags.JoinedGame:
					{
						Debug.WriteLine($"- GameCode        {packet.ReadInt32()}");
						Debug.WriteLine($"- ClientId        {packet.ReadInt32()}");
						Debug.WriteLine($"- HostId          {packet.ReadInt32()}");
						var playerCount = packet.ReadPackedUInt32();
						Debug.WriteLine($"- PlayerCount     {playerCount}");
						for (var i = 0; i < playerCount; i++)
						{
							var playerId = packet.ReadPackedUInt32();
							Debug.WriteLine($"-     PlayerId    {playerId}");
							// do we need those PlayerIds?
							/*if (!Clients.ContainsKey(playerId))
							{
								Clients[playerId] = new Client();
							}*/
						}
						break;
					}
				case MessageFlags.AlterGame:
					Debug.WriteLine($"- GameCode        {packet.ReadInt32()}");
					Debug.WriteLine($"- Flag            {packet.ReadSByte()}");
					Debug.WriteLine($"- Value           {packet.ReadBoolean()}");
					break;
				case MessageFlags.GetGameListV2:
					{
						var reader = packet.ReadMessage();
						while (reader.Position < reader.Length)
						{
							var objReader = reader.ReadMessage();
							// GameListing Deserialize
							var ip = objReader.ReadInt32();
							var port = objReader.ReadUInt16();
							var gameId = objReader.ReadInt32();
							var hostName = objReader.ReadString();
							var playerCount = objReader.ReadByte();
							var age = objReader.ReadPackedUInt32();
							var mapId = objReader.ReadByte();
							var numImpostors = objReader.ReadByte();
							var maxPlayers = objReader.ReadByte();
							Debug.WriteLine($"Game: {hostName}");
						} 

						break;
					}
				default:
					break;
			}
		}

		// InnerNetServer_HandleMessage
		public static void HandleToServer(string source, IMessageReader packet)
		{
			var tagName = Enum.GetName(typeof(MessageFlags), packet.Tag) ?? "Unknown";
			Console.ForegroundColor = ConsoleColor.White;
			Debug.WriteLine($"{source,-15} To Server: {packet.Tag,-2} {tagName}");

			switch ((MessageFlags)packet.Tag)
			{
				case MessageFlags.HostGame:
					Debug.WriteLine($"- GameInfo Length {packet.ReadBytesAndSize().Length}");
					break;
				case MessageFlags.JoinGame:
					Debug.WriteLine($"- GameCode        {packet.ReadInt32()}");
					Debug.WriteLine($"- MapPurchase     {packet.ReadPackedUInt32()}");
					break;
				case MessageFlags.GameData:
					HandleGameData(false, packet);
					break;
				case MessageFlags.GameDataTo:
					HandleGameData(true, packet);
					break;
			}
		}

		// InnerNetClient_HandleGameData
		public static void HandleGameData(bool toPlayer, IMessageReader packet)
		{
			Debug.WriteLine($" - Game Code        {packet.ReadInt32()}");
			if (toPlayer)
			{
				Debug.WriteLine($" - Target       {packet.ReadPackedInt32()}");
			}

			//Debug.WriteLine(HexUtils.HexDump(packet.Buffer.ToArray()));
			// InnerNetClient_HandleGameDataInner
			while (packet.Position < packet.Length)
			{
				using var reader = packet.ReadMessage();
				Debug.WriteLine($" - Tag          {reader.Tag}");
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
						Debug.WriteLine($"Prefab {prefabID}");
						var clientID = reader.ReadPackedUInt32();
						Debug.WriteLine($"ClientID {clientID}");
						var flags = reader.ReadByte();
						Debug.WriteLine($"SpawnFlags {flags}");
						var componentCount = reader.ReadPackedUInt32();
						Debug.WriteLine($"Comp Count {componentCount}");
						for (var i = 0; i < componentCount; i++)
						{
							var netId = reader.ReadPackedUInt32();
							Debug.WriteLine($"NetID {netId}");
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
											Debug.WriteLine($"Player {playerId}: {playerInfo.Name}");
										}
										break;
									}
								case 4: // InnerPlayerControl
									{
										var isNew = objReader.ReadBoolean();
										var playerId = objReader.ReadByte();
										var player = Players.EnsureKey(netId);
										player.PlayerId = playerId;
										break;
									}
								default:
									Debug.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
										GetRange(objReader.Offset, objReader.Length).ToArray()));
									break;
							}
						}
						break;
					default:
						Debug.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
							GetRange(reader.Offset, reader.Length).ToArray()));
						break;
				}
			}
		}

		public static void HandleRPC(uint netID, IMessageReader packet)
		{
			//TODO: get name from netid, default to netid if not yet seen
			Debug.WriteLine($" - NetID        {GetPlayerName(netID)}");
			var callID = packet.ReadByte();
			Debug.WriteLine($" - CallID       {callID}");
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
						Debug.WriteLine(HexUtils.HexDump(packet.Buffer.ToList().
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
								Debug.WriteLine($"Player {playerId} not found");
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
			if (Players.TryGetValue(netID, out var player))
			{
				return $"{player.Info.Name} ({netID})";
			}
			return $"({netID})";
		}
	}
}
