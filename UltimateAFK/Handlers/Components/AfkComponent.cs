using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp096;
using UltimateAFK.Resources;
using UnityEngine;

namespace UltimateAFK.Handlers.Components
{
    /// <summary>
    /// Component that performs a constant afk check.
    /// </summary>
    [RequireComponent(typeof(ReferenceHub))]
    public class AfkComponent : MonoBehaviour
    {
        private void Awake()
        {
            if (Player.Get(gameObject) is not { } ply)
            {
                Log.Error($"{this} Error Getting Player");
                Destroy(this);
                return;
            }
            

            Owner = ply;
            // Coroutine dies when the component or the ReferenceHub (Player) is destroyed.
            _checkHandle = Timing.RunCoroutine(Check().CancelWith(this).CancelWith(gameObject));
            Log.Debug($"Component full loaded Owner: {Owner.Nickname} ({Owner.UserId})");
        }
        
        private void OnDestroy()
        {
            Log.Debug($"Calling OnDestroy");

            if (Owner is null)
                Log.Debug("Owner was null at the time of destroying the component");

            Timing.KillCoroutines(_checkHandle);
        }

        /// <summary>
        /// Public method to destroy the component from the outside.
        /// </summary>
        public void Destroy()
        {
            Destroy(this);
        }

        private IEnumerator<float> Check()
        {
            for (;;)
            {
                yield return Timing.WaitForSeconds(1.3f);
                
                Log.Debug("Calling CheckAFK");
                try
                {
                    CheckAfk();
                }
                catch (Exception e)
                {
                    Log.Error($"Error in {nameof(Check)}: &2{e.TargetSite}&r ||  {e.StackTrace}");
                }
            }
        }

        private void CheckAfk()
        {
            if(!Continue(Owner)) return;
            
            if (Owner == null)
            {
                Log.Error($"{nameof(CheckAfk)}: Player is null");
                Destroy(this);
                return;
            }

            
            var ownerPosition = Owner.Position;
            var cameraPosition = Owner.Role is Exiled.API.Features.Roles.Scp079Role scp079 ? scp079.Camera.Position : Owner.CameraTransform.position;
            var cameraRotation = Owner.Role is Exiled.API.Features.Roles.Scp079Role scp0792 ? new Quaternion(scp0792.Camera.Rotation.x, scp0792.Camera.Rotation.y, scp0792.Camera.Rotation.z, 0f ) : Owner.CameraTransform.rotation;
            
            // Set OwnerRoleType 
            OwnerRoleType = Owner.Role;
            // Player is moving
            if (cameraPosition != _cameraPosition || ownerPosition != _ownerPosition || _cameraRotation != cameraRotation)
            {
                _cameraPosition = cameraPosition;
                _cameraRotation = cameraRotation;
                _ownerPosition = ownerPosition;
                _afkTime = 0f;
                OwnerLastPosition = ownerPosition;
            }
            // The player is not moving and is not SCP-096 with his TryToNotCry ability.
            else if (!(Owner.Role == RoleTypeId.Scp096 && (Owner.Role.Base as Scp096Role).IsAbilityState(Scp096AbilityState.TryingNotToCry)))
            {
                Log.Debug($"{Owner.Nickname} is in not moving, AFKTime: {_afkTime}");

                if(_afkTime++ < UltimateAFK.Singleton.Config.AfkTime) return;

                var graceNumb = UltimateAFK.Singleton.Config.GraceTime - (_afkTime - UltimateAFK.Singleton.Config.AfkTime);
                
                if (graceNumb > 0)
                {
                    // The player is in grace time, so let's warn him that he has been afk for too long.
                    Owner.Broadcast( 2,string.Format(UltimateAFK.Singleton.Config.MsgGrace, graceNumb),
                        shouldClearPrevious: true);
                }
                else
                {
                    Log.Info($"{Owner.Nickname} ({Owner.UserId}) Detected as AFK");
                    _afkTime = 0f;
                    Replace(Owner, Owner.Role);
                }
            }
            
        }

        #region API
        public Player Owner { get; private set; }

        public RoleTypeId OwnerRoleType;

