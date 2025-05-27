using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;

using static CounterStrikeSharp.API.Core.Listeners;

namespace SharpModMenu;

public sealed class PlayerData : IDisposable
{
	private CCSPlayerController Player { get; }
	private TeamDeathmatchPlugin Plugin { get; }

	public bool PrimaryWeaponModified { get; set; }
	public bool SecondaryWeaponModified { get; set; }

	private Gun _PrimaryWeapon = null!;
	public Gun PrimaryWeapon
	{
		get => _PrimaryWeapon;
		set
		{
			if (_Disposed)
				throw new InvalidOperationException($"This object has been disposed");
			if (value == _PrimaryWeapon)
				return;
			_PrimaryWeapon?.SubCount();
			value.AddCount();
			_PrimaryWeapon = value;

			if (PrimaryWeaponModified)
				return;
			PrimaryWeaponModified = true;
			Plugin.EquipLoadout(Player, LoadoutMode.Primary, this);
		}
	}

	private Gun _SecondaryWeapon = null!;
	public Gun SecondaryWeapon
	{
		get => _SecondaryWeapon;
		set
		{
			if (_Disposed)
				throw new InvalidOperationException($"This object has been disposed");
			if (value == _SecondaryWeapon)
				return;
			_SecondaryWeapon?.SubCount();
			value.AddCount();
			_SecondaryWeapon = value;

			if (SecondaryWeaponModified)
				return;
			SecondaryWeaponModified = true;
			Plugin.EquipLoadout(Player, LoadoutMode.Secondary, this);
		}
	}

	public PlayerData(CCSPlayerController player, TeamDeathmatchPlugin plugin)
	{
		Player = player;
		Plugin = plugin;
		PrimaryWeapon = Gun.AK47;
		SecondaryWeapon = Gun.Deagle;
	}

	private bool _Disposed;
	public void Dispose()
	{
		if (_Disposed)
			return;
		_Disposed = true;

		PrimaryWeapon.SubCount();
		SecondaryWeapon.SubCount();
	}
}

[MinimumApiVersion(314)]
public sealed partial class TeamDeathmatchPlugin : BasePlugin
{
	public override string ModuleName => "CsGals.TeamDeathmatch";
	public override string ModuleDescription => "Team Deathmatch gamemode plugin";
	public override string ModuleVersion => Verlite.Version.Full;

	private ConVar? BotQuota { get; set; }

	public override void Load(bool hotReload)
	{
		// use our own custom quota mechanism, as this one is borked
		BotQuota = ConVar.Find("bot_quota");
		BotQuota?.SetValue(0);

		bool mapLoaded = false;

		RegisterListener<OnMapEnd>(() => mapLoaded = false);
		RegisterListener<OnMapStart>(map =>
		{
			if (mapLoaded)
				return;
			mapLoaded = true;
			BotQuota?.SetValue(0);

			Server.ExecuteCommand(
				"""
				bot_quota 0
				sv_cheats 1;
				sv_alltalk 1;
				sv_disable_radar 0;
				mp_solid_teammates 1;
				mp_autokick 0;
				mp_randomspawn 0;
				mp_match_end_changelevel 1;
				mp_spectators_max 5;
				//mp_use_respawn_waves 1;

				mp_timelimit 60;
				mp_roundtime 60;
				mp_roundtime_defuse 60;
				mp_roundtime_deployment 60;
				mp_roundtime_hostage 60;
				mp_maxrounds 0;
				mp_warmuptime 1;
				mp_freezetime 1;
				mp_match_restart_delay 1;
				mp_death_drop_grenade 0;
				mp_death_drop_gun 0;
				mp_death_drop_healthshot 0;
				mp_death_drop_c4 0;
				mp_death_drop_taser 0;
				mp_drop_grenade_enable 0;
				mp_defuser_allocation 0;
				mp_playercashawards 0;
				mp_teamcashawards 0;
				mp_hostages_max 0;
				cash_team_bonus_shorthanded 0;

				mp_teammates_are_enemies 0;
				mp_respawn_on_death_ct 1;
				mp_respawn_on_death_t 1;
				mp_dm_teammode 1;
				mp_dm_bonus_length_max 0;
				mp_dm_bonus_length_min 0;
				mp_dm_time_between_bonus_max 9999;
				mp_dm_time_between_bonus_min 9999;
				mp_respawn_immunitytime 5;

				// buy config
				mp_buytime 0;
				mp_buy_anywhere 0;
				mp_buy_during_immunity 0;
				sv_buy_status_override -1;
				mp_buy_allow_grenades 0;
				mp_weapons_allow_typecount -1;
				mp_weapons_allow_zeus 1;
				mp_give_player_c4 0;
				mp_max_armor 0;

				sv_cheats 0;
				""");
		});
	}

