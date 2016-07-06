using Discord;
using DynamicExpresso;
using Meebey.SmartIrc4net;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using twitchbot.Discord2;
using twitchbot.Twitch;

namespace twitchbot
{
    public class Bot
    {
        // ctor
        public Bot()
        {
            StartTime = DateTime.Now;

            Load();

            BttvGlobalEmotes["xd"] = BttvGlobalEmotes["xD"] = new TwitchEmote { Url = "https://fourtf.com/img/xD.png" };

            Interpreter = new Interpreter();
            Interpreter.SetVariable("Bot", this, typeof(Bot));

            saveTimer.Elapsed += (s, e) =>
            {
                Save();
            };
            saveTimer.Start();
            secondsTimer.Elapsed += onSecondTick;
            secondsTimer.Start();
        }

        private const string globalTwitchEmoteCachePath = "./db/twitch_global_emotes_cache.json";
        private const string twitchemotesSubsCache = "./db/twitchemtotes_subs.json";
        private const string twitchemotesGlobalCache = "./db/twitchemtotes_global.json";
        private const string bttvEmotesGlobalCache = "./db/bttv_global.json";


        // USERS
        public ConcurrentDictionary<string, User> TwitchUsersByID = new ConcurrentDictionary<string, User>();
        public ConcurrentDictionary<string, User> TwitchUsersByName = new ConcurrentDictionary<string, User>();

        public ConcurrentDictionary<string, User> DiscordUsersByID = new ConcurrentDictionary<string, User>();
        public ConcurrentDictionary<string, User> DiscordUsersByName = new ConcurrentDictionary<string, User>();


        // SETTINGS
        public bool EnableTwitchWhispers { get; set; } = false;
        public bool EnableRQ { get; set; } = false;


        // INTERPRETER
        public Interpreter Interpreter { get; private set; }


        // GACHI SONGS
        public List<Song> GachiSongs = new List<Song>();


        // TWITCH EMOTES
        public ConcurrentDictionary<string, TwitchEmote> TwitchEmotes { get; private set; } = new ConcurrentDictionary<string, TwitchEmote>();
        public ConcurrentDictionary<int, TwitchEmote> TwitchEmotesById { get; private set; } = new ConcurrentDictionary<int, TwitchEmote>();
        public ConcurrentDictionary<string, TwitchEmote> BttvGlobalEmotes { get; private set; } = new ConcurrentDictionary<string, TwitchEmote>();


        // CHANNELS
        List<Channel> channels = new List<Channel>();
        List<TwitchChannel> twitchChannels = new List<TwitchChannel>();
        List<DiscordChannel> discordChannels = new List<DiscordChannel>();


        // CONNECTION
        public event EventHandler<MessageEventArgs> ChannelMessageReceived;

        public IrcClient TwitchIrc { get; private set; } = null;
        public DiscordClient DiscordClient { get; private set; } = null;

        public string TwitchOwner { get; set; } = null;
        public string DiscordOwner { get; set; } = null;

        bool connectedTwitch = false;


        public DateTime StartTime { get; private set; }
        public TimeSpan Uptime { get { return DateTime.Now - StartTime; } }

        System.Timers.Timer saveTimer = new System.Timers.Timer(1000 * 60 * 5);
        System.Timers.Timer twitchPingTimer = new System.Timers.Timer(1000 * 30);

        bool receivedTwitchPong = false;

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

