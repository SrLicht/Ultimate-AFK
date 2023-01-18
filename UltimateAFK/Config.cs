using System.Collections.Generic;
using System.ComponentModel;
using Exiled.API.Interfaces;
using PlayerRoles;

namespace UltimateAFK
{
    public class Config : IConfig
    {
        [Description("Setting this to false will stop the plugin from working in the next round.")]
        public bool IsEnabled { get; set; } = true;

        [Description("If you have any error in the plugin operation activate this and create an Issue in Github https://github.com/SrLicht/Ultimate-AFK/issues")]
        public bool Debug { get; set; } = false;

        [Description("This bool activates the logs that easily spams the console, and normally they are not required, that's why they have this separate configuration :)")]
        public bool SpamLogs { get; set; } = false;

        [Description("When a player is replaced it is called a delay, if when replacing the player the position is not updated correctly, increase this value but it must not exceed 2.5 since it would be too long.")]
        public float ReplaceDelay { get; set; } = 1.4f;

        [Description("If the number of players is less than this the plugin will not work.")]
        public int MinPlayers { get; set; } = 8;

        [Description("Tutorials should be ignored ?")]
        public bool IgnoreTut { get; set; } = true;

        [Description("RoleTypes on this list will not be replaced by other players")]
        public List<RoleTypeId> RoleTypeBlacklist { get; set; } = new() { RoleTypeId.Scp0492 };

        [Description("The time it takes for a player to stand still before he is detected as AFK")]
        public int AfkTime { get; set; } = 80;

        [Description("After being detected as AFK a message will appear on his face and he will be given this time to move or he will be Kicked/Moved to spectator.")]
        public int GraceTime { get; set; } = 30;

        [Description("The number of times a player must be moved to spectator for a player to be kicked from the server. Use -1 to disable it")]
        public int AfkCount { get; set; } = -1;

        [Description("When the player is detected as AFK and is in grace period this message will appear on his face. {0} represents the seconds the player has to move or be moved to spectator.")]
        public string MsgGrace { get; set; } = "<color=white>[</color><color=green>Ultimate-AFK</color><color=white>]</color> <color=red>You will be moved to spectator if you do not move in less than <color=white>{0}</color> seconds.</color>";

        [Description("This message will be sent to the player who has been moved to spectator when he is detected as AFK, it is also sent to the player's console.")]
        public string MsgFspec { get; set; } = "<color=red>You were detected as AFK and were moved to spectator</color>";

        [Description("This will be the reason for the Kick, due to the VSR it is obligatory to clarify that it is a plugin with flags like [UltimateAFK] or something similar.")]
        public string MsgKick { get; set; } = "[Ultimate-AFK] You were removed from the server for being AFK for too long.!";

        [Description("When a player replaces another player, this message will appear on the player's face and on the player console. | {0} it is the name of the player who was afk")]
        public string MsgReplace { get; set; } = "<color=red> You replaced {0} who was afk.</color>";
    }
}