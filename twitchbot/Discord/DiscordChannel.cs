using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;

namespace twitchbot.Discord
{
    public class DiscordChannel : Channel
    {
        public DiscordChannel(Bot bot)
            : base(bot)
        {

        }

        public override ChannelType Type
        {
            get
            {
                return ChannelType.Discord;
            }
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }

        public override void Disconnect()
        {
            throw new NotImplementedException();
        }

        public override void Load()
        {
            throw new NotImplementedException();
        }

        public override void Save()
        {
            throw new NotImplementedException();
        }

        public override void Say(string message)
        {
            throw new NotImplementedException();
        }

        public override void Say(string message, bool slashMe, bool force)
        {
            throw new NotImplementedException();
        }

        public override void SayMe(string message)
        {
            throw new NotImplementedException();
        }

        public override void SayRaw(string message)
        {
            throw new NotImplementedException();
        }

        public override void SayRaw(string message, bool force)
        {
            throw new NotImplementedException();
        }

        public override void TryWhisperUser(User u, string message)
        {
            throw new NotImplementedException();
        }
    }
}
