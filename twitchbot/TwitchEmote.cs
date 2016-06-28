using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class TwitchEmote
    {
        public string Name { get; set; }
        public EmoteType Type { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public int EmoteSet { get; set; }
        public string Url { get; set; }
        public string ImageFormat { get; set; } = "png";
    }

    public enum EmoteType : byte
    {
        TwitchGlobal,
        TwitchSub,
        BttvGlobal,
        BttvChannel
    }
}