        public Vector3 OwnerLastPosition;
        
        public int AfkTimes { get; set; }
        
        public bool IsKickEnabled { get; set; } = UltimateAFK.Singleton.Config.AfkCount > -1;
        #endregion

        #region Private variables

        // Position in the world
        private Vector3 _ownerPosition;

        // Player camera position
        private Vector3 _cameraPosition;

        // Player camera rotation
        private Quaternion _cameraRotation;

        // The time the player was afk
        private float _afkTime;
        
        // Using a MEC Coroutine is more optimized than using Unity methods.
        private CoroutineHandle _checkHandle;
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Check if the player is alive, if the round has started, if the players on the server meet the requirements for check afk to work, if the player is tutorial and the configuration allows the tutorial to be detected as afk.
        /// </summary>
        /// <returns>True if all requirements are met</returns>
        private bool Continue(Player ply)
        {
            return ply.IsAlive && Round.IsStarted && Player.Dictionary.Count >= UltimateAFK.Singleton.Config.MinPlayers &&
                   (Owner.Role != RoleTypeId.Tutorial || !UltimateAFK.Singleton.Config.IgnoreTut);
        }
        
        /// <summary>
        /// Perform player replacement.
        /// </summary>
        /// <param name="ply">player to replace</param>
        /// <param name="roleType"><see cref="RoleTypeId"/> of the player afk</param>
        private void Replace(Player ply, RoleTypeId roleType)
        {
            try
            {
                // Check if role is blacklisted
                if (UltimateAFK.Singleton.Config.RoleTypeBlacklist?.Count > 0 && UltimateAFK.Singleton.Config.RoleTypeBlacklist.Contains(roleType))
                {
                    Log.Debug($"player {ply.Nickname} ({ply.UserId}) has a role that is blacklisted so he will not be searched for a replacement player");
                    
                    ply.ClearInventory();
                    ply.Role.Set(RoleTypeId.Spectator);
                
                    if (IsKickEnabled)
                    {
                        AfkTimes++;

                        if (AfkTimes >= UltimateAFK.Singleton.Config.AfkCount)
                        {
                            ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgKick, "white");
                            ply.Kick(UltimateAFK.Singleton.Config.MsgKick);
                            return;
                        }
                    }
                
                    ply.Broadcast(30, UltimateAFK.Singleton.Config.MsgFspec, shouldClearPrevious: true);
                    ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgFspec, "white");
                    return;
                }
                
                // Get player replacement
                Player replacement = GetReplacement();
                
                // If replacement is null
                if (replacement is null)
                {
                    Log.Debug("Unable to find replacement player, moving to spectator...");
                    
                    ply.ClearInventory();
                    ply.Role.Set(RoleTypeId.Spectator);

                    if (IsKickEnabled)
                    {
                        AfkTimes++;

                        if (AfkTimes >= UltimateAFK.Singleton.Config.AfkCount)
                        {
                            ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgKick, "white");

                            ply.Kick(UltimateAFK.Singleton.Config.MsgKick);

                            return;
                        }
                    }

