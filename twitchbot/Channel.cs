using Meebey.SmartIrc4net;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public abstract class Channel
    {
        public Bot Bot { get; private set; }
        public ConcurrentDictionary<string, User> Users = new ConcurrentDictionary<string, User>();
        public List<Tuple<DateTime, Action>> DelayedActions { get; set; } = new List<Tuple<DateTime, Action>>();
        public ConcurrentQueue<Tuple<DateTime, string, string>> UserCommandCache { get; private set; } = new ConcurrentQueue<Tuple<DateTime, string, string>>();

        public abstract ChannelType Type { get; }

        public abstract void Say(string message);
        public abstract void SayMe(string message);
        public abstract void Say(string message, bool slashMe, bool force);
        public abstract void SayRaw(string message);
        public abstract void SayRaw(string message, bool force);

        public abstract void Connect();
        public abstract void Disconnect();

        public abstract void Save();
        public abstract void Load();


        public abstract void TryWhisperUser(User u, string message);

        public void Say10(string message)
        {
            for (int i = 0; i < 10; i++)
            {
                Say(message);
            }
        }

        public event EventHandler<MessageEventArgs> MessageReceived;

        public void TriggerMessageReceived(MessageEventArgs args)
        {
            MessageReceived?.Invoke(this, args);
        }

        // USERS
        public User GetUser(string user)
        {
            return Users[user.ToLower().Trim()];
        }

        public User GetOrCreateUser(string user)
        {
            User u;
            if (!Users.TryGetValue(user, out u))
            {
                u = new User()
                {
                    Name = user
                };
                Users[user] = u;
            }
            return u;
        }

        public User GetUserOrDefault(string user)
        {
            User u;
            return Users.TryGetValue(user, out u) ? u : null;
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

        // RAFFLES
        public ConcurrentDictionary<string, User> RaffleUsers { get; } = new ConcurrentDictionary<string, User>();

        public bool RaffleActive { get; set; } = false;

        // TRADES
        public TimeSpan TradeTimeout { get; set; } = TimeSpan.FromMinutes(30);

        public List<TradeItem> Trades { get; private set; } = new List<TradeItem>();

        public class TradeItem
        {
            public IEnumerable<Tuple<ShopItem, long>> Gives { get; set; }
            public IEnumerable<Tuple<ShopItem, long>> Wants { get; set; }

            public DateTime ExpireDate { get; set; }
            public string User { get; set; }
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
            {
                Duels.RemoveAll(d => d.ExpireDate < now);
            }

            lock (Trades)
            {
                Trades.RemoveAll(d => d.ExpireDate < now);
            }

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

            lock (DelayedActions)
            {
                for (int i = 0; i < DelayedActions.Count; i++)
                {
                    var item = DelayedActions[i];
                    if (item.Item1 < now)
                    {
                        item.Item2();
                        DelayedActions.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        // query an action to be executes soon
        public void QueueAction(double seconds, Action action)
        {
            lock (DelayedActions)
            {
                DelayedActions.Add(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(seconds), action));
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
