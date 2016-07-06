using Meebey.SmartIrc4net;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static twitchbot.Bot;

namespace twitchbot
{
    public abstract class Channel
    {
        public ChannelSettings Settings { get; private set; } = new ChannelSettings();

        public abstract ConcurrentDictionary<string, User> UsersByName { get; }
        public abstract ConcurrentDictionary<string, User> UsersByID { get; }

        public Bot Bot { get; private set; }
        public ConcurrentQueue<Tuple<DateTime, string, string>> UserCommandCache { get; private set; } = new ConcurrentQueue<Tuple<DateTime, string, string>>();

        public ConcurrentDictionary<string, int> ModReffleValueAvailable { get; private set; } = new ConcurrentDictionary<string, int>();
        public ConcurrentDictionary<string, DateTime> CommandDescriptionCooldown { get; private set; } = new ConcurrentDictionary<string, DateTime>();
        public TimeSpan DefaultCommandDescriptionCooldown = TimeSpan.FromMinutes(0.75);

        public bool CanPostCommandDescription(string command)
        {
            DateTime time;
            if (CommandDescriptionCooldown.TryGetValue(command, out time))
            {
                if (time > DateTime.Now)
                {
                    return false;
                }
            }
            CommandDescriptionCooldown[command] = DateTime.Now + DefaultCommandDescriptionCooldown;
            return true;
        }

        public abstract string ChannelSaveID { get; }

        public List<EvalCommand> ChannelEvalCommands = new List<EvalCommand>();

        public int Max { get; set; }

        public abstract string LongName { get; }

        public abstract ChannelType Type { get; }

        public abstract bool IsOwner(User user);

        public abstract void Say(string message, bool slashMe, bool force);
        public abstract void SayRaw(string message, bool force);

        public PyramidType PyramidType { get; set; } = PyramidType.None;
        public int PyramidWidth { get; set; } = 0;
        public int PyramidTempValue { get; set; } = 0;
        public int PyramidHeight { get; set; }
        public TwitchEmote PyramidEmote { get; set; }


        public void Say(string message)
        {
            Say(message, false, false);
        }

        public void SayMe(string message)
        {
            Say(message, true, false);
        }

        public void SayRaw(string message)
        {
            SayRaw(message, false);
        }

        public Channel Wait(double time)
        {
            Thread.Sleep((int)(time * 1000));
            return this;
        }

        public abstract void Connect();
        public abstract void Disconnect();

        public virtual void Save()
        {
            File.WriteAllLines($"./db/{ChannelSaveID}.evalcommands.txt", ChannelEvalCommands.Select(c => (c.AdminOnly ? "%" : "") + (c.IgnoreExceptions ? "!" : "") + c.Name + "=" + c.Expression));
            Settings.Save($"./db/{ChannelSaveID}.settings.ini");
            lock (BetEntries)
            {
                try
                {
                    if (BetEntries.Count == 0)
                    {
                        if (File.Exists($"./db/{ChannelSaveID}.bets.ini"))
                            File.Delete($"./db/{ChannelSaveID}.bets.ini");
                    }
                    else
                    {
                        File.WriteAllLines($"./db/{ChannelSaveID}.bets.ini", new string[] { CurrentBetName }.Concat(BetEntries.Select(x => x.UserID + ":" + x.Points + ":" + x.Score.Item1 + ":" + x.Score.Item2)));
                    }
                }
                catch { }
            }
        }

        public virtual void Load()
        {
            try
            {
                lock (ChannelEvalCommands)
                {
                    if (File.Exists($"./db/{ChannelSaveID}.evalcommands.txt"))
                    {
                        File.ReadAllLines($"./db/{ChannelSaveID}.evalcommands.txt").Do(line =>
                        {
                            try
                            {
                                int index;
                                if ((index = line.IndexOf('=')) != -1)
                                {
                                    string name = line.Remove(index);
                                    ChannelEvalCommands.Add(new EvalCommand(Bot, name.TrimStart('%', '!'), line.Substring(index + 1), name.IndexOf('!') != -1) { AdminOnly = name.IndexOf('%') != -1 });
                                }
                            }
                            catch
                            {

                            }
                        });
                    }
                }
            }
            catch
            {

            }
            Settings.Load($"./db/{ChannelSaveID}.settings.ini");

            try
            {
                if (File.Exists($"./db/{ChannelSaveID}.bets.ini"))
                {
                    using (var r = new StreamReader($"./db/{ChannelSaveID}.bets.ini"))
                    {
                        CurrentBetName = r.ReadLine();
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            var S = line.Split(':');
                            lock (BetEntries)
                            {
                                BetEntries.Add(new SportsBetItem { UserID = S[0], Points = long.Parse(S[1]), Score = Tuple.Create(int.Parse(S[2]), int.Parse(S[3])) });
                            }
                        }
                    }
                }
            }
            catch {; }
        }

