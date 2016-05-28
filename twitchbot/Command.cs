using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class Command
    {
        public string Name { get; private set; }

        public Action<string, User, Channel> Action { get; private set; }
        public bool AdminOnly { get; set; } = false;
        public bool ModOnly { get; set; } = false;

        public bool AllowOtherCommands { get; private set; }

        public TimeSpan Cooldown { get; set; }
        public bool HasUserCooldown { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.MinValue;

        public Command(string name, Action<string, User, Channel> action, bool adminOnly = false, bool modOnly = false, bool hasUserCooldown = true, TimeSpan? cooldown = null, bool allowOtherCommands = false)
        {
            Name = name;
            Action = action;
            AdminOnly = adminOnly;
            ModOnly = modOnly;
            Cooldown = cooldown ?? new TimeSpan(0, 0, 2);
            AllowOtherCommands = allowOtherCommands;
            HasUserCooldown = hasUserCooldown;
        }
    }
}