                    ply.Broadcast(30, UltimateAFK.Singleton.Config.MsgFspec, shouldClearPrevious: true);
                    ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgFspec, "white");
                }
                else
                {
                    // if not
                    Log.Debug($"Replacement Player found: {replacement.Nickname} ({replacement.UserId})");

                    // Check if AFK role is SCP-079 
                    if (roleType is RoleTypeId.Scp079)
                    {
                        //Adds the replacement player to the dictionary with all the necessary information
                        AddData(ply, replacement, true);
                
                        // Self-explanatory
                        ply.Role.Set(RoleTypeId.Spectator);
                
                        if (IsKickEnabled)
                        {
                            AfkTimes++;

                            // Check if the player should be removed from the server for being too many times afk
                            if (AfkTimes >= UltimateAFK.Singleton.Config.AfkCount)
                            {
                                ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgKick, "white");

                                ply.Kick(UltimateAFK.Singleton.Config.MsgKick);

                                replacement.Role.Set(roleType);
                                return;
                            }
                        }

                        //Send player a broadcast for being too long afk
                        ply.Broadcast(30, UltimateAFK.Singleton.Config.MsgFspec, shouldClearPrevious: true);
                        ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgFspec, "white");
                
                        // Sends replacement to the role that had the afk
                        replacement.Role.Set(roleType);
                    }
                    else
                    {
                        // Adds the replacement player to the dictionary with all the necessary information
                        AddData(ply, replacement, false);

                        if (IsKickEnabled)
                        {
                            AfkTimes++;
                    
                            // Check if the player should be removed from the server for being too many times afk
                            if (AfkTimes >= UltimateAFK.Singleton.Config.AfkCount)
                            {
                                ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgKick, "white");

                                ply.Kick(UltimateAFK.Singleton.Config.MsgKick);

                                replacement.Role.Set(roleType);
                                return;
                            }
                        }
                        
                        // Clear player inventory
                        ply.ClearInventory();
                        //Send player a broadcast for being too long afk
                        ply.Broadcast( 25, UltimateAFK.Singleton.Config.MsgFspec, shouldClearPrevious: true);
                        ply.SendConsoleMessage(UltimateAFK.Singleton.Config.MsgFspec, "white");
                        // Sends player to spectator
                        ply.Role.Set(RoleTypeId.Spectator);
                        // Sends replacement to the role that had the afk
                        replacement.Role.Set(roleType);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error on {nameof(Replace)}: IsOwnerNull: {Owner is null} || {e.Data} -- {e.StackTrace}");
            }
        }
        
        /// <summary>
        /// Obtains a player who qualifies for replacement.
        /// </summary>
        private Player GetReplacement()
        {
            foreach (var player in Player.List)
            {
                if (player.IsAlive || player == Owner || player.CheckPermission("uafk.ignore")  || player.UserId.Contains("@server"))
                    continue;

                return player;
            }

            return null;
        }
        
        /// <summary>
        /// Add player data to ReplacingPlayers dictionary.
        /// </summary>
        private void AddData(Player player, Player replacement, bool is079 = false)
        {
            try
            {
                if (is079)
                {
                    if (player.Role.Base is Scp079Role scp079Role && scp079Role.SubroutineModule.TryGetComponent(out Scp079TierManager tierManager)
                                                                 && scp079Role.SubroutineModule.TryGetSubroutine(out Scp079AuxManager energyManager))
                    {
                        MainHandler.ReplacingPlayers.Add(replacement, new AFKData
                        {
                            NickName = player.Nickname,
                            Position = player.Position,
                            Role = player.Role,
                            Ammo = null,
                            Health = player.Health,
                            Items = player.GetItems(),
                            SCP079 = new Scp079Data
                            {
                                Role = scp079Role,
                                Energy = energyManager.CurrentAux,
                                Experience = tierManager.TotalExp,
                            }
                        });
                    }
                
                    return;
                }
            
                // If I make Ammo = player.ReferenceHub.inventory.UserInventory.ReserveAmmo for some reason it gets buggy and ammo becomes null when changing the player to spectator.
                // So I create a temporary dictionary stored in cache (ram) and then clean the information by deleting it from the ReplacingPlayers.
                var ammo = GetAmmo(player);
                MainHandler.ReplacingPlayers.Add(replacement, new AFKData
                {
                    NickName = player.Nickname,
                    Position = player.Position,
                    Role = player.Role,
                    Ammo = ammo,
                    Health = player.Health,
                    Items = player.GetItems(),
                    SCP079 = new Scp079Data
                    {
                        Role = null,
                        Energy = 0f,
                        Experience = 0
                    }
                });
            }
            catch (Exception e)
            {
                Log.Error($"Error on {nameof(AddData)}: {e.Data} -- {e.StackTrace}");
            }
        }
        
        /// <summary>
        /// Cache player's ammunition
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private Dictionary<ItemType, ushort> GetAmmo(Player player)
        {
            var result = new Dictionary<ItemType, ushort>();

            foreach (var ammo in player.ReferenceHub.inventory.UserInventory.ReserveAmmo)
            {
                result.Add(ammo.Key, ammo.Value);
            }

            return result;
        }

        #endregion
        
    }
}