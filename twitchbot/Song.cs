using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class Song
    {
        public string YoutubeID { get; set; }
        public string Name { get; set; }

        public int TimesInPlaylist { get; set; }
        public string Type { get; set; } = "Unknown";

        public DateTime TimeAdded { get; set; }
    }
}
