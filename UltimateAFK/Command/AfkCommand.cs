using CommandSystem;
using CustomPlayerEffects;
using MapGeneration;
using MEC;
using PlayerRoles;
using PluginAPI.Core;
using System;
using System.Collections.Generic;
using UltimateAFK.API;
using UnityEngine;

namespace UltimateAFK.Command
{
    [CommandHandler(typeof(ClientCommandHandler))]
    internal sealed class AfkCommand : ICommand
    {
        public bool SanitizeResponse => false;
        public string Command { get; } = "afk";
        public string[] Aliases { get; } = Array.Empty<string>();
        public string Description { get; } = "By using this command you will be moved to spectator and if the server allows it a player will replace you.";

        public Dictionary<string, float> InCooldown = new();

        public static Dictionary<string, int> CommandUses = new();
        public static List<string> InProcess = new();
        public static List<CoroutineHandle> AllCoroutines = new();

        private Config PluginConfig => EntryPoint.Instance.Config;

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            try
            {
                if (!PluginConfig.CommandConfig.IsEnabled)
                {
                    response = PluginConfig.CommandConfig.Responses.OnDisable;
                    return false;
                }
                if (!Round.IsRoundStarted)
                {
                    response = PluginConfig.CommandConfig.Responses.OnRoundIsNotStarted;
                    return false;
                }

                var ply = Player.Get(sender);
                if (ply == null || !ply.IsReady)
                {
                    response = "The player is not fully connected to the server. you cannot use this command, please try to reconnect to the server.";
                    return false;
                }
                if (!ply.IsAlive)
                {
                    response = PluginConfig.CommandConfig.Responses.OnPlayerIsDead;
                    return false;
                }
                if (ply.Zone == FacilityZone.Other || ply.EffectsManager.TryGetEffect<Corroding>(out var corroding) && corroding.IsEnabled)
                {
                    response = PluginConfig.CommandConfig.Responses.OnPocketDimension;
                    return false;
                }
                if (PluginConfig.CommandConfig.DisableForCertainRole && PluginConfig.CommandConfig.RoleTypeIdBlackList.Contains(ply.Role))
                {
                    response = string.Format(PluginConfig.CommandConfig.Responses.OnBlackListedRole,
                        ply.Role);
                    return false;
                }
                if (PluginConfig.CommandConfig.ExclusiveForGroups &&
                    !PluginConfig.CommandConfig.UserGroupsAllowed.Contains(ply.ReferenceHub.serverRoles.Group.BadgeText))
                {
                    response = PluginConfig.CommandConfig.Responses.OnGroupExclusive;
                    return false;
                }
                if (ply.EffectsManager.TryGetEffect<SeveredHands>(out var severedHands) && severedHands.IsEnabled)
                {
                    response = PluginConfig.CommandConfig.Responses.OnSevereHands;
                    return false;
                }
                if (ply.EffectsManager.TryGetEffect<CardiacArrest>(out var cardiacArrest) && cardiacArrest.IsEnabled)
                {
                    response = PluginConfig.CommandConfig.Responses.OnHearthAttack;
                    return false;
                }
                if (ply.InElevator())
                {
                    response = PluginConfig.CommandConfig.Responses.OnElevatorMoving;
                    return false;
                }
                if (ply.TemporaryData.StoredData.ContainsKey("uafk_disable"))
                {
                    response = PluginConfig.CommandConfig.Responses.OnUafkDisable;
                    return false;
                }
                if (CommandUses.TryGetValue(ply.UserId, out var uses) && PluginConfig.CommandConfig.UseLimitsPerRound > 0 && uses > PluginConfig.CommandConfig.UseLimitsPerRound)
                {
                    response = PluginConfig.CommandConfig.Responses.OnLimit;
                    return false;
                }
                if (InProcess.Contains(ply.UserId))
                {
                    response = string.Format(PluginConfig.CommandConfig.Responses.OnTryToExecute, PluginConfig.CommandConfig.SecondsStill);
                    return false;
                }
                if (InCooldown.TryGetValue(ply.UserId, out var cooldown))
                {
                    // In cooldown
                    if (cooldown >= Time.time)
                    {
                        var cooldownTimer = (int)(cooldown - Time.time);

                        response = string.Format(PluginConfig.CommandConfig.Responses.OnCooldown,
                            cooldownTimer);
                        return false;
                    }
                    else
                    {
                        // Not in cooldown
                        InCooldown.Remove(ply.UserId);
                    }
                }

                AllCoroutines.Add(Timing.RunCoroutine(AfkCountdown(ply, ply.UserId, ply.Role, ply.Position)));
                InCooldown.Add(ply.UserId, Time.time + PluginConfig.CommandConfig.Cooldown);
                response = string.Format(PluginConfig.CommandConfig.Responses.OnSuccess, PluginConfig.CommandConfig.SecondsStill);
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Error on {nameof(AfkCommand)}::{nameof(Execute)}: {e} || Typeof {e.GetType()}");
                response = $"Error on {nameof(AfkCommand)}::{nameof(Execute)}: {e.Message}";
                return false;
            }
        }


        private IEnumerator<float> AfkCountdown(Player player, string userid, RoleTypeId roleType, Vector3 position)
        {
            InProcess.Add(userid);

            for (int i = PluginConfig.CommandConfig.SecondsStill; i >= 0; i--)
            {
                yield return Timing.WaitForSeconds(1);

                if (player == null || !player.IsAlive || !Round.IsRoundStarted)
                {
                    InProcess.Remove(userid);
                    yield break;
                }

                if (position != player.Position)
                {
                    InProcess.Remove(userid);
                    player.SendConsoleMessage(string.Format(PluginConfig.CommandConfig.Responses.OnMoving, PluginConfig.CommandConfig.SecondsStill), "red");
                    player.ReceiveHint(string.Format(PluginConfig.CommandConfig.Responses.OnMoving, PluginConfig.CommandConfig.SecondsStill), 5);
                    yield break;
                }

                string text = string.Format(PluginConfig.CommandConfig.Responses.OnWaitingForAfk, i);
                player.ReceiveHint(text, 1);
                player.SendConsoleMessage(text, "red");
            }

            player.Replace(roleType);

            API.EventArgs.Events.OnDetectedAfkPlayer(new(player, true));

            if (PluginConfig.CommandConfig.UseLimitsPerRound > 0)
                AddUse(userid);

            InProcess.Remove(userid);
        }

        private void AddUse(string userID)
        {
            if (CommandUses.TryGetValue(userID, out var uses))
            {
                CommandUses[userID] = uses + 1;
            }
            else
            {
                CommandUses.Add(userID, 1);
            }
        }

        public static void ClearAllCachedData()
        {
            AllCoroutines.ForEach(c => Timing.KillCoroutines(c));
            AllCoroutines.Clear();
            InProcess.Clear();
            CommandUses.Clear();
        }
    }
}