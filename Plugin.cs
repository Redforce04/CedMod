﻿using System;
using System.Collections.Generic;
using EXILED;

namespace CedMod
{
    public class Plugin : EXILED.Plugin
    {
        //Instance variable for eventhandlers
        public BanSystem BanSystemEvents;
        public PlayerJoinBC PlayerJoinBCEvents;
        public FriendlyFireAutoBan FFAEvents;
        public Commands Commands;
        public Misc Misc;
        public PlayerStatistics PlayerStats;

        public override void OnEnable()
        {
            try
            {
                string GEOString = "";
                List<string> GEOList = GameCore.ConfigFile.ServerConfig.GetStringList("bansystem_geo");
                foreach (string s in GEOList)
                {
                    GEOString = GEOString + s + "+";
                }
                if (GEOList != null)
                {
                    ServerConsole.AccessRestriction = true;
                }
                Log.Debug("Initializing event handlers..");
                //Set instance varible to a new instance, this should be nulled again in OnDisable
                BanSystemEvents = new BanSystem(this);
                //Hook the events you will be using in the plugin. You should hook all events you will be using here, all events should be unhooked in OnDisabled 
                Events.RemoteAdminCommandEvent += BanSystemEvents.OnCommand;
                Events.PlayerJoinEvent += BanSystemEvents.OnPlayerJoin;
                PlayerJoinBCEvents = new PlayerJoinBC(this);
                Events.PlayerJoinEvent += PlayerJoinBCEvents.OnPlayerJoin;
                FFAEvents = new FriendlyFireAutoBan(this);
                Events.RoundStartEvent += FFAEvents.OnRoundStart;
                Events.PlayerDeathEvent += FFAEvents.Ondeath;
                Events.ConsoleCommandEvent += FFAEvents.ConsoleCommand;
                Commands = new Commands(this);
                Events.RemoteAdminCommandEvent += Commands.OnCommand;
                Events.RoundEndEvent += Commands.OnRoundEnd;
                Misc = new Misc(this);
                Events.DoorInteractEvent += Misc.OnDoorAccess;
                Events.RoundStartEvent += Misc.OnRoundStart;
                Events.RoundEndEvent += Misc.OnRoundEnd;
                PlayerStats = new PlayerStatistics(this);
                Events.RoundEndEvent += PlayerStats.OnRoundEnd;
                Events.PlayerDeathEvent += PlayerStats.OnPlayerDeath;
                Log.Info($"CedMod has loaded. c:");
            }
            catch (Exception e)
            {
                //This try catch is redundant, as EXILED will throw an error before this block can, but is here as an example of how to handle exceptions/errors
                Log.Error($"There was an error loading the plugin: {e}");
            }
        }

        public override void OnDisable()
        {
            Events.RemoteAdminCommandEvent -= BanSystemEvents.OnCommand;
            Events.PlayerJoinEvent -= BanSystemEvents.OnPlayerJoin;
            Events.PlayerJoinEvent -= PlayerJoinBCEvents.OnPlayerJoin;
            Events.RoundStartEvent -= FFAEvents.OnRoundStart;
            Events.RemoteAdminCommandEvent -= Commands.OnCommand;
            Events.RoundEndEvent -= Commands.OnRoundEnd;
            Events.ConsoleCommandEvent -= FFAEvents.ConsoleCommand;
            Events.RoundEndEvent -= PlayerStats.OnRoundEnd;
            Events.PlayerDeathEvent -= PlayerStats.OnPlayerDeath;
            BanSystemEvents = null;
            PlayerJoinBCEvents = null;
            FFAEvents = null;
            Commands = null;
            PlayerStats = null;

        }

        public override void OnReload()
        {
            //This is only fired when you use the EXILED reload command, the reload command will call OnDisable, OnReload, reload the plugin, then OnEnable in that order. There is no GAC bypass, so if you are updating a plugin, it must have a unique assembly name, and you need to remove the old version from the plugins folder
        }

        public override string getName { get; } = "CedModV2";
    }
}