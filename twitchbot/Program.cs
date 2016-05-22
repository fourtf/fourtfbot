using DynamicExpresso;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public static string RecipesUrl { get; private set; } = "no url set up";
        public static string RecipesPath { get; private set; } = null;

        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(new FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).Directory.FullName);

            Bot bot;

            try
            {
                Owner = File.ReadAllText("owner").Trim();
                string username = File.ReadAllText("username").Trim();
                string oauthkey = File.ReadAllText("oauthkey").Trim();
                string[] channels = File.ReadAllLines("channels").Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => "#" + s.Trim()).ToArray();

                try
                {
                    RecipesUrl = File.ReadAllText("recipesurl").Trim();
                    RecipesPath = File.ReadAllText("recipespath").Trim();
                }
                catch { }

                bot = new Bot(username, oauthkey)
                {
                    Channels = channels,
                    Admin = Owner,
                };

                bot.Connect();
            }
            catch (Exception exc)
            {
                Console.WriteLine("Couldn't start bot: " + exc.Message);
                Console.ReadKey();
                return;
            }

            if (RecipesPath != null)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(RecipesPath))
                    {
                        writer.Write("var recipes = [");

                        ShopItem.Items.Values.Where(item => item.Recipe != null).Do(x => writer.WriteLine($"[\"{(x.CraftCount > 1 ? x.CraftCount + " " : "")}{x.Name}\", \"{string.Join(", ", x.Recipe.Select(y => y.Item1.GetNumber(y.Item2)))}\"],"));

                        writer.Write("]");
                    }
                }
                catch { }
            }


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
                        if (bot.Users.TryGetValue(s, out user))
                        {
                            bot.Say(c, $"{s} has {user.Points} pointz.");
                            return;
                        }
                    }

                    bot.Whisper(u.Name, $"{u}, you have {u.Points} pointz.");
                    //bot.Say(c, $"{u}, you have {u.Points} pointz.");
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
                        if (bot.Users.TryGetValue(s, out user))
                        {
                            bot.Say(c, $"{s} has {user.Points} pointz.");
                            return;
                        }
                    }

                    bot.Say(c, $"{u}, you have {u.Points} pointz.");
                    //bot.Say(c, $"{u}, you have {u.Points} pointz.");
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
                            bot.Say(c, $"{u} is trying to bet 0 {item.GetPlural()} LUL");
                        else if (currentCount > 0 ? count < 0 || currentCount < count : count > 0 || currentCount > count)
                            bot.Say(c, $"{u}, you don't have that many {(item == null ? "pointz" : item.GetPlural())} LUL");
                        else if (Util.GetRandom(50))
                        {
                            bot.Say(c, $"{u} lost {item.GetNumber(count)} in roulete and now has {item.GetNumber(currentCount - count)}! LUL");
                            if (item == null)
                                u.Points -= count;
                            else
                                u.RemoveItem(item.Name, count);
                        }
                        else
                        {
                            bot.Say(c, $"{u} won {item.GetNumber(count)} in roulete and now has {item.GetNumber(currentCount + count)}! PogChamp");
                            if (item == null)
                                u.Points += count;
                            else
                                u.AddItem(item.Name, count);
                        }
                    }
                    else
                    {
                        bot.Say(c, $"{u}, to gamble type !roulete <count> or !roulete <count> <item>");
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
                        bot.Whisper(u.Name, $"{u}, {item.GetPlural()} are not available for sale.");
                    }
                    else
                    {
                        if ((item.Price > 0 && u.Points >= item.Price * count) || (item.Price < 0 && u.Points <= item.Price * count))
                        {
                            bot.Whisper(u.Name, $"{u}, you bought {item.GetNumber(count)} for {item.Price} pointz{(count == 1 || count == -1 ? "" : " each")}.");

                            u.Points -= item.Price * count;
                            u.AddItem(item.Name, count);
                            if (item.Name == "vape")
                            {
                                u.AddItem("liquid", 5 * count);
                            }
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"{u}, you don't have enough pointz to buy {item.GetNumber(count)}.");
                        }
                    }
                }
                else
                {
                    string s = "items (price) in the shop: ";
                    foreach (var i in ShopItem.Items.Values)
                    {
                        if (i.Price > 0)
                            s += i.Name + " (" + i.Price + "), ";
                    }

                    bot.Say(c, s.TrimEnd(' ', ','));
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
                    bot.Whisper(u.Name, $"{u}, use !buy <item> to buy items from one of the shops :)");

                string s = "a shady dealer shows you the following items (price): ";
                foreach (var item in ShopItem.Items.Values)
                {
                    if (item.Price < 0)
                        s += item.Name + " (" + item.Price + "), ";
                }

                bot.Say(c, s.TrimEnd(' ', ','));
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
                S.TryGetUser(1, bot, out user);
                user = user ?? u;

                string ans = $"";

                if (user.Inventory != null)
                    foreach (var item in user.Inventory)
                    {
                        ans += item.Count + " " + item.Name + (item.Count == 1 ? "" : "s") + ", ";
                    }

                if (u == user)
                    bot.Whisper(u.Name, $"You have {(user.Inventory == null ? "no items FeelsBadMan" : ans.TrimEnd(' ', ','))}");
                else
                    bot.Say(c, $"{u}, {(u == user ? "you have" : user.Name + " has")} {(user.Inventory == null ? "no items FeelsBadMan" : ans.TrimEnd(' ', ','))}");
            }
            ));

            bot.Commands.Add(new Command(
            "useritems",
            (m, u, c) =>
            {
                string[] S = m.ToLowerInvariant().Split();

                User user;
                S.TryGetUser(1, bot, out user);
                user = user ?? u;

                string ans = $"";

                if (user.Inventory != null)
                    foreach (var item in user.Inventory)
                    {
                        ans += item.Count + " " + item.Name + (item.Count == 1 ? "" : "s") + ", ";
                    }

                bot.Say(c, $"{u}, {(u == user ? "you have" : user.Name + " has")} {(user.Inventory == null ? "no items FeelsBadMan" : ans.TrimEnd(' ', ','))}");
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
                        bot.Whisper(u.Name, $"{u} is trying to eat {item.GetPlural()} LUL");
                    }
                    else
                    {
                        if (count <= u.ItemCount(item.Name))
                        {
                            bot.Say(c, $"{u} ate {item.GetNumber(count)} with {item.Calories} calories{(count > 1 ? " each" : "")}{getEatEmote(item)}");

                            u.RemoveItem(item.Name, count);
                            u.Calories += count * item.Calories;
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"{u}, you don't have that many {item.GetPlural()}.");
                        }
                    }
                }
                else
                {
                    bot.Say(c, $"{u}, to eat something type \"!eat <count> <item>\" SeemsGood");
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
                        bot.Say(c, $"{u} is trying to lose 0 calories LUL");
                    else if (u.Calories > 0 ? calories < 0 || u.Calories < calories : calories > 0 || u.Calories > calories)
                        bot.Say(c, $"{u}, you don't have that many calories FeelsGoodMan");
                    else if (Util.GetRandom(66))
                    {
                        bot.Say(c, $"{u} lost {calories} calories with his diet and now has {u.Calories - calories} calories! FeelsGoodMan");
                        u.Calories -= calories;
                    }
                    else
                    {
                        bot.Say(c, $"{u} did the diet but got frustrated, gained {(long)(calories * 2.5)} calories and now has {u.Calories + (long)(calories * 2.5)} calories FeelsBadMan");
                        u.Calories += (long)(calories * 2.5);
                    }
                }
                else
                {
                    bot.Say(c, $"{u}, type !diet <count> loose some calories OpieOP");
                }
            }
            ));

            bot.Commands.Add(new Command(
            "diet",
            (m, u, c) =>
            {
                bot.Say(c, $"{u}, to gamble you calories type !diet <count>");
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

                if (S.TryGetUser(1, bot, out target) && (
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(3, false, item == null ? u.Points : u.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(3, out item).True() && S.TryGetInt(2, false, item == null ? u.Points : u.ItemCount(item.Name), out count))))
                {
                    if (item == null)
                    {
                        if (u.Points >= count)
                        {
                            bot.Say(c, $"{u} gave {target.Name} {item.GetNumber(count)}");
                            u.Points -= count;
                            target.Points += count;
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"{u}, you don't have that many pointz.");
                        }
                    }
                    else
                    {
                        if (u.HasItem(item.Name, count))
                        {
                            bot.Say(c, $"{u} gave {target.Name} {item.GetNumber(count)} ");
                            u.RemoveItem(item.Name, count);
                            target.AddItem(item.Name, count);
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"{u}, you don't have that many {item.GetPlural()}.");
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

                if (S.TryGetUser(1, bot, out target) && (
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(3, false, item == null ? u.Points : u.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(3, out item).True() && S.TryGetInt(2, false, item == null ? u.Points : u.ItemCount(item.Name), out count))))
                {
                    if (item == null)
                    {
                        bot.Say(c, $"{u} gave {target.Name} {item.GetNumber(count)} Keepo");
                        target.Points += count;
                    }
                    else
                    {
                        bot.Say(c, $"{u} gave {target.Name} {item.GetNumber(count)} Keepo");
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

                if (S.TryGetUser(1, bot, out user) && (
                    (S.TryGetItemOrPointz(2, out item) && S.TryGetInt(3, false, item == null ? user.Points : user.ItemCount(item.Name), out count).True()) ||
                    (S.TryGetItemOrPointz(3, out item).True() && S.TryGetInt(2, false, item == null ? user.Points : user.ItemCount(item.Name), out count))))
                {
                    if (!u.IsAdmin && u == user)
                    {
                        bot.Say(c, $"{u} is trying to fight himself LUL");
                        return;
                    }

                    lock (bot.Duels)
                    {
                        if (bot.Duels.Any(d => d.ToUser == user.Name))
                        {
                            bot.Whisper(u.Name, $"{user} is already getting duelled.");
                        }
                        else
                        {
                            if (item == null)
                            {
                                if (u.Points < count)
                                {
                                    bot.Whisper(u.Name, $"You don't have {item.GetNumber(count)}. You currently have {item.GetNumber(u.Points)}.");
                                    return;
                                }
                                else if (user.Points < count)
                                {
                                    bot.Whisper(u.Name, $"{user} doesn't have {item.GetNumber(count)}. They only have {item.GetNumber(user.Points)}.");
                                    return;
                                }
                            }
                            else
                            {
                                if (u.ItemCount(item.Name) < count)
                                {
                                    bot.Whisper(u.Name, $"You don't have {item.GetNumber(count)}. You currently have {item.GetNumber(u.ItemCount(item.Name))}.");
                                    return;
                                }
                                else if (user.ItemCount(item.Name) < count)
                                {
                                    bot.Whisper(u.Name, $"{user} doesn't have {item.GetNumber(count)}. They only have {item.GetNumber(user.ItemCount(item.Name))}.");
                                    return;
                                }
                            }

                            bot.Duels.Add(new Bot.DuelItem
                            {
                                ExpireDate = DateTime.Now + bot.DuelTimeout,
                                Count = count,
                                Item = item,
                                FromUser = u.Name,
                                ToUser = user.Name
                            });

                            bot.Say(c, $"{u} is dueling {user} for {item.GetNumber(count)} PogChamp");

                            bot.Whisper(user.Name, $"{u} is dueling you for {item.GetNumber(count)}. Type \"!fight accept\" in chat to accept or \"!fight deny\" to deny.");
                        }
                    }
                }
                else if (S.TryIsString(1, "accept"))
                {
                    lock (bot.Duels)
                    {
                        int index;
                        if ((index = bot.Duels.FindIndex(d => d.ToUser == u.Name)) != -1)
                        {
                            var duel = bot.Duels[index];

                            User from = bot.GetUserOrDefault(duel.FromUser);

                            if (from == null)
                                return;

                            if (duel.Item == null ? (u.Points < duel.Count) : (u.ItemCount(duel.Item.Name) < duel.Count))
                            {
                                bot.Say(c, $"{c}, you don't have enought {duel.Item.GetPlural()}, the duel was canceled.");
                            }
                            else if (duel.Item == null ? (from.Points < duel.Count) : (from.ItemCount(duel.Item.Name) < duel.Count))
                            {
                                bot.Say(c, $"{c}, {from} doesn't have enought {duel.Item.GetPlural()}, the duel was canceled.");
                            }
                            else
                            {
                                User winner = Util.GetRandom(50) ? u : from;
                                User loser = winner == u ? from : u;

                                bot.ForceSay(c, $"{winner} won the duel against {loser} and got {duel.Item.GetNumber(duel.Count)} PogChamp");
                                bot.Whisper(winner.Name, $"You won the duel against {loser} and got {duel.Item.GetNumber(duel.Count)} PogChamp");
                                bot.Whisper(loser.Name, $"You lost the duel against {winner} for {duel.Item.GetNumber(duel.Count)} FeelsBadMan");

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
                            bot.Duels.RemoveAt(index);
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"You are not getting duelled at the moment.");
                        }
                    }
                }
                else if (S.TryIsString(1, "deny"))
                {
                    lock (bot.Duels)
                    {
                        int index;
                        if ((index = bot.Duels.FindIndex(d => d.ToUser == u.Name)) != -1)
                        {
                            var duel = bot.Duels[index];

                            User from = bot.GetUserOrDefault(duel.FromUser);

                            if (from == null)
                                return;

                            bot.Whisper(from.Name, $"{u} denied your duel for {duel.Item.GetNumber(duel.Count)}.");

                            bot.Duels.RemoveAt(index);
                        }
                    }
                }
                else
                {
                    bot.Say(c, $"{u}, to duel someone type !fight <user> <count> [item]");
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
                    lock (bot.Trades)
                    {
                        var item = bot.Trades.FirstOrDefault(x => x.User == u.Name);
                        if (item != null)
                            bot.Trades.Remove(item);
                    }

                    var notEnough = gives.Where(x => x.Item1 == null ? x.Item2 > u.Points : x.Item2 > u.ItemCount(x.Item1.Name));

                    if (notEnough.Count() > 0)
                    {
                        bot.Whisper(u.Name, "You don't have enought of the following items: " + string.Join(", ", notEnough.Select(x => x.Item1?.Name ?? "pointz")));
                    }
                    else
                    {
                        lock (bot.Trades)
                        {
                            bot.Trades.Add(new Bot.TradeItem { ExpireDate = DateTime.Now + bot.TradeTimeout, User = u.Name, Wants = wants, Gives = gives });
                            bot.Say(c, $"type \"!trade {u.Name}\" to accept his trade SeemsGood");
                        }
                    }
                }
                else if (S.TryGetUser(1, bot, out user))
                {
                    lock (bot.Trades)
                    {
                        var trade = bot.Trades.FirstOrDefault(x => x.User == user.Name);
                        if (trade != null)
                        {
                            if (trade.Gives.Where(x => x.Item1 == null ? x.Item2 > u.Points : x.Item2 > u.ItemCount(x.Item1.Name)).Count() > 0)
                            {
                                bot.Say(c, $"{u}, {trade.User} does not have enough items/pointz anymore. The trade was canceled.");
                                bot.Trades.Remove(trade);
                            }
                            else if (trade.Wants.Where(x => x.Item1 == null ? x.Item2 > user.Points : x.Item2 > user.ItemCount(x.Item1.Name)).Count() > 0)
                            {
                                bot.Whisper(user.Name, $"You don't have enough items/pointz to accept the trade of {trade.User}.");
                            }
                            else
                            {
                                bot.Say(c, $"{u} traded with {user} SeemsGood");
                            }
                        }
                    }
                }
                else if (S.TryIsString(1, "cancel"))
                {
                    lock (bot.Trades)
                    {
                        var item = bot.Trades.FirstOrDefault(x => x.User == u.Name);
                        if (item != null)
                        {
                            bot.Trades.Remove(item);
                            bot.Say(c, $"{u}, trade canceled SeemsGood");
                        }
                    }
                }
                else
                {
                    bot.Say(c, $"{u}, to open a trade type \"!trade <count> <item> for <count> <item>\". You can use \"and\" to trade multiple items.");
                }
            }
            ));
            #endregion

            #region throw
            Regex throwRegex = new Regex(@"^[^\s]+ \s+ (?<object>[^\s]+) \s+ (at \s+)? (?<user>[^\s]+)", RegexOptions.Compiled | RegexOptions.IgnorePatternWhitespace);

            string[] hitPhrases = new[] {
                "but it didn't seem to bother him.",
                "but he seems to have liked it.",
                "but you accidentally hit pajbot instead.",
                "and he gave you a weird look.",
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

                    User target = bot.GetUserOrDefault(_target);
                    if (target == null)
                        return;

                    ShopItem item;
                    if ((item = ShopItem.GetItem(name)) != null)
                    {
                        if (u.HasItem(name, 1))
                        {
                            u.RemoveItem(name, 1);
                            if (Util.GetRandom(5))
                                bot.Say(c, $"{u}, you missed your shot LUL");
                            else
                            {
                                if (item.Edible && Util.GetRandom(75))
                                {
                                    bot.ForceSay(c, $"{u}, you threw {item.GetNumber(1)} at {target.Name}. He picked it up, ate it and gained {item.Calories} calories{getEatEmote(item)}");
                                    target.Calories += item.Calories;
                                }
                                else
                                {
                                    bot.ForceSay(c, $"{u}, you threw {item.GetNumber(1)} at {target.Name} " + hitPhrases[Util.GetRandom(0, hitPhrases.Length)]);
                                }
                            }
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"{u}, you don't have any {name}s.");
                        }
                    }
                }
                else
                {
                    bot.Say(c, $"{u}, to throw an item at someone type !throw <item> at <user>.");
                }
            }
            ));
            #endregion


            // ROLEPLAYER SHIT
            #region shoot
            string[] shotPhrases = new[] {
                "and he gave you a scared look.",
                "and it seems to have hurt.",
                "and blood is dripping down from his 4Head",
            };

            bot.Commands.Add(new Command(
            "shoot",
            (m, u, c) =>
            {
                Match match = throwRegex.Match(m.ToLower());
                if (match.Success)
                {
                    User target = bot.GetUserOrDefault(match.Groups["user"].Value);

                    if (target != null)
                    {
                        string name = match.Groups["object"].Value;

                        ShopItem item;

                        if (ShopItem.Items.TryGetValue(name, out item) || ShopItem.Items.TryGetValue(name.TrimEnd('s'), out item))
                        {
                            if (u.HasItem(item.Name, 1))
                            {
                                ShopItem weapon = null;
                                ShopItem ammo = null;

                                if (item.IsWeapon)
                                {
                                    weapon = item;
                                    foreach (var i in u.Inventory)
                                    {
                                        ShopItem I;
                                        if (ShopItem.Items.TryGetValue(i.Name, out I))
                                        {
                                            if (I.AmmoFor != null && I.AmmoFor.Contains(weapon.Name))
                                            {
                                                ammo = I;
                                                break;
                                            }
                                        }
                                    }
                                    if (ammo == null)
                                    {
                                        bot.Whisper(u.Name, $"{u}, you don't have ammo for your {weapon.Name}.");
                                    }
                                    else
                                    {
                                        bot.Say(c, $"{u} shot {target.Name} with {weapon.GetNumber(1)} " + shotPhrases[Util.GetRandom(0, shotPhrases.Length)]);
                                        {
                                            u.RemoveItem(ammo.Name, 1);
                                        }
                                    }
                                }
                                else if (item.IsAmmo)
                                {
                                    ammo = item;
                                    foreach (var i in u.Inventory)
                                    {
                                        ShopItem I;
                                        if (ShopItem.Items.TryGetValue(i.Name, out I) && I.WeaponFor != null && I.WeaponFor.Contains(ammo.Name))
                                        {
                                            weapon = I;
                                            break;
                                        }
                                    }
                                    if (weapon == null)
                                    {
                                        bot.Whisper(u.Name, $"{u}, you don't have a weapon to shoot your {ammo.Name}.");
                                    }
                                    else
                                    {
                                        bot.Say(c, $"{u} shot {target.Name} with {weapon.GetNumber(1)} " + shotPhrases[Util.GetRandom(0, shotPhrases.Length)]);
                                        {
                                            u.RemoveItem(ammo.Name, 1);
                                        }
                                    }
                                }
                                else
                                {
                                    bot.Say(c, $"{u}, {item.Name} is not a weapon and not ammo.");
                                }
                            }
                            else
                            {
                                bot.Whisper(u.Name, $"{u}, you don't have {item.GetNumber(1)}.");
                            }
                        }
                    }
                }
                else
                {
                    bot.Say(c, $"{u}, to shoot someone type !shoot <weapon or ammo> at <user>.");
                }
            }
            ));
            #endregion

            #region whip
            string[] whipPhrases = new[] {
                "but it didn't seem to bother him gachiGASM",
                "but he seems to have liked it a lot gachiGASM",
                "and it seems to have hurt gachiGASM",
                "and you ripped the skin gachiGASM",
                "and he made a weird sound gachiGASM",
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
                        User target = bot.GetUserOrDefault(S[1]);
                        if (target != null)
                        {
                            bot.Say(c, $"{u} whiped {target} " + whipPhrases[Util.GetRandom(0, whipPhrases.Length)]);
                        }
                    }
                }
                else
                {
                    bot.Whisper(u.Name, $"{u}, you need a whip to whip someone.");
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
                "and then looks at the fat clouds that he just ripped.",

                "and then creates a portal to the matrix.",
                "and then takes the red pill.",
                "and then takes the blue pill.",

                "but rips his vape and blows six hot sicky clouds.",

                "and waves at the news reporter.",
                "and then sits down on a park bench.",
                "and the cloud looks like a pajaW.",

                "and then thinks about his strent situation.",

                "and cures his terminal cancer.",
                "and makes a \\//\\ sign with his fingers.",
                "and then licks the vape.",
                "and gains an IQ.",

                //"but then wakes up from his dream about fat clouds.",
                "but forgot to turn it on.",
            };

            bot.Commands.Add(new Command(
                "vape",
                (m, u, c) =>
                {
                    if (u.HasItem("vape", 1))
                    {
                        List<ShopItem> liquids = u.Inventory.Select(x => ShopItem.GetItem(x.Name)).Where(x => x.Flags.HasFlag(ShopItemFlags.Liquid)).ToList();

                        if (liquids.Count == 0)
                        {
                            bot.Whisper(u.Name, $"You don't have anymore vape liquid. You can \"!buy <count> liquid\". You can also !craft flavored liquids {RecipesUrl}");
                        }
                        else
                        {
                            ShopItem liquid = null;
                            ShopItem l;
                            string[] S = m.ToLower().Split();
                            if ((S.Length > 1 && (l = ShopItem.GetItem(S[1] + "-liquid")) != null) || S.TryGetItemOrPointz(1, out l))
                            {
                                if (liquids.Contains(l) && l.Flags.HasFlag(ShopItemFlags.Liquid))
                                    liquid = l;
                            }

                            if (liquid == null && u.HasItem("liquid", 1))
                                liquid = ShopItem.GetItem("liquid");

                            liquid = liquid ?? liquids[Util.GetRandom(0, liquids.Count)];

                            if (liquid.Name == "liquid")
                                bot.Say(c, $"{u} {(u.HasItem("vaping-dog", 1) ? "vapes with his OhMyDog" : "begins to vape")} {vapePhrases[Util.GetRandom(0, vapePhrases.Length)]} VapeNation");
                            else
                                bot.Say(c, $"{u} {(u.HasItem("vaping-dog", 1) ? $"vapes {liquid.Name} with his OhMyDog" : $"vapes {liquid.Name}")} {vapePhrases[Util.GetRandom(0, vapePhrases.Length)]} VapeNation");
                            u.RemoveItem(liquid.Name, 1);
                        }
                    }
                    else
                    {
                        bot.Whisper(u.Name, $"{u}, you need a vape in order to vape. You can buy one in the !shop");
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
                                bot.Say(c, $"I'm not changing my algorithm for you to have a bigger dick size. It's {(float)u.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm get over it.");
                            else
                                bot.Say(c, $"{u} measures his dick and the tape measure says {(float)u.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm.");

                        }

                        ShopItem item = ShopItem.GetItem(S[1]);

                        if (u.HasItem(item.Name, 1))
                        {
                            bot.Say(c, $"{u}, {item.Description}");
                        }
                        else
                        {
                            bot.Say(c, $"{u}, you don't have {item.GetNumber(1)} to inspect.");
                        }
                    }
                },
                cooldown: TimeSpan.FromSeconds(5)));
            #endregion

            #region pet
            Dictionary<string, string[]> petPhrases = new Dictionary<string, string[]>
            {
                ["cat"] = new[]
                {
                    "and it starts to purr CoolCat",
                },
                ["dog"] = new[]
                {
                    "and it starts licking their face OhMyDog",
                },
                ["vaping-dog"] = new[]
                {
                    "and a little cloud is coming out of it's mouth VapeNation",
                    "and rips some fat clouds. VapeNation",

                    "and it rips some even fatter clouds. VapeNation",
                    "and it rips a cloud that looks like a pile of poop. VapeNation",
                    "and it rips a rectangular cloud. VapeNation",
                    "and it rips an entire rainbow. VapeNation",
                    "and then looks at the fat clouds that it just ripped. VapeNation",

                    "and then licks the news reporter. VapeNation",
                    "and then sits down next to the park bench. VapeNation",
                    "and the cloud looks like a FrankerZ",

                    "and it makes a \\//\\ sign with his paws. VapeNation",
                },
                ["vape"] = new[]
                {
                    "and wishes it a good night VapeNation",
                    "and then licks it VapeNation",
                },
                ["hamster"] = new[]
                {
                    "and it makes a squeaky sound KKona",
                    "and it makes a squishy sound KKona",
                },
                ["negative-cat"] = new[]
                {
                    "but it bites them CoolCat",
                    "and it licks their arm CoolCat",
                },
                ["cheeseburger"] = new[]
                {
                    "OpieOP"
                },
                ["bacon"] = new[]
                {
                    "OpieOP"
                },
                ["chickennugget"] = new[]
                {
                    "OpieOP"
                },
                ["pistol"] = new[]
                {
                    "haHAA"
                },
                ["ak47"] = new[]
                {
                    "haHAA"
                },
                ["whip"] = new[]
                {
                    "gachiGASM"
                },
                ["fisting"] = new[]
                {
                    "gachiGASM"
                },
                ["slap"] = new[]
                {
                    "gachiGASM"
                },
                ["liquid"] = new[]
                {
                    "and think about all the fat vapes that he will rip with it VapeNation"
                },
                ["cobra"] = new[]
                {
                    "and it bites them WutFace",
                    "and it pretends to be asleep KKaper",
                },
                ["roleplayer"] = new[]
                {
                    "but it spits snus at them DansGame",
                },
                ["viewbot"] = new[]
                {
                    "and it makes a beeping sound MrDestructoid"
                },
                ["apple"] = new[]
                {
                    "until it's nice and shiny KKona"
                },
                ["swiftapple"] = new[]
                {
                    "but it's already shiny KKona"
                },
            };

            bot.Commands.Add(new Command(
                "pet",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    ShopItem item;
                    User user;
                    if (S.TryGetItemOrPointz(1, out item) && petPhrases.ContainsKey(item.Name))
                    {
                        if (u.HasItem(item.Name, 1))
                        {
                            bot.Say(c, $"{u} pets their {item.Name} {petPhrases[item.Name][Util.GetRandom(0, petPhrases[item.Name].Length)]}");
                        }
                        else
                        {
                            bot.Whisper(u.Name, $"{u}, you don't have a {item.Name} to pet FeelsBadMan");
                        }
                    }
                    else if (S.TryGetUser(1, bot, out user))
                    {
                        bot.Say(c, $"{u} pets {user} and then gently kisses their 4Head");
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
                            bot.Whisper(u.Name, $"You can not craft {item.GetPlural()}.");
                        }
                        else
                        {
                            if (item.Recipe.Any(x => !u.HasItem(x.Item1.Name, x.Item2 * count)))
                            {
                                bot.Whisper(u.Name, $"You don't have enough items to craft {item.GetNumber(count)} FeelsBadMan");
                            }
                            else
                            {
                                item.Recipe.Do(x => u.RemoveItem(x.Item1.Name, count * x.Item2));
                                bot.Say(c, $"{u}, you crafted {item.GetNumber(count * item.CraftCount)}");
                                u.AddItem(item.Name, count * item.CraftCount);
                            }
                        }
                    }
                    else
                    {
                        bot.Say(c, $"{u}, to craft items type !craft <count> <item>. Available items to craft: {RecipesUrl}");
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
                    bot.Say(c, $"Random Gachi: {song.Name} http://youtube.com/watch?v={song.YoutubeID} (95 % chance it's gachi)");
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
                    if (m.ToLower().Split().TryGetUser(1, bot, out user))
                        bot.Say(c, $"{u}, {user} wrote gachiGASM {user.GachiGASM} times gachiGASM");
                    else
                        bot.Say(c, $"{u}, you wrote gachiGASM {u.GachiGASM} times gachiGASM");
                }
                ));

            bot.Commands.Add(new Command(
                "topgachigasm",
                (m, u, c) =>
                {
                    bot.Say(c, $"{u}, top gachiGASM count: {string.Join(", ", bot.Users.Values.OrderBy(x => x.GachiGASM * -1).Take(3).Where(user => user.GachiGASM != 0).Select(user => $"{user.Name} ({user.GachiGASM})"))}");
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
                        if (bot.Users.TryGetValue(s, out user))
                        {
                            bot.Say(c, $"{u.Name}, pajaHop {user.Name}'s dick is {(float)user.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm long pajaHop");
                            return;
                        }
                    }

                    bot.Say(c, $"{u}, pajaHop your dick is {(float)u.Name.ToUpper().GetHashCode() / int.MaxValue * 10 + 7:0.00}cm long pajaHop");
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
                            bot.Say(c, $"{u}, {command} was used {count} time{(count == 1 ? "" : "s")} PogChamp");
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
                        if (bot.Users.TryGetValue(s, out user))
                        {
                            bot.Say(c, $"{u}, {s} wrote {user.MessageCount} messages with a total of {user.CharacterCount} characters.");
                            return;
                        }
                    }

                    bot.Say(c, $"{u}, you wrote {u.MessageCount} messages with a total of {u.CharacterCount} characters.");
                }));
            #endregion

            #region top
            int topCount = 6;

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
                            bot.Say(c, $"{u}, top pointz: {string.Join(", ", bot.Users.Values.OrderBy(x => x.Points * -1).Take(topCount).Where(user => user.Points != 0).Select(user => $"{user.Name} ({user.Points})"))}");
                        }
                        else if (item == "calorie" || item == "calories")
                        {
                            bot.Say(c, $"{u}, top calories: {string.Join(", ", bot.Users.Values.OrderBy(x => x.Calories * -1).Take(topCount).Where(user => user.Calories != 0).Select(user => $"{user.Name} ({user.Calories})"))}");
                        }
                        else if (item == "message" || item == "messages")
                        {
                            bot.Say(c, $"{u}, top messages: {string.Join(", ", bot.Users.Values.OrderBy(x => x.MessageCount * -1).Take(topCount).Where(user => user.MessageCount != 0).Select(user => $"{user.Name} ({user.MessageCount})"))}");
                        }
                        else if (item == "characters" || item == "chars")
                        {
                            bot.Say(c, $"{u}, top characters in messages: {string.Join(", ", bot.Users.Values.OrderBy(x => x.CharacterCount * -1).Where(user => user.CharacterCount != 0).Take(topCount).Select(user => $"{user.Name} ({user.CharacterCount})"))}");
                        }
                        else
                        {
                            ShopItem i = ShopItem.GetItem(item);
                            if (i != null)
                                bot.Say(c, $"{u}, top {i.GetPlural()}: {string.Join(", ", bot.Users.Values.OrderBy(x => x.ItemCount(i.Name) * -1).Take(topCount).Where(user => user.ItemCount(i.Name) != 0).Select(user => $"{user.Name} ({user.ItemCount(i.Name)})"))}");
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
                    bot.Say(c, $"{u}, bottom pointz: {string.Join(", ", bot.Users.Values.OrderBy(x => x.Points).Take(topCount).Where(user => user.Points != 0).Select(user => $"{user.Name} ({user.Points})"))}");
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

                            bot.Say(c, $"{u}, added command alias {S[2]} for {_command}");
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
                            bot.Say(c, $"{u}, removed command alias {S[1]}");
                    }
                }));

            bot.Commands.Add(new Command(
                "alias",
                (m, u, c) =>
                {
                    string[] S = m.ToLowerInvariant().Split();

                    if (S.Length > 1)
                    {
                        bot.Say(c, $"{u}, aliases for {S[1]}: {string.Join(", ", bot.CommandAliases.Where(k => k.Value == S[1]).Select(k => k.Key))}");
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
                        if (bot.Users.TryGetValue(s, out user))
                        {
                            if (user.Calories == 0)
                                bot.Say(c, $"{s} doesn't have any calories FeelsGoodMan.");
                            else
                                bot.Say(c, $"{s} ate food with a total of {user.Calories} calories in chat OpieOP");
                            return;
                        }
                    }

                    if (u.Calories == 0)
                        bot.Say(c, $"{u}, you don't have any calories FeelsGoodMan");
                    else
                        bot.Say(c, $"{u}, you ate food with a total of {u.Calories} calories in chat OpieOP");
                }
                ));
            #endregion

            #region topcalories
            bot.Commands.Add(new Command(
            "topcalories",
                (m, u, c) =>
                {
                    bot.Say(c, $"{u}, top calories: {string.Join(", ", bot.Users.Values.OrderBy(x => x.Calories * -1).Take(3).Where(user => user.Calories != 0).Select(user => $"{user.Name} ({user.Calories})"))}");
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
                        var user = bot.GetUser(_user);

                        var _flag = S[2];

                        UserFlags flag;
                        if (Enum.TryParse(_flag, true, out flag))
                        {
                            user.Flags |= flag;
                        }
                        bot.Whisper(u.Name, $"edited userflag for {_user}");
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
                        var user = bot.GetUser(_user);

                        var _flag = S[2];

                        UserFlags flag;
                        if (Enum.TryParse(_flag, out flag))
                        {
                            user.Flags &= ~flag;
                        }
                        bot.Whisper(u.Name, $"edited userflag for {_user}");
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
                        var user = bot.GetUser(_user);

                        bot.Whisper(u.Name, $"{user.Flags}");
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

                    bot.ForceSay(c, $"A reffle for {item.GetNumber(count)} started. Type {(group.Success ? group.Value : item?.Emote ?? "Kappa")} / to join it. The reffle will end in 45 seconds.");

                    bot.RaffleActive = true;
                    bot.QueueAction(S.Contains("fast") ? 10 : 45, () =>
                    {
                        bot.RaffleActive = false;

                        int userCount = bot.RaffleUsers.Count;
                        if (userCount > 0)
                        {
                            List<User> potentialWinners = new List<User>(bot.RaffleUsers.Values);
                            List<User> winners = new List<User>();

                            int winnerCount = Math.Min((userCount / 8) + 1, 3);

                            for (int i = 0; i < winnerCount; i++)
                            {
                                int index = Util.GetRandom(0, userCount);
                                winners.Add(potentialWinners[index]);
                                potentialWinners.RemoveAt(index);
                            }

                            bot.RaffleUsers.Clear();

                            if (item == null)
                            {
                                bot.ForceSay(c, $"The reffle ended and {string.Join(", ", winners.Select(w => w.Name))} won {count} {(winners.Count() > 1 ? "each " : "")}pointz FeelsGoodMan");
                                winners.Do(winner => winner.Points += count);
                            }
                            else
                            {
                                bot.ForceSay(c, $"The reffle ended and {string.Join(", ", winners.Select(w => w.Name))} won {item.GetNumber(count)} {(winners.Count() > 1 ? "each " : "")}FeelsGoodMan");
                                winners.Do(winner => winner.AddItem(item.Name, count));
                            }
                        }
                        else
                            bot.ForceSay(c, $"Nobody entered the reffle LUL");
                    });
                }
            },
            adminOnly: true
            ));

            bot.Irc.OnChannelMessage += (s, e) =>
            {
                if (bot.RaffleActive)
                {
                    var u = bot.GetOrCreateUser(e.Data.Nick.ToLower());

                    if (e.Data.MessageArray.Contains("/") || e.Data.MessageArray.Contains("\\"))
                    {
                        if (!u.IsBot)
                        {
                            bot.RaffleUsers[u.Name] = u;
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
            bot.Commands.Add(new Command(
                "eval",
                (m, u, c) =>
                {
                    try
                    {
                        object o = bot.Interpreter.Eval(m.Substring("!eval ".Length), new Parameter("C", c));

                        if (o != null)
                            bot.Whisper(u.Name, o.ToString());
                    }
                    catch (Exception exc) { bot.Say(c, exc.Message); }
                },
                adminOnly: true));

            bot.Commands.Add(new Command(
                "print",
                (m, u, c) =>
                {
                    try
                    {
                        object o = bot.Interpreter.Eval(m.Substring("!print ".Length), new Parameter("C", c));

                        if (o != null)
                            bot.Say(c, o.ToString());
                    }
                    catch (Exception exc) { bot.Say(c, exc.Message); }
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
                                bot.Say(c, $"{u}, {command.Expression}");
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
                        bot.Say(c, $"{u}, {string.Join(", ", bot.EvalCommands.Select(x => x.Name))}");
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
                                bot.Say(c, $"{u}, removed eval command \"{name}\"");
                            }
                        }
                    }
                },
                adminOnly: true));
            #endregion

            ManualResetEvent waitEvent = new ManualResetEvent(false);
            waitEvent.WaitOne();

            //AppDomain.CurrentDomain.ProcessExit += (s, e) => { bot.Save(); };
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
