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
using System.Threading.Tasks;

namespace twitchbot
{
    public abstract class Channel
    {
        public abstract ConcurrentDictionary<string, User> UsersByName { get; }
        public abstract ConcurrentDictionary<string, User> UsersByID { get; }

        public Bot Bot { get; private set; }
        public List<Tuple<DateTime, Action>> DelayedActions { get; set; } = new List<Tuple<DateTime, Action>>();
        public ConcurrentQueue<Tuple<DateTime, string, string>> UserCommandCache { get; private set; } = new ConcurrentQueue<Tuple<DateTime, string, string>>();

        public ConcurrentDictionary<string, int> ModReffleValueAvailable { get; private set; } = new ConcurrentDictionary<string, int>();
        public int Max { get; set; }

        public abstract string LongName { get; }

        public abstract ChannelType Type { get; }

        public abstract bool IsOwner(User user);

        public abstract void Say(string message, bool slashMe, bool force);
        public abstract void SayRaw(string message, bool force);

        public bool IsForsens { get; set; }

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

        public abstract void Connect();
        public abstract void Disconnect();

        public virtual void Save() { }
        //{
        //    try
        //    {
        //        var savePath = UserSavePath;

        //        using (Stream filestream = File.OpenWrite(savePath))
        //        {
        //            using (Stream stream = new GZipStream(filestream, CompressionMode.Compress))
        //            {
        //                stream.WriteByte(1);
        //                bool first = true;
        //                foreach (User u in UsersByName.Values)
        //                {
        //                    if (first)
        //                        first = false;
        //                    else
        //                        stream.WriteByte(0);

        //                    stream.WriteString(u.Name);
        //                    stream.WriteString(u.ID);

        //                    if (u.Calories != 0) { stream.WriteByte(0x01); stream.WriteLong(u.Calories); }
        //                    if (u.MessageCount != 0) { stream.WriteByte(0x02); stream.WriteLong(u.MessageCount); }
        //                    if (u.CharacterCount != 0) { stream.WriteByte(0x03); stream.WriteLong(u.CharacterCount); }
        //                    if (u.Points != 0) { stream.WriteByte(0x04); stream.WriteLong(u.Points); }

        //                    if (u.Flags != 0) { stream.WriteByte(0x05); stream.WriteInt((int)u.Flags); }
        //                    if (u.GachiGASM != 0) { stream.WriteByte(0x06); stream.WriteLong(u.GachiGASM); }

        //                    if (u.Inventory != null)
        //                    {
        //                        lock (u.Inventory)
        //                        {
        //                            foreach (InventoryItem item in u.Inventory)
        //                            {
        //                                stream.WriteByte(0x10);
        //                                stream.WriteString(item.Name);
        //                                stream.WriteLong(item.Count);
        //                            }
        //                        }
        //                    }
        //                }
        //                stream.WriteByte(0xFF);
        //            }
        //        }
        //    }
        //    catch { }
        //}

        public virtual void Load() { }
        //{
        //    try
        //    {
        //        var savePath = UserSavePath;

        //        if (File.Exists(savePath))
        //        {
        //            try
        //            {
        //                using (Stream filestream = File.OpenRead(savePath))
        //                {
        //                    using (Stream stream = new GZipStream(filestream, CompressionMode.Decompress))
        //                    {
        //                        int b = stream.ReadByte();
        //                        if (b != 0 && b != 1)
        //                            throw new Exception();

        //                        while (true)
        //                        {
        //                            User user = new User();
        //                            user.Name = stream.ReadString();
        //                            if (b == 0)
        //                                user.ID = user.Name;
        //                            else
        //                                user.ID = stream.ReadString();

        //                            while (true)
        //                            {
        //                                switch (stream.ReadByte())
        //                                {
        //                                    case 0:
        //                                        UsersByID[user.ID] = user;
        //                                        if (!UsersByName.ContainsKey(user.Name))
        //                                            UsersByName[user.Name.ToLower()] = user;
        //                                        goto end;
        //                                    case 1:
        //                                        user.Calories = stream.ReadLong();
        //                                        break;
        //                                    case 2:
        //                                        user.MessageCount = stream.ReadLong();
        //                                        break;
        //                                    case 3:
        //                                        user.CharacterCount = stream.ReadLong();
        //                                        break;
        //                                    case 4:
        //                                        user.Points = stream.ReadLong();
        //                                        break;
        //                                    case 5:
        //                                        user.Flags = (UserFlags)stream.ReadInt();
        //                                        break;
        //                                    case 6:
        //                                        user.GachiGASM = stream.ReadLong();
        //                                        break;
        //                                    case 0x10:
        //                                        user.AddItem(stream.ReadString(), stream.ReadLong());
        //                                        break;
        //                                    case 0xFF:
        //                                        UsersByID[user.ID] = user;
        //                                        if (!UsersByName.ContainsKey(user.Name))
        //                                            UsersByName[user.Name.ToLower()] = user;
        //                                        goto veryend;
        //                                }
        //                            }
        //                            end:;
        //                        }
        //                        veryend:;
        //                    }
        //                }
        //            }
        //            catch
        //            {

        //            }
        //        }
        //    }
        //    catch { }
        //}

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
