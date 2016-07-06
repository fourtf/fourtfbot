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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace twitchbot.Twitch
{
    public class TwitchChannel : Channel
    {
        public TwitchChannel(Bot bot, string channel)
            : base(bot)
        {
            ChannelName = channel;

            setMessageTimer(false);

            userUpdateTimer.Elapsed += userUpdateTick;
            userUpdateTimer.Enabled = true;

            Bot.QueueAction(30, () => userUpdateTick(null, null));

            messageLimitTimer.Elapsed += (s, e) => { messagecount = 0; };
        }


        public void GiveEveryoneExceptSkyriseNowPointz(long pointz)
        {
            foreach (var t in UsersByID.Values)
            {
                if (t.Name != "skyrisenow")
                    t.Points += pointz;
            }
        }

        public void FixTokens()
        {
            foreach (var t in UsersByID.Values)
            {
                long count = t.ItemCount("slotmachine-token");
                if (count != 0)
                {
                    t.RemoveItem("slotmachine-token", count);
                    t.AddItem("token", count);
                }
            }
        }


        // PROPERTIES
        //public IrcClient Irc { get; set; } = null;
        public string ChannelName { get; set; }

        public EmoteClrServer EmoteClrServer { get; set; } = null;

        System.Timers.Timer userUpdateTimer = new System.Timers.Timer(5 * 1000 * 60);
        public override ChannelType Type { get { return ChannelType.Twitch; } }

        public bool IsMod { get; private set; } = true;

        public ConcurrentDictionary<string, TwitchEmote> BttvChannelEmotes { get; private set; } = new ConcurrentDictionary<string, TwitchEmote>();


        // MESSAGES
        System.Timers.Timer messageTimer;
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(7);

        public override ConcurrentDictionary<string, User> UsersByName
        {
            get
            {
                return Bot.TwitchUsersByName;
            }
        }

        public override ConcurrentDictionary<string, User> UsersByID
        {
            get
            {
                return Bot.TwitchUsersByID;
            }
        }

        public override string LongName
        {
            get
            {
                return ChannelName + "'s chat";
            }
        }
        
        public override string ChannelSaveID
        {
            get
            {
                return "twitch." + ChannelName;
            }
        }

        Queue<Message> messageQueue = new Queue<Message>();

        struct Message
        {
            public string Text { get; set; }
            public string Channel { get; set; }
            public DateTime ExpireDate { get; set; }
        }

        int messagecount = 0;

        void setMessageTimer(bool isMod)
        {
            if (isMod != IsMod)
            {
                IsMod = isMod;
                messageTimer = new System.Timers.Timer(isMod ? 0.01 : 1.1);
                messageTimer.Elapsed += (s, e) =>
                {
                    if (messagecount < (IsMod ? 40 : 10))
                    {
                        lock (messageQueue)
                        {
                            while (messageQueue.Count > 0)
                            {
                                Message msg = messageQueue.Dequeue();

                                if (msg.ExpireDate < DateTime.Now)
                                    continue;

                                messagecount++;

                                Bot.TwitchIrc?.SendMessage(SendType.Message, "#" + msg.Channel, msg.Text);

                                break;
                            }

                            messageTimer.Enabled = messageQueue.Count > 0;
                        }
                    }
                };
            }
        }

        System.Timers.Timer messageLimitTimer = new System.Timers.Timer(1000 * 15);


        // METHODS
        public override void Say(string message, bool slashMe, bool force)
        {
            SayRaw((slashMe ? "/me " : ". ") + message, force);
        }

        public override void TryWhisperUser(User u, string message)
        {
            if (Bot.EnableTwitchWhispers)
                Bot.TwitchIrc?.SendMessage(SendType.Message, "#jtv", $"/w {u} {message}");
            else
                Say($"{u}, {message}");
        }


        DateTime lastMessage = DateTime.MinValue;

        public override void SayRaw(string message, bool force)
        {
            if (!Settings.EnableLinks)
            {
                message = message.Replace(".", ". ");
                message = message.Length > 193 ? message.Remove(190) + "..." : message;
            }

            if (message.Length > Settings.MaxMessageLength)
                message = message.Remove(Settings.MaxMessageLength - 3) + "...";

            lock (messageQueue)
            {
                if (!messageTimer.Enabled)
                {
                    messagecount++;
                    if (messagecount < (IsMod ? 40 : 10))
                    {
                        lastMessage = DateTime.Now;
                        Bot.TwitchIrc?.SendMessage(SendType.Message, "#" + ChannelName, message);
                        messageTimer.Enabled = true;
                    }
                }
                else
                {
                    messageQueue.Enqueue(new Message { ExpireDate = DateTime.Now + (!force ? TimeSpan.FromDays(1) : MessageTimeout), Text = message, Channel = ChannelName });
                }
            }
        }

        public User GetUser(string user)
        {
            return UsersByName[user.ToLower().Trim()];
        }

        //public override User GetOrCreateUser(string id, string name)
        //{
        //    return base.GetOrCreateUser(id.ToLower(), name);
        //}

        //public override User GetUserOrDefaultByName(string id)
        //{
        //    return base.GetUserOrDefaultByName(id.ToLower());
        //}

        // PRIVATE
        void userUpdateTick(object sender, EventArgs e)
        {
            new Task(() =>
            {
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        string response = client.DownloadString($"htt{""}p://tmi.twitch.tv/group/user/{ChannelName.TrimStart('#')}/chatters");
                        dynamic root = JsonConvert.DeserializeObject<dynamic>(response);

                        Action<string> processUser = (u) =>
                        {
                            User user = GetUserOrDefaultByName(u.ToLower());
                            if (user != null)
                                user.Points += 3;
                        };

                        bool isMod = false;

                        dynamic chatters = root.chatters;
                        if (chatters != null)
                        {
                            foreach (string u in chatters?.moderators ?? new string[] { })
                            {
                                if (u.ToLower() == Bot.TwitchBotName)
                                    isMod = true;
                                processUser(u);
                            }
                            foreach (string u in chatters?.staff ?? new string[] { })
                                processUser(u);
                            foreach (string u in chatters?.admins ?? new string[] { })
                                processUser(u);
                            foreach (string u in chatters?.global_mods ?? new string[] { })
                                processUser(u);
                            foreach (string u in chatters?.viewers ?? new string[] { })
                                processUser(u);
                        }

                        setMessageTimer(isMod);
                    }
                }
                catch (Exception exc)
                {
                    File.WriteAllText("userupdateerror", exc.Message + "\r\n\r\n" + exc.StackTrace);
                }
            }).Start();

            foreach (var u in UsersByID.Values.Where(x => x.Flags.HasFlag(UserFlags.Mod)))
            {
                ModReffleValueAvailable.AddOrUpdate(u.ID, 10000, (key, i) => Math.Min(i + 40, 10000));
            }
        }

        public override void Connect()
        {
            new Task(() =>
            {
                try
                {
                    JsonParser parser = new JsonParser();

                    var bttvChannelEmotesCache = "./db/twitch." + ChannelName + ".bttv_channel_emotes.json";

                    if (!File.Exists(bttvChannelEmotesCache) || DateTime.Now - new FileInfo(bttvChannelEmotesCache).LastWriteTime > TimeSpan.FromHours(24))
                    {
                        try
                        {
                            if (Util.IsLinux)
                            {
                                Util.LinuxDownloadFile("https://api.betterttv.net/2/channels/" + ChannelName, bttvChannelEmotesCache);
                            }
                            else
                            {
                                using (var webClient = new WebClient())
                                using (var readStream = webClient.OpenRead("https://api.betterttv.net/2/channels/" + ChannelName))
                                using (var writeStream = File.OpenWrite(bttvChannelEmotesCache))
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

                    using (var stream = File.OpenRead(bttvChannelEmotesCache))
                    {
                        dynamic json = parser.Parse(stream);
                        var template = "https:" + json["urlTemplate"]; //{{id}} {{image}}

                        foreach (dynamic e in json["emotes"])
                        {
                            string id = e["id"];
                            string code = e["code"];
                            string imageType = e["imageType"];
                            string url = template.Replace("{{id}}", id).Replace("{{image}}", "3x");

                            BttvChannelEmotes[code] = new TwitchEmote { EmoteSet = -1, Height = 112, Width = 112, Name = code, Type = EmoteType.BttvChannel, Url = url, ImageFormat = imageType };
                        }
                    }

                }
                catch { }
            }).Start();

            Bot.TwitchIrc.RfcJoin("#" + ChannelName);

            messageLimitTimer.Start();
        }

        public override void Disconnect()
        {
            Bot.TwitchIrc?.RfcPart("#" + ChannelName);
        }

        public override void Save()
        {
            base.Save();

            //try
            //{
            //    var savePath = "./db/twitch-" + ChannelName;

            //    using (Stream filestream = File.OpenWrite(savePath))
            //    {
            //        using (Stream stream = new GZipStream(filestream, CompressionMode.Compress))
            //        {
            //            foreach (User u in Users.Values)
            //            {
            //                stream.WriteByte(0);

            //                stream.WriteString(u.Name);

            //                if (u.Calories != 0) { stream.WriteByte(0x01); stream.WriteLong(u.Calories); }
            //                if (u.MessageCount != 0) { stream.WriteByte(0x02); stream.WriteLong(u.MessageCount); }
            //                if (u.CharacterCount != 0) { stream.WriteByte(0x03); stream.WriteLong(u.CharacterCount); }
            //                if (u.Points != 0) { stream.WriteByte(0x04); stream.WriteLong(u.Points); }

            //                if (u.Flags != 0) { stream.WriteByte(0x05); stream.WriteInt((int)u.Flags); }
            //                if (u.GachiGASM != 0) { stream.WriteByte(0x06); stream.WriteLong(u.GachiGASM); }

            //                if (u.Inventory != null)
            //                {
            //                    lock (u.Inventory)
            //                    {
            //                        foreach (InventoryItem item in u.Inventory)
            //                        {
            //                            stream.WriteByte(0x10);
            //                            stream.WriteString(item.Name);
            //                            stream.WriteLong(item.Count);
            //                        }
            //                    }
            //                }
            //            }
            //            stream.WriteByte(0xFF);
            //        }
            //    }
            //}
            //catch { }
        }

        public override void Load()
        {
            base.Load();

            //try
            //{
            //    var savePath = "./db/twitch-" + ChannelName;

            //    if (File.Exists(savePath))
            //    {
            //        try
            //        {
            //            using (Stream filestream = File.OpenRead(savePath))
            //            {
            //                using (Stream stream = new GZipStream(filestream, CompressionMode.Decompress))
            //                {
            //                    if (stream.ReadByte() != 0)
            //                        throw new Exception();

            //                    while (true)
            //                    {
            //                        User user = new User();
            //                        user.Name = stream.ReadString();

            //                        while (true)
            //                        {
            //                            switch (stream.ReadByte())
            //                            {
            //                                case 0:
            //                                    Users[user.Name] = user;
            //                                    goto end;
            //                                case 1:
            //                                    user.Calories = stream.ReadLong();
            //                                    break;
            //                                case 2:
            //                                    user.MessageCount = stream.ReadLong();
            //                                    break;
            //                                case 3:
            //                                    user.CharacterCount = stream.ReadLong();
            //                                    break;
            //                                case 4:
            //                                    user.Points = stream.ReadLong();
            //                                    break;
            //                                case 5:
            //                                    user.Flags = (UserFlags)stream.ReadInt();
            //                                    break;
            //                                case 6:
            //                                    user.GachiGASM = stream.ReadLong();
            //                                    break;
            //                                case 0x10:
            //                                    user.AddItem(stream.ReadString(), stream.ReadLong());
            //                                    break;
            //                                case 0xFF:
            //                                    Users[user.Name] = user;
            //                                    goto veryend;
            //                            }
            //                        }
            //                        end:;
            //                    }
            //                    veryend:;
            //                }
            //            }
            //        }
            //        catch
            //        {

            //        }
            //    }
            //}
            //catch { }
        }

        public void EasyQuest(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SayMe("Such an easy quest LUL");
            }
        }

        public override bool IsOwner(User user)
        {
            return string.Equals(Bot.TwitchOwner, user.ID, StringComparison.OrdinalIgnoreCase);
        }
    }
}
