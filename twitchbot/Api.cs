using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Cache = System.Collections.Concurrent.ConcurrentDictionary<string, System.Tuple<System.DateTime, string>>;

namespace twitchbot
{
    public static class Api
    {
        // API
        static string recipesCache = null;
        static string itemsCache = null;
        static Cache itemCache = new Cache();
        public static TimeSpan CacheCooldown = TimeSpan.FromMinutes(1);

        public static void StartApiServer(object parameter)
        {
            Channel c = (Channel)parameter;

            IPAddress address = IPAddress.Parse("127.0.0.1");
            TcpListener listener = new TcpListener(address, 5200);
            listener.Start();

            while (true)
            {
                try
                {
                    var client = listener.AcceptSocket();
                    byte[] bytes = new byte[256];

                    int length = client.Receive(bytes);
                    var request = Encoding.UTF8.GetString(bytes, 0, length);
                    string[] S = request.ToLower().Split(new char[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);

                    StringBuilder builder = new StringBuilder(1024);
                    string cache = null;
                    builder.Append("{ success: ");
                    bool success = false;

                    if (S.TryIsString(0, "user"))
                    {
                        #region
                        User user;
                        if (S.TryGetUser(1, c, out user))
                        {
                            appendDataObject(builder, success = true,
                                () =>
                                {
                                    appendProperty(builder, "name", user.Name);
                                    appendProperty(builder, "points", user.Points);
                                    appendProperty(builder, "flags", user.Flags.ToString());
                                    appendProperty(builder, "calories", user.Calories);
                                    appendProperty(builder, "messageCount", user.MessageCount);
                                    appendProperty(builder, "characterCount", user.CharacterCount);
                                    appendProperty(builder, "gachiGASM", user.GachiGASM);
                                    appendDataArray(builder, "items", () =>
                                    {
                                        if (user.Inventory != null)
                                            lock (user.Inventory)
                                            {
                                                bool first = true;
                                                foreach (var item in user.Inventory)
                                                {
                                                    appendPair(builder, item.Name, item.Count, first);
                                                    first = false;
                                                }
                                            }
                                    });
                                });
                        }
                        #endregion
                    }
                    else if (S.TryIsString(0, "recipes"))
                    {
                        #region
                        recipesCache = cache = cache ?? new Func<string>(() =>
                        {
                            lock (ShopItem.Items)
                            {
                                appendDataArray(builder, true, () =>
                                {
                                    bool first = true;
                                    foreach (var item in ShopItem.Items.Values.Where(item => item.Recipe != null))
                                    {
                                        appendPair(builder, (item.CraftCount == 1 ? "" : item.CraftCount + " ") + item.Name, string.Join(", ", item.Recipe.Select(x => x.Item1.GetNumber(x.Item2))), first);
                                        first = false;
                                    }
                                });
                            }
                            return builder.ToString();
                        })();
                        success = true;
                        #endregion
                    }
                    else if (S.TryIsString(0, "top"))
                    {
                        #region
                        long topCount;
                        if (S.TryGetInt(2, false, 50, out topCount))
                            topCount = Math.Min(50, topCount);
                        else
                            topCount = 50;

                        ShopItem item;
                        if (S.TryGetItemOrPointz(1, out item))
                        {
                            if (itemCache.TryGetCache(item?.Name ?? "points", out cache))
                            {
                                success = true;
                            }
                            else
                            {
                                if (item == null)
                                {
                                    appendDataArray(builder, success = true, () =>
                                    {
                                        bool first = true;
                                        foreach (var x in c.UsersByName.Values.OrderBy(user => user.Points * -1).Take((int)topCount).Where(user => user.Points != 0))
                                        {
                                            appendPair(builder, x.Name, x.Points, first);
                                            first = false;
                                        }
                                    });
                                    itemCache.SetCache("pointz", cache = builder.ToString());
                                }
                                else
                                {
                                    appendDataArray(builder, success = true, () =>
                                    {
                                        bool first = true;
                                        foreach (var x in c.UsersByName.Values.OrderBy(user => user.ItemCount(item.Name) * -1).Take((int)topCount).Where(user => user.ItemCount(item.Name) != 0))
                                        {
                                            appendPair(builder, x.Name, x.ItemCount(item.Name), first);
                                            first = false;
                                        }
                                    });
                                    itemCache.SetCache(item.Name, cache = builder.ToString());
                                }
                            }
                        }
                        #endregion
                    }
                    else if (S.TryIsString(0, "list"))
                    {
                        if (S.TryIsString(1, "items"))
                        {
                            #region
                            if (itemsCache == null)
                            {
                                appendDataArray(builder, success = true, () =>
                                {
                                    bool first = true;
                                    lock (ShopItem.Items)
                                    {
                                        foreach (var x in ShopItem.Items.Values.Select(item => item.Name))
                                        {
                                            if (!first)
                                                builder.Append(',');
                                            appendString(builder, x);
                                            first = false;
                                        }
                                    }
                                });
                                cache = itemsCache = builder.ToString();

                            }
                            else
                            {
                                cache = itemsCache;
                            }
                            success = true;
                            #endregion
                        }
                        else if (S.TryIsString(1, "flag"))
                        {
                            UserFlags flags;
                            if (S.Length > 2)
                            {
                                if (Enum.TryParse(S[2], true, out flags))
                                {
                                    #region
                                    if (itemsCache == null)
                                    {
                                        appendDataArray(builder, success = true, () =>
                                        {
                                            bool first = true;
                                            foreach (var u in c.UsersByID.Values.Where(x => (x.Flags & flags) == flags))
                                            {
                                                if (!first)
                                                    builder.Append(',');
                                                appendString(builder, u.Name);
                                                first = false;
                                            }
                                        });
                                        cache = itemsCache = builder.ToString();

                                    }
                                    else
                                    {
                                        cache = itemsCache;
                                    }
                                    success = true;
                                    #endregion
                                }
                            }
                        }
                    }

                    if (!success)
                    {
                        builder.Append("false }");
                    }

                    bytes = Encoding.UTF8.GetBytes(cache ?? builder.ToString());
                    client.Send(bytes);

                    client.Close();
                }
                catch (Exception exc)
                {
                    File.WriteAllText("apiservererror", exc.Message);
                    break;
                }
            }
        }

        static void appendDataObject(StringBuilder builder, bool success, Action action)
        {
            builder.Append(success ? "true" : "false");
            builder.Append(", data: {");
            action?.Invoke();
            builder.Append("} }");
        }

        static void appendDataArray(StringBuilder builder, bool success, Action action)
        {
            builder.Append(success ? "true" : "false");
            builder.Append(", data: [");
            action?.Invoke();
            builder.Append("]}");
        }

        static void appendDataObject(StringBuilder builder, string name, Action action)
        {
            builder.Append(name);
            builder.Append(": {");
            action?.Invoke();
            builder.Append("}");
        }

        static void appendDataArray(StringBuilder builder, string name, Action action)
        {
            builder.Append(name);
            builder.Append(": [");
            action?.Invoke();
            builder.Append("]");
        }

        static void appendProperty(StringBuilder builder, string name, long item)
        {
            builder.Append(name);
            builder.Append(":");
            builder.Append(item);
            builder.Append(",");
        }

        static void appendProperty(StringBuilder builder, string name, string item)
        {
            builder.Append(name);
            builder.Append(":");
            builder.Append('"');
            builder.Append(item);
            builder.Append('"');
            builder.Append(",");
        }

        static void appendLastProperty(StringBuilder builder, string name, long item)
        {
            builder.Append(name);
            builder.Append(":");
            builder.Append(item);
        }

        static void appendLastProperty(StringBuilder builder, string name, string item)
        {
            builder.Append(name);
            builder.Append(":");
            builder.Append('"');
            builder.Append(item);
            builder.Append('"');
        }

        static void appendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            builder.Append(value);
            builder.Append('"');
        }

        static void appendPair(StringBuilder builder, string object1, string object2, bool first)
        {
            if (!first)
                builder.Append(',');
            builder.Append('[');
            builder.Append('"');
            builder.Append(object1);
            builder.Append("\",\"");
            builder.Append(object2);
            builder.Append('"');
            builder.Append(']');
        }

        static void appendPair(StringBuilder builder, string object1, long object2, bool first)
        {
            if (!first)
                builder.Append(',');
            builder.Append('[');
            builder.Append('"');
            builder.Append(object1);
            builder.Append("\",");
            builder.Append(object2);
            builder.Append(']');
        }
    }

    public static class ApiExtensions
    {
        public static bool TryGetCache(this Cache cache, string key, out string value)
        {
            Tuple<DateTime, string> item;
            if (cache.TryGetValue(key, out item))
            {
                if (item.Item1 > DateTime.Now)
                {
                    value = item.Item2;
                    return true;
                }
            }
            value = null;
            return false;
        }

        public static void SetCache(this Cache cache, string key, string value)
        {
            cache[key] = Tuple.Create(DateTime.Now + Api.CacheCooldown, value);
        }
    }
}
