# fourtfbot
A twitch.tv roleplayer bot created by fourtf

# Build on windows
Use Visual Studio 2015 to build fourtfbot.

# Build on linux
	apt-get install mono-complete
	apt-get install modo-devel
	git clone https://github.com/fourtf/fourtfbot.git
	
	bash linuxbuild.sh

There should now be a folder called build that contains all the application

# How to use
In the programs directory create the following files:
+ "username" where you store the twitch.tv username of your bot
+ "oauthkey" where you store the the oauth key of for the bot's twitch account (should look like xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx)
+ "owner" where you store the name of your normal twitch.tv account
+ "channels" in which every line is a twitch channel that you want to join (eg. "fourtf")

# Run on linux
    mono twitchbot.exe
