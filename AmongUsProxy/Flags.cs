using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmongUsProxy
{
	public enum MessageFlags : byte
	{
		HostGame = 0,
		JoinGame = 1,
		StartGame = 2,
		RemoveGame = 3,
		RemovePlayer = 4,
		GameData = 5,
		GameDataTo = 6,
		JoinedGame = 7,
		EndGame = 8,
		GetGameList = 9,
		AlterGame = 10,
		KickPlayer = 11,
		WaitForHost = 12,
		Redirect = 13,
		ReselectServer = 14,
		GetGameListV2 = 16,
	}

	public enum GameDataTag : byte
	{
		DataFlag = 1,
		RPCFlag = 2,
		SpawnFlag = 4,
		DespawnFlag = 5,
		F6 = 6,
		F7 = 7,
		F8 = 8,
	}

	public enum RPCCallID : uint
	{
		CompleteTask = 0x1,
		SetInfected = 0x3,
		SetName = 0x6,
		SetSkin = 0xa,
		ReportBody = 0xb,
		MurderPlayer = 0xc, // is it 0xc or 0xf?
		Chat = 0xd,
		SetScanner = 0xf,
		SetPet = 0x11,
		EnterVent = 0x13,
		ExitVent = 0x14,
		CheckForEndVoting = 0x17,
		CastVote = 0x18,
		PlayerInfo = 0x1e,
	}
}
