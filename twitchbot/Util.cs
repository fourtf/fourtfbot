using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace twitchbot
{
    public static class Util
    {
        public static T Log<T>(this T obj)
        {
            Log(obj, 0);
            return obj;
        }

        public static void Log(this object obj, int intend)
        {
            if (obj == null)
            {
                Console.WriteLine(intend > 0 ? new string(' ', intend) + "null" : "null");
            }
            else if (!(obj is string) && obj is IEnumerable)
            {
                Console.WriteLine(intend > 0 ? new string(' ', intend) + obj.ToString() : obj.ToString());
                if (((IEnumerable)obj).GetEnumerator().MoveNext())
                {
                    foreach (object o in (IEnumerable)obj)
                        Log(o, intend + 2);
                }
            }
            else if (obj is char)
            {
                if ((char)obj <= ' ')
                    Console.WriteLine(intend > 0 ? new string(' ', intend) + "0x" + (short)(char)obj : "0x" + (short)(char)obj);
                else
                    Console.WriteLine(intend > 0 ? new string(' ', intend) + obj.ToString() : obj.ToString());
            }
            else
            {
                Console.WriteLine(intend > 0 ? new string(' ', intend) + obj.ToString() : obj.ToString());
            }
        }

        public static void LogEach<T>(this IEnumerable<T> obj, Func<T, object> func)
        {
            Console.WriteLine(obj);
            foreach (T t in obj)
                Console.WriteLine("  " + func(t));
        }

        public static bool Process<T>(this T element, Action<T> action)
        {
            if (element != null)
            {
                action(element);
                return true;
            }
            return false;
        }

        public static void Do<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (T element in source)
                action(element);
        }

        static Random random = new Random();
        static RandomNumberGenerator random2 = RandomNumberGenerator.Create();

        public static int GetRandom(int min, int max)
        {
            return random.Next(min, max);
        }

        public static bool GetRandom(double percent)
        {
            byte[] bytes = new byte[4];
            random2.GetBytes(bytes);

            return ((BitConverter.ToUInt32(bytes, 0) / (double)uint.MaxValue) * 100) < percent;
        }

        //  Logs a message in a specific color if not in quiet mode
        public static void Log(object message, ConsoleColor? color = null)
        {
            ConsoleColor defaultColor = Console.ForegroundColor;

            if (color != null)
                Console.ForegroundColor = color.Value;

            Console.WriteLine(message);

            if (color != null)
                Console.ForegroundColor = defaultColor;
        }

        public static bool TryGetUser(this string[] items, int index, Bot bot, out User user)
        {
            if (items.Length > index)
            {
                string s = items[index];

                user = bot.GetUserOrDefault(s);
                return user != null;
            }

            user = null;
            return false;
        }

        public static bool TryGetItemOrPointz(this string[] items, int index, out ShopItem item)
        {
            if (items.Length > index)
            {
                string s = items[index];

                if (s == "point" || s == "pointz" || s == "points")
                {
                    item = null;
                    return true;
                }

                item = ShopItem.GetItem(s);
                return item != null;
            }

            item = null;
            return false;
        }

        public static bool TryGetInt(this string[] items, int index, bool allowNegative, long? totalAvailable, out long count)
        {
            if (items.Length > index)
            {
                string s = items[index];

                long i;
                if (s == "a" || s == "an")
                {
                    count = 1;
                    return true;
                }

                if (totalAvailable != null && s == "all")
                {
                    count = totalAvailable.Value;
                    return true;
                }

                if (long.TryParse(s, out i) && ((allowNegative && i != 0) || i >= 1))
                {
                    count = i;
                    return true;
                }
            }

            count = 1;
            return false;
        }

        public static bool True(this object obj)
        {
            return true;
        }


        // Showitem
        public static string GetNumber(this ShopItem item, long count)
        {
            bool single = count == 1 || count == -1;
            if (item == null)
                return $"{(count == 1 ? "a" : count.ToString())} {"point"}{(single ? "" : "z")}";
            else
                return $"{(count == 1 ? item.SingularArticle : count.ToString())} {item.Name}{(single ? "" : ((item.Name[item.Name.Length - 1] == 's') ? "'" : "s"))}";
        }

        public static string GetPlural(this ShopItem item)
        {
            if (item == null)
                return "pointz";
            else
                return $"{item.Name}{((item.Name[item.Name.Length - 1] == 's') ? "'" : "s")}";
        }

        static char[] ws = new char[] { ' ' };

        public static string[] Split(this string value)
        {
            return value.Split(ws, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
