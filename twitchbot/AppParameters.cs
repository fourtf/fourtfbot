using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class AppParameters
    {
        [Option('v', "verbose", DefaultValue = false)]
        public bool Verbose { get; set; }
    }
}
