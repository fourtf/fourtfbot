using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using twitchbot.Twitch;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace twitchbot
{
    public class EmoteClrServer
    {
        WebSocketServer server;

        public EmoteClrServer(TwitchChannel channel)
        {
            server = new WebSocketServer(IPAddress.Any, 5201);
            server.AddWebSocketService<EmoteClrServerBehaviour>("/");

            server.Start();
        }

        class EmoteClrServerBehaviour : WebSocketBehavior
        {
            protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
            {
                base.OnMessage(e);
            }
        }

        public void SendEmote(string url)
        {
            server.WebSocketServices.Broadcast("addemote " + url);
        }

        public void Send(string message)
        {
            server.WebSocketServices.Broadcast(message);
        }
    }
}
