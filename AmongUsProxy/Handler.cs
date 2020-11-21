using Impostor.Api.Net.Messages;
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

			Console.WriteLine(HexUtils.HexDump(packet.Buffer.ToArray()));
			while (packet.Position < packet.Length)
			{
				using var reader = packet.ReadMessage();
				Console.WriteLine($" - Tag          {reader.Tag}");
				switch (reader.Tag)
				{
					default:
						break;
				}
			}
		}
	}
}
