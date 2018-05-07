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
        private string _token = "";
        private string _twitchclientid = "";
        private List<string> _optout;

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
            _token = botvars.token;
            _twitchclientid = botvars.twitchclientid;
            string[] optoutarray = botvars.optout.ToObject<string[]>();
            _optout = optoutarray.ToList();

            await _client.LoginAsync(TokenType.Bot, _token);
            await _client.StartAsync();

            _client.Ready += () =>
            {
                int n = _client.Guilds.Sum(guild => guild.Users.Count);
                Console.WriteLine(
                    $"{_client.CurrentUser.Username} is connected to {_client.Guilds.Count} guild, serving a total of {n} online users.");
                return Task.CompletedTask;
            };

            await Task.Delay(Timeout.Infinite);
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }
    }
}
