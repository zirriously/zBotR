using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace zBotR
{
    public class UserHandler
    {
        private DiscordSocketClient _discordSocketClient;
        private IRole _liveRole;
        private TwitchLookup _twitchLookup;

        public UserHandler(DiscordSocketClient discordSocketClient, IRole liveRole, TwitchLookup twitchLookup)
        {
            _discordSocketClient = discordSocketClient;
            _liveRole = liveRole;
            _twitchLookup = twitchLookup;
        }

        public async Task CheckUsers(List<string> optout)
        {
            await Task.Run(async () =>
            {
                foreach (var guild in _discordSocketClient.Guilds)
                {
                    foreach (var user in guild.Users)
                    {
                        // check if streaming
                        if (user.Activity != null && user.Activity.Type == ActivityType.Streaming
                                                  && !optout.Contains(user.Id.ToString()))
                        {
                            CheckSingleUser(user);
                        }

                        // check if not streaming but has role
                        else if (user.Activity != null &&
                                 (user.Roles.Contains(_liveRole) && user.Activity.Type != ActivityType.Streaming))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            await Program.Log(new LogMessage(LogSeverity.Info, "Client", $"{user} is no longer streaming. Removing role."));
                            Console.ResetColor();
                            await user.RemoveRoleAsync(_liveRole);
                        }
                        else if (user.Activity == null && user.Roles.Contains(_liveRole))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            await Program.Log(new LogMessage(LogSeverity.Info, "Client", $"{user} is no longer streaming. Removing role."));
                            Console.ResetColor();
                            await user.RemoveRoleAsync(_liveRole);
                        }
                    }
                }

                return Task.CompletedTask;
            });
        }

        private async Task CheckSingleUser(SocketGuildUser user)
        {
            StreamingGame streamingGame;
            if (user.Activity != null && user.Activity.Type == ActivityType.Streaming)
            {
                streamingGame = (StreamingGame)user.Activity;
            }
            else
            {
                await Program.Log(new LogMessage(LogSeverity.Info, "CRITICAL", "Error"));
                return;
            }
            var twitchUserName = streamingGame.Url.Substring(streamingGame.Url.LastIndexOf('/') + 1);
            var apiRequest = Program.ApiLink + twitchUserName;
            var apiRequestResponse = await _twitchLookup.TwitchRequest(apiRequest);

            if (apiRequestResponse == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Program.Log(new LogMessage(LogSeverity.Info, "API", "Error fetching API data."));
                Console.ResetColor();
                return;
            }

            var responseJson = JsonConvert.DeserializeObject<dynamic>(apiRequestResponse);
            var gameStreamed = responseJson.stream.game;

            await Program.Log(new LogMessage(LogSeverity.Info, "API", $"{user} is streaming {gameStreamed}"));

            if (gameStreamed == "Factorio" && !user.Roles.Contains(_liveRole))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                await Program.Log(new LogMessage(LogSeverity.Info, "API", $"{user} is streaming Factorio and does not have live role. Assigning!"));
                Console.ResetColor();
                await user.AddRoleAsync(_liveRole);
            }
            else if (gameStreamed == "Factorio" && user.Roles.Contains(_liveRole))
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                await Program.Log(new LogMessage(LogSeverity.Info, "API",
                    $"{user} is streaming Factorio, but already has role. Not assigning."));
                Console.ResetColor();
            }
            else if (gameStreamed != "Factorio" && user.Roles.Contains(_liveRole))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Program.Log(new LogMessage(LogSeverity.Info, "Client", $"{user} is streaming, but not Factorio. Removing role."));
                Console.ResetColor();
                await user.RemoveRoleAsync(_liveRole);
            }
        }
    }
}