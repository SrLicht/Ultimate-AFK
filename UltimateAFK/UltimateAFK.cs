using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using System;
using Exiled.Events.EventArgs.Player;
using GameCore;
using UltimateAFK.Handlers;
using UltimateAFK.Resources;
using Version = System.Version;
using Server = Exiled.Events.Handlers.Server;
using Player = Exiled.Events.Handlers.Player;

namespace UltimateAFK
{
    /// <summary>
    /// Main class where all the handlers are loaded.
    /// </summary>
    public class UltimateAFK : Plugin<Config>
    {
        public static UltimateAFK Singleton;
        public override string Author { get; } = "SrLicht";
        public override string Name { get; } = "Ultimate-AFK";
        public override string Prefix { get; } = "ultimate_afk";
        
        public override Version Version { get; } = new Version(6, 1, 0);
        public override Version RequiredExiledVersion { get; } = new Version(6, 0, 1);
        public MainHandler MainHandler { get; private set; }
        
        public override void OnEnabled()
        {
            Singleton = this;
            MainHandler = new MainHandler(this);
            
            if (ConfigFile.ServerConfig.GetFloat("afk_time", 90f) > 0)
            {
                Exiled.API.Features.Log.Warn($"You have enabled the AFK detector of the base game, please disable it by setting &6afk_time = 0&r in &4config_gameplay.txt&r");
            }

            Player.ChangingRole += MainHandler.OnChangingRole;
            Player.Verified += MainHandler.OnPlayerJoin;
            Player.Dying += MainHandler.OnPlayerDeath;
            Server.WaitingForPlayers += MainHandler.OnWaitingForPlayers;
            
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            MainHandler.ReplacingPlayers.Clear();
            MainHandler.ReplacingPlayers = null;
            
            Player.ChangingRole -= MainHandler.OnChangingRole;
            Player.Verified -= MainHandler.OnPlayerJoin;
            Player.Dying -= MainHandler.OnPlayerDeath;
            Server.WaitingForPlayers -= MainHandler.OnWaitingForPlayers;
            MainHandler = null;
            base.OnDisabled();
        }
    }
}