	public override void Unload(bool hotReload)
	{
	}

	private Dictionary<int, PlayerData> PlayerData { get; } = new();

	public bool TryGetPlayerData(CCSPlayerController? player, [NotNullWhen(true)] out PlayerData? data)
	{
		if (player is null || !player.IsValid)
		{
			data = null;
			return false;
		}
		return PlayerData.TryGetValue(player.Slot, out data);
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnPlayerDeath(EventPlayerDeath e, GameEventInfo info)
	{
		if (e.Attacker is not null && e.Attacker.IsValid)
		{
			// do health award
			var attackerPawn = e.Attacker.PlayerPawn;
			if (attackerPawn.IsValid && attackerPawn.Value is not null && attackerPawn.Value.IsValid)
			{
				if (attackerPawn.Value.LifeState == (int)LifeState_t.LIFE_ALIVE)
				{
					var newHealth = Math.Min(attackerPawn.Value.MaxHealth, attackerPawn.Value.Health + 5);
					attackerPawn.Value.Health = newHealth;
					Utilities.SetStateChanged(attackerPawn.Value, "CBaseEntity", "m_iHealth");
				}
			}
		}

		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnWeaponReload(EventWeaponReload e, GameEventInfo info)
	{
		var player = e.Userid;
		if (player is null || !player.IsValid)
			return HookResult.Continue;

		var playerPawn = player.PlayerPawn.Value;
		if (playerPawn is null || !playerPawn.IsValid)
			return HookResult.Continue;

		var activeWeapon = playerPawn.WeaponServices?.ActiveWeapon.Value;
		if (activeWeapon is null || !activeWeapon.IsValid)
			return HookResult.Continue;

		//max out the ReserveAmmo on each reload
		activeWeapon.ReserveAmmo[0] += 999;

		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Pre)]
	public HookResult OnPlayerConnect(EventPlayerConnectFull e, GameEventInfo info)
	{
		if (e.Userid is null || !e.Userid.IsValid)
			return HookResult.Continue;

		PlayerData.Add(e.Userid.Slot, new(e.Userid, this));
		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Pre)]
	public HookResult OnPlayerDisconnect(EventPlayerDisconnect e, GameEventInfo info)
	{
		if (e.Userid is null || !e.Userid.IsValid)
			return HookResult.Continue;

		if (PlayerData.Remove(e.Userid.Slot, out var removed))
			removed.Dispose();
		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnPlayerSpawn(EventPlayerSpawn e, GameEventInfo info)
	{
		//Console.WriteLine(nameof(OnPlayerSpawn));
		if (e.Userid is null || !e.Userid.IsValid)
			return HookResult.Continue;

		EquipLoadout(e.Userid, LoadoutMode.Spawn);
		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnGameStart(EventGameStart e, GameEventInfo info)
	{
		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnRoundStart(EventRoundStart e, GameEventInfo info)
	{
		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnBeginNewMatch(EventBeginNewMatch e, GameEventInfo info)
	{
		return HookResult.Continue;
	}

	[GameEventHandler(HookMode.Post)]
	public HookResult OnGameInit(EventGameInit e, GameEventInfo info)
	{
		return HookResult.Continue;
	}
}