        public abstract void TryWhisperUser(User u, string message);

        public event EventHandler<MessageEventArgs> MessageReceived;

        public void TriggerMessageReceived(MessageEventArgs args)
        {
            MessageReceived?.Invoke(this, args);
        }

        // USERS
        public virtual User GetOrCreateUser(string id, string name)
        {
            User u;
            if (!UsersByID.TryGetValue(id, out u))
            {
                u = new User()
                {
                    Name = name,
                    ID = id
                };
                UsersByID[id] = u;
                if (!UsersByName.ContainsKey(name.ToLower()))
                    UsersByName[name.ToLower()] = u;
            }
            u.Name = name;
            return u;
        }

        public virtual User GetUserOrDefaultByName(string id)
        {
            User u;
            return UsersByName.TryGetValue(id, out u) ? u : null;
        }

        public TimeSpan UserCommandCooldown = TimeSpan.FromSeconds(13);

        // DUELS
        public TimeSpan DuelTimeout { get; set; } = TimeSpan.FromMinutes(30);

        public List<DuelItem> Duels { get; private set; } = new List<DuelItem>();

        public struct DuelItem
        {
            public ShopItem Item { get; set; }
            public long Count { get; set; }
            public DateTime ExpireDate { get; set; }
            public string FromUser { get; set; }
            public string ToUser { get; set; }
        }

        // HIGH5
        public List<High5Item> High5s = new List<High5Item>();

        public class High5Item
        {
            public User From { get; set; }
            public User To { get; set; }
            public DateTime ExpireDate { get; set; }
        }

        // SOCCER BETS
        public string CurrentBetName { get; set; } = null;
        public bool BetClosed { get; set; } = true;
        public List<SportsBetItem> BetEntries = new List<SportsBetItem>();

        public class SportsBetItem
        {
            public string UserID { get; set; }
            public long Points { get; set; }
            public Tuple<int, int> Score { get; set; }
        }

        // RAFFLES
        public ConcurrentDictionary<string, User> RaffleUsers { get; } = new ConcurrentDictionary<string, User>();

        public bool RaffleActive { get; set; } = false;

        // VOTES
        public ConcurrentDictionary<User, string> VoteUsers { get; } = new ConcurrentDictionary<User, string>();

        public bool VoteActive { get; set; } = false;
        public string[] CurrentVoteEmotes = new string[0];

        // TRADES
        public TimeSpan TradeTimeout { get; set; } = TimeSpan.FromMinutes(30);

        public List<TradeItem> Trades { get; private set; } = new List<TradeItem>();

        public class TradeItem
        {
            public IEnumerable<Tuple<ShopItem, long>> Gives { get; set; }
            public IEnumerable<Tuple<ShopItem, long>> Wants { get; set; }

            public DateTime ExpireDate { get; set; }
            public User User { get; set; }
        }

        // TIMER
        protected System.Timers.Timer secondsTimer = new System.Timers.Timer(1000);

        public Channel(Bot bot)
        {
            Bot = bot;
            secondsTimer.Elapsed += secondTick;
            secondsTimer.Start();
        }

        protected void secondTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;

            lock (Duels)
                Duels.RemoveAll(d => d.ExpireDate < now);

            lock (Trades)
                Trades.RemoveAll(d => d.ExpireDate < now);

            lock (High5s)
                High5s.RemoveAll(d => d.ExpireDate < now);

            {
                Tuple<DateTime, string, string> cacheItem;

                while (UserCommandCache.TryPeek(out cacheItem))
                {
                    if (cacheItem.Item1 < now)
                        UserCommandCache.TryDequeue(out cacheItem);
                    else
                        break;
                }
            }
        }
    }

    public enum ChannelType
    {
        Other,
        Twitch,
        Discord,
    }

    public class MessageEventArgs : EventArgs
    {
        public Channel Channel { get; private set; }
        public User User { get; private set; }
        public string UserID { get; private set; }
        public string Message { get; private set; }

        string[] lowerSplitMessage = null;
        public string[] LowerSplitMessage
        {
            get { return lowerSplitMessage ?? (lowerSplitMessage = Message.Split()); }
        }

        public MessageEventArgs(Channel channel, User user, string userID, string message)
        {
            Channel = channel;
            User = user;
            UserID = userID;
            Message = message;
        }
    }
}
