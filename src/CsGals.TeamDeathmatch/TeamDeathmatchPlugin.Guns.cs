using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;

using CSSUniversalMenuAPI;

namespace SharpModMenu;

public sealed record Gun(string Class, string Name)
{
	public int? Limit { get; set; }
	public int _Count;
	public int Count => _Count;

	public void AddCount()
	{
		Interlocked.Increment(ref _Count);
	}
	public void SubCount()
	{
		Interlocked.Decrement(ref _Count);
	}

	public string MenuTitle => Limit switch
	{
		null or 0 => Name,
		_ => $"{Name} [{Count}/{Limit}]",
	};

	public static Gun Knife = new("weapon_knife", "Knife");
	public static Gun Deagle = new("weapon_deagle", "Deagle");
	public static Gun R8 = new("weapon_revolver", "R8");
	public static Gun Glock = new("weapon_glock", "Glock");
	public static Gun USP = new("weapon_usp_silencer", "USP");
	public static Gun CZ75 = new("weapon_cz75a", "CZ75");
	public static Gun FiveSeven = new("weapon_fiveseven", "Five Seven");
	public static Gun P250 = new("weapon_p250", "P250");
	public static Gun Tec9 = new("weapon_tec9", "Tec-9");
	public static Gun Elite = new("weapon_elite", "Elite");
	public static Gun P2000 = new("weapon_hkp2000", "P2000");
	public static Gun MP9 = new("weapon_mp9", "MP9");
	public static Gun MAC10 = new("weapon_mac10", "MAC 10");
	public static Gun PPBizon = new("weapon_bizon", "PP-Bizon");
	public static Gun MP7 = new("weapon_mp7", "MP7");
	public static Gun UMP = new("weapon_ump45", "UMP45");
	public static Gun P90 = new("weapon_p90", "P90");
	public static Gun MP5 = new("weapon_mp5sd", "MP5");
	public static Gun Famas = new("weapon_famas", "Famas");
	public static Gun Galil = new("weapon_galilar", "Galil");
	public static Gun M4A4 = new("weapon_m4a4", "M4A4");
	public static Gun M4A1S = new("weapon_m4a1_silencer", "M4A1-S");
	public static Gun AK47 = new("weapon_ak47", "AK47");
	public static Gun AUG = new("weapon_aug", "AUG");
	public static Gun Krieg = new("weapon_sg553", "Krieg");
	public static Gun Scout = new("weapon_ssg08", "Scout");
	public static Gun AWP = new("weapon_awp", "AWP") { Limit = 2 };
	public static Gun SCAR20 = new("weapon_scar20", "SCAR-20") { Limit = 0 }; // CT autosniper
	public static Gun G3SG1 = new("weapon_g3sg1", "G3SG1") { Limit = 0 }; // T autosniper
	public static Gun Nova = new("weapon_nova", "Nova");
	public static Gun XM1014 = new("weapon_xm1014", "XM1014");
	public static Gun MAG7 = new("weapon_mag7", "MAG-7");
	public static Gun SawedOff = new("weapon_sawedoff", "Sawed-Off");
	public static Gun M249 = new("weapon_m249", "M249");
	public static Gun Negev = new("weapon_negev", "Negev");
	public static Gun Decoy = new("weapon_decoy", "Decoy");
	public static Gun Flashbang = new("weapon_flashbang", "Flashbang Grenade");
	public static Gun Smoke = new("weapon_smokegrenade", "Smoke Grenade");
	public static Gun Nade = new("weapon_hegrenade", "HE Grenade");
	public static Gun Molly = new("weapon_molotov", "Molotov");
	public static Gun Incendiary = new("weapon_incgrenade", "Incendiary");

	public static Gun None = new("none", "None");
	public static Gun Random = new("random", "Random");
}

public enum LoadoutMode
{
	Primary,
	Secondary,
	Spawn,
}