        public void ConnectTwitch(string username, string oauthPassword)
        {
            if (!connectedTwitch)
            {
                //fetch emotes
                new Task(() =>
                {
                    JsonParser parser = new JsonParser();

                    // twitch.tv api
                    /*if (!File.Exists(globalTwitchEmoteCachePath) || DateTime.Now - new FileInfo(globalTwitchEmoteCachePath).LastWriteTime > TimeSpan.FromHours(24))
                    {
                        try
                        {
                            using (var webClient = new WebClient())
                            using (var readStream = webClient.OpenRead("https://api.twitch.tv/kraken/chat/emoticons"))
                            using (var writeStream = File.OpenWrite(globalTwitchEmoteCachePath))
                            {
                                readStream.CopyTo(writeStream);
                            }
                        }
                        catch { }
                    }

                    JsonParser parser = new JsonParser();
                    using (var stream = File.OpenRead(globalTwitchEmoteCachePath))
                    {
                        dynamic json = parser.Parse(stream);
                        foreach (dynamic d in json["emoticons"])
                        {
                            string regex = d["regex"];
                            dynamic image = d["images"][0];
                            string url = image["url"];
                            int emoticonSet = int.TryParse(image["emoticon_set"], out emoticonSet) ? emoticonSet : -1;
                            int width = int.TryParse(image["width"], out width) ? width : 28;
                            int height = int.TryParse(image["height"], out height) ? height : 28;

                            TwitchEmotes[regex] = new TwitchEmote { Name = regex, Type = emoticonSet == -1 ? EmoteType.TwitchGlobal : EmoteType.TwitchSub, Height = height, Width = width, EmoteSet = emoticonSet, Url = url };
                        }
                    }*/

                    // twitchemotes api global emotes
                    if (!File.Exists(twitchemotesGlobalCache) || DateTime.Now - new FileInfo(twitchemotesGlobalCache).LastWriteTime > TimeSpan.FromHours(24))
                    {
                        try
                        {
                            if (Util.IsLinux)
                            {
                                Util.LinuxDownloadFile("https://twitchemotes.com/api_cache/v2/global.json", twitchemotesGlobalCache);
                            }
                            else
                            {
                                using (var webClient = new WebClient())
                                using (var readStream = webClient.OpenRead("https://twitchemotes.com/api_cache/v2/global.json"))
                                using (var writeStream = File.OpenWrite(twitchemotesGlobalCache))
                                {
                                    readStream.CopyTo(writeStream);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            e.Message.Log("emotes");
                        }
                    }

                    using (var stream = File.OpenRead(twitchemotesGlobalCache))
                    {
                        dynamic json = parser.Parse(stream);
                        dynamic templates = json["template"];
                        string template112 = templates["large"];

                        foreach (dynamic e in json["emotes"])
                        {
                            string code = e.Key;
                            dynamic value = e.Value;
                            string imageId = value["image_id"];
                            int imageIdInt = int.Parse(imageId);
                            string url = template112.Replace("{image_id}", imageId);

                            TwitchEmotes[code] = new TwitchEmote { EmoteSet = -1, Height = -1, Width = -1, Name = code, Type = EmoteType.TwitchGlobal, Url = url };
                            TwitchEmotesById[imageIdInt] = new TwitchEmote { EmoteSet = -1, Height = -1, Width = -1, Name = code, Type = EmoteType.TwitchGlobal, Url = url };
                        }
                    }

                    // twitchemotes api sub emotes
                    if (!File.Exists(twitchemotesSubsCache) || DateTime.Now - new FileInfo(twitchemotesSubsCache).LastWriteTime > TimeSpan.FromHours(24))
                    {
                        try
                        {
                            if (Util.IsLinux)
                            {
                                Util.LinuxDownloadFile("https://twitchemotes.com/api_cache/v2/subscriber.json", twitchemotesSubsCache);
                            }
                            else
                            {
                                using (var webClient = new WebClient())
                                using (var readStream = webClient.OpenRead("https://twitchemotes.com/api_cache/v2/subscriber.json"))
                                using (var writeStream = File.OpenWrite(twitchemotesSubsCache))
                                {
                                    readStream.CopyTo(writeStream);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            e.Message.Log("emotes");
                        }
                    }

                    using (var stream = File.OpenRead(twitchemotesSubsCache))
                    {
                        dynamic json = parser.Parse(stream);
                        dynamic templates = json["template"];
                        string template112 = templates["large"];

                        foreach (dynamic c in json["channels"].Values)
                        {
                            int set = int.Parse(c["set"]);

                            foreach (dynamic e in c["emotes"])
                            {
                                string code = e["code"];
                                string imageId = e["image_id"];
                                string url = template112.Replace("{image_id}", imageId);

                                TwitchEmotes[code] = new TwitchEmote { EmoteSet = set, Height = 112, Width = 112, Name = code, Type = EmoteType.TwitchSub, Url = url };
                                TwitchEmotesById[int.Parse(imageId)] = new TwitchEmote { EmoteSet = set, Height = 112, Width = 112, Name = code, Type = EmoteType.TwitchSub, Url = url };
                            }
                        }
                    }

                    // better twitch tv emotes
                    if (!File.Exists(bttvEmotesGlobalCache) || DateTime.Now - new FileInfo(bttvEmotesGlobalCache).LastWriteTime > TimeSpan.FromHours(24))
                    {
                        try
                        {
                            if (Util.IsLinux)
                            {
                                Util.LinuxDownloadFile("https://api.betterttv.net/2/emotes", bttvEmotesGlobalCache);
                            }
                            else
                            {
                                using (var webClient = new WebClient())
                                using (var readStream = webClient.OpenRead("https://api.betterttv.net/2/emotes"))
                                using (var writeStream = File.OpenWrite(bttvEmotesGlobalCache))
                                {
                                    readStream.CopyTo(writeStream);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            e.Message.Log("emotes");
                        }
                    }

                    using (var stream = File.OpenRead(bttvEmotesGlobalCache))
                    {
                        dynamic json = parser.Parse(stream);
                        var template = "https:" + json["urlTemplate"]; //{{id}} {{image}}

                        foreach (dynamic e in json["emotes"])
                        {
                            string id = e["id"];
                            string code = e["code"];
                            string imageType = e["imageType"];
                            string url = template.Replace("{{id}}", id).Replace("{{image}}", "3x");

                            BttvGlobalEmotes[code] = new TwitchEmote { EmoteSet = -1, Height = 112, Width = 112, Name = code, Type = EmoteType.BttvChannel, Url = url, ImageFormat = imageType };
                        }
                    }
                }).Start();

                TwitchBotName = username;

                // connect irc
                Action connect = () =>
                {
                    TwitchIrc = new IrcClient();
                    TwitchIrc.Encoding = new UTF8Encoding();

                    TwitchIrc.OnRawMessage += onRawMessage;

                    try
                    {
                        "connecting to irc.twitch.tv".Log("irc");
                        TwitchIrc.Connect("irc.twitch.tv", 6667);
                    }
                    catch (ConnectionException e)
                    {
                        $"ConnectionException: {e.Message}".Log("irc");
                    }

                    try
                    {
                        "logging in".Log("irc");
                        TwitchIrc.Login(username, username, 0, username, "oauth:" + oauthPassword);

                        TwitchIrc.WriteLine("CAP REQ :twitch.tv/commands");
                        TwitchIrc.WriteLine("CAP REQ :twitch.tv/tags");

                        new Task(() =>
                        {
                            TwitchIrc.Listen();
                        }).Start();

                        foreach (var channel in TwitchChannels)
                        {
                            $"joining #{channel.ChannelName}".Log("irc");
                            TwitchIrc.RfcJoin("#" + channel.ChannelName);
                        }

                        connectedTwitch = true;
                    }
                    catch (ConnectionException e)
                    {
                        $"ConnectionException_2: {e.Message}".Log("irc");
                        return;
                    }
                    catch (Exception e)
                    {
                        $"Exception: {e.Message}".Log("irc");
                        return;
                    }
                };

                connect();

                twitchPingTimer.Elapsed += (s, e) =>
                {
                    if (connectedTwitch)
                    {
                        receivedTwitchPong = false;
                        TwitchIrc.WriteLine("PING");

                        QueueAction(15, () =>
                        {
                            if (!receivedTwitchPong)
                            {
                                "disconnected from irc.twitch.tv".Log("irc");
                                TwitchIrc.OnRawMessage -= onRawMessage;
                                connectedTwitch = false;
                                connect();
                            }
                        });
                    }
                    else
                    {
                        "not connected to irc.twitch.tv".Log("irc");
                    }
                };

                twitchPingTimer.Start();
            }
        }

        void onRawMessage(object s, IrcEventArgs e)
        {
            if (e.Data.RawMessageArray.Length > 4 && e.Data.RawMessageArray[2] == "PRIVMSG")
            {
                OnTwitchChannelMessage(e.Data);
            }
            if (Program.Parameters.Verbose)
                Util.Log(e.Data.RawMessage);

            if (e.Data.RawMessageArray.Length > 0 && e.Data.RawMessageArray[0] == "PONG")
            {
                receivedTwitchPong = true;
            }
        }

        void OnTwitchChannelMessage(IrcMessageData data)
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
                if (data.Tags.ContainsKey("emotes"))
                    u.Name = data.Tags["display-name"];

                // emotes
                //if (C.EmoteClrServer != null)
                {
                    int firstOccurence = -1;
                    TwitchEmote emote = null;

                    var S = message.SplitWords();
                    int wordCount = S.Length;
                    int emoteCount = 0;
                    bool allSameEmotes = true;

                    if (data.Tags.ContainsKey("emotes") && !string.IsNullOrWhiteSpace(data.Tags["emotes"]))
                    {
                        try
                        {
                            // 80481:0-4,6-10,12-16,18-22/93064:24-30

                            string[] emotes = data.Tags["emotes"].Split('/');

                            string firstEmote = emotes[0];

                            foreach (string item in emotes.Skip(1))
                            {
                                if (item != firstEmote)
                                {
                                    allSameEmotes = false;
                                    break;
                                }
                            }

                            int index = firstEmote.IndexOf(':');
                            string id = firstEmote.Remove(index);

                            firstOccurence = int.Parse(firstEmote.Substring(index + 1).Split(',')[0].Split('-')[0]);

                            emoteCount += firstEmote.Substring(index + 1).Count(x => x == ',') + 1;

                            TwitchEmotesById.TryGetValue(int.Parse(id), out emote);
                        }
                        catch { }
                    }

                    TwitchEmote e = null;
                    TwitchEmote lastEmote = null;
                    foreach (string s in S)
                    {
                        if (BttvGlobalEmotes.TryGetValue(s, out e) || C.BttvChannelEmotes.TryGetValue(s, out e))
                        {
                            emoteCount++;
                            if (emote != null && e != lastEmote)
                            {
                                allSameEmotes = false;
                                break;
                            }
                            lastEmote = e;
                        }
                    }
                    emote = emote ?? lastEmote;

                    if (C.Settings.EnablePyramids && allSameEmotes && S.Length == emoteCount)
                    {
                        if (emote != C.PyramideEmote)
                        {
                            C.PyramideType = PyramideType.None;
                        }
                        C.PyramideEmote = emote;

                        start:

                        switch (C.PyramideType)
                        {
                            case PyramideType.None:
                                C.PyramideHeight = 1;
                                if (S.Length == 1)
                                {
                                    C.PyramideType = PyramideType.SingleEmote;
                                }
                                else
                                {
                                    C.PyramideType = PyramideType.E;
                                    C.PyramideWidth = S.Length;
                                }
                                break;
                            case PyramideType.SingleEmote:
                                if (S.Length > 3)
                                {
                                    C.PyramideType = PyramideType.Hammer;
                                    C.PyramideWidth = S.Length;
                                }
                                else
                                {
                                    C.PyramideType = PyramideType.None;
                                    goto start;
                                }
                                break;
                            case PyramideType.Hammer:
                                if (S.Length == 1)
                                {
                                    C.SayMe($"Congratulation {u}, you finished a {C.PyramideWidth} width and {C.PyramideHeight} height {emote.Name} {(u.ID == "swiftapples" || !C.Settings.EnableGachi ? "hammer" : "dick")} PogChamp");
                                    C.PyramideType = PyramideType.None;
                                }
                                else
                                {
                                    if (C.PyramideWidth != S.Length)
                                        C.PyramideType = PyramideType.None;
                                }
                                break;
                            case PyramideType.E:
                                if (C.PyramideHeight == 2 || C.PyramideHeight == 4)
                                {
                                    if (S.Length != 1)
                                    {
                                        C.PyramideType = PyramideType.None;
                                    }
                                }
                                else
                                {
                                    {
                                        if (C.PyramideHeight == 5)
                                        {
                                            C.SayMe($"Congratulations {u}, you finished a {C.PyramideWidth} width {emote.Name} \"E\" PogChamp");
                                            C.PyramideType = PyramideType.None;
                                        }
                                    }
                                    else
                                    {
                                        C.PyramideType = PyramideType.None;
                                    }
                                }
                                break;
                        }

                        C.PyramideHeight++;
                    }
                    else
                    {
                        C.PyramideType = PyramideType.None;
                    }

                    if (emote != null)
                    {
                        C.EmoteClrServer?.SendEmote(emote.Url);
                    }
                }

                // process commands
                C.TriggerMessageReceived(new MessageEventArgs(C, u, u.Name, message));
            }
            catch (Exception exc)
            {
                File.AppendAllText("error", exc.Message + "\r\n" + exc.StackTrace + "\r\n--------\r\n");
            }
        }


        // COMMANDS
        public List<Command> Commands { get; private set; } = new List<Command>();
        public Dictionary<string, string> CommandAliases { get; private set; } = new Dictionary<string, string>();

        public ConcurrentDictionary<string, long> CommandUses { get; private set; } = new ConcurrentDictionary<string, long>();

        public long GetCommandUses(string name)
        {
            long l = 0;
            CommandUses.TryGetValue(name.ToLower(), out l);
            return l;
        }


        // EVAL COMMANDS
        public List<EvalCommand> EvalCommands = new List<EvalCommand>();

        public string TwitchBotName { get; private set; }


        // ACTION QUEUE
        public List<Tuple<DateTime, Action>> DelayedActions { get; set; } = new List<Tuple<DateTime, Action>>();

        protected System.Timers.Timer secondsTimer = new System.Timers.Timer(1000);

        public void QueueAction(double seconds, Action action)
        {
            lock (DelayedActions)
            {
                DelayedActions.Add(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(seconds), action));
            }
        }

        protected void onSecondTick(object sender, EventArgs e)
        {
            var now = DateTime.Now;

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

        // CHANNELS
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

        public IEnumerable<TwitchChannel> TwitchChannels
        {
            get
            {
                List<TwitchChannel> C;
                lock (channels)
                {
                    C = new List<TwitchChannel>(twitchChannels);
                }
                foreach (var c in C)
                {
                    yield return c;
                }
            }
        }

        public IEnumerable<DiscordChannel> DiscordChannels
        {
            get
            {
                List<DiscordChannel> C;
                lock (channels)
                {
                    C = new List<DiscordChannel>(discordChannels);
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
                        TwitchChannel c = new TwitchChannel(this, channel);
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

            ProcessMessage(message, u, C);
        }

        public void ProcessMessage(string message, User u, Channel C)
        {
            bool isAdmin = C.IsOwner(u) || u.IsAdmin;
            bool isMod = u.IsMod;

            if (!u.IsBanned && message.Length > 2)
                if (C.Settings.EnableCommands && message[0] == '!')
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

                    if (evalCommand == null)
                        lock (C.ChannelEvalCommands)
                        {
                            evalCommand = C.ChannelEvalCommands.FirstOrDefault(x => x.Name == _command) ?? C.ChannelEvalCommands.FirstOrDefault(x => x.Name == aliasFor);
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
                                        $"{u.Name} {message}".Log("mods");
                                    }
                                }
                                else if (c.AdminOnly)
                                {
                                    if (isAdmin)
                                    {
                                        c.Action(message, u, C);
                                        $"{u.Name} {message}".Log("mods");
                                    }
                                }
                                else if (c.ModOnly)
                                {
                                    if (isMod || isAdmin)
                                    {
                                        c.Action(message, u, C);
                                        $"{u.Name} {message}".Log("mods");
                                    }
                                }
                                else
                                {
                                    c.Action(message, u, C);
                                    cooldown = true;
                                }

                                if (cooldown)
                                    C.UserCommandCache.Enqueue(Tuple.Create(DateTime.Now + TimeSpan.FromSeconds(12), u.Name, c.Name));

                                CommandUses.AddOrUpdate(c.Name, 1, (k, v) => v + 1);
                            }
                        }
                    });
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
                                    CommandUses[line.Remove(index)] = count;
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
                                    EvalCommands.Add(new EvalCommand(this, name.TrimStart('%'), line.Substring(index + 1)) { AdminOnly = name.IndexOf('%') != -1 });
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
            File.WriteAllLines("./db/stats.txt", CommandUses.Select(k => k.Key + "=" + k.Value));

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
    }
}
