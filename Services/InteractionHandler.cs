using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

using Discord.WebSocket;
using Discord.Interactions;
using System.Reflection;
using Discord;

namespace DownloadBot.Services
{
    public class InterractionHandler
    {
        private readonly DiscordSocketClient client;
        private readonly InteractionService handler;
        private readonly IServiceProvider services;

        public InterractionHandler(DiscordSocketClient client, InteractionService handler, IServiceProvider services)
        {
            this.client = client;
            this.handler = handler;
            this.services = services;
        }

        public Task InitializeAsync()
        {
            // subscribe to events
            handler.Log += LogAsync;
            client.Ready += ReadyAsync;

            // add the public commands to the InteractionService 
            handler.AddModulesAsync(Assembly.GetEntryAssembly(), services);
            client.InteractionCreated += HandleInteraction;

            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
            => await handler.RegisterCommandsGloballyAsync(true);

        private async Task LogAsync(LogMessage log)
            => Console.WriteLine(log);

        private async Task HandleInteraction(SocketInteraction interaction)
        {
            try
            {
                // create an execution context for our modules
                var ctx = new SocketInteractionContext(client, interaction);
                // execute the incoming command
                await handler.ExecuteCommandAsync(ctx, services);
            }
            catch
            {
                // if the interaction fails, make sure we delete the original interaction
                if (interaction.Type is InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }
    }
}