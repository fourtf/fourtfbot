//using ChatSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Meebey.SmartIrc4net;
using System.Threading;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net;
using System.Collections.Concurrent;
using System.IO.Compression;
using DynamicExpresso;
using System.Net.Sockets;
using System.Diagnostics;

namespace twitchbot
{
    public class Bot
    {
        // Properies
        public IrcClient Irc { get; private set; }
        public Interpreter Interpreter { get; private set; }
        public ConcurrentDictionary<string, User> Users = new ConcurrentDictionary<string, User>();

        // CHANNEL SETTINGS
        public string Username { get; private set; }
        public string OAuthPassword { get; private set; }
        public string[] Channels { get; set; }
        public string Admin { get; set; }

        bool connected = false;


        // TIMERS
        System.Timers.Timer userUpdateTimer = new System.Timers.Timer(5 * 1000 * 60);
        System.Timers.Timer messageLimitTimer = new System.Timers.Timer(1000 * 15);

        System.Timers.Timer secondsTimer = new System.Timers.Timer(1000);

        public List<Tuple<DateTime, Action>> DelayedActions { get; set; } = new List<Tuple<DateTime, Action>>();


        // MESSAGES
        System.Timers.Timer messageTimer = new System.Timers.Timer(1100);
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(7);

        Queue<Message> messageQueue = new Queue<Message>();

        struct Message
        {
            public string Text { get; set; }
            public string Channel { get; set; }
            public DateTime ExpireDate { get; set; }
        }


        // COMMANDS
        public List<Command> Commands { get; private set; } = new List<Command>();
        public Dictionary<string, string> CommandAliases { get; private set; } = new Dictionary<string, string>();

        public ConcurrentDictionary<string, long> CommandCount { get; private set; } = new ConcurrentDictionary<string, long>();

        public TimeSpan UserCommandCooldown = TimeSpan.FromSeconds(13);
        public ConcurrentQueue<Tuple<DateTime, string, string>> UserCommandCache { get; private set; } = new ConcurrentQueue<Tuple<DateTime, string, string>>();


        // RAFFLES
        public ConcurrentDictionary<string, User> RaffleUsers { get; } = new ConcurrentDictionary<string, User>();

        public bool RaffleActive { get; set; } = false;


        // GACHI SONGS
        public List<Song> GachiSongs = new List<Song>();


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


        // EVAL COMMANDS
        public List<EvalCommand> EvalCommands = new List<EvalCommand>();

        public class EvalCommand : Command
        {
            public string Expression { get; private set; }

            public EvalCommand(Bot bot, string name, string expression)
                : base(name, (m, u, c) =>
                {
                    try
                    {
                        object o = bot.Interpreter.Eval(expression, new Parameter("C", c));

                        if (o != null)
                            bot.Say(c, o.ToString());
                    }
                    catch (Exception exc) { bot.Say(c, exc.Message); }
                })
            {
                Expression = expression;
            }
        }


