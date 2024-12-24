using Interactables.Interobjects;
using InventorySystem;
using InventorySystem.Items;
using InventorySystem.Items.Firearms;
using InventorySystem.Items.Firearms.Attachments;
using NWAPIPermissionSystem;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.Spectating;
using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using InventorySystem.Items.Firearms.Modules;
using UltimateAFK.API.Components;
using UltimateAFK.API.Structs;

namespace UltimateAFK.API
{
    /// <summary>
    /// A collection of extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Gets a list of all Elevators in the facility.
        /// </summary>
        public static List<ElevatorChamber> AllElevators = new(Map.Elevators);

        /// <summary>
        /// Determines whether the round has ended.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the round has ended; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsRoundEnded()
        {
            return !Round.IsRoundStarted && Round.Duration.TotalSeconds > 0;
        }

        /// <summary>
        /// Obtains a list of ItemType instances representing the items held by the player, excluding ammo.
        /// </summary>
        /// <param name="player">The player whose items are to be retrieved.</param>
        /// <returns>A list of ItemType instances representing the non-ammo items held by the player.</returns>
        public static List<ItemType> GetItemTypes(this Player player)
        {
            // Retrieve the items from the player's inventory
            var playerItem = player.ReferenceHub.inventory.UserInventory.Items.Values;

            // Create a list to store the resulting ItemType
            List<ItemType> result = new List<ItemType>();

            foreach (var item in playerItem)
            {
                // Exclude items with the Ammo category
                if (item.Category == ItemCategory.Ammo)
                    continue;
                // Add the ItemType of the item to the result list
                result.Add(item.ItemTypeId);
            }

            // Return the list of non-ammo ItemType
            return result;
        }

        /// <summary>
        /// Retrieves a dictionary representing the ammunition held by the player.
        /// </summary>
        /// <param name="player">The player whose ammunition is to be retrieved.</param>
        /// <returns>A dictionary where keys are ItemType instances and values are the corresponding ammunition amounts.</returns>
        public static Dictionary<ItemType, ushort> GetAmmo(this Player player)
        {
            // Retrieve the ammunition from the player's inventory and create a copy using ToDictionary
            Dictionary<ItemType, ushort> result = player.ReferenceHub.inventory.UserInventory.ReserveAmmo.ToDictionary(entry => entry.Key, entry => entry.Value);

            return result;
        }

