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
using twitchbot.Twitch;
using twitchbot.Discord2;
using Discord;

namespace twitchbot
{
    public class Bot
    {
        public event EventHandler<MessageEventArgs> ChannelMessageReceived;


        public ConcurrentDictionary<string, User> TwitchUsersByID = new ConcurrentDictionary<string, User>();
        public ConcurrentDictionary<string, User> TwitchUsersByName = new ConcurrentDictionary<string, User>();

        public ConcurrentDictionary<string, User> DiscordUsersByID = new ConcurrentDictionary<string, User>();
        public ConcurrentDictionary<string, User> DiscordUsersByName = new ConcurrentDictionary<string, User>();


        // Properies
        public IrcClient TwitchIrc { get; private set; }
        public DiscordClient DiscordClient { get; private set; }
        public Interpreter Interpreter { get; private set; }


        // CHANNEL SETTINGS
        public string TwitchOwner { get; set; } = null;
        public string DiscordOwner { get; set; } = null;

        bool connected = false;

        public bool EnableWhispers { get; set; } = false;

        public DateTime StartTime { get; private set; }

        System.Timers.Timer saveTimer = new System.Timers.Timer(5 * 1000 * 60);


        public bool EnableRQ { get; set; } = false;

        // COMMANDS
        public List<Command> Commands { get; private set; } = new List<Command>();
        public Dictionary<string, string> CommandAliases { get; private set; } = new Dictionary<string, string>();

        public ConcurrentDictionary<string, long> CommandCount { get; private set; } = new ConcurrentDictionary<string, long>();


