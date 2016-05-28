﻿using DynamicExpresso;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace twitchbot
{
    public static class Program
    {
        static Regex singleItem = new Regex(@"^[^\s]+\s+([^\s]+)");

        public static string Owner { get; private set; }
        public static string UrlRoot { get; private set; } = "no url set up";
        public static string RecipesUrl { get; private set; } = "no url set up";
        public static string UserUrl { get; private set; } = "no url set up";

        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).Directory.FullName);

            Bot bot;

            try
            {
                Owner = File.ReadAllText("owner").Trim();
                string username = File.ReadAllText("username").Trim();
                string oauthkey = File.ReadAllText("oauthkey").Trim();
                string[] channels = File.ReadAllLines("channels").Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();

                try
                {
                    UrlRoot = File.ReadAllText("urlroot").Trim();
                    RecipesUrl = UrlRoot + "recipes/";
                    UserUrl = UrlRoot + "user/";
                }
                catch { }

                bot = new Bot(username, oauthkey)
                {
                    Admin = Owner,
                };

                foreach (var c in channels)
                {
                    bot.AddChannel(ChannelType.Twitch, c);
                }


                bot.Connect();
            }
            catch (Exception exc)
            {
                Console.WriteLine("Couldn't start bot: " + exc.Message);
                Console.ReadKey();
                return;
            }

            bot.EnableWhispers = true;

            bot.Channels.FirstOrDefault(x => (x as Twitch.TwitchChannel)?.ChannelName.Equals("pajlada") ?? false).Process(x => new Thread(Api.StartApiServer).Start(x));

            // ITEM COMMANDS
            #region pointz
            bot.Commands.Add(new Command(
                "pointz",
                (m, u, c) =>
                {
                    var match = singleItem.Match(m.ToUpper());
                    if (match.Success)
                    {
                        User user;
                        string s = match.Groups[1].Value.ToLower();
                        if (c.Users.TryGetValue(s, out user))
                        {
                            c.Say($"{s} has {user.Points} pointz.");
                            return;
                        }
                    }

                    c.TryWhisperUser(u, $"{u}, you have {u.Points} pointz.");
                    //c.Say($"{u}, you have {u.Points} pointz.");
                },
                hasUserCooldown: false
                ));

            bot.Commands.Add(new Command(
                "userpointz",
                (m, u, c) =>
                {
                    var match = singleItem.Match(m.ToUpper());
                    if (match.Success)
                    {
                        User user;
                        string s = match.Groups[1].Value.ToLower();
                        if (c.Users.TryGetValue(s, out user))
                        {
                            c.Say($"{s} has {user.Points} pointz.");
                            return;
                        }
                    }

                    c.Say($"{u}, you have {u.Points} pointz.");
                    //c.Say($"{u}, you have {u.Points} pointz.");
                },
                hasUserCooldown: false
                ));
            #endregion

            #region roulete
            bot.Commands.Add(new Command(
                "roulete",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    ShopItem item;
                    long count;

                    if ((S.TryGetItemOrPointz(1, out item) && S.TryGetInt(2, true, item == null ? u.Points : u.ItemCount(item.Name), out count).True()) ||
                        (S.TryGetItemOrPointz(2, out item).True() && S.TryGetInt(1, true, item == null ? u.Points : u.ItemCount(item.Name), out count)))
                    {
                        long currentCount = item == null ? u.Points : u.ItemCount(item.Name);

                        if (count == 0)
                            c.Say($"{u} is trying to bet 0 {item.GetPlural()} LUL");
                        else if (currentCount > 0 ? count < 0 || currentCount < count : count > 0 || currentCount > count)
                            c.TryWhisperUser(u, $"You don't have that many {item.GetPlural()}");
                        else if (Util.GetRandom(50))
                        {
                            c.Say($"{u} lost {item.GetNumber(count)} in roulete and now has {item.GetNumber(currentCount - count)}! LUL");
                            if (item == null)
                                u.Points -= count;
                            else
                                u.RemoveItem(item.Name, count);
                        }
                        else
                        {
                            c.Say($"{u} won {item.GetNumber(count)} in roulete and now has {item.GetNumber(currentCount + count)}! PogChamp");
                            if (item == null)
                                u.Points += count;
                            else
                                u.AddItem(item.Name, count);
                        }
                    }
                    else
                    {
                        c.Say($"{u}, to gamble type !roulete <count> or !roulete <count> <item>");
                    }
                }
                ));
            #endregion

            #region buy
            bot.Commands.Add(new Command(
            "buy",
            (m, u, c) =>
            {
                string[] S = m.ToLower().Split();

                long count;
                ShopItem item;

                if ((S.TryGetItemOrPointz(1, out item) && S.TryGetInt(2, true, item.Price == 0 ? 1 : (u.Points / item.Price), out count).True()) ||
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(1, true, item.Price == 0 ? 1 : (u.Points / item.Price), out count)))
                {
                    if (item.Price == 0)
                    {
                        c.TryWhisperUser(u, $"{u}, {item.GetPlural()} are not available for sale.");
                    }
                    else
                    {
                        if ((item.Price > 0 && u.Points >= item.Price * count) || (item.Price < 0 && u.Points <= item.Price * count))
                        {
                            c.TryWhisperUser(u, $"{u}, you bought {item.GetNumber(count)} for {item.Price} pointz{(count == 1 || count == -1 ? "" : " each")}.");

                            u.Points -= item.Price * count;
                            u.AddItem(item.Name, count);
                            if (item.Name == "vape")
                            {
                                u.AddItem("liquid", 5 * count);
                            }
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you don't have enough pointz to buy {item.GetNumber(count)}.");
                        }
                    }
                }
                else
                {
                    string s = "items (price) in the shop: ";
                    lock (ShopItem.Items)
                    {
                        foreach (var i in ShopItem.Items.Values)
                        {
                            if (i.Price > 0)
                                s += i.Name + " (" + i.Price + "), ";
                        }
                    }

                    c.Say(s.TrimEnd(' ', ','));
                }
            },
                hasUserCooldown: false
            ));

            bot.Commands.Add(new Command(
            "blackmarket",
            (m, u, c) =>
            {
                string[] S = m.Split();
                if (S.Length > 1)
                    c.TryWhisperUser(u, $"{u}, use !buy <item> to buy items from one of the shops :)");

                string s = "a shady dealer shows you the following items (price): ";
                lock (ShopItem.Items)
                {
                    foreach (var item in ShopItem.Items.Values)
                    {
                        if (item.Price < 0)
                            s += item.Name + " (" + item.Price + "), ";
                    }
                }

                c.Say(s.TrimEnd(' ', ','));
            },
            cooldown: TimeSpan.FromSeconds(10)));
            #endregion

            #region items
            bot.Commands.Add(new Command(
            "items",
            (m, u, c) =>
            {
                string[] S = m.ToLowerInvariant().Split();

                User user;
                S.TryGetUser(1, c, out user);
                user = user ?? u;

                string ans = $"";

                if (user.Inventory != null)
                {
                    lock (user.Inventory)
                    {
                        if (user.Inventory.Count > 20)
                        {
                            if (u == user)
                                c.Say($"{u}, your inventory is too large to send as text FeelsGoodMan {UserUrl + user.Name}");
                            else
                                c.Say($"{user} has too many items to print LUL {UserUrl + user.Name}");
                            return;
                        }
                        else
                        {
                            foreach (var item in user.Inventory)
                            {
                                ans += item.Count + " " + item.Name + (item.Count == 1 ? "" : "s") + ", ";
                            }
                        }
                    }
                }

                if (u == user)
                    c.TryWhisperUser(u, $"You have {(user.Inventory == null ? "no items FeelsBadMan" : ans.TrimEnd(' ', ','))}");
                else
                    c.Say($"{u}, {(u == user ? "you have" : user.Name + " has")} {(user.Inventory == null ? "no items FeelsBadMan" : ans.TrimEnd(' ', ','))}");
            }
            ));

            bot.Commands.Add(new Command(
            "useritems",
            (m, u, c) =>
            {
                string[] S = m.ToLowerInvariant().Split();

                User user;
                S.TryGetUser(1, c, out user);
                user = user ?? u;

                string ans = $"";

                if (user.Inventory != null)
                {
                    lock (user.Inventory)
                    {
                        if (user.Inventory.Count > 20)
                        {
                            c.Say($"{u}, your inventory is too large to send as text FeelsGoodMan {UserUrl + user.Name}");
                            return;
                        }

                        foreach (var item in user.Inventory)
                        {
                            ans += item.Count + " " + item.Name + (item.Count == 1 ? "" : "s") + ", ";
                        }
                    }
                }

                c.Say($"{u}, {(u == user ? "you have" : user.Name + " has")} {(user.Inventory == null ? "no items FeelsBadMan" : ans.TrimEnd(' ', ','))}");
            }
            ));
            #endregion

            #region eat
            Func<ShopItem, string> getEatEmote = (item) =>
            {
                string emote = " OpieOP";
                if (item.Name == "hamster")
                    emote = " KKona Call PETA";
                else if (item.Name == "dog")
                    emote = ". Call PETA FrankerZ";
                else if (item.Name == "cat")
                    emote = " MingLee Call PETA";
                else if (item.Name == "negative-cat")
                    emote = " CoolCat";
                else if (item.Name == "cobra")
                    emote = " OSkomodo Call PETA";
                return emote;
            };

            bot.Commands.Add(new Command(
            "eat",
            (m, u, c) =>
            {
                string[] S = m.ToLower().Split();

                long count;
                ShopItem item;

                if ((S.TryGetItemOrPointz(1, out item) && S.TryGetInt(2, true, item.Price == 0 ? 1 : u.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(1, true, item.Price == 0 ? 1 : u.ItemCount(item.Name), out count)))
                {
                    if (!item.Edible)
                    {
                        if (item.Flags.HasFlag(ShopItemFlags.Liquid))
                            c.Say($"{u}, the only liquid you can eat is bacon-liquid OpieOP");
                        else
                            c.Say($"{u} is trying to eat {item.GetPlural()} LUL");
                    }
                    else
                    {
                        if (count <= u.ItemCount(item.Name))
                        {
                            c.Say($"{u} ate {item.GetNumber(count)} with {item.Calories} calories{(count > 1 ? " each" : "")}{getEatEmote(item)}");

                            u.RemoveItem(item.Name, count);
                            u.Calories += count * item.Calories;
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you don't have that many {item.GetPlural()}.");
                        }
                    }
                }
                else
                {
                    c.Say($"{u}, to eat something type \"!eat <count> <item>\" SeemsGood");
                }

            },
                hasUserCooldown: false
            ));
            #endregion

            #region diet
            bot.Commands.Add(new Command(
            "diet",
            (m, u, c) =>
            {
                string[] S = m.ToLower().Split();

                long calories;

                if (S.TryGetInt(1, true, u.Calories, out calories))
                {
                    if (calories == 0)
                        c.Say($"{u} is trying to lose 0 calories LUL");
                    else if (u.Calories > 0 ? calories < 0 || u.Calories < calories : calories > 0 || u.Calories > calories)
                        c.Say($"{u}, you don't have that many calories FeelsGoodMan");
                    else if (Util.GetRandom(66))
                    {
                        c.Say($"{u} lost {calories} calories with their diet and now has {u.Calories - calories} calories! FeelsGoodMan");
                        u.Calories -= calories;
                    }
                    else
                    {
                        c.Say($"{u} did the diet but got frustrated, gained {(long)(calories * 2.5)} calories and now has {u.Calories + (long)(calories * 2.5)} calories FeelsBadMan");
                        u.Calories += (long)(calories * 2.5);
                    }
                }
                else
                {
                    c.Say($"{u}, type !diet <count> loose some calories OpieOP");
                }
            }
            ));

            bot.Commands.Add(new Command(
            "diet",
            (m, u, c) =>
            {
                c.Say($"{u}, to gamble you calories type !diet <count>");
            }
            ));
            #endregion

            #region give
            bot.Commands.Add(new Command(
            "give",
            (m, u, c) =>
            {
                string[] S = m.ToLower().Split();

                User target;
                ShopItem item;
                long count;

                if (S.TryGetUser(1, c, out target) && (
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(3, false, item == null ? u.Points : u.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(3, out item).True() && S.TryGetInt(2, false, item == null ? u.Points : u.ItemCount(item.Name), out count))))
                {
                    if (item == null)
                    {
                        if (u.Points >= count)
                        {
                            c.Say($"{u} gave {target.Name} {item.GetNumber(count)}");
                            u.Points -= count;
                            target.Points += count;
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you don't have that many pointz.");
                        }
                    }
                    else
                    {
                        if (u.HasItem(item.Name, count))
                        {
                            c.Say($"{u} gave {target.Name} {item.GetNumber(count)} ");
                            u.RemoveItem(item.Name, count);
                            target.AddItem(item.Name, count);
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you don't have that many {item.GetPlural()}.");
                        }
                    }
                }
            },
                hasUserCooldown: false
            ));

            bot.Commands.Add(new Command(
            "give2",
            (m, u, c) =>
            {
                string[] S = m.ToLower().Split();

                User target;
                ShopItem item;
                long count;

                if (S.TryGetUser(1, c, out target) && (
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(3, false, item == null ? u.Points : u.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(3, out item).True() && S.TryGetInt(2, false, item == null ? u.Points : u.ItemCount(item.Name), out count))))
                {
                    if (item == null)
                    {
                        c.Say($"{u} gave {target.Name} {item.GetNumber(count)} Keepo");
                        target.Points += count;
                    }
                    else
                    {
                        c.Say($"{u} gave {target.Name} {item.GetNumber(count)} Keepo");
                        target.AddItem(item.Name, count);
                    }
                }
            },
            adminOnly: true
            ));
            #endregion

            #region fight
            bot.Commands.Add(new Command(
            "fight",
            (m, u, c) =>
            {
                string[] S = m.ToLowerInvariant().Split();

                User user;
                ShopItem item;
                long count;

                if (S.TryGetUser(1, c, out user) && (
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(3, false, item == null ? user.Points : user.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(3, out item).True() && S.TryGetInt(2, false, item == null ? user.Points : user.ItemCount(item.Name), out count))))
                {
                    if (!u.IsAdmin && u == user)
                    {
                        c.Say($"{u} is trying to fight himself LUL");
                        return;
                    }

                    lock (c.Duels)
                    {
                        if (c.Duels.Any(d => d.ToUser == user.Name))
                        {
                            c.TryWhisperUser(u, $"{user} is already getting duelled.");
                        }
                        else
                        {
                            if (item == null)
                            {
                                if (u.Points < count)
                                {
                                    c.TryWhisperUser(u, $"You don't have {item.GetNumber(count)}. You currently have {item.GetNumber(u.Points)}.");
                                    return;
                                }
                                else if (user.Points < count)
                                {
                                    c.TryWhisperUser(u, $"{user} doesn't have {item.GetNumber(count)}. They only have {item.GetNumber(user.Points)}.");
                                    return;
                                }
                            }
                            else
                            {
                                if (u.ItemCount(item.Name) < count)
                                {
                                    c.TryWhisperUser(u, $"You don't have {item.GetNumber(count)}. You currently have {item.GetNumber(u.ItemCount(item.Name))}.");
                                    return;
                                }
                                else if (user.ItemCount(item.Name) < count)
                                {
                                    c.TryWhisperUser(u, $"{user} doesn't have {item.GetNumber(count)}. They only have {item.GetNumber(user.ItemCount(item.Name))}.");
                                    return;
                                }
                            }

                            c.Duels.Add(new Channel.DuelItem
                            {
                                ExpireDate = DateTime.Now + c.DuelTimeout,
                                Count = count,
                                Item = item,
                                FromUser = u.Name,
                                ToUser = user.Name
                            });

                            c.Say($"{u} is dueling {user} for {item.GetNumber(count)} PogChamp");

                            c.TryWhisperUser(user, $"{u} is dueling you for {item.GetNumber(count)}. Type \"!fight accept\" in chat to accept or \"!fight deny\" to deny.");
                        }
                    }
                }
                else if (S.TryIsString(1, "accept"))
                {
                    lock (c.Duels)
                    {
                        int index;
                        if ((index = c.Duels.FindIndex(d => d.ToUser == u.Name)) != -1)
                        {
                            var duel = c.Duels[index];

                            User from = c.GetUserOrDefault(duel.FromUser);

                            if (from == null)
                                return;

                            if (duel.Item == null ? (u.Points < duel.Count) : (u.ItemCount(duel.Item.Name) < duel.Count))
                            {
                                c.Say($"{c}, you don't have enought {duel.Item.GetPlural()}, the duel was canceled.");
                            }
                            else if (duel.Item == null ? (from.Points < duel.Count) : (from.ItemCount(duel.Item.Name) < duel.Count))
                            {
                                c.Say($"{c}, {from} doesn't have enought {duel.Item.GetPlural()}, the duel was canceled.");
                            }
                            else
                            {
                                User winner = Util.GetRandom(50) ? u : from;
                                User loser = winner == u ? from : u;

                                c.Say($"{winner} won the duel against {loser} and got {duel.Item.GetNumber(duel.Count)} PogChamp", true, true);
                                c.TryWhisperUser(winner, $"You won the duel against {loser} and got {duel.Item.GetNumber(duel.Count)} PogChamp");
                                c.TryWhisperUser(loser, $"You lost the duel against {winner} for {duel.Item.GetNumber(duel.Count)} FeelsBadMan");

                                if (duel.Item == null)
                                {
                                    winner.Points += duel.Count;
                                    loser.Points -= duel.Count;
                                }
                                else
                                {
                                    winner.AddItem(duel.Item.Name, duel.Count);
                                    loser.RemoveItem(duel.Item.Name, duel.Count);
                                }
                            }
                            c.Duels.RemoveAt(index);
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"You are not getting duelled at the moment.");
                        }
                    }
                }
                else if (S.TryIsString(1, "deny"))
                {
                    lock (c.Duels)
                    {
                        int index;
                        if ((index = c.Duels.FindIndex(d => d.ToUser == u.Name)) != -1)
                        {
                            var duel = c.Duels[index];

                            User from = c.GetUserOrDefault(duel.FromUser);

                            if (from == null)
                                return;

                            c.TryWhisperUser(from, $"{u} denied your duel for {duel.Item.GetNumber(duel.Count)}.");

                            c.Duels.RemoveAt(index);
                        }
                    }
                }
                else
                {
                    c.Say($"{u}, to duel someone type !fight <user> <count> [item]");
                }
            }
            ));
            #endregion

            #region trade
            bot.Commands.Add(new Command(
            "trade",
            (m, u, c) =>
            {
                string[] S = m.ToLowerInvariant().Split();

                bool worked = true;

                User user;

                List<Tuple<ShopItem, long>> gives = new List<Tuple<ShopItem, long>>();
                List<Tuple<ShopItem, long>> wants = new List<Tuple<ShopItem, long>>();

                int i = 0;

                if (worked)
                {
                    do
                    {
                        i++;
                        ShopItem item;
                        long count;
                        if (S.TryGetItemOrPointz(i + 1, out item) && S.TryGetInt(i, false, item == null ? u.Points : u.ItemCount(item.Name), out count))
                        {
                            var item2 = gives.FirstOrDefault(x => x.Item1 == item);
                            if (item2 != null)
                            {
                                gives.Remove(item2);
                                gives.Add(Tuple.Create(item, count + item2.Item2));
                            }
                            else
                            {
                                gives.Add(Tuple.Create(item, count));
                            }
                            i += 2;
                        }
                        else if (S.TryGetItemOrPointz(i, out item))
                        {
                            var item2 = gives.FirstOrDefault(x => x.Item1 == item);
                            if (item2 != null)
                            {
                                gives.Remove(item2);
                                gives.Add(Tuple.Create(item, 1 + item2.Item2));
                            }
                            else
                            {
                                gives.Add(Tuple.Create(item, 1L));
                            }
                            i++;
                        }
                        else
                        {
                            worked = false;
                            break;
                        }
                    }
                    while (S.TryIsString(i, "and"));

                    if (!S.TryIsString(i, "for"))
                    {
                        worked = false;
                    }
                }

                if (worked)
                {
                    do
                    {
                        i++;
                        ShopItem item;
                        long count;
                        if (S.TryGetItemOrPointz(i + 1, out item) && S.TryGetInt(i, false, null, out count))
                        {
                            var item2 = wants.FirstOrDefault(x => x.Item1 == item);
                            if (item2 != null)
                            {
                                wants.Remove(item2);
                                wants.Add(Tuple.Create(item, count + item2.Item2));
                            }
                            else
                            {
                                wants.Add(Tuple.Create(item, count));
                            }
                            i += 2;
                        }
                        else if (S.TryGetItemOrPointz(i, out item))
                        {
                            var item2 = wants.FirstOrDefault(x => x.Item1 == item);
                            if (item2 != null)
                            {
                                wants.Remove(item2);
                                wants.Add(Tuple.Create(item, 1 + item2.Item2));
                            }
                            else
                            {
                                wants.Add(Tuple.Create(item, 1L));
                            }
                            i++;
                        }
                        else
                        {
                            worked = false;
                            break;
                        }
                    }
                    while (S.TryIsString(i, "and"));
                }

                if (i < S.Length - 1)
                {
                    worked = false;
                }

                if (worked)
                {
                    lock (c.Trades)
                    {
                        var item = c.Trades.FirstOrDefault(x => x.User == u.Name);
                        if (item != null)
                            c.Trades.Remove(item);
                    }

                    var notEnough = gives.Where(x => x.Item1 == null ? x.Item2 > u.Points : x.Item2 > u.ItemCount(x.Item1.Name));

                    if (notEnough.Count() > 0)
                    {
                        c.TryWhisperUser(u, "You don't have enought of the following items: " + string.Join(", ", notEnough.Select(x => x.Item1?.Name ?? "pointz")));
                    }
                    else
                    {
                        lock (c.Trades)
                        {
                            c.Trades.Add(new Channel.TradeItem { ExpireDate = DateTime.Now + c.TradeTimeout, User = u.Name, Wants = wants, Gives = gives });
                            c.Say($"type \"!trade {u.Name}\" to accept their trade SeemsGood");
                        }
                    }
                }
                else if (S.TryGetUser(1, c, out user))
                {
                    lock (c.Trades)
                    {
                        var trade = c.Trades.FirstOrDefault(x => x.User == user.Name);
                        if (trade != null)
                        {
                            if (trade.Gives.Any(x => x.Item1 == null ? x.Item2 > u.Points : x.Item2 > u.ItemCount(x.Item1.Name)))
                            {
                                c.Say($"{u}, {trade.User} does not have enough items/pointz anymore. The trade was canceled.");
                                c.Trades.Remove(trade);
                            }
                            else if (trade.Wants.Any(x => x.Item1 == null ? x.Item2 > user.Points : x.Item2 > user.ItemCount(x.Item1.Name)))
                            {
                                c.TryWhisperUser(user, $"You don't have enough items/pointz to accept the trade of {trade.User}.");
                            }
                            else
                            {
                                c.Say($"{u} traded with {user} SeemsGood");

                                foreach (var x in trade.Gives)
                                {
                                    if (x.Item1 == null)
                                    {
                                        user.Points -= x.Item2;
                                        u.Points += x.Item2;
                                    }
                                    else
                                    {
                                        user.RemoveItem(x.Item1.Name, x.Item2);
                                        u.AddItem(x.Item1.Name, x.Item2);
                                    }
                                }
                                foreach (var x in trade.Wants)
                                {
                                    if (x.Item1 == null)
                                    {
                                        u.Points -= x.Item2;
                                        user.Points += x.Item2;
                                    }
                                    else
                                    {
                                        u.RemoveItem(x.Item1.Name, x.Item2);
                                        user.AddItem(x.Item1.Name, x.Item2);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (S.TryIsString(1, "cancel"))
                {
                    lock (c.Trades)
                    {
                        var item = c.Trades.FirstOrDefault(x => x.User == u.Name);
                        if (item != null)
                        {
                            c.Trades.Remove(item);
                            c.Say($"{u}, trade canceled SeemsGood");
                        }
                    }
                }
                else
                {
                    c.Say($"{u}, to open a trade type \"!trade <count> <item> for <count> <item>\". You can use \"and\" to trade multiple items.");
                }
            }
            ));
            #endregion

            #region throw
            Regex throwRegex = new Regex(@"^[^\s]+ \s+ (?<object>[^\s]+) \s+ (at \s+)? (?<user>[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

            string[] hitPhrases = new[] {
                "but it didn't seem to bother him.",
                "but they seems to have liked it.",
                "but you accidentally hit pajbot instead.",
                "and they gave you a weird look.",
            };
            bot.Commands.Add(new Command(
            "throw",
            (m, u, c) =>
            {
                Match match = throwRegex.Match(m.ToLower());

                if (match.Success)
                {
                    string name = match.Groups["object"].Value.ToLower();
                    string _target = match.Groups["user"].Value.ToLower();

                    User target = c.GetUserOrDefault(_target);
                    if (target == null)
                        return;

                    ShopItem item;
                    if ((item = ShopItem.GetItem(name)) != null)
                    {
                        if (u.HasItem(name, 1))
                        {
                            u.RemoveItem(name, 1);
                            if (Util.GetRandom(5))
                                c.Say($"{u}, you missed your shot LUL");
                            else
                            {
                                if (item.Edible && Util.GetRandom(75))
                                {
                                    c.Say($"{u}, you threw {item.GetNumber(1)} at {target.Name}. they picked it up, ate it and gained {item.Calories} calories{getEatEmote(item)}");
                                    target.Calories += item.Calories;
                                }
                                else
                                {
                                    c.Say($"{u}, you threw {item.GetNumber(1)} at {target.Name} " + hitPhrases[Util.GetRandom(0, hitPhrases.Length)]);
                                }
                            }
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you don't have any {name}s.");
                        }
                    }
                }
                else
                {
                    c.Say($"{u}, to throw an item at someone type !throw <item> at <user>.");
                }
            }
            ));
            #endregion


            // ROLEPLAYER SHIT
            #region shoot
            string[] shotPhrases = new[] {
                "and they gave you a scared look.",
                "and it seems to have hurt.",
                "and blood is dripping down from their 4Head",
            };

            bot.Commands.Add(new Command(
            "shoot",
            (m, u, c) =>
            {
                Match match = throwRegex.Match(m.ToLower());
                if (match.Success)
                {
                    User target = c.GetUserOrDefault(match.Groups["user"].Value);

                    if (target != null)
                    {
                        string name = match.Groups["object"].Value;

                        ShopItem item;

                        lock (ShopItem.Items)
                        {
                            if (ShopItem.Items.TryGetValue(name, out item) || ShopItem.Items.TryGetValue(name.TrimEnd('s'), out item))
                            {
                                if (u.HasItem(item.Name, 1))
                                {
                                    ShopItem weapon = null;
                                    ShopItem ammo = null;

                                    if (item.IsWeapon)
                                    {
                                        weapon = item;
                                        lock (u.Inventory)
                                        {
                                            foreach (var i in u.Inventory)
                                            {
                                                ShopItem I;
                                                lock (ShopItem.Items)
                                                {
                                                    if (ShopItem.Items.TryGetValue(i.Name, out I))
                                                    {
                                                        if (I.AmmoFor != null && I.AmmoFor.Contains(weapon.Name))
                                                        {
                                                            ammo = I;
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        if (ammo == null)
                                        {
                                            c.TryWhisperUser(u, $"{u}, you don't have ammo for your {weapon.Name}.");
                                        }
                                        else
                                        {
                                            c.Say($"{u} shot {target.Name} with {weapon.GetNumber(1)} " + shotPhrases[Util.GetRandom(0, shotPhrases.Length)]);
                                            {
                                                u.RemoveItem(ammo.Name, 1);
                                            }
                                        }
                                    }
                                    else if (item.IsAmmo)
                                    {
                                        ammo = item;
                                        lock (u.Inventory)
                                        {
                                            foreach (var i in u.Inventory)
                                            {
                                                ShopItem I;
                                                lock (ShopItem.Items)
                                                {
                                                    if (ShopItem.Items.TryGetValue(i.Name, out I) && I.WeaponFor != null && I.WeaponFor.Contains(ammo.Name))
                                                    {
                                                        weapon = I;
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        if (weapon == null)
                                        {
                                            c.TryWhisperUser(u, $"{u}, you don't have a weapon to shoot your {ammo.Name}.");
                                        }
                                        else
                                        {
                                            c.Say($"{u} shot {target.Name} with {weapon.GetNumber(1)} " + shotPhrases[Util.GetRandom(0, shotPhrases.Length)]);
                                            {
                                                u.RemoveItem(ammo.Name, 1);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        c.Say($"{u}, {item.Name} is not a weapon and not ammo.");
                                    }
                                }
                                else
                                {
                                    c.TryWhisperUser(u, $"{u}, you don't have {item.GetNumber(1)}.");
                                }
                            }
                        }
                    }
                }
                else
                {
                    c.Say($"{u}, to shoot someone type !shoot <weapon or ammo> at <user>.");
                }
            }
            ));
            #endregion

            #region whip
            string[] whipPhrases = new[] {
                "but it didn't seem to bother him gachiGASM",
                "but they seems to have liked it a lot gachiGASM",
                "and it seems to have hurt gachiGASM",
                "and you ripped the skin gachiGASM",
                "and they made a weird sound gachiGASM",
            };

            bot.Commands.Add(new Command(
            "whip",
            (m, u, c) =>
            {
                if (u.HasItem("whip", 1))
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 1)
                    {
                        User target = c.GetUserOrDefault(S[1]);
                        if (target != null)
                        {
                            c.Say($"{u} whiped {target} " + whipPhrases[Util.GetRandom(0, whipPhrases.Length)]);
                        }
                    }
                }
                else
                {
                    c.TryWhisperUser(u, $"{u}, you need a whip to whip someone.");
                }
            }
            ));
            #endregion

            #region vape
            string[] vapePhrases = new[]
            {
                "and rips some fat clouds.",
                "and rips some even fatter clouds.",
                "and rips a cloud with the shape of a star.",
                "and rips a rectangular cloud.",
                "and rips an entire rainbow.",
                "and then looks at the fat clouds that they just ripped.",

                "and then creates a portal to the matrix.",
                "and then takes the red pill.",
                "and then takes the blue pill.",

                "but rips their vape and blows six hot sicky clouds.",

                "and waves at the news reporter.",
                "and then sits down on a park bench.",
                "and the cloud looks like a pajaW.",

                "and then thinks about their strent situation.",

                "and cures their terminal cancer.",
                "and makes a \\//\\ sign with their fingers.",
                "and then licks the vape.",
                "and gains an IQ.",

                "but forgot to turn it on.",
            };

            string[] tripleVapePhrases = new[]
            {
                "and rips three fat clouds.",
                "and rips three even fatter clouds.",
                "and rips three clouds with the shape of stars.",
                "and rips three rectangular clouds.",
                //"and rips an entire rainbow.",
                //"and then looks at the fat clouds that they just ripped.",

                //"and then creates a portal to the matrix.",
                "and then takes three red pills.",
                "and then takes three blue pills.",

                "but rips their vape and blows 18 hot sicky clouds.",

                "and blows three clouds at the news reporter.",
                //"and then sits down on a park bench.",
                "and the three clouds looks like pajaW.",

                //"and then thinks about their strent situation.",

                //"and cures their terminal cancer.",
                "and makes a \\//\\ sign with their arms.",
                "and then licks the vape three times.",
                "and gains three IQ.",

                "but forgot to turn it on.",
            };

            string[] perfectVapePhrases = new[]
            {
                "and rips the perfect cloud.",
                "and rips the fattest fucking clouds.",
                "and rips an entire universe.",
                "and then looks at the fat clouds that they just ripped.",

                "and then creates a portal to their local burger king.",
                "and then takes a glass of red pills.",
                "and then takes a glass of blue pills.",

                //"but rips their vape and blows 6 hot sicky clouds.",

                "and takes a selfie with the news reporter.",
                "and then sits down on their own perfect cloud.",

                "and cures all terminal cancer on earth.",
                "and makes a \\//\\ sign with their feet.",
                //"and then licks the vape.",
                "and get 140 IQ.",

                "but forgot to turn it on.",
            };

            bot.Commands.Add(new Command(
                "vape",
                (m, u, c) =>
                {
                    string error = "";
                    bool triple = false;
                    bool perfect = false;
                    try
                    {
                        if ((perfect = u.HasItem("perfect-vape", 1)) || (triple = u.HasItem("triple-vape", 1)) || u.HasItem("vape", 1))
                        {
                            error += 1;
                            List<ShopItem> liquids;

                            lock (u.Inventory)
                            {
                                liquids = u.Inventory.Select(x => ShopItem.GetItem(x.Name)).Where(x => x != null && x.Flags.HasFlag(ShopItemFlags.Liquid)).ToList();
                            }
                            error += 2;

                            if (liquids.Count == 0)
                            {
                                error += 3;

                                c.TryWhisperUser(u, $"You don't have anymore vape liquid. You can \"!buy <count> liquid\" to buy liquid for 5 pointz. You can also !craft flavored liquids {RecipesUrl}");
                            }
                            else
                            {
                                error += 4;
                                ShopItem liquid = null;
                                ShopItem l;
                                error += 5;

                                string[] S = m.ToLower().Split();
                                if ((S.Length > 1 && (l = ShopItem.GetItem(S[1] + "-liquid")) != null) || S.TryGetItemOrPointz(1, out l))
                                {
                                    if (liquids.Contains(l) && l.Flags.HasFlag(ShopItemFlags.Liquid))
                                        liquid = l;
                                }
                                error += 6;

                                if (liquid == null && u.HasItem("liquid", 1))
                                    liquid = ShopItem.GetItem("liquid");

                                liquid = liquid ?? liquids[Util.GetRandom(0, liquids.Count)];
                                error += 7;

                                var phrases = (perfect ? perfectVapePhrases : (triple ? tripleVapePhrases : vapePhrases));

                                if (liquid.Name == "liquid")
                                    c.Say($"{u} {(u.HasItem("vaping-dog", 1) ? "vapes with their OhMyDog" : "begins to vape")} {phrases[Util.GetRandom(0, phrases.Length)] } VapeNation");
                                else
                                    c.Say($"{u} {(u.HasItem("vaping-dog", 1) ? $"vapes {liquid.Name} with their OhMyDog" : $"vapes {liquid.Name}")} {phrases[Util.GetRandom(0, phrases.Length)] } VapeNation");
                                error += 8;

                                u.RemoveItem(liquid.Name, 1);
                            }
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you need a vape in order to vape. You can buy one in the !shop");
                        }
                    }
                    catch (Exception exc)
                    {
                        c.Say($"{error} {exc.Message}");
                    }
                }
                ));
            #endregion

            #region inspect
            bot.Commands.Add(new Command(
                "inspect",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 1)
                    {
                        var _item = S[1];
                        if (_item == "penis" || _item == "dick" || _item == "cock")
                        {
                            if (u.Name == "pajlada" || u.Name == "akantor2206")
                                c.Say($"I'm not changing my algorithm for you to have a bigger dick size. It's {(float)u.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm get over it.");
                            else
                                c.Say($"{u} measures their dick and the tape measure says {(float)u.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm.");

                        }

                        ShopItem item = ShopItem.GetItem(S[1]);

                        if (u.HasItem(item.Name, 1))
                        {
                            c.Say($"{u}, {item.Description}");
                        }
                        else
                        {
                            c.Say($"{u}, you don't have {item.GetNumber(1)} to inspect.");
                        }
                    }
                },
                cooldown: TimeSpan.FromSeconds(5)));
            #endregion

            #region pet
            string[] dickPetPhrases = new[]
            {
                "and the waitress gives them a weird look cmonBruh",
                "and the waitress joins them gachiGASM",
                "and the waitress throws them out of the coffeeshop OMGScoots",
            };

            bot.Commands.Add(new Command(
                "pet",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    ShopItem item;
                    User user;
                    if (S.TryIsString(1, "dick") || S.TryIsString(1, "cock"))
                    {
                        c.Say($"{u} pets their {S[1]} {dickPetPhrases[Util.GetRandom(0, dickPetPhrases.Length)]}");
                    }
                    else if (S.TryGetItemOrPointz(1, out item) && item.PetPhrases != null)
                    {
                        if (u.HasItem(item.Name, 1))
                        {
                            c.Say($"{u} pets their {item.Name} {item.PetPhrases[Util.GetRandom(0, item.PetPhrases.Length)]}");
                        }
                        else
                        {
                            c.TryWhisperUser(u, $"{u}, you don't have a {item.Name} to pet FeelsBadMan");
                        }
                    }
                    else if (S.TryGetUser(1, c, out user))
                    {
                        c.Say($"{u} pets {user} and then gently kisses their 4Head");
                    }
                }, hasUserCooldown: false));
            #endregion

            #region craft
            bot.Commands.Add(new Command(
                "craft",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    ShopItem item;
                    long count;
                    if ((S.TryGetItemOrPointz(1, out item) && S.TryGetInt(2, false, item == null ? u.Points : u.ItemCount(item.Name), out count).True()) ||
                        (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(1, false, item == null ? u.Points : u.ItemCount(item.Name), out count)))
                    {
                        if (item.Recipe == null)
                        {
                            c.TryWhisperUser(u, $"You can not craft {item.GetPlural()}.");
                        }
                        else
                        {
                            if (item.Recipe.Any(x => !u.HasItem(x.Item1.Name, x.Item2 * count)))
                            {
                                c.TryWhisperUser(u, $"You don't have enough items to craft {item.GetNumber(count)} FeelsBadMan");
                            }
                            else
                            {
                                item.Recipe.Do(x => u.RemoveItem(x.Item1.Name, count * x.Item2));
                                c.Say($"{u}, you crafted {item.GetNumber(count * item.CraftCount)}");
                                u.AddItem(item.Name, count * item.CraftCount);
                            }
                        }
                    }
                    else
                    {
                        c.Say($"{u}, to craft items type !craft <count> <item>. Available items to craft: {RecipesUrl}");
                    }
                },
                hasUserCooldown: false));
            #endregion


            // MISC COMMANDS
            #region randomgachi
            bot.Commands.Add(new Command(
            "randomgachi",
            (m, u, c) =>
            {
                lock (bot.GachiSongs)
                {
                    var song = bot.GachiSongs[Util.GetRandom(0, bot.GachiSongs.Count - 1)];
                    c.Say($"Random Gachi: {song.Name} http://youtube.com/watch?v={song.YoutubeID} (95 % chance it's gachi)");
                }
            },
            cooldown: TimeSpan.FromSeconds(5)
            ));
            #endregion

            #region gachicount
            bot.Commands.Add(new Command(
                "gachigasm",
                (m, u, c) =>
                {
                    User user;
                    if (m.ToLower().Split().TryGetUser(1, c, out user))
                        c.Say($"{u}, {user} wrote gachiGASM {user.GachiGASM} times gachiGASM");
                    else
                        c.Say($"{u}, you wrote gachiGASM {u.GachiGASM} times gachiGASM");
                }
                ));

            bot.Commands.Add(new Command(
                "topgachigasm",
                (m, u, c) =>
                {
                    c.Say($"{u}, top gachiGASM count: {string.Join(", ", c.Users.Values.OrderBy(x => x.GachiGASM * -1).Take(3).Where(user => user.GachiGASM != 0).Select(user => $"{user.Name} ({user.GachiGASM})"))}");
                },
                cooldown: TimeSpan.FromSeconds(15)
                ));
            #endregion gachi

            #region dicklength
            bot.Commands.Add(new Command(
                "dicklength",
                (m, u, c) =>
                {
                    var match = singleItem.Match(m.ToUpper());
                    if (match.Success)
                    {
                        User user;
                        string s = match.Groups[1].Value.ToLower();
                        if (c.Users.TryGetValue(s, out user))
                        {
                            c.Say($"{u.Name}, pajaHop {user.Name}'s dick is {(float)user.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm long pajaHop");
                            return;
                        }
                    }

                    c.Say($"{u}, pajaHop your dick is {(float)u.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm long pajaHop");
                }));
            #endregion

            #region commanduses
            bot.Commands.Add(new Command(
                "commanduses",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 1)
                    {
                        string command = S[1];

                        if (bot.Commands.Any(x => x.Name == command))
                        {
                            long count = bot.GetCommandUses(command);
                            c.Say($"{u}, {command} was used {count} time{(count == 1 ? "" : "s")} PogChamp");
                        }
                    }
                },
                cooldown: TimeSpan.FromSeconds(5)));
            #endregion

            #region message
            bot.Commands.Add(new Command(
                "message",
                (m, u, c) =>
                {
                    var match = singleItem.Match(m.ToUpper());
                    if (match.Success)
                    {
                        User user;
                        string s = match.Groups[1].Value.ToLower();
                        if (c.Users.TryGetValue(s, out user))
                        {
                            c.Say($"{u}, {s} wrote {user.MessageCount} messages with a total of {user.CharacterCount} characters.");
                            return;
                        }
                    }

                    c.Say($"{u}, you wrote {u.MessageCount} messages with a total of {u.CharacterCount} characters.");
                }));
            #endregion

            #region top
            int topCount = 3;

            bot.Commands.Add(new Command(
                "top",
                (m, u, c) =>
                {
                    string[] S = m.ToLower().Split();
                    if (S.Length >= 2)
                    {
                        string item = S[1];

                        if (item == "point" || item == "pointz" || item == "points")
                        {
                            c.Say($"{u}, top pointz: {string.Join(", ", c.Users.Values.OrderBy(x => x.Points * -1).Take(topCount).Where(user => user.Points != 0).Select(user => $"{user.Name} ({user.Points})"))}");
                        }
                        else if (item == "calorie" || item == "calories")
                        {
                            c.Say($"{u}, top calories: {string.Join(", ", c.Users.Values.OrderBy(x => x.Calories * -1).Take(topCount).Where(user => user.Calories != 0).Select(user => $"{user.Name} ({user.Calories})"))}");
                        }
                        else if (item == "message" || item == "messages")
                        {
                            c.Say($"{u}, top messages: {string.Join(", ", c.Users.Values.OrderBy(x => x.MessageCount * -1).Take(topCount).Where(user => user.MessageCount != 0).Select(user => $"{user.Name} ({user.MessageCount})"))}");
                        }
                        else if (item == "characters" || item == "chars")
                        {
                            c.Say($"{u}, top characters in messages: {string.Join(", ", c.Users.Values.OrderBy(x => x.CharacterCount * -1).Where(user => user.CharacterCount != 0).Take(topCount).Select(user => $"{user.Name} ({user.CharacterCount})"))}");
                        }
                        else
                        {
                            ShopItem i = ShopItem.GetItem(item);
                            if (i != null)
                                c.Say($"{u}, top {i.GetPlural()}: {string.Join(", ", c.Users.Values.OrderBy(x => x.ItemCount(i.Name) * -1).Take(topCount).Where(user => user.ItemCount(i.Name) != 0).Select(user => $"{user.Name} ({user.ItemCount(i.Name)})"))}");
                        }
                    }
                }, hasUserCooldown: false
                ));
            #endregion

            #region bottompointzs
            bot.Commands.Add(new Command(
                "bottompointz",
                (m, u, c) =>
                {
                    c.Say($"{u}, bottom pointz: {string.Join(", ", c.Users.Values.OrderBy(x => x.Points).Take(topCount).Where(user => user.Points != 0).Select(user => $"{user.Name} ({user.Points})"))}");
                },
                cooldown: TimeSpan.FromSeconds(15)
                ));
            #endregion


            // ADMIN COMMANDS
            #region alias
            bot.Commands.Add(new Command(
                "+alias",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 2)
                    {
                        string _command = S[1];
                        if (bot.Commands.Any(x => _command == x.Name))
                        {
                            bot.CommandAliases[S[2]] = _command;

                            c.Say($"{u}, added command alias {S[2]} for {_command}");
                        }
                    }
                }));

            bot.Commands.Add(new Command(
                "-alias",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 1)
                    {
                        if (bot.CommandAliases.Remove(S[1]))
                            c.Say($"{u}, removed command alias {S[1]}");
                    }
                }));

            bot.Commands.Add(new Command(
                "alias",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 1)
                    {
                        c.Say($"{u}, aliases for {S[1]}: {string.Join(", ", bot.CommandAliases.Where(k => k.Value == S[1]).Select(k => k.Key))}");
                    }
                }));
            #endregion

            #region calories
            bot.Commands.Add(new Command(
                "calories",
                (m, u, c) =>
                {
                    var match = singleItem.Match(m.ToLower());
                    if (match.Success)
                    {
                        User user;
                        string s = match.Groups[1].Value;
                        if (c.Users.TryGetValue(s, out user))
                        {
                            if (user.Calories == 0)
                                c.Say($"{s} doesn't have any calories FeelsGoodMan.");
                            else
                                c.Say($"{s} ate food with a total of {user.Calories} calories in chat OpieOP");
                            return;
                        }
                    }

                    if (u.Calories == 0)
                        c.Say($"{u}, you don't have any calories FeelsGoodMan");
                    else
                        c.Say($"{u}, you ate food with a total of {u.Calories} calories in chat OpieOP");
                }
                ));
            #endregion

            #region topcalories
            bot.Commands.Add(new Command(
            "topcalories",
                (m, u, c) =>
                {
                    c.Say($"{u}, top calories: {string.Join(", ", c.Users.Values.OrderBy(x => x.Calories * -1).Take(3).Where(user => user.Calories != 0).Select(user => $"{user.Name} ({user.Calories})"))}");
                },
                cooldown: TimeSpan.FromSeconds(15)
                ));
            #endregion

            #region userflag
            bot.Commands.Add(new Command(
                "+flag",
                (m, u, c) =>
                {
                    try
                    {
                        var S = m.Split();
                        var _user = S[1].ToLower();
                        var user = c.GetUser(_user);

                        var _flag = S[2];

                        UserFlags flag;
                        if (Enum.TryParse(_flag, true, out flag))
                        {
                            user.Flags |= flag;
                        }
                        c.TryWhisperUser(u, $"edited userflag for {_user}");
                    }
                    catch
                    {

                    }
                },
                adminOnly: true));

            bot.Commands.Add(new Command(
                "-flag",
                (m, u, c) =>
                {
                    try
                    {
                        var S = m.Split();
                        var _user = S[1].ToLower();
                        var user = c.GetUser(_user);

                        var _flag = S[2];

                        UserFlags flag;
                        if (Enum.TryParse(_flag, out flag))
                        {
                            user.Flags &= ~flag;
                        }
                        c.TryWhisperUser(u, $"edited userflag for {_user}");
                    }
                    catch
                    {

                    }
                },
                adminOnly: true));

            bot.Commands.Add(new Command(
                "flags",
                (m, u, c) =>
                {
                    try
                    {
                        var S = m.Split();
                        var _user = S[1].ToLower();
                        var user = c.GetUser(_user);

                        c.TryWhisperUser(u, $"{user.Flags}");
                    }
                    catch
                    {

                    }
                },
                adminOnly: true));
            #endregion

            #region reffle
            bot.Commands.Add(new Command(
            "reffle",
            (m, u, c) =>
            {
                string[] S = m.ToLower().Split();

                long count;
                ShopItem item;

                if ((S.TryGetItemOrPointz(1, out item) && S.TryGetInt(2, true, null, out count).True()) ||
                    (S.TryGetInt(1, true, null, out count) && S.TryGetItemOrPointz(2, out item).True()))
                {
                    if (item != null && count < 0)
                        return;

                    var group = Regex.Match(m, @"^[^\s]+\s+[^\s]+\s+[^\s]+(\s+(?<emote>.+))?$").Groups["emote"];

                    c.SayMe($"A reffle for {item.GetNumber(count)} started. Type {(group.Success ? group.Value : item?.Emote ?? "Kappa")} / to join it. The reffle will end in 45 seconds.");

                    c.RaffleActive = true;
                    c.QueueAction(S.Contains("fast") ? 10 : 45, () =>
                    {
                        c.RaffleActive = false;

                        int userCount = c.RaffleUsers.Count;
                        if (userCount > 0)
                        {
                            List<User> potentialWinners = new List<User>(c.RaffleUsers.Values);
                            List<User> winners = new List<User>();

                            int winnerCount = Math.Min((userCount / 8) + 1, 3);

                            for (int i = 0; i < winnerCount; i++)
                            {
                                int index = Util.GetRandom(0, userCount);
                                winners.Add(potentialWinners[index]);
                                potentialWinners.RemoveAt(index);
                            }

                            c.RaffleUsers.Clear();

                            if (item == null)
                            {
                                c.SayMe($"The reffle ended and {string.Join(", ", winners.Select(w => w.Name))} won {count} {(winners.Count() > 1 ? "each " : "")}pointz FeelsGoodMan");
                                winners.Do(winner => winner.Points += count);
                            }
                            else
                            {
                                c.SayMe($"The reffle ended and {string.Join(", ", winners.Select(w => w.Name))} won {item.GetNumber(count)} {(winners.Count() > 1 ? "each " : "")}FeelsGoodMan");
                                winners.Do(winner => winner.AddItem(item.Name, count));
                            }
                        }
                        else
                            c.SayMe($"Nobody entered the reffle LUL");
                    });
                }
            },
            modOnly: true
            ));

            bot.ChannelMessageReceived += (s, e) =>
            {
                if (e.Channel.RaffleActive)
                {
                    var u = e.User;

                    if (e.LowerSplitMessage.Contains("/") || e.LowerSplitMessage.Contains("\\"))
                    {
                        if (!u.IsBot)
                        {
                            e.Channel.RaffleUsers[u.Name] = u;
                        }
                    }
                }
            };

            //bot.Commands.Add(new Command(
            //"yoin",
            //(m, u, c) =>
            //{
            //    if (!u.IsBot && bot.RaffleActive)
            //    {
            //        bot.RaffleUsers[u.Name] = u;
            //    }
            //},
            //cooldown: TimeSpan.Zero
            //));
            #endregion

            #region eval
            //bot.Commands.Add(new Command(
            //    "eval",
            //    (m, u, c) =>
            //    {
            //        try
            //        {
            //            object o = bot.Interpreter.Eval(m.Substring("!eval ".Length), new Parameter("C", c));

            //            if (o != null)
            //                c.TryWhisperUser(u, o.ToString());
            //        }
            //        catch (Exception exc) { c.Say(exc.Message); }
            //    },
            //    adminOnly: true));

            bot.Commands.Add(new Command(
                "feval",
                (m, u, c) =>
                {
                    try
                    {
                        object o = bot.Interpreter.Eval(m.Substring("!print ".Length), new Parameter("C", c));

                        if (o != null)
                            c.Say(o.ToString());
                    }
                    catch (Exception exc) { c.Say(exc.Message); }
                },
                adminOnly: true));
            #endregion

            #region evalcommands
            Regex addCommandRegex = new Regex(@"^[^\s]+ \s+ (?<name>[^\s]+) \s+ (?<command>.+)$", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

            bot.Commands.Add(new Command(
                "command",
                (m, u, c) =>
                {
                    string[] S = m.Split();

                    if (S.Length > 1)
                    {
                        lock (bot.EvalCommands)
                        {
                            string name = S[1].ToLower();

                            var command = bot.EvalCommands.FirstOrDefault(x => x.Name == name);
                            if (command != null)
                            {
                                c.Say($"{u}, {command.Expression}");
                            }
                        }
                    }
                },
                adminOnly: true));

            bot.Commands.Add(new Command(
                "allcommands",
                (m, u, c) =>
                {
                    lock (bot.EvalCommands)
                    {
                        c.Say($"{u}, {string.Join(", ", bot.EvalCommands.Select(x => x.Name))}");
                    }
                },
                adminOnly: true));

            bot.Commands.Add(new Command(
                "+command",
                (m, u, c) =>
                {
                    var match = addCommandRegex.Match(m);
                    if (match.Success)
                    {
                        var name = match.Groups["name"].Value.ToLower();
                        bool adminOnly = name.IndexOf('%') != -1;
                        name = name.Trim('%');

                        var expression = match.Groups["command"].Value;
                        lock (bot.EvalCommands)
                        {
                            int index;
                            if ((index = bot.EvalCommands.FindIndex(x => x.Name == name)) != -1)
                                bot.EvalCommands.RemoveAt(index);

                            bot.EvalCommands.Add(new Bot.EvalCommand(bot, name, expression) { AdminOnly = adminOnly });
                        }
                    }
                },
                adminOnly: true));

            bot.Commands.Add(new Command(
                "-command",
                (m, u, c) =>
                {
                    string[] S = m.Split();

                    if (S.Length > 1)
                    {
                        lock (bot.EvalCommands)
                        {
                            string name = S[1].ToLower();

                            var command = bot.EvalCommands.FirstOrDefault(x => x.Name == name);
                            if (command != null)
                            {
                                bot.EvalCommands.Remove(command);
                                c.Say($"{u}, removed eval command \"{name}\"");
                            }
                        }
                    }
                },
                adminOnly: true));
            #endregion

            #region save
            bot.Commands.Add(new Command(
                "save",
                (m, u, c) =>
                {
                    bot.Save();
                },
                adminOnly: true));
            #endregion

            #region api
            bot.Commands.Add(new Command(
                "api",
                (m, u, c) =>
                {
                    try
                    {
                        TcpClient tcpclnt = new TcpClient();

                        tcpclnt.Connect("127.0.0.1", 5200);

                        string str = m.Substring("!api ".Length);
                        Stream stm = tcpclnt.GetStream();

                        byte[] bytes = Encoding.UTF8.GetBytes(str);

                        stm.Write(bytes, 0, bytes.Length);

                        bytes = new byte[2048];
                        int k = stm.Read(bytes, 0, 2048);

                        tcpclnt.Close();

                        c.Say(Encoding.UTF8.GetString(bytes, 0, k));
                    }
                    catch
                    {

                    }
                },
                adminOnly: true));
            #endregion

            #region tase
            string[] tasePhrases = new[] {
                "but it didn't seem to bother him gachiGASM",
                "but they seems to have liked it a lot gachiGASM",
                "and it seems to have hurt gachiGASM",
                "and they made a weird sound gachiGASM",
            };

            bot.Commands.Add(new Command(
                "tase",
                (m, u, c) =>
                {
                    string[] S = m.ToLower().Split();

                    User user;

                    if (S.TryGetUser(1, c, out user))
                    {
                        if (u.HasItem("taser", 1))
                        {
                            c.SayRaw($"/timeout {user.Name} 3", false);
                            c.Say($"{u} tased {user} {tasePhrases[Util.GetRandom(0, tasePhrases.Length)]}");
                        };
                    }

                }));
            #endregion


            //AppDomain.CurrentDomain.ProcessExit += (s, e) => { bot.Save(); };

            ManualResetEvent waitEvent = new ManualResetEvent(false);
            waitEvent.WaitOne();
        }

        static bool IsValidRepeatMessage(string s)
        {
            return !HasLink(s) && s.Length < 200 && !s.StartsWith("!");
        }

        static bool HasLink(string s)
        {
            for (int i = 1; i < s.Length - 1; i++)
            {
                if (s[i] == '.' && !char.IsWhiteSpace(s[i - 1]) && !char.IsWhiteSpace(s[i + 1]))
                    return true;
            }
            return false;
        }
    }
}