        /// <summary>
        /// Replaces the player with another.
        /// </summary>
        /// <param name="player">The player to be replaced.</param>
        /// <param name="role">The role type of the player to be replaced.</param>
        /// <param name="countForKicl">If this is true will be counted for kick if reach the max afk times</param>
        public static void Replace(this Player player, RoleTypeId role, bool countForKicl = false)
        {
            if (!player.TryGetComponent<AfkCheckComponent>(out var component)) 
                return;
            
            string ownerUserId = player.UserId;
            int afkTimes = component.GetAfkTimes();

            // Check if role is blacklisted
            if (EntryPoint.Instance.Config.RoleTypeBlacklist?.Contains(role) == true)
            {
                Log.Debug($"player {player.Nickname} ({player.UserId}) has a role that is blacklisted so he will not be searched for a replacement player", EntryPoint.Instance.Config.DebugMode);

                player.ClearInventory();
                player.SetRole(RoleTypeId.Spectator);

                if (EntryPoint.Instance.Config.AfkCount > -1 && countForKicl)
                {

                    if (++afkTimes >= EntryPoint.Instance.Config.AfkCount)
                    {
                        player.SendConsoleMessage(EntryPoint.Instance.Config.MsgKick, "white");
                        player.Kick(EntryPoint.Instance.Config.MsgKick);
                        return;
                    }
                }

                component.SetAfkTimes(afkTimes);

                player.SendBroadcast(EntryPoint.Instance.Config.MsgFspec, 30, shouldClearPrevious: true);
                player.SendConsoleMessage(EntryPoint.Instance.Config.MsgFspec, "white");
                return;
            }

            // Get a replacement player
            Player? replacement = GetReplacement(ownerUserId);

            if (replacement == null)
            {
                if (EntryPoint.Instance.Config.DisableReplacement)
                    Log.Debug("AFK player replacement is disabled by the plugin configuration.", EntryPoint.Instance.Config.DebugMode);
                else
                    Log.Debug("Unable to find replacement player, moving to spectator...", EntryPoint.Instance.Config.DebugMode);

                player.ClearInventory();

                if (player.IsSCP)
                    player.ReferenceHub.roleManager.ServerSetRole(RoleTypeId.Spectator, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
                else
                    player.SetRole(RoleTypeId.Spectator);

                if (EntryPoint.Instance.Config.AfkCount > -1 && countForKicl)
                {
                    afkTimes++;

                    if (afkTimes >= EntryPoint.Instance.Config.AfkCount)
                    {
                        player.SendConsoleMessage(EntryPoint.Instance.Config.MsgKick, "white");

                        player.Kick(EntryPoint.Instance.Config.MsgKick);

                        return;
                    }
                }

                player.SendBroadcast(EntryPoint.Instance.Config.MsgFspec, 30, shouldClearPrevious: true);
                player.SendConsoleMessage(EntryPoint.Instance.Config.MsgFspec, "white");
            }
            else
            {
                Log.Debug($"Replacement Player found: {replacement.LogName}", EntryPoint.Instance.Config.DebugMode);

                SaveData(player, replacement.UserId, role is RoleTypeId.Scp079);

                if (EntryPoint.Instance.Config.AfkCount > -1 && countForKicl)
                {

                    if (++afkTimes >= EntryPoint.Instance.Config.AfkCount)
                    {
                        player.ClearInventory();
                        player.SendConsoleMessage(EntryPoint.Instance.Config.MsgKick, "white");
                        player.Kick(EntryPoint.Instance.Config.MsgKick);
                        replacement.SetRole(role);
                        return;
                    }
                }

                component.SetAfkTimes(afkTimes);
                Log.Debug($"Cleaning player {player.Nickname} inventory", EntryPoint.Instance.Config.DebugMode);
                // Clear player inventory
                player.ClearInventory();
                //Send player a broadcast for being too long afk
                player.SendBroadcast(EntryPoint.Instance.Config.MsgFspec, 25, shouldClearPrevious: true);
                player.SendConsoleMessage(EntryPoint.Instance.Config.MsgFspec, "white");

                // Sends replacement to the role that had the afk
                Log.Debug($"Changing replacement player {replacement.LogName} role to {role}", EntryPoint.Instance.Config.DebugMode);
                replacement.SetRole(role);
                // Sends player to spectator
                Log.Debug($"Changing player {player.Nickname} to spectator", EntryPoint.Instance.Config.DebugMode);

                if (player.IsSCP)
                    player.ReferenceHub.roleManager.ServerSetRole(RoleTypeId.Spectator, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
                else
                    player.SetRole(RoleTypeId.Spectator);

                player.SendConsoleMessage(string.Format(EntryPoint.Instance.Config.MsgReplaced, replacement.Nickname), "white");
            }
        }

        /// <summary>
        /// Checks if the specified <see cref="ItemType"/> is ammo.
        /// </summary>
        /// <param name="type">The <see cref="ItemType"/> to check.</param>
        /// <returns><see langword="true"/> if the <see cref="ItemType"/> is ammo; otherwise, <see langword="false"/>.</returns>
        public static bool IsAmmo(this ItemType type)
        {
            var itemBase = type.GetItemBase();
            return itemBase?.Category == ItemCategory.Ammo;
        }

        /// <summary>
        /// Retrieves the base item associated with a specific item type.
        /// </summary>
        /// <param name="type">The item type for which the base item is requested.</param>
        /// <returns>
        /// The base item associated with the specified item type if available; otherwise, returns null.
        /// </returns>
        public static ItemBase? GetItemBase(this ItemType type)
        {
            if (!InventoryItemLoader.AvailableItems.TryGetValue(type, out ItemBase @base))
                return null;

            return @base;
        }

        /// <summary>
        /// Gives the ammunition you want a player to have.
        /// </summary>
        public static void SendAmmo(this Player ply, Dictionary<ItemType, ushort> ammo)
        {
            foreach (var ammoItem in ammo)
            {
                ply.SetAmmo(ammoItem.Key, ammoItem.Value);
            }
        }

        /// <summary>
        /// Reloads all player <see cref="Firearm"/>'s
        /// </summary>
        /// <param name="ply"></param>
        public static void ReloadAllWeapons(this Player ply)
        {
            var item = ply.Items.Where(i => i is Firearm);

            foreach (ItemBase? weapon in item)
            {
                if (weapon is not Firearm firearm) 
                    continue;
                
                if (firearm.Modules.FirstOrDefault(m => m is MagazineModule) is MagazineModule magazineModule)
                {
                    magazineModule.AmmoStored = magazineModule.AmmoMax;
                }
            }
        }

        /// <summary>
        /// Adds several items at the same time to a player.
        /// </summary>
        public static void SendItems(this Player player, List<ItemType> types)
        {
            if (types.IsEmpty())
                return;

            foreach (var item in types)
            {
                player.AddItem(item);
            }
        }

        /// <summary>
        /// Applies player attachments preferences.
        /// </summary>
        /// <param name="ply"></param>
        public static void ApplyAttachments(this Player ply)
        {
            var item = ply.Items.Where(i => i is Firearm);

            foreach (ItemBase? fire in item)
            {
                if (fire is not Firearm fireArm) 
                    continue;
                
                if (AttachmentsServerHandler.PlayerPreferences.TryGetValue(ply.ReferenceHub, out var value) && value.TryGetValue(fireArm.ItemTypeId, out var value2))
                    fireArm.ApplyAttachmentsCode(value2, reValidate: true);
            }
        }

        /// <summary>
        /// Gets if the current player is in a elevator.
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public static bool InElevator(this Player player)
        {
            foreach (var elevator in AllElevators)
            {
                if (elevator.WorldspaceBounds.Contains(player.Position) && elevator.IsMoving())
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if the current <see cref="ElevatorChamber"/> is moving.
        /// </summary>
        /// <param name="elevator"></param>
        /// <returns></returns>
        public static bool IsMoving(this ElevatorChamber elevator) => elevator.CurSequence is ElevatorChamber.ElevatorSequence.MovingAway or ElevatorChamber.ElevatorSequence.Arriving;
        
        /// <summary>
        /// Gets the player to be used as a replacement, typically the longest-active spectator.
        /// </summary>
        /// <returns>The player to be used as a replacement, or <c>null</c> if no suitable replacement is found.</returns>
        private static Player? GetReplacement(string ownerUserId)
        {
            try
            {
                // Return null if replacement is disable
                if (EntryPoint.Instance.Config.DisableReplacement)
                    return null;

                Player? longestSpectator = null;
                float maxActiveTime = 0f;

                // Find the longest-active spectator among non-ignored players
                foreach (var player in Player.GetPlayers().Where(p => !IgnorePlayer(p, ownerUserId)))
                {
                    if (player.Role == RoleTypeId.Spectator && player.RoleBase is SpectatorRole spectatorRole)
                    {
                        // Update the longest spectator if the current one has a longer active time
                        if (spectatorRole.ActiveTime > maxActiveTime)
                        {
                            maxActiveTime = spectatorRole.ActiveTime;
                            longestSpectator = player;
                        }
                    }
                }

                return longestSpectator;
            }
            catch (Exception e)
            {
                // Log and handle any exceptions that occur during the process
                Log.Error($"Error in {nameof(GetReplacement)}: {e.Message} || Type: {e.GetType()}");
                return null;
            }
        }

        /// <summary>
        /// Determines whether the specified player should be ignored in the context of AFK checks.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="ownerUserId"></param>
        /// <returns>
        ///   <c>true</c> if the player should be ignored; otherwise, <c>false</c>.
        /// </returns>
        private static bool IgnorePlayer(Player player, string ownerUserId)
        {
            // Check various conditions to determine if the player should be ignored
            return !player.IsReady || // Player is not ready
                   EntryPoint.Instance.Config.UserIdIgnored.Contains(ownerUserId) || // Player's user ID is in the ignored list
                   player.TemporaryData.StoredData.ContainsKey("uafk_disable") || // Player has AFK checking disabled
                   player.UserId == ownerUserId || // Player is the same as the owner
                   player.IsAlive || // Player is alive
                   player.CheckPermission("uafk.ignore") || // Player has the uafk.ignore permission
                   player.IsServer || // Player is a server
                   player.UserId.Contains("@server") || // Player's user ID contains "@server"
                   player.UserId.Contains("@npc") || // Player's user ID contains "@npc"
                   MainHandler.ReplacingPlayersData.TryGetValue(player.UserId, out _); // Player is being replaced
        }

        private static void SaveData(Player owner, string replacementUserId, bool isScp079 = false)
        {
            AfkData data;

            if (isScp079)
            {
                if (owner.RoleBase is Scp079Role scp079Role && scp079Role.SubroutineModule.TryGetSubroutine(out Scp079TierManager tierManager)
                    && scp079Role.SubroutineModule.TryGetSubroutine(out Scp079AuxManager energyManager))
                {
                    data = new AfkData(owner.Nickname, owner.Position, owner.Role, null, new(), owner.Health, new(tierManager.TotalExp, energyManager.CurrentAux));
                }
                else
                {
                    // SCP-079 data cannot be obtained
                    data = new AfkData(owner.Nickname, owner.Position, owner.Role, null, new(), owner.Health, null);
                }
            }
            else
            {
                data = new AfkData(owner.Nickname, owner.Position, owner.Role, owner.GetAmmo(), owner.GetItemTypes(), owner.Health, null);
            }

            MainHandler.ReplacingPlayersData.Add(replacementUserId, data);
        }
    }
}
