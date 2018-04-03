using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiscordDumper
{
    class Program
    {
        private string token;
        private DiscordClient client;

        private static void ConsoleLog(string msg, bool isRead = false)
        {
            if (!isRead)
                Console.WriteLine($"[Discord DM tool] [{DateTime.Now.ToShortTimeString()}]: {msg}");
            else Console.Write($"[Discord DM tool] [{DateTime.Now.ToShortTimeString()}]: {msg}");
        }

        static void Main(string[] args)
        {
            var program = new Program();
            program.Run().GetAwaiter().GetResult();
        }

        private async Task Run()
        {
            var welcomeMsg = "-----[Discord DM tool by Splinter#8029]-----";
            Console.SetCursorPosition((Console.WindowWidth - welcomeMsg.Length) / 2, Console.CursorTop);
            Console.WriteLine(welcomeMsg);
            var verMsg = "Version: 0.1";
            Console.SetCursorPosition((Console.WindowWidth - verMsg.Length) / 2, Console.CursorTop);
            Console.WriteLine(verMsg);
            var dateMsg = "Release: 10.2017";
            Console.SetCursorPosition((Console.WindowWidth - dateMsg.Length) / 2, Console.CursorTop);
            Console.WriteLine(dateMsg);
            Console.OutputEncoding = Encoding.UTF8;

            try
            {
                var json = "";
                using (var fs = File.OpenRead("config.json"))
                using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = await sr.ReadToEndAsync();
                var cfgJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (!cfgJson.ContainsKey("token") || string.IsNullOrEmpty(cfgJson["token"]?.ToString()))
                {
                    ConsoleLog("error: invalid token");
                    return;
                }
                    

                client = new DiscordClient(new DiscordConfiguration
                {
                    AutoReconnect = true,
                    Token = cfgJson["token"].ToString(),
                    TokenType = TokenType.User,
                    LogLevel = LogLevel.Info,
                    UseInternalLogHandler = false
                });
                client.Ready += Client_Ready;
                client.ClientErrored += Client_ClientErrored;
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                ConsoleLog($"exception occured: {ex.GetType()} {ex.Message}");
                Run().GetAwaiter().GetResult();
            }
            await Task.Delay(-1);
        }

        private Task Client_ClientErrored(DSharpPlus.EventArgs.ClientErrorEventArgs e)
        {
            //ConsoleLog($"exception occured: {e.Exception.GetType()} {e.Exception.Message}");
            return Task.CompletedTask;
        }

        private async Task Client_Ready(DSharpPlus.EventArgs.ReadyEventArgs e)
        {
            ConsoleLog("client is ready to process events");
            ConsoleLog("Commands:");
            ConsoleLog("1. clear [DM channel ID]");
            ConsoleLog("2. dump [DM channel ID]");
            ConsoleLog("[DM channel ID] argument is optional. Leave it empty to clear/dump all DM messages/channels");
            //ClearMessages().GetAwaiter().GetResult();
            while (true)
            {
                ConsoleLog("Type your command: ", true);
                var readLine = Console.ReadLine()?.Trim()?.Split(' ');
                var command = readLine[0];
                var arg = readLine.Length == 2 ? readLine[1] : null;
                var isValidArg = ulong.TryParse(arg, out ulong id);
                if (arg != null && !isValidArg)
                {
                    ConsoleLog("error: invalid argument type");
                    continue;
                }

                if (command == "clear")
                {
                    await ClearDmChannelAsync(id);
                }
                else if (command == "dump")
                {
                    await GetDump(id);
                }
                else
                {
                    ConsoleLog("error: invalid command");
                }
            }
        }

        private async Task ClearDmChannelAsync(ulong? id = null)
        {
            var dmChannels = client.PrivateChannels.ToList();
            if (id != null)
            {
                var targetDmChannel = dmChannels.Find(x => x.Id == id);
                if (targetDmChannel == null)
                {
                    ConsoleLog("error: this DM channel does not exist");
                    return;
                }
                else
                {
                    dmChannels = new List<DiscordDmChannel> { targetDmChannel };
                }
            }

            for (int i = 0; i < dmChannels.Count; i++)
            {
                try
                {
                    if (dmChannels[i].Recipients != null && dmChannels[i].Recipients.Count != 0 && dmChannels[i].Recipients[0] != null)
                        ConsoleLog($"current user: {dmChannels[i].Recipients[0].Username}");
                    var messages = await GetDmChannelMessagesAsync(dmChannels[i]).ConfigureAwait(false);
                    await ClearMessagesAsync(messages).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ConsoleLog($"exception occured: {ex.GetType()} {ex.Message}");
                }
            }
            ConsoleLog("all messages have been removed");
            return;
        }

        private async Task<List<DiscordMessage>> GetDmChannelMessagesAsync(DiscordDmChannel dmChannel)
        {
            var messages = new List<DiscordMessage>();
            ulong lastMessageId = 0;

            while (true)
            {
                var messagesBlock = lastMessageId == 0 ? await dmChannel.GetMessagesAsync() : await dmChannel.GetMessagesAsync(before: lastMessageId);
                if (messagesBlock != null && messagesBlock.Count != 0)
                {
                    messages.AddRange(messagesBlock.ToList().FindAll(x => x.Author.Username == client.CurrentUser.Username));
                    lastMessageId = messagesBlock[messagesBlock.Count - 1].Id;
                }
                if (messagesBlock.Count < 100)
                    break;
            }
            return messages;
        }

        private async Task ClearMessagesAsync(List<DiscordMessage> messages)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                try
                {
                    var stringLength = messages[i].Content.Length > 20 ? 20 : messages[i].Content.Length;
                    var message = string.IsNullOrEmpty(messages[i].Content) ? "[attachment]" : messages[i].Content.Substring(0, stringLength);
                    ConsoleLog($"trying to delete {message}...");
                    await messages[i].DeleteAsync();
                    await Task.Delay(300);
                }
                catch (DSharpPlus.Exceptions.UnauthorizedException ex)
                {                    
                    if (ex.WebResponse.Response.Contains("50021"))
                        continue;
                    ConsoleLog($"exception occured: {ex.GetType()} {ex.Message}");
                }
                catch (Exception ex)
                {
                    ConsoleLog($"exception occured: {ex.GetType()} {ex.Message}");
                    i--;
                }
            }
        }

        private async Task GetDump(ulong? id)
        {
            var dmChannels = client.PrivateChannels.ToList();
            if (id != null)
            {
                var targetDmChannel = dmChannels.Find(x => x.Id == id);
                if (targetDmChannel == null)
                {
                    ConsoleLog("error: this DM channel does not exist");
                    return;
                }
                else
                {
                    dmChannels = new List<DiscordDmChannel> { targetDmChannel };
                }
            }
            ulong lastMessageId = 0;
            string dumpText = string.Empty;
            string filename = string.Empty;
            string currentChannelName = string.Empty;
            for (int i = 0; i < dmChannels.Count; i++)
            {
                try
                {
                    if (string.IsNullOrEmpty(currentChannelName))
                    {
                        currentChannelName = dmChannels[i].Recipients.Count != 0 && dmChannels[i].Recipients[0].Username != null ? $"{Regex.Replace(dmChannels[i].Recipients[0].Username, "[*\"<>?:/|]", "_", RegexOptions.IgnoreCase)}" : $"{dmChannels[i].Id}";
                        ConsoleLog($"current channel: {currentChannelName}");
                    }
                    var messages = lastMessageId == 0 ? await dmChannels[i].GetMessagesAsync() : await dmChannels[i].GetMessagesAsync(before: lastMessageId);
                    if (messages != null && messages.Count != 0)
                    {
                        for (int j = 0; j < messages.Count; j++)
                        {
                            string links = string.Empty;
                            var attachments = messages[j].Attachments;
                            if (attachments != null && attachments.Count != 0)
                            {
                                foreach (var attch in attachments)
                                {
                                    links += $"{attch.Url}{Environment.NewLine}";
                                }
                            }
                            switch (messages[j].MessageType)
                            {
                                case MessageType.Call:
                                    continue;
                                case MessageType.RecipientAdd:
                                    continue;
                                case MessageType.RecipientRemove:
                                    continue;
                            }
                            if (lastMessageId == 0 && j == 0)
                                dumpText += $"{messages[j].Author.Username} ({messages[j].Timestamp}){Environment.NewLine}{messages[j].Content}{links}";
                            else dumpText += $"{Environment.NewLine}{Environment.NewLine}{messages[j].Author.Username} ({messages[j].Timestamp}){Environment.NewLine}{messages[j].Content}{links}";
                            links = string.Empty;
                        }
                        if (string.IsNullOrEmpty(filename))
                            filename = dmChannels[i].Recipients.Count != 0 && dmChannels[i].Recipients[0].Username != null ? $"{Regex.Replace(dmChannels[i].Recipients[0].Username, "[*\"<>?:/|]", "_", RegexOptions.IgnoreCase)}.txt" : $"Конфа_{dmChannels[i].Id}.txt";
                        lastMessageId = messages[messages.Count - 1].Id;
                    }
                    if (messages.Count < 100)
                    {
                        await File.AppendAllTextAsync(filename, dumpText);
                        currentChannelName = string.Empty;
                        filename = string.Empty;
                        dumpText = string.Empty;
                        lastMessageId = 0;
                    }
                    else i--;
                }
                catch (Exception ex)
                {
                    ConsoleLog($"exception occured: {ex.GetType()} {ex.Message}");
                }
            }
            ConsoleLog("all messages have been dumped");
            return;
        }
    }
}
