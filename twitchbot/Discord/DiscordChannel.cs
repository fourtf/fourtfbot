using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using System.IO;
using System.IO.Compression;
using System.Collections.Concurrent;

namespace twitchbot.Discord2
{
    public class DiscordChannel : Channel
    {
        public DiscordClient Client { get; private set; }
        public Discord.Channel Channel { get; private set; }

        System.Timers.Timer userUpdateTimer = new System.Timers.Timer(5 * 1000 * 60);

        public ulong ID { get; private set; }

        public DiscordChannel(Bot bot, DiscordClient client, ulong id)
            : base(bot)
        {
            ID = id;
            Client = client;

            userUpdateTimer.Elapsed += (s, e) =>
            {
                Client.GetChannel(id).Process(c => c.Users.Do(u => GetOrCreateUser(u.Id.ToString(), u.Name).Points += 10));
            };

            userUpdateTimer.Start();
        }

        public override ChannelType Type
        {
            get
            {
                return ChannelType.Discord;
            }
        }

        public override ConcurrentDictionary<string, User> UsersByName
        {
            get
            {
                return Bot.DiscordUsersByName;
            }
        }

        public override ConcurrentDictionary<string, User> UsersByID
        {
            get
            {
                return Bot.DiscordUsersByID;
            }
        }

        public override string LongName
        {
            get
            {
                return $"the channel \"{Channel.Name}\"";
            }
        }

        public override string ChannelSaveID
        {
            get
            {
                return "discord." + ID;
            }
        }

        public override void Connect()
        {
            
        }

        public override void Disconnect()
        {
            
        }

        public override void Say(string message, bool slashMe, bool force)
        {
            Client.GetChannel(ID)?.SendMessage(message);
        }

        public override void SayRaw(string message, bool force)
        {
            Client.GetChannel(ID)?.SendMessage(message);
        }

        public override void TryWhisperUser(User u, string message)
        {
            Client.GetChannel(ID)?.SendMessage(u.Name + ", " + message);
        }

        public override bool IsOwner(User user)
        {
            return Bot.DiscordOwner == user.ID;
        }
    }
}
