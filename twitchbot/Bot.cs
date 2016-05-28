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

namespace twitchbot
{
    public class Bot
    {
        public event EventHandler<MessageEventArgs> ChannelMessageReceived;

        // Properies
        public IrcClient TwitchIrc { get; private set; }
        public Interpreter Interpreter { get; private set; }

        // CHANNEL SETTINGS
        public string Username { get; private set; }
        public string OAuthPassword { get; private set; }
        public string Admin { get; set; }

        bool connected = false;

        public bool EnableWhispers { get; set; } = false;


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


        // ctor
        public Bot(string username, string oauth)
        {

            Username = username;
            OAuthPassword = oauth;

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
        }

        List<Channel> channels = new List<Channel>();
        List<TwitchChannel> twitchChannels = new List<TwitchChannel>();

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
                        return c;
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
        }


        // Connection
        public void Connect()
        {
            if (!connected)
            {
                connected = true;

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
                    TwitchIrc.Login(Username, Username, 0, Username, "oauth:" + OAuthPassword);

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

                User u = C.GetOrCreateUser(user);
                bool isAdmin = u.IsAdmin;
                bool isMod = u.IsMod;

                u.MessageCount++;
                u.CharacterCount += message.Length;

                C.TriggerMessageReceived(new MessageEventArgs(C, u, u.Name, message));

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
                            //if ((u.Flags & UserFlags.NotNew) == UserFlags.None)
                            //{
                            //    Whisper(u.Name, $"Hey {u.Name}! ")
                            //    u.Flags |= UserFlags.NotNew;
                            //}

                            if (!c.HasUserCooldown || isAdmin || isMod || !C.UserCommandCache.Any(t => t.Item2 == u.Name && t.Item3 == c.Name))
                            {
                                C.UserCommandCache.Enqueue(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(15), u.Name, c.Name));

                                if (isAdmin || isMod || DateTime.Now - c.LastUsed > c.Cooldown)
                                {
                                    //if (!isAdmin && !isMod)
                                    //    c.LastUsed = DateTime.Now;

                                    if (c.AdminOnly)
                                    {
                                        if (isAdmin)
                                            c.Action(message, u, C);
                                    }
                                    else if (c.ModOnly)
                                    {
                                        if (isMod || isAdmin)
                                            c.Action(message, u, C);
                                    }
                                    else
                                    {
                                        c.Action(message, u, C);
                                    }

                                    CommandCount.AddOrUpdate(c.Name, 1, (k, v) => v + 1);
                                }
                            }
                        });
                    }
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
        }
    }
}