        // ctor
        public Bot(string username, string oauth)
        {
            Username = username;
            OAuthPassword = oauth;

            Load();

            Interpreter = new Interpreter();
            Interpreter.SetVariable("Bot", this, typeof(Bot));

            Irc = new IrcClient();

            Irc.OnErrorMessage += (s, e) => { Util.Log(e.Data.ReplyCode.ToString() + " " + e.Data.RawMessage, ConsoleColor.Red); };
            Irc.OnRawMessage += (s, e) =>
            {
                Util.Log(e.Data.RawMessage);

                if (e.Data.RawMessageArray.Length > 1 && e.Data.RawMessageArray[1] == "WHISPER")
                {
                    User u = GetUserOrDefault(e.Data.Nick);

                    if (u != null && u.IsAdmin)
                    {
                        var data = new IrcMessageData(Irc, e.Data.From, e.Data.Nick, e.Data.Ident, e.Data.Host, Channels.FirstOrDefault(), e.Data.Message, e.Data.RawMessage, e.Data.Type, e.Data.ReplyCode);

                        OnChannelMessage(data);
                    }
                }
            };

            Irc.OnError += (s, e) =>
            {
                try
                {
                    File.AppendAllText("ircerror", $"{DateTime.Now.ToLongTimeString()} {e.ErrorMessage} - {e.Data.From}: \"{e.Data.Message}\"");
                }
                catch
                {

                }
            };

            Irc.OnChannelMessage += (s, e) =>
            {
                OnChannelMessage(e.Data);
            };

            userUpdateTimer.Elapsed += (s, e) =>
            {
                UserUpdateTick();
            };

            secondsTimer.Elapsed += (s, e) =>
            {
                DateTime now = DateTime.Now;

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
            };

            messageTimer.Elapsed += (s, e) =>
            {
                lock (messageQueue)
                {
                    while (messageQueue.Count > 0)
                    {
                        Message msg = messageQueue.Dequeue();

                        if (msg.ExpireDate < DateTime.Now)
                            continue;

                        messagecount++;

                        if (CanSayMessage)
                            Irc.SendMessage(SendType.Message, msg.Channel, msg.Text);

                        break;
                    }

                    messageTimer.Enabled = messageQueue.Count > 0;
                }
            };

            new Thread(Api.ApiServer).Start(this);
        }

        // Connection
        public void Connect()
        {
            if (!connected)
            {
                connected = true;

                try
                {
                    Irc.Connect("irc.twitch.tv", 6667);
                }
                catch (ConnectionException e)
                {
                    Console.WriteLine("couldn't connect! Reason: " + e.Message);
                    return;
                }

                try
                {
                    Irc.Login(Username, Username, 0, Username, "oauth:" + OAuthPassword);

                    foreach (string channel in Channels)
                        Irc.RfcJoin(channel);

                    Irc.WriteLine("CAP REQ :twitch.tv/commands");

                    new Task(() =>
                    {
                        while (true)
                        {
                            try
                            {
                                if (connected)
                                    Irc.Listen();
                            }
                            catch (Exception exc)
                            {
                                File.WriteAllText("error", exc.Message + "\r\n\r\n" + exc.StackTrace);
                            }

                            Thread.Sleep(10000);
                        }
                    }).Start();
                }
                catch (ConnectionException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error occurred! Message: " + e.Message);
                    Console.WriteLine("Exception: " + e.StackTrace);
                    return;
                }


                userUpdateTimer.Start();
                UserUpdateTick();

                messageLimitTimer.Elapsed += (s, e) => { messagecount = 0; };
                messageLimitTimer.Start();

                secondsTimer.Start();
            }
        }

        public void Disconnect()
        {
            connected = false;
            Irc.Disconnect();
        }


        // messages sent in the last 15 seconds
        int messagecount = 0;


        // checks if it can say the message without being globally banned
        public bool CanSayMessage
        {
            get
            {
                //return messagecount < 11;
                return messagecount < 31;
            }
        }

        DateTime lastMessage = DateTime.MinValue;
        //TimeSpan messageTimeOffset = TimeSpan.FromSeconds(1.05);
        TimeSpan messageTimeOffset = TimeSpan.FromSeconds(0.1);


        // Say
        public void Say(string channel, string message, bool noTimeout = false)
        {
            SayRaw(channel, ". " + (message.Length < 360 ? message : message.Remove(357) + "..."), noTimeout);
        }

        public void SayMe(string channel, string message, bool noTimeout = false)
        {
            SayRaw(channel, "/me " + (message.Length < 360 ? message : message.Remove(357) + "..."), noTimeout);
        }

        public void ForceSay(string channel, string message)
        {
            Say(channel, message, true);
        }

        public void SayRaw(string channel, string message, bool noTimeout = false)
        {
            lock (messageQueue)
            {
                if (!messageTimer.Enabled)
                {
                    messagecount++;
                    if (CanSayMessage)
                    {
                        lastMessage = DateTime.Now;
                        Irc.SendMessage(SendType.Message, channel, message);
                        messageTimer.Enabled = true;
                    }
                }
                else
                {
                    messageQueue.Enqueue(new Message { ExpireDate = DateTime.Now + (noTimeout ? TimeSpan.FromDays(1) : MessageTimeout), Text = message, Channel = channel });
                }
            }
        }

