using MEC;
using PluginAPI.Core;
using System.Collections.Generic;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerStatsSystem;
using UltimateAFK.Handlers.Components;
using UltimateAFK.Resources;
using UnityEngine;
using Scp079Role = Exiled.API.Features.Roles.Scp079Role;

namespace UltimateAFK.Handlers
{
    /// <summary>
    /// Main class where players are given the AFK component and replacement players are stored.
    /// </summary>
    public class MainHandler
    {
        #region Ignore this
        private readonly UltimateAFK Plugin;
        public MainHandler(UltimateAFK plugin)
        {
            Plugin = plugin;
        }

        #endregion

        /// <summary>
        /// A dictionary where replacement players are stored to give them the stats and items of the original player.
        /// </summary>
        public static Dictionary<Exiled.API.Features.Player, AFKData> ReplacingPlayers;

        /// <summary>
        /// When a player joins I give him the component.
        /// </summary>
        /// <param name="player"></param>
        public void OnPlayerJoin(VerifiedEventArgs ev)
        {
            if(!Plugin.Config.IsEnabled || ev.Player.UserId.Contains("@server")) return;
            
            Log.Debug($"Adding the Component to  {ev.Player.Nickname} | Player already have component: {ev.Player.GameObject.TryGetComponent<AfkComponent>(out _)}", UltimateAFK.Singleton.Config.Debug);

            ev.Player.GameObject.AddComponent<AfkComponent>();
        }

        /// <summary>
        /// When a player changes roles I make sure that if it is a replacement I give him all the things of the person who replaced.
        /// </summary>
        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            try
            {
                if (ev.Player == null || !ReplacingPlayers.TryGetValue(ev.Player, out var data) ||
                    !ev.Player.GameObject.TryGetComponent<AfkComponent>(out var _))
                    return;

                Log.Debug($"Detecting player {ev.Player.Nickname} ({ev.Player.UserId}) who replaced a player who was afk", UltimateAFK.Singleton.Config.Debug);
                
                Timing.CallDelayed(Plugin.Config.ReplaceDelay, () => GiveAfkData(ev.Player, data, ev.Player.Role.Type));
            }
            catch (System.Exception e)
            {
                Log.Error($"Error on {GetType().Name} {nameof(OnChangingRole)} | {e.Data} {e.StackTrace} | Player is null {ev.Player is null} | List is null{ReplacingPlayers is null} | Instance is null {Plugin is null}");
            }
        }

        /// <summary>
        /// Performs the change of stats and items to the replacement player
        /// </summary>
        private void GiveAfkData(Exiled.API.Features.Player ply, AFKData data, RoleTypeId newRole)
        {
            Log.Debug($"Replacing player is {ply.Nickname} ({ply.UserId}) new role is {newRole}", Plugin.Config.Debug);

            if (newRole == RoleTypeId.Scp079)
            {
                Log.Debug("The new role is a SCP079, transferring energy and experience.", UltimateAFK.Singleton.Config.Debug);
                if (ply.Role is Scp079Role scp079Role && scp079Role.SubroutineModule.TryGetSubroutine(out Scp079TierManager tierManager) 
                   && scp079Role.SubroutineModule.TryGetSubroutine(out Scp079AuxManager energyManager))
                {
                    ply.Broadcast( 16, string.Format(UltimateAFK.Singleton.Config.MsgReplace, data.NickName), shouldClearPrevious: true);
                    ply.SendConsoleMessage(string.Format(UltimateAFK.Singleton.Config.MsgReplace, data.NickName), "white");

                    tierManager.TotalExp = data.SCP079.Experience;
                    energyManager.CurrentAux = data.SCP079.Energy;
                    ReplacingPlayers.Remove(ply);
                    
                    Log.Debug($"Energy and experience transferred to the player", UltimateAFK.Singleton.Config.Debug);
                }
                else
                {
                    Log.Error($"Error transferring experience and level to the replacement player, Player.RoleBase is not Scp079 or there was an error obtaining the subroutines.");
                }
                
                Log.Debug("Removing the replacement player from the dictionary", UltimateAFK.Singleton.Config.Debug);
                ReplacingPlayers.Remove(ply);
            }
            else
            {
                Log.Debug("Clearing replacement player inventory", Plugin.Config.Debug);
                ply.ClearInventory();
                Log.Debug($"Adding Ammo to {ply.Nickname} ({ply.UserId})", UltimateAFK.Singleton.Config.Debug);
                // I add the ammunition first since it is the slowest thing to be done.
                ply.SendAmmo(data.Ammo);
                
                // This call delayed is necessary.
                Timing.CallDelayed(0.1f, () =>
                {
                    Log.Debug($"Changing player {ply.Nickname} ({ply.UserId})  position and HP", Plugin.Config.Debug);
                    ply.Position = data.Position;
                    ply.Health = data.Health;
                    Log.Debug($"Adding items to {ply.Nickname} ({ply.UserId})", UltimateAFK.Singleton.Config.Debug);
                    ply.SendItems(data.Items);
                    // I apply the modifications of the replacement player not of the afk, I could do it but I sincerely prefer this method.
                    ply.ApplyAttachments();
                    // I refill the ammunition of the weapons, since it is annoying to appear without a loaded weapon.
                    ply.ReloadAllWeapons();
                    // Send the broadcast to the player
                    ply.Broadcast(message:string.Format(UltimateAFK.Singleton.Config.MsgReplace, data.NickName), duration:16, shouldClearPrevious: true);
                    ply.SendConsoleMessage(string.Format(UltimateAFK.Singleton.Config.MsgReplace, data.NickName), "white");
                });
                
                Log.Debug("Removing the replacement player from the dictionary", UltimateAFK.Singleton.Config.Debug);
                ReplacingPlayers.Remove(ply);
            }
        }
        
        /// <summary>
        /// At the beginning of a round I clean the ReplacingPlayers dictionary.
        /// </summary>
        public void OnWaitingForPlayers()
        {
            ReplacingPlayers.Clear();
        }

        /// <summary>
        /// I make sure that no player is in the dictionary in case it has not been cleaned correctly, this event is also executed when a player disconnects.
        /// </summary>
        public void OnPlayerDeath(DyingEventArgs ev)
        {
            if (ev.Player != null && ReplacingPlayers.TryGetValue(ev.Player, out var data))
            {
                ReplacingPlayers.Remove(ev.Player);
            }
        }
    }
}