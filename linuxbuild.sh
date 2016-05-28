mkdir -p build
mcs -out:./build/twitchbot.exe -r:./packages/Discord.Net.0.9.3/lib/net45/Discord.Net.dll -r:./packages/DynamicExpresso.Core.1.3.1.0/lib/net40/DynamicExpresso.Core.dll -r:./packages/Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll -r:./packages/Nito.AsyncEx.3.0.1/lib/net45/Nito.AsyncEx.dll -r:./packages/Nito.AsyncEx.3.0.1/lib/net45/Nito.AsyncEx.Enlightenment.dll -r:./packages/Nito.AsyncEx.3.0.1/lib/net45/Nito.AsyncEx.Concurrent.dll -r:./packages/RestSharp.105.2.3/lib/net45/RestSharp.dll -r:./packages/WebSocket4Net.0.14.1/lib/net45/WebSocket4Net.dll -r:./twitchbot/irc/Meebey.SmartIrc4net.dll -r:./twitchbot/irc/StarkSoftProxy.dll /reference:System.Net.Http.dll ./twitchbot/*.cs ./twitchbot/Twitch/*.cs

cp -n ./packages/Discord.Net.0.9.3/lib/net45/Discord.Net.dll ./build/Discord.Net.dll
cp -n ./packages/DynamicExpresso.Core.1.3.1.0/lib/net40/DynamicExpresso.Core.dll ./build/DynamicExpresso.Core.dll
cp -n ./packages/Newtonsoft.Json.8.0.3/lib/net45/Newtonsoft.Json.dll ./build/Newtonsoft.Json.dll
cp -n ./packages/Nito.AsyncEx.3.0.1/lib/net45/Nito.AsyncEx.dll ./build/Nito.AsyncEx.dll
cp -n ./packages/Nito.AsyncEx.3.0.1/lib/net45/Nito.AsyncEx.Enlightenment.dll ./build/Nito.AsyncEx.Enlightenment.dll
cp -n ./packages/Nito.AsyncEx.3.0.1/lib/net45/Nito.AsyncEx.Concurrent.dll ./build/Nito.AsyncEx.Concurrent.dll
cp -n ./packages/RestSharp.105.2.3/lib/net45/RestSharp.dll ./build/RestSharp.dll
cp -n ./packages/WebSocket4Net.0.14.1/lib/net45/WebSocket4Net.dll ./build/WebSocket4Net.dll
cp -n ./twitchbot/irc/Meebey.SmartIrc4net.dll ./build/Meebey.SmartIrc4net.dll
cp -n ./twitchbot/irc/StarkSoftProxy.dll ./build/StarkSoftProxy.dll