        public bool Whisper(string user, string text)
        {
            Irc.SendMessage(SendType.Message, "#jtv", $"/w {user} {text}");
            return true;
        }

        // Users
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

        public long GetCommandUses(string name)
        {
            long l = 0;
            CommandCount.TryGetValue(name.ToLower(), out l);
            return l;
        }

        void OnChannelMessage(IrcMessageData data)
        {
            try
            {
                string user = data.Nick;

                string message = data.Message;

                User u = GetOrCreateUser(user);
                bool isAdmin = u.IsAdmin;
                bool isMod = u.IsMod;

                u.MessageCount++;
                u.CharacterCount += message.Length;

                if (message.Contains("forsenGASM"))
                {
                    int index = -1;
                    while ((index = message.IndexOf("forsenGASM", index + 1)) != -1)
                        u.GachiGASM++;
                }

                if (message.Contains("gachiGASM"))
                {
                    int index = -1;
                    while ((index = message.IndexOf("gachiGASM", index + 1)) != -1)
                        u.GachiGASM++;
                }

                if (!u.IsBanned && message.Length > 2)
                {
                    if (message[0] == '!')
                    {
                        string _command = null;

                        int index;
                        if ((index = message.IndexOf(' ')) != -1)
                            _command = message.Substring(1, index - 1);

                        _command = _command ?? message.Substring(1);

                        string aliasFor;
                        CommandAliases.TryGetValue(_command, out aliasFor);

                        EvalCommand evalCommand = null;
                        lock (EvalCommands)
                        {
                            evalCommand = EvalCommands.FirstOrDefault(x => x.Name == _command) ?? EvalCommands.FirstOrDefault(x => x.Name == aliasFor);
                        }

                        (Commands.FirstOrDefault(x => x.Name == _command) ?? Commands.FirstOrDefault(x => x.Name == aliasFor) ?? evalCommand).Process(c =>
                        {
                            //if ((u.Flags & UserFlags.NotNew) == UserFlags.None)
                            //{
                            //    Whisper(u.Name, $"Hey {u.Name}! ")
                            //    u.Flags |= UserFlags.NotNew;
                            //}

                            if (!c.HasUserCooldown || isAdmin || isMod || !UserCommandCache.Any(t => t.Item2 == u.Name && t.Item3 == c.Name))
                            {
                                UserCommandCache.Enqueue(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(15), u.Name, c.Name));

                                if (isAdmin || isMod || DateTime.Now - c.LastUsed > c.Cooldown)
                                {
                                    //if (!isAdmin && !isMod)
                                    //    c.LastUsed = DateTime.Now;

                                    if (c.AdminOnly)
                                    {
                                        if (isAdmin)
                                            c.Action(message, u, data.Channel);
                                    }
                                    else if (c.ModOnly)
                                    {
                                        if (isMod)
                                            c.Action(message, u, data.Channel);
                                    }
                                    else
                                    {
                                        c.Action(message, u, data.Channel);
                                    }

                                    CommandCount.AddOrUpdate(c.Name, 1, (k, v) => v + 1);
                                }
                            }
                        });
                    }
                }
            }
            catch (Exception exc)
            {
                File.AppendAllText("error", exc.Message + "\r\n" + exc.StackTrace + "\r\n--------\r\n");
            }
        }