        // GACHI SONGS
        public List<Song> GachiSongs = new List<Song>();


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
                            c.Say(o.ToString());
                    }
                    catch (Exception exc) { c.Say(exc.Message); }
                })
            {
                Expression = expression;
            }
        }

        public string TwitchBotName { get; private set; }


        // ctor
        public Bot()
        {
            StartTime = DateTime.Now;

            Load();

            Interpreter = new Interpreter();
            Interpreter.SetVariable("Bot", this, typeof(Bot));

            TwitchIrc = new IrcClient();

            TwitchIrc.OnErrorMessage += (s, e) => { Util.Log(e.Data.ReplyCode.ToString() + " " + e.Data.RawMessage, ConsoleColor.Red); };
            //TwitchIrc.OnRawMessage += (s, e) =>
            //{
            //    Util.Log(e.Data.RawMessage);

            //    if (e.Data.RawMessageArray.Length > 1 && e.Data.RawMessageArray[1] == "WHISPER")
            //    {
            //        User u = GetUserOrDefault(e.Data.Nick);

            //        if (u != null && u.IsAdmin)
            //        {
            //            var data = new IrcMessageData(TwitchIrc, e.Data.From, e.Data.Nick, e.Data.Ident, e.Data.Host, channels.FirstOrDefault(), e.Data.Message, e.Data.RawMessage, e.Data.Type, e.Data.ReplyCode);

            //            OnChannelMessage(data);
            //        }
            //    }
            //};

            TwitchIrc.OnError += (s, e) =>
            {
                try
                {
                    File.AppendAllText("ircerror", $"{DateTime.Now.ToLongTimeString()} {e.ErrorMessage} - {e.Data.From}: \"{e.Data.Message}\"");
                }
                catch
                {

                }
            };

            TwitchIrc.OnChannelMessage += (s, e) =>
            {
                twitchChannelMessage(e.Data);
            };

            saveTimer.Elapsed += (s, e) =>
            {
                Save();
            };
            saveTimer.Start();
        }

        public void ConnectDiscord(string email, string pass)
        {
            DiscordClient = new DiscordClient();
            DiscordClient.Connect(email, pass);
            foreach (var c in DiscordClient.PrivateChannels)
            {
                AddChannel(ChannelType.Discord, c.Id.ToString());
            }
            DiscordClient.MessageReceived += (s, e) =>
            {
                lock (discordChannels)
                {
                    foreach (var c in discordChannels)
                    {
                        if (c.ID == e.Channel.Id)
                        {
                            c.TriggerMessageReceived(new MessageEventArgs(c, c.GetOrCreateUser(e.User.Id.ToString(), e.User.Name.ToLower()), e.User.Id.ToString(), e.Message.Text));
                            break;
                        }
                    }
                }
            };
        }

        List<Channel> channels = new List<Channel>();
        List<TwitchChannel> twitchChannels = new List<TwitchChannel>();
        List<DiscordChannel> discordChannels = new List<DiscordChannel>();

        public IEnumerable<Channel> Channels
        {
            get
            {
                List<Channel> C;
                lock (channels)
                {
                    C = new List<Channel>(channels);
                }
                foreach (var c in C)
                {
                    yield return c;
                }
            }
        }

        public Channel AddChannel(ChannelType type, string channel)
        {
            switch (type)
            {
                case ChannelType.Twitch:
                    {
                        TwitchChannel c = new TwitchChannel(this, TwitchIrc, channel);
                        c.MessageReceived += onChannelMessageReceived;
                        lock (channels)
                            channels.Add(c);
                        lock (twitchChannels)
                            twitchChannels.Add(c);
                        c.Connect();
                        c.Load();
                        //c.Say("running now pajaHop");
                        return c;
                    }
                case ChannelType.Discord:
                    {
                        ulong id;
                        if (ulong.TryParse(channel, out id))
                        {
                            var c = new DiscordChannel(this, DiscordClient, id);
                            c.MessageReceived += onChannelMessageReceived;
                            lock (channels)
                                channels.Add(c);
                            lock (discordChannels)
                                discordChannels.Add(c);
                            c.Connect();
                            c.Load();
                            return c;
                        }

                        return null;
                    }
                default:
                    return null;
            }
        }

        public void RemoveChannel(Channel c)
        {
            lock (channels)
                channels.Remove(c);
            TwitchChannel t = c as TwitchChannel;
            if (t != null)
                twitchChannels.Remove(t);
            c.Disconnect();
        }

        private void onChannelMessageReceived(object sender, MessageEventArgs e)
        {
            ChannelMessageReceived?.Invoke(this, e);

            var message = e.Message;
            var C = e.Channel;
            var u = e.User;

            bool isAdmin = C.IsOwner(u) || u.IsAdmin;
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
                        if (!c.HasUserCooldown || isAdmin || isMod || !C.UserCommandCache.Any(t => t.Item2 == u.Name && t.Item3 == c.Name))
                        {
                            if (isAdmin || isMod || DateTime.Now - c.LastUsed > c.Cooldown)
                            {
                                //if (!isAdmin && !isMod)
                                //    c.LastUsed = DateTime.Now;

                                bool cooldown = false;

                                if (c.OwnerOnly)
                                {
                                    if (C.IsOwner(u))
                                    {
                                        c.Action(message, u, C);
                                        File.AppendAllText("modlogs", $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} {u.Name} {message}\n");
                                    }
                                }
                                else if (c.AdminOnly)
                                {
                                    if (isAdmin)
                                    {
                                        c.Action(message, u, C);
                                        File.AppendAllText("modlogs", $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} {u.Name} {message}\n");
                                    }
                                }
                                else if (c.ModOnly)
                                {
                                    if (isMod || isAdmin)
                                    {
                                        c.Action(message, u, C);
                                        File.AppendAllText("modlogs", $"{DateTime.Now:yyyy-MM-dd hh:mm:ss} {u.Name} {message}\n");
                                    }
                                }
                                else
                                {
                                    c.Action(message, u, C);
                                    cooldown = true;
                                }

                                if (cooldown)
                                    C.UserCommandCache.Enqueue(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(12), u.Name, c.Name));

                                CommandCount.AddOrUpdate(c.Name, 1, (k, v) => v + 1);
                            }
                        }
                    });
                }
        }


        // Connection
        public void ConnectTwitch(string username, string oauthPassword)
        {
            if (!connected)
            {
                connected = true;

                TwitchBotName = username;

                try
                {
                    TwitchIrc.Connect("irc.twitch.tv", 6667);
                }
                catch (ConnectionException e)
                {
                    Console.WriteLine("couldn't connect! Reason: " + e.Message);
                    return;
                }

                try
                {
                    TwitchIrc.Login(username, username, 0, username, "oauth:" + oauthPassword);

                    TwitchIrc.WriteLine("CAP REQ :twitch.tv/commands");

                    new Task(() =>
                    {
                        while (true)
                        {
                            try
                            {
                                if (connected)
                                    TwitchIrc.Listen();
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
            }
        }

        public void Disconnect()
        {
            connected = false;
            TwitchIrc.Disconnect();
        }


        // Users
        public long GetCommandUses(string name)
        {
            long l = 0;
            CommandCount.TryGetValue(name.ToLower(), out l);
            return l;
        }

        void twitchChannelMessage(IrcMessageData data)
        {
            try
            {
                if (data.Channel.Length < 1)
                    return;

                TwitchChannel C;
                lock (twitchChannels)
                {
                    var channelName = data.Channel.Substring(1);
                    C = twitchChannels.FirstOrDefault(x => x.ChannelName == channelName);
                }

                if (C == null)
                    return;


                string user = data.Nick.ToLower();

                string message = data.Message;

                User u = C.GetOrCreateUser(user, user);

                C.TriggerMessageReceived(new MessageEventArgs(C, u, u.Name, message));
            }
            catch (Exception exc)
            {
                File.AppendAllText("error", exc.Message + "\r\n" + exc.StackTrace + "\r\n--------\r\n");
            }
        }


        // IO
        private void Load()
        {
            try
            {
                if (!Directory.Exists("./db"))
                    Directory.CreateDirectory("./db");
                //if (File.Exists("./db/users.json"))
                //{
                //    File.Move("./db/users.json", "./db/users.json.2");
                //    dynamic users = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("./db/users.json.2")).Users;
                //
                //    foreach (dynamic d in users)
                //    {
                //        User s = d.ToObject(typeof(User));
                //        Users[s.Name] = s;
                //    }
                //}
                //else 

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

            loadUsers(TwitchUsersByName, TwitchUsersByID, "./db/twitch");
            loadUsers(DiscordUsersByName, DiscordUsersByID, "./db/discord");
        }

        private void loadUsers(ConcurrentDictionary<string, User> UsersByName, ConcurrentDictionary<string, User> UsersByID, string UserSavePath)
        {
            try
            {
                var savePath = UserSavePath;

                if (File.Exists(savePath))
                {
                    try
                    {
                        using (Stream filestream = File.OpenRead(savePath))
                        {
                            using (Stream stream = new GZipStream(filestream, CompressionMode.Decompress))
                            {
                                int b = stream.ReadByte();
                                if (b != 0 && b != 1)
                                    throw new Exception();

                                while (true)
                                {
                                    User user = new User();
                                    user.Name = stream.ReadString();
                                    if (b == 0)
                                        user.ID = user.Name;
                                    else
                                        user.ID = stream.ReadString();

                                    while (true)
                                    {
                                        switch (stream.ReadByte())
                                        {
                                            case 0:
                                                UsersByID[user.ID] = user;
                                                if (!UsersByName.ContainsKey(user.Name))
                                                    UsersByName[user.Name.ToLower()] = user;
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
                                                UsersByID[user.ID] = user;
                                                if (!UsersByName.ContainsKey(user.Name))
                                                    UsersByName[user.Name.ToLower()] = user;
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
            }
            catch { }
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

            lock (channels)
            {
                foreach (var c in channels)
                {
                    c.Save();
                }
            }

            File.WriteAllLines("./db/aliases.txt", CommandAliases.Select(k => k.Key + "=" + k.Value));
            File.WriteAllLines("./db/stats.txt", CommandCount.Select(k => k.Key + "=" + k.Value));

            File.WriteAllLines("./db/evalcommands.txt", EvalCommands.Select(c => (c.AdminOnly ? "%" : "") + c.Name + "=" + c.Expression));
            //w.Stop();
            //Say("#pajlada", $"Saved in {w.Elapsed.TotalSeconds:0.000} seconds.");

            saveUsers(TwitchUsersByName, "./db/twitch");
            saveUsers(DiscordUsersByName, "./db/discord");
        }

        private void saveUsers(ConcurrentDictionary<string, User> UsersByName, string UserSavePath)
        {
            try
            {
                var savePath = UserSavePath;

                using (Stream filestream = File.OpenWrite(savePath))
                {
                    using (Stream stream = new GZipStream(filestream, CompressionMode.Compress))
                    {
                        stream.WriteByte(1);
                        bool first = true;
                        foreach (User u in UsersByName.Values)
                        {
                            if (first)
                                first = false;
                            else
                                stream.WriteByte(0);

                            stream.WriteString(u.Name);
                            stream.WriteString(u.ID);

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
            }
            catch { }
        }



        //public void Restart()
        //{
        //    if (Util.IsLinux)
        //    {
        //        Save();
        //        Process.Start("bash", $"-c 'service fourtfbot restart'");
        //    }
        //}
    }
}