public partial class TeamDeathmatchPlugin
{
	internal void EquipLoadout(CCSPlayerController player, LoadoutMode mode, PlayerData? playerData = null)
	{
		if (mode is LoadoutMode.Spawn)
		{
			player.RemoveWeapons();
			player.GiveNamedItem(Gun.Knife.Class);
			player.GiveNamedItem(Gun.Nade.Class);
			player.GiveNamedItem("item_assaultsuit");
		}

		if (playerData is null && !TryGetPlayerData(player, out playerData))
		{
			player.GiveNamedItem(Gun.Deagle.Class);
			switch (RandomNumberGenerator.GetInt32(0, 101))
			{
				case < 25:
					player.GiveNamedItem(Gun.M4A1S.Class);
					break;
				case < 50:
					player.GiveNamedItem(Gun.M4A4.Class);
					break;
				case < 100:
					player.GiveNamedItem(Gun.AK47.Class);
					break;
				case < 101:
				default:
					player.GiveNamedItem(Gun.AWP.Class);
					break;
			}
			return;
		}

		if (mode is LoadoutMode.Spawn)
		{
			playerData.PrimaryWeaponModified = false;
			playerData.SecondaryWeaponModified = false;
		}

		if (mode is LoadoutMode.Spawn or LoadoutMode.Primary)
		{
			if (playerData.PrimaryWeapon == Gun.None)
				;
			else if (playerData.PrimaryWeapon == Gun.Random)
				;
			else
			{
				player.GiveNamedItem(playerData.PrimaryWeapon.Class);
			}
		}

		if (mode is LoadoutMode.Spawn or LoadoutMode.Secondary)
		{
			if (playerData.SecondaryWeapon == Gun.None)
				;
			else if (playerData.SecondaryWeapon == Gun.Random)
				;
			else
				player.GiveNamedItem(playerData.SecondaryWeapon.Class);
		}
	}

	public static Gun[] PrimaryGuns { get; } =
	{
		Gun.M4A4,
		Gun.M4A1S,
		Gun.AK47,
		Gun.Galil,
		Gun.M249,
		Gun.Famas,
		Gun.Krieg,
		Gun.AUG,
		Gun.Nova,
		Gun.AWP,
		Gun.SCAR20,
		Gun.G3SG1,
		Gun.XM1014,
		Gun.MAC10 ,
		Gun.MP9,
		Gun.MP5,
		Gun.UMP,
		Gun.P90,
		Gun.Scout,
		Gun.MAG7,
		Gun.SawedOff,
		Gun.PPBizon,
		Gun.MP7,
		Gun.Negev,
		Gun.Random,
		Gun.None,
	};

	public static Gun[] SecondaryGuns { get; } =
	{
		Gun.USP,
		Gun.Glock,
		Gun.Deagle,
		Gun.P250,
		Gun.Elite,
		Gun.FiveSeven,
		Gun.P2000,
		Gun.Tec9,
		Gun.CZ75,
		Gun.R8,
		Gun.Random,
		Gun.None,
	};

	[ConsoleCommand("css_guns")]
	public void GunsMenu(CCSPlayerController player, CommandInfo info)
	{
		if (!TryGetPlayerData(player, out var playerData))
			return;

		var menu = UniversalMenu.CreateMenu(player);
		menu.Title = "Primary Weapon";

		foreach (var gun in PrimaryGuns)
		{
			var item = menu.CreateItem();
			item.Context = gun;
			item.Title = gun.MenuTitle;
			item.Enabled =
				gun.Limit is null ||
				gun == playerData.PrimaryWeapon ||
				gun.Count < gun.Limit;

			if (item.Enabled)
				item.Selected += PrimaryGun_Selected;
		}

		menu.Display();
	}

	private void PrimaryGun_Selected(IMenuItem selectedItem)
	{
		if (!TryGetPlayerData(selectedItem.Player, out var playerData))
		{
			selectedItem.Menu.Close();
			return;
		}

		playerData.PrimaryWeapon = (selectedItem.Context as Gun)!;

		var menu = UniversalMenu.CreateMenu(selectedItem.Menu);
		menu.Title = "Secondary Weapon";

		foreach (var gun in SecondaryGuns)
		{
			var item = menu.CreateItem();
			item.Context = gun;
			item.Title = gun.MenuTitle;
			item.Enabled =
				gun.Limit is null ||
				gun == playerData.SecondaryWeapon ||
				gun.Count < gun.Limit;

			if (item.Enabled)
				item.Selected += SecondaryGun_Selected;
		}

		menu.Display();
	}

	private void SecondaryGun_Selected(IMenuItem selectedItem)
	{
		if (!TryGetPlayerData(selectedItem.Player, out var playerData))
		{
			selectedItem.Menu.Close();
			return;
		}

		playerData.SecondaryWeapon = (selectedItem.Context as Gun)!;

		selectedItem.Menu.Exit();
	}
}