        // executed every 5 minutes
        void UserUpdateTick()
        {
            lock (Duels)
            {
                Duels.RemoveAll(d => d.ExpireDate < DateTime.Now);
            }

            lock (Trades)
            {
                Trades.RemoveAll(d => d.ExpireDate < DateTime.Now);
            }

            new Task(() =>
            {
                try
                {
                    using (WebClient client = new WebClient())
                        foreach (string channel in Channels)
                        {
                            if (channel.Contains("pajlada"))
                            {
                                string response = client.DownloadString($"http://tmi.twitch.tv/group/user/{channel.TrimStart('#')}/chatters");
                                dynamic root = JsonConvert.DeserializeObject<dynamic>(response);

                                Action<string> processUser = (u) =>
                                {
                                    User user = GetOrCreateUser(u);

                                    user.Points += 3;
                                };

                                dynamic chatters = root.chatters;
                                if (chatters != null)
                                {
                                    foreach (string u in chatters?.moderators ?? new string[] { })
                                        processUser(u);
                                    foreach (string u in chatters?.staff ?? new string[] { })
                                        processUser(u);
                                    foreach (string u in chatters?.admins ?? new string[] { })
                                        processUser(u);
                                    foreach (string u in chatters?.global_mods ?? new string[] { })
                                        processUser(u);
                                    foreach (string u in chatters?.viewers ?? new string[] { })
                                        processUser(u);
                                }
                            }
                        }
                    Save();
                }
                catch (Exception exc)
                {
                    File.WriteAllText("error", exc.Message + "\r\n\r\n" + exc.StackTrace);
                }
            }).Start();
        }


