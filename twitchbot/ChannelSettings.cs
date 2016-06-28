using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class ChannelSettings
    {
        // settings
        public bool EnableCommands { get; set; } = true;
        public bool EnableWhispers { get; set; } = true;
        public bool EnableLinks { get; set; } = true;
        public bool EnableGachi { get; set; } = true;
        public bool EnableTimeouts { get; set; } = false;
        public int MaxMessageLength { get; set; } = 350;
        public int MaxInventoryItemPrint { get; set; } = 20;

        // static stuff
        static ConcurrentDictionary<string, PropertyInfo> properties = new ConcurrentDictionary<string, PropertyInfo>();

        static ChannelSettings()
        {
            Type T = typeof(ChannelSettings);

            foreach (var property in T.GetProperties())
            {
                properties[property.Name] = property;
            }
        }

        // IO
        public void Load(string path)
        {
            IniSettings settings = new IniSettings();
            settings.Load(path);

            foreach (var prop in properties.Values)
            {
                if (prop.PropertyType == typeof(string))
                    prop.SetValue(this, settings.GetString(prop.Name, (string)prop.GetValue(this)));
                else if (prop.PropertyType == typeof(int))
                    prop.SetValue(this, settings.GetInt(prop.Name, (int)prop.GetValue(this)));
                else if (prop.PropertyType == typeof(double))
                    prop.SetValue(this, settings.GetDouble(prop.Name, (double)prop.GetValue(this)));
                else if (prop.PropertyType == typeof(bool))
                    prop.SetValue(this, settings.GetBool(prop.Name, (bool)prop.GetValue(this)));
            }
        }

        public void Save(string path)
        {
            IniSettings settings = new IniSettings();

            foreach (var prop in properties.Values)
            {
                if (prop.PropertyType == typeof(string))
                    settings.Set(prop.Name, (string)prop.GetValue(this));
                else if (prop.PropertyType == typeof(int))
                    settings.Set(prop.Name, (int)prop.GetValue(this));
                else if (prop.PropertyType == typeof(double))
                    settings.Set(prop.Name, (double)prop.GetValue(this));
                else if (prop.PropertyType == typeof(bool))
                    settings.Set(prop.Name, (bool)prop.GetValue(this));
            }

            settings.Save(path);
        }
    }
}
