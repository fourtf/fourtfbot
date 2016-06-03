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
using System.Threading.Tasks;

namespace twitchbot.Twitch
{
    public class TwitchChannel : Channel
    {
        public TwitchChannel(Bot bot, IrcClient irc, string channel)
            : base(bot)
        {
            ChannelName = channel;

            Irc = irc;
            setMessageTimer(false);

            userUpdateTimer.Elapsed += userUpdateTick;
            userUpdateTimer.Enabled = true;

            QueueAction(30, () => userUpdateTick(null, null));

            messageLimitTimer.Elapsed += (s, e) => { messagecount = 0; };
        }


        // PROPERTIES
        public IrcClient Irc { get; private set; }
        public string ChannelName { get; set; }

        System.Timers.Timer userUpdateTimer = new System.Timers.Timer(5 * 1000 * 60);
        public override ChannelType Type { get { return ChannelType.Twitch; } }

        public bool IsMod { get; private set; } = true;


        // MESSAGES
        System.Timers.Timer messageTimer;
        public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(7);

        public override string UserSavePath
        {
            get
            {
                return "./db/twitch-" + ChannelName;
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
                messageTimer = new System.Timers.Timer(isMod ? 0.01 : 1.05);
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

                            Irc.SendMessage(SendType.Message, "#" + msg.Channel, msg.Text);

                            break;
                        }

                        messageTimer.Enabled = messageQueue.Count > 0;
                    }
                };
            }
        }

        System.Timers.Timer messageLimitTimer = new System.Timers.Timer(1000 * 15);


        // METHODS
        public override void Say(string message, bool slashMe, bool force)
        {
            SayRaw((slashMe ? "/me " : ". ") + (message.Length < 360 ? message : message.Remove(357) + "..."), force);
        }

        public override void TryWhisperUser(User u, string message)
        {
            if (Bot.EnableWhispers)
                Irc.SendMessage(SendType.Message, "#jtv", $"/w {u} {message}");
            else
                Say($"{u}, {message}");
        }


        DateTime lastMessage = DateTime.MinValue;
        public override void SayRaw(string message, bool force)
        {
            lock (messageQueue)
            {
                if (!messageTimer.Enabled)
                {
                    messagecount++;
                    if (messagecount < (IsMod ? 50 : 10))
                    {
                        lastMessage = DateTime.Now;
                        Irc.SendMessage(SendType.Message, "#" + ChannelName, message);
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
                            User user = GetOrCreateUser(u.ToLower(), u);

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
            Irc.RfcJoin("#" + ChannelName);

            messageLimitTimer.Start();
        }

        public override void Disconnect()
        {
            Irc.RfcPart("#" + ChannelName);
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
            return Bot.TwitchOwner == user.ID;
        }
    }
}
