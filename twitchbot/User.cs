﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class User
    {
        public string Name { get; set; }
        public string NameNoPing
        {
            get { return Name.Remove(1) + '\u05C4' + Name.Substring(1); }
        }
        public string ID { get; set; }

        public long Points { get; set; }
        public long NegativePoints { get; set; }

        public long GachiGASM { get; set; }

        public long MessageCount { get; set; }
        public long CharacterCount { get; set; }

        public long Calories { get; set; }

        public UserFlags Flags { get; set; }

        public bool IsBanned { get { return Flags.HasFlag(UserFlags.Banned); } }
        public bool IsAdmin { get { return Flags.HasFlag(UserFlags.Admin); } }
        public bool IsMod { get { return Flags.HasFlag(UserFlags.Mod) || Flags.HasFlag(UserFlags.Admin); } }
        public bool IsBot { get { return Flags.HasFlag(UserFlags.Bot); } }

        public List<InventoryItem> Inventory { get; set; } = null;

        public void AddItem(string name, long count)
        {
            if (Inventory == null)
                Inventory = new List<InventoryItem>(2);

            lock (Inventory)
            {
                for (int i = 0; i < Inventory.Count; i++)
                {
                    if (Inventory[i].Name == name)
                    {
                        Inventory[i] = new InventoryItem(Inventory[i].Name, Inventory[i].Count + count);
                        return;
                    }
                }

                Inventory.Add(new InventoryItem(name, count));
            }
        }

        public bool HasItem(string name, long count)
        {
            if (Inventory == null)
                return false;

            lock (Inventory)
            {
                foreach (var item in Inventory)
                {
                    if (item.Name == name)
                        return item.Count >= count;
                }
            }

            return false;
        }

        public void RemoveItem(string name, long count)
        {
            if (Inventory == null)
                return;

            lock (Inventory)
            {
                for (int i = 0; i < Inventory.Count; i++)
                {
                    var item = Inventory[i];

                    if (item.Name == name)
                    {
                        item.Count = Math.Max(0, item.Count - count);
                        if (item.Count == 0)
                        {
                            Inventory.Remove(item);
                            if (Inventory.Count == 0)
                                Inventory = null;
                        }
                        return;
                    }
                }
            }
        }

        public long ItemCount(string name)
        {
            if (Inventory == null)
                return 0;

            foreach (var item in Inventory)
            {
                if (item.Name == name)
                {
                    return item.Count;
                }
            }

            return 0;
        }

        public override string ToString()
        {
            return Name;
        }

        private int hash = 0;
        public override int GetHashCode()
        {
            return hash == 0 ? (hash = ID.GetHashCode()) : hash;
        }
    }

    public class InventoryItem
    {
        public string Name { get; set; }
        public long Count { get; set; }

        public InventoryItem(string name, long count)
        {
            Name = name;
            Count = count;
        }
    }

    [Flags]
    public enum UserFlags
    {
        None = 0,
        Admin = 1,
        Mod = 2,
        Banned = 4,
        Bot = 8,
        NotNew = 0x10,
    }
}
