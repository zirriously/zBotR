using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace zBotR
{
    class Program
    {
        /*
        Current plugins:
        LiveRole      | Enabled  | Assigns people currently playing selected game specified role on Discord. Automatically checks every minute.
        EventManager  | WIP      | Allows users to sign up or out for community announcement pings.
        */
        private DiscordSocketClient _client;
        private string _twitchclientid = "";
        private TwitchLookup _twitchLookup;
        private UserHandler _userHandler;
        private List<string> _optout;
        public const string ApiLink = "https://api.twitch.tv/kraken/streams/";
        private ulong _liveRoleId;
        private IRole _liveRole;
        private string _token;

        static void Main() => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 100
            });

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;

            //json to vars
            var botvars = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(@".\botvars.json"));
            _twitchclientid = botvars.twitchclientid;
            string[] optoutarray = botvars.optout.ToObject<string[]>();
            _optout = optoutarray.ToList();
            _liveRoleId = botvars.roleid;
            _token =  botvars.token;
            _twitchLookup = new TwitchLookup(_twitchclientid);


            await _client.LoginAsync(TokenType.Bot, _token);
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
                CheckUsersTimer();

                foreach (var guild in _client.Guilds) // weird way of getting role. Should fix
                {
                    _liveRole = guild.GetRole(_liveRoleId);
                }

                _userHandler = new UserHandler(_client, _liveRole, _twitchLookup);
                return Task.CompletedTask;
            };
            await Task.Delay(Timeout.Infinite);
        }

        #region LiveRole
        private async Task MessageReceived(SocketMessage message)
        {
            if (message.Channel.Name == "bot-stuff" && message.Content.StartsWith(".."))
            {
                bool changedList = false;
                switch (message.Content)
                {
                    case "..optin" when _optout.Contains(message.Author.Id.ToString()):
                        _optout.Remove(message.Author.Id.ToString());
                        changedList = true;
                        await message.Channel.SendMessageAsync($"{message.Author.Username} has opted in.");
                        await Log(new LogMessage(LogSeverity.Info, "Optout", $"{message.Author.Username} has opted in."));
                        break;
                    case "..optin" when !_optout.Contains(message.Author.Id.ToString()):
                        await message.Channel.SendMessageAsync($"Error - {message.Author.Username} is already opted in.");
                        await Log(new LogMessage(LogSeverity.Info, "Optout", $"Error - {message.Author.Username} is already opted in."));
                        break;
                    case "..optout" when !_optout.Contains(message.Author.Id.ToString()):
                        _optout.Add(message.Author.Id.ToString());
                        changedList = true;
                        await message.Channel.SendMessageAsync($"{message.Author.Username} has opted out.");
                        await Log(new LogMessage(LogSeverity.Info, "Optout", $"{message.Author.Username} has opted out."));
                        break;
                    case "..optout" when _optout.Contains(message.Author.Id.ToString()):
                        await message.Channel.SendMessageAsync($"Error - {message.Author.Username} is already opted out.");
                        await Log(new LogMessage(LogSeverity.Info, "Optout", $"Error - {message.Author.Username} is already opted out."));
                        break;
                }

                if (changedList)
                {
                    var botvars = new JObject(
                        new JProperty("roleid", _liveRoleId),
                        new JProperty("token", _token),
                        new JProperty("twitchclientid", _twitchclientid),
                        new JProperty("optout", _optout));
                    File.WriteAllText(@".\botvars.json", botvars.ToString());
                    await Log(new LogMessage(LogSeverity.Info, "Optout", "Successfully saved new json file."));
                }
            }
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
                    await _userHandler.CheckUsers(_optout);
                    Console.ForegroundColor = ConsoleColor.Blue;
                    await Log(new LogMessage(LogSeverity.Info, "Timer", "Done. Sleeping for 1 minute..."));
                    Console.ResetColor();
                    await Task.Delay(60000);
                }
            });
        }
        #endregion

        public static Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }
    }
}
