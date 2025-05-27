using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

namespace SharpModMenu;

file struct ExecDeferred : IDisposable
{
	private Action? Func { get; set; }
	public ExecDeferred(Action func)
	{
		Func = func;
	}

	public void Dispose()
	{
		Func?.Invoke();
		Func = null;
	}
}

public partial class TeamDeathmatchPlugin
{
	private int MinPlayersPerTeam = 3;

	private bool ExecutingPlayerTeams = false;
	[GameEventHandler(HookMode.Post)]
	public HookResult OnPlayerTeam(EventPlayerTeam e, GameEventInfo info)
	{
		if (ExecutingPlayerTeams)
			return HookResult.Continue;
		if (e.Userid is null || !e.Userid.IsValid)
			return HookResult.Continue;
		if (e.Isbot)
			return HookResult.Continue;

		AddTimer(0.1f, BalanceBots, TimerFlags.STOP_ON_MAPCHANGE);
		return HookResult.Continue;
	}

	public void BalanceBots()
	{
		if (ExecutingPlayerTeams)
			return;

		ExecutingPlayerTeams = true;
		using var inhibitor = new ExecDeferred(() => ExecutingPlayerTeams = false);

		int tBots = 0;
		int tHumans = 0;
		int ctBots = 0;
		int ctHumans = 0;
		int totalHumans = 0;

		for (int i = 0; i < Server.MaxPlayers; i++)
		{
			var player = Utilities.GetPlayerFromSlot(i);

			if (player is null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
				continue;

			var team = player.Team;
			if (!player.IsBot)
				totalHumans++;

			switch (player.Team)
			{
				case CsTeam.CounterTerrorist:
					if (player.IsBot)
						ctBots++;
					else
						ctHumans++;
					break;
				case CsTeam.Terrorist:
					if (player.IsBot)
						tBots++;
					else
						tHumans++;
					break;
				default:
					break;
			}
		}

		if (totalHumans == 0)
			return;

		int tDesiredBots = Math.Max(0, MinPlayersPerTeam - tHumans);
		int ctDesiredBots = Math.Max(0, MinPlayersPerTeam - ctHumans);

		int tDelta = tDesiredBots - tBots;
		int ctDelta = ctDesiredBots - ctBots;

		int swapTeamsCount = 0;
		CsTeam swapTeamsDst = CsTeam.None;

		if (tDelta > 0 && ctDelta < 0)
		{
			swapTeamsDst = CsTeam.Terrorist;
			swapTeamsCount = Math.Min(tDelta, Math.Abs(ctDelta));
			tDelta -= swapTeamsCount;
			ctDelta += swapTeamsCount;
		}
		else if (ctDelta > 0 && tDelta < 0)
		{
			swapTeamsDst = CsTeam.CounterTerrorist;
			swapTeamsCount = Math.Min(ctDelta, Math.Abs(tDelta));
			ctDelta -= swapTeamsCount;
			tDelta += swapTeamsCount;
		}

		// kick or swap bots
		if (ctDelta != 0 || tDelta != 0 || ctDelta != 0)
			Console.WriteLine($"Bot quota: CT: {ctDelta} T: {tDelta}: Swap: {swapTeamsCount}");
		Debug.Assert(Math.Abs(ctDelta) <= MinPlayersPerTeam);
		Debug.Assert(Math.Abs(tDelta) <= MinPlayersPerTeam);
		Debug.Assert(Math.Abs(swapTeamsCount) <= MinPlayersPerTeam);

		// swap teams
		for (int i = 0; i < Server.MaxPlayers && swapTeamsCount > 0; i++)
		{
			var player = Utilities.GetPlayerFromSlot(i);

			if (player is null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
				continue;

			if (!player.IsBot)
				continue;

			if (player.Team != swapTeamsDst)
			{
				swapTeamsCount--;
				player.SwitchTeam(swapTeamsDst);
			}
		}

		// remove bots
		while (tDelta < 0)
		{
			tDelta++;
			Server.ExecuteCommand("bot_kick t");
		}
		while (ctDelta < 0)
		{
			ctDelta++;
			Server.ExecuteCommand("bot_kick ct");
		}

		// add bots
		while (tDelta > 0)
		{
			tDelta--;
			Server.ExecuteCommand("bot_add t");
		}

		while (ctDelta > 0)
		{
			ctDelta--;
			Server.ExecuteCommand("bot_add ct");
		}
	}
}
