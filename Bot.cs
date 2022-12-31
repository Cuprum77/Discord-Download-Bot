using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Specialized;

using DownloadBot.Services;

using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace DownloadBot
{
    public class Bot
    {
        public static int MaxFileSize = 8388608;
        public static int SearchLimit;

        private readonly IServiceProvider services;
        private static string token = "";
        private static string status = "";

        private readonly DiscordSocketConfig config = new()
        {
            LogLevel = LogSeverity.Warning,
            // remove the unused priviledges, this is just a lazy way of doing it
            GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents)
        };

        public Bot()
        {
            // start the service provider
            services = new ServiceCollection()
                .AddSingleton(config)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<WebHandler>()
                .AddSingleton<InterractionHandler>()
                .BuildServiceProvider();
        }

        public async Task RunAsync()
        {
            // retrieve all settings
            ReadAllSettings();

            // get the discord client
            var client = services.GetRequiredService<DiscordSocketClient>();
            // attach functions to events
            client.Log += LogAsync;
            client.Ready += Ready;

            // initialize the interractions
            await services.GetRequiredService<InterractionHandler>().InitializeAsync();

            // log into discord
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // make sure we arent quitting any time soon
            await Task.Delay(Timeout.Infinite);
        }
        
        private Task Ready()
        {
            // get the discord client and print in a welcome message
            var client = services.GetRequiredService<DiscordSocketClient>();
            Console.WriteLine($$"""
                --------------------------------------------------
                Bot is logged in as {{client.CurrentUser.Username ?? "UNKNOWN"}}!
                --------------------------------------------------

                Status set to: {{(string.IsNullOrEmpty(status) ? "[NONE]" : status)}}
                Logged in to {{client.Guilds.Count}} servers

                --------------------------------------------------
                """);
            return Task.CompletedTask;
        }

        private async Task LogAsync(LogMessage message)
            => Console.WriteLine(message);

        private static void ReadAllSettings()
        {
            try
            {
                var settings = ConfigurationManager.AppSettings;

                // if there are no data in the configuration file, throw an exception
                if (settings.Count == 0)
                    throw new ConfigurationErrorsException("App settings are empty!");
                else
                {
                    // attempt to retrieve the data
                    token = settings["discord-token"] ?? "";
                    // throw an exception if we cant find the token
                    if (string.IsNullOrEmpty(token))
                        throw new ConfigurationErrorsException("The \"discord-token\"-field can't be empty!");
                    
                    // retrieve the rest of the data if it exists
                    status = settings["status"] ?? "";

                    // attempt to parse the settings as an integer
                    string searchLimit = settings["search-limit"] ?? "";

                    if (!string.IsNullOrEmpty(searchLimit))
                    {
                        if (int.TryParse(searchLimit, out int limit))
                            SearchLimit = limit;
                        else
                            throw new ConfigurationErrorsException("The \"search-limit\"-field must be an integer!");
                    }
                }
            }
            catch (ConfigurationErrorsException e)
            {
                Console.WriteLine(e);
                // close the app
                Environment.Exit(0);
            }
        }
    }
}
