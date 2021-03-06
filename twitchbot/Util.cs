﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace twitchbot
{
    public static class Util
    {
        // log in file
        public static string LogBasePath { get; set; } = "./log/";
        static bool checkedLogBase = false;

        static ConcurrentDictionary<string, StreamWriter> logWriters = new ConcurrentDictionary<string, StreamWriter>();

        public static void Log(string file, string format, params object[] args)
        {
            if (!checkedLogBase)
            {
                try
                {
                    if (!Directory.Exists(LogBasePath))
                        Directory.CreateDirectory(LogBasePath);
                }
                catch { }
                checkedLogBase = true;
            }

            var now = DateTime.Now;
            var logFilename = Path.Combine(LogBasePath, file + "." + now.ToString("yyyy-MM") + ".txt");

            try
            {
                var sw = logWriters.GetOrAdd(logFilename, name => File.AppendText(name));
                sw.WriteLine(now.ToString("yyyy-MM-dd HH:mm:ss") + " " + string.Format(format, args).Replace("\n", "\n        "));
                sw.Flush();
            }
            catch { }
        }

        public static T Log<T>(this T obj, string file, string format = "{0}")
        {
            Log(file, format, obj);

            return obj;
        }

        // log in console
        public static T Log<T>(this T obj)
        {
            Log(obj, 0);
            return obj;
        }

        private static void Log(this object obj, int intend)
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

        public static bool TryGetUser(this string[] items, int index, Channel channel, out User user)
        {
            if (items.Length > index)
            {
                string s = items[index];

                user = channel.GetUserOrDefaultByName(s);
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

                if (totalAvailable != null && s.EndsWith("%"))
                {
                    int percent;
                    if (int.TryParse(s.Remove(s.Length - 1), out percent))
                    {
                        percent = Math.Max(1, Math.Min(100, percent));

                        count = totalAvailable.Value * percent / 100;
                        if (count == 0)
                            count = totalAvailable > 0 ? 1 : -1;
                        return true;
                    }
                    else
                    {
                        count = 1;
                        return false;
                    }
                }
                else if (s.EndsWith("k") && long.TryParse(s.Remove(s.Length - 1), out i) && (i = i * 1000).True() && ((allowNegative && i != 0) || i >= 1))
                {
                    count = i;
                    return true;
                }
                else if (long.TryParse(s, out i) && ((allowNegative && i != 0) || i >= 1))
                {
                    count = i;
                    return true;
                }
            }

            count = 1;
            return false;
        }

        public static bool TryIsString(this string[] items, int index, string value)
        {
            if (items.Length > index)
            {
                return items[index] == value;
            }

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

        public static string[] SplitWords(this string value)
        {
            return value.Split(ws, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string SubstringFromWordIndex(this string s, int index)
        {
            int i = 0;

            for (; i < s.Length; i++)
            {
                if (s[i] != ' ')
                    break;
            }

            for (int j = 0; j < index; j++)
            {
                for (; i < s.Length; i++)
                {
                    if (s[i] == ' ')
                        break;
                }

                for (; i < s.Length; i++)
                {
                    if (s[i] != ' ')
                        break;
                }
            }

            if (i < s.Length)
                return s.Substring(i);

            return null;
        }

        public static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        public static void LinuxDownloadFile(string url, string path)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wget",
                    Arguments = $"-O {path} {url}"
                }
            };
            process.Start();
            process.WaitForExit();
        }
    }
}