        // query an action to be executes soon
        public void QueueAction(double seconds, Action action)
        {
            lock (DelayedActions)
            {
                DelayedActions.Add(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(seconds), action));
            }
        }


        // IO
        private void Load()
        {
            try
            {
                if (!Directory.Exists("./db"))
                    Directory.CreateDirectory("./db");
                if (File.Exists("./db/users.json"))
                {
                    File.Move("./db/users.json", "./db/users.json.2");
                    dynamic users = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("./db/users.json.2")).Users;

                    foreach (dynamic d in users)
                    {
                        User s = d.ToObject(typeof(User));
                        Users[s.Name] = s;
                    }
                }
                else if (File.Exists("./db/user"))
                {
                    try
                    {

                        using (Stream filestream = File.OpenRead("./db/user"))
                        {
                            using (Stream stream = new GZipStream(filestream, CompressionMode.Decompress))
                            {
                                if (stream.ReadByte() != 0)
                                    throw new Exception();

                                while (true)
                                {
                                    User user = new User();
                                    user.Name = stream.ReadString();

                                    while (true)
                                    {
                                        switch (stream.ReadByte())
                                        {
                                            case 0:
                                                Users[user.Name] = user;
                                                goto end;
                                            case 1:
                                                user.Calories = stream.ReadLong();
                                                break;
                                            case 2:
                                                user.MessageCount = stream.ReadLong();
                                                break;
                                            case 3:
                                                user.CharacterCount = stream.ReadLong();
                                                break;
                                            case 4:
                                                user.Points = stream.ReadLong();
                                                break;
                                            case 5:
                                                user.Flags = (UserFlags)stream.ReadInt();
                                                break;
                                            case 6:
                                                user.GachiGASM = stream.ReadLong();
                                                break;
                                            case 0x10:
                                                user.AddItem(stream.ReadString(), stream.ReadLong());
                                                break;
                                            case 0xFF:
                                                Users[user.Name] = user;
                                                goto veryend;
                                        }
                                    }
                                    end:;
                                }
                                veryend:;
                            }
                        }
                    }
                    catch
                    {

                    }
                }
                if (File.Exists("./db/aliases.txt"))
                {
                    using (StreamReader r = new StreamReader("./db/aliases.txt"))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            int index;
                            if ((index = line.IndexOf('=')) != -1)
                                CommandAliases[line.Remove(index)] = line.Substring(index + 1);
                        }
                    }
                }
                if (File.Exists("./db/stats.txt"))
                {
                    using (StreamReader r = new StreamReader("./db/stats.txt"))
                    {
                        string line;
                        while ((line = r.ReadLine()) != null)
                        {
                            int index;
                            if ((index = line.IndexOf('=')) != -1)
                            {
                                long count;
                                if (long.TryParse(line.Substring(index + 1), out count))
                                    CommandCount[line.Remove(index)] = count;
                            }
                        }
                    }
                }

                if (File.Exists("./db/evalcommands.txt"))
                {
                    lock (EvalCommands)
                    {
                        File.ReadAllLines("./db/evalcommands.txt").Do(line =>
                        {
                            try
                            {
                                int index;
                                if ((index = line.IndexOf('=')) != -1)
                                {
                                    string name = line.Remove(index);
                                    EvalCommands.Add(new EvalCommand(this, name.Trim('%'), line.Substring(index + 1)) { AdminOnly = name.IndexOf('%') != -1 });
                                }
                            }
                            catch
                            {

                            }
                        });
                    }
                }

                // Load gachi songs
                List<Song> songs = new List<Song>();
                if (File.Exists("songs.json"))
                {
                    // have to use this because json.net never disposed the data it deserializes
                    using (StreamReader sr = new StreamReader("songs.json"))
                    using (JsonTextReader reader = new JsonTextReader(sr))
                    {
                        while (reader.TokenType != JsonToken.PropertyName && ((string)reader.Value) != "Songs")
                            reader.Read();
                        reader.Read();
                        if (reader.TokenType != JsonToken.StartArray)
                            return;
                        reader.Read();

                        while (reader.TokenType == JsonToken.StartObject)
                        {
                            JsonSerializer serializer = new JsonSerializer();

                            var song = serializer.Deserialize<Song>(reader);
                            songs.Add(song);
                            reader.Read();
                        }
                    }

                    // this piece of shit code takes 20 mb of ram for no reason
                    //dynamic json = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("songs.json"));
                    //dynamic _songs = json.Songs;
                    //foreach (dynamic d in _songs)
                    //{
                    //    Song s = d.ToObject(typeof(Song));
                    //    songs.Add(s);
                    //}
                }

                GachiSongs = new List<Song>(songs.Where(s => s.Type == "Gachi"));
            }
            catch
            {

            }
        }

        public void Save()
        {
            //dynamic root = new JObject();
            //JArray o = (JArray)JToken.FromObject(Users.Select(k => k.Value).ToArray());
            //
            //root.Users = o;
            //root.Timestamp = DateTime.Now;
            //
            //string json = root.ToString();
            //File.WriteAllText("./db/users.json", json);

            //var w = Stopwatch.StartNew();
            using (Stream filestream = File.OpenWrite("./db/user"))
            {
                using (Stream stream = new GZipStream(filestream, CompressionMode.Compress))
                {
                    foreach (User u in Users.Values)
                    {
                        stream.WriteByte(0);

                        stream.WriteString(u.Name);

                        if (u.Calories != 0) { stream.WriteByte(0x01); stream.WriteLong(u.Calories); }
                        if (u.MessageCount != 0) { stream.WriteByte(0x02); stream.WriteLong(u.MessageCount); }
                        if (u.CharacterCount != 0) { stream.WriteByte(0x03); stream.WriteLong(u.CharacterCount); }
                        if (u.Points != 0) { stream.WriteByte(0x04); stream.WriteLong(u.Points); }

                        if (u.Flags != 0) { stream.WriteByte(0x05); stream.WriteInt((int)u.Flags); }
                        if (u.GachiGASM != 0) { stream.WriteByte(0x06); stream.WriteLong(u.GachiGASM); }

                        if (u.Inventory != null)
                        {
                            lock (u.Inventory)
                            {
                                foreach (InventoryItem item in u.Inventory)
                                {
                                    stream.WriteByte(0x10);
                                    stream.WriteString(item.Name);
                                    stream.WriteLong(item.Count);
                                }
                            }
                        }
                    }
                    stream.WriteByte(0xFF);
                }
            }

            File.WriteAllLines("./db/aliases.txt", CommandAliases.Select(k => k.Key + "=" + k.Value));
            File.WriteAllLines("./db/stats.txt", CommandCount.Select(k => k.Key + "=" + k.Value));

            File.WriteAllLines("./db/evalcommands.txt", EvalCommands.Select(c => (c.AdminOnly ? "%" : "") + c.Name + "=" + c.Expression));
            //w.Stop();
            //Say("#pajlada", $"Saved in {w.Elapsed.TotalSeconds:0.000} seconds.");
        }
    }
}
