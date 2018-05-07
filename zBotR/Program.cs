using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace zBotR
{
    class Program
    {
        private DiscordSocketClient _client;
        private string _twitchclientid = "";
        private List<string> _optout;
        private const string _apiLink = "https://api.twitch.tv/kraken/streams/";
        private ulong _liveRoleID;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 100
            });

            _client.Log += Log;

            var botvars = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(@"..\..\botvars.json"));
            string token = botvars.token;
            _twitchclientid = botvars.twitchclientid;
            string[] optoutarray = botvars.optout.ToObject<string[]>();
            _optout = optoutarray.ToList();
            _liveRoleID = botvars.roleid;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            _client.Ready += () =>
            {
                int n = _client.Guilds.Sum(guild => guild.Users.Count);
                Console.ForegroundColor = ConsoleColor.Blue;
                Log(new LogMessage(LogSeverity.Info, "Client",
                        $"{_client.CurrentUser.Username} is connected to" +
                        $" {_client.Guilds.Count} guild, serving a total of {n} online users."));
                Log(new LogMessage(LogSeverity.Info, "Client", $"Total of {_optout.Count} users opted out."));
                Console.ResetColor();
                //CheckUsersTimer();

                return Task.CompletedTask;
            };
            await Task.Delay(Timeout.Infinite);
        }

        private async Task CheckUsers()
        {
            await Task.Run(async () =>
            {
                foreach (var guild in _client.Guilds)
                {
                    foreach (var user in guild.Users)
                    {
                        if (user.Activity != null && user.Activity.Type == ActivityType.Streaming && !_optout.Contains(user.Id.ToString())) // check if streaming
                        {
                            await Log(new LogMessage(LogSeverity.Info, "Client", $"{user} stream title -  {user.Activity.Name}"));
                        }

                        // check if not streaming but has role
                    }
                }

                return Task.CompletedTask;
            });
        }

        private async Task CheckSingleUser(SocketGuildUser user)
        {

        }

        private async Task AssignLiveRole(SocketGuildUser user)
        {

        }

        private void CheckUsersTimer()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    await Log(new LogMessage(LogSeverity.Info, "Timer", "Checking users for stream status..."));
                    Console.ResetColor();
                    await CheckUsers();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    await Log(new LogMessage(LogSeverity.Info, "Timer", "Done. Sleeping for 5000 ms..."));
                    Console.ResetColor();
                    await Task.Delay(5000);
                }
            });
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }
    }
}
