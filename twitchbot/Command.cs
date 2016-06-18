﻿using DynamicExpresso;
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

        public Func<string, User, Channel, bool> Action { get; private set; }
        public bool AdminOnly { get; set; } = false;
        public bool ModOnly { get; set; } = false;
        public bool OwnerOnly { get; set; } = false;

        public bool AllowOtherCommands { get; private set; }

        public TimeSpan Cooldown { get; set; }
        public bool HasUserCooldown { get; set; }
        public DateTime LastUsed { get; set; } = DateTime.MinValue;

        public Command(string name, Action<string, User, Channel> action, bool adminOnly = false, bool modOnly = false, bool ownerOnly = false, bool hasUserCooldown = true, TimeSpan? cooldown = null, bool allowOtherCommands = false)
            : this(name, (m, u, c) => { action(m, u, c); return true; }, adminOnly, modOnly, ownerOnly, hasUserCooldown, cooldown)
        {

        }

        public Command(string name, Func<string, User, Channel, bool> action, bool adminOnly = false, bool modOnly = false, bool ownerOnly = false, bool hasUserCooldown = true, TimeSpan? cooldown = null, bool allowOtherCommands = false)
        {
            Name = name;
            Action = action;
            AdminOnly = adminOnly;
            ModOnly = modOnly;
            OwnerOnly = ownerOnly;
            Cooldown = cooldown ?? new TimeSpan(0, 0, 2);
            HasUserCooldown = hasUserCooldown;
        }
    }

    public class EvalCommand : Command
    {
        public string Expression { get; private set; }

        public EvalCommand(Bot bot, string name, string expression)
            : base(name, (m, u, c) =>
            {
                try
                {
                    object o = bot.Interpreter.Eval(expression, new Parameter("C", c), new Parameter("U", u), new Parameter("M", m), new Parameter("S", m.SplitWords()));

                    if (o != null)
                        c.Say(o.ToString());
                }
                catch (Exception exc) { c.Say(exc.Message); }
            })
        {
            Expression = expression;
        }
    }
}
