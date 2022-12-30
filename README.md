# Discord-Download-Bot
This bot can download content of the internet and upload it onto Discord
It can currently download content from both Youtube and Reddit, with plans to include more in the future

# Setup
To set up the bot you need to do the following steps in order
 - 1: Head to the [Discord Developer Portal](https://discord.com/developers/applications) and create a new Bot account and grab a bot token. Do not share this with anyone!
 - 2: Edit the "App.config" file and input your bot token in the correct field.
 - 3: Run "docker-compose up -d" to build the Docker container for this project
 
# Problems
The docker container setup does not work with OpenMediaVault installations. To install there, you need to precompile this project and make a custom docker file for this

