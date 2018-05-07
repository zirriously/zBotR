using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace zBotR
{
    class Program
    {
        private DiscordSocketClient _client;
        private string _twitchclientid = "";
        private List<string> _optout;
        private const string _apiLink = "https://api.twitch.tv/kraken/streams/";
        private ulong _liveRoleID;
        private IRole _liveRole;
        string _token;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 100
            });

            _client.Log += Log;
            _client.MessageReceived += MessageReceived;

            var botvars = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(@"..\..\botvars.json"));
            _twitchclientid = botvars.twitchclientid;
            string[] optoutarray = botvars.optout.ToObject<string[]>();
            _optout = optoutarray.ToList();
            _liveRoleID = botvars.roleid;
            _token =  botvars.token;

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

                foreach (var guild in _client.Guilds)
                {
                    _liveRole = guild.GetRole(_liveRoleID);
                }

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
                        // check if streaming
                        if (user.Activity != null && user.Activity.Type == ActivityType.Streaming
                            && !_optout.Contains(user.Id.ToString()))
                        {
                            await Task.Delay(1);
                            CheckSingleUser(user);
                        }

                        // check if not streaming but has role
                        else if (user.Activity != null &&
                                 (user.Roles.Contains(_liveRole) && user.Activity.Type != ActivityType.Streaming))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            await Log(new LogMessage(LogSeverity.Info, "Client", $"{user} is no longer streaming. Removing role."));
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
                streamingGame = (StreamingGame) user.Activity;
            }
            else
            {
                await Log(new LogMessage(LogSeverity.Info, "CRITICAL", $"Error"));
                return;
            }
            var twitchUserName = streamingGame.Url.Substring(streamingGame.Url.LastIndexOf('/') + 1);
            var apiRequest = _apiLink + twitchUserName;
            var apiRequestResponse = await TwitchRequest(apiRequest);

            if (apiRequestResponse == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Log(new LogMessage(LogSeverity.Info, "API", "Error fetching API data."));
                Console.ResetColor();
                return;
            }

            var responseJson = JsonConvert.DeserializeObject<dynamic>(apiRequestResponse);
            var gameStreamed = responseJson.stream.game;

            await Log(new LogMessage(LogSeverity.Info, "API", $"{user} is streaming {gameStreamed}"));

            if (gameStreamed == "Factorio" && !user.Roles.Contains(_liveRole))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                await Log(new LogMessage(LogSeverity.Info, "API", $"{user} is streaming Factorio and does not have live role. Assigning!"));
                Console.ResetColor();
                await user.AddRoleAsync(_liveRole);
            }
            else if (gameStreamed == "Factorio" && user.Roles.Contains(_liveRole))
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                await Log(new LogMessage(LogSeverity.Info, "API",
                    $"{user} is streaming Factorio, but already has role. Not assigning."));
                Console.ResetColor();
            }
            else if (gameStreamed != "Factorio" && user.Roles.Contains(_liveRole))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                await Log(new LogMessage(LogSeverity.Info, "Client", $"{user} is streaming, but not Factorio. Removing role."));
                Console.ResetColor();
                await user.RemoveRoleAsync(_liveRole);
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
                    await CheckUsers();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    await Log(new LogMessage(LogSeverity.Info, "Timer", "Done. Sleeping for 1 minute..."));
                    Console.ResetColor();
                    await Task.Delay(60000);
                }
            });
        }

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
                    JObject botvars = new JObject(
                        new JProperty("roleid", _liveRoleID),
                        new JProperty("token", _token),
                        new JProperty("twitchclientid", _twitchclientid),
                        new JProperty("optout", _optout.ToArray()));
                    File.WriteAllText(@"..\..\botvars.json", botvars.ToString());
                    await Log(new LogMessage(LogSeverity.Info, "Optout", $"Successfully saved new json file."));
                }
            }
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }

        private Task<string> TwitchRequest(string url)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Headers.Add("Client-ID", _twitchclientid);
            req.Timeout = 2000;
            req.KeepAlive = true;

            var webResponse = (HttpWebResponse)req.GetResponse();

            if (webResponse.StatusCode == HttpStatusCode.OK)
            {
                var sr = new StreamReader(webResponse.GetResponseStream() ?? throw new InvalidOperationException());
                string strResponse = sr.ReadToEnd();
                sr.Close();
                return Task.FromResult(strResponse);
            }
            return null;

        }
    }
}
