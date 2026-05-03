using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Exceptions;

namespace TelegramBot.Services
{
    public class MyBotService : BackgroundService
    {
        private readonly ILogger<MyBotService> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly ApiService _apiService;
        private readonly Dictionary<long, string> _userStates = new();
        private readonly Dictionary<long, int> _lastBotMessageIds = new();

        public MyBotService(ILogger<MyBotService> logger, ITelegramBotClient botClient, ApiService apiService)
        {
            _logger = logger;
            _botClient = botClient;
            _apiService = apiService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("System initialization...");

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: stoppingToken
            );

            var me = await _botClient.GetMe(stoppingToken);
            _logger.LogInformation("Bot @{Username} is online.", me.Username);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.Message && update.Message?.Text != null)
                {
                    await HandleMessageAsync(botClient, update.Message, cancellationToken);
                }
                else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling update.");
            }
        }

        private async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            long chatId = message.Chat.Id;
            string text = message.Text!;

            await SafeDeleteMessage(chatId, message.MessageId, botClient, cancellationToken);

            if (text == "/start")
            {
                await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

                var userExists = await _apiService.IsUserExists(chatId);
                if (!userExists)
                {
                    await _apiService.CreateUser(chatId);
                }

                _userStates.Remove(chatId);
                await ShowMainMenu(chatId, "System initialized. Welcome to your personal library.", cancellationToken);
                return;
            }

            if (_userStates.TryGetValue(chatId, out var state) && state == "awaiting_search")
            {
                await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
                _userStates.Remove(chatId);
                await PerformSearch(chatId, text, cancellationToken);
            }
        }

        private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            long chatId = callbackQuery.Message!.Chat.Id;
            string data = callbackQuery.Data ?? string.Empty;

            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            if (data == "menu_main")
            {
                _userStates.Remove(chatId);
                await ShowMainMenu(chatId, "Main Menu. Select an action.", cancellationToken);
            }
            else if (data == "cmd_add")
            {
                _userStates[chatId] = "awaiting_search";
                await UpdateBotInterface(chatId, "Awaiting input. Please type the title of the anime in English.", cancellationToken, GetBackKeyboard());
            }
            else if (data == "cmd_remove_list")
            {
                await ShowRemoveList(chatId, cancellationToken);
            }
            else if (data.StartsWith("add:"))
            {
                int animeId = int.Parse(data.Split(':')[1]);
                await AddToWatched(chatId, animeId, cancellationToken);
            }
            else if (data.StartsWith("del:"))
            {
                int animeId = int.Parse(data.Split(':')[1]);
                await RemoveFromWatched(chatId, animeId, cancellationToken);
            }
        }

        private async Task ShowMainMenu(long chatId, string text, CancellationToken cancellationToken)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Add Anime", "cmd_add") },
                new[] { InlineKeyboardButton.WithCallbackData("Remove Anime", "cmd_remove_list") }
            });

            await UpdateBotInterface(chatId, text, cancellationToken, keyboard);
        }

        private async Task PerformSearch(long chatId, string query, CancellationToken cancellationToken)
        {
            try
            {
                var animes = await _apiService.FindAnime(query);

                if (animes == null || !animes.Any())
                {
                    await UpdateBotInterface(chatId, $"No results found for \"{query}\".", cancellationToken, GetBackKeyboard());
                    return;
                }

                var buttons = new List<InlineKeyboardButton[]>();
                foreach (var anime in animes.Take(5))
                {
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(anime.Name, $"add:{anime.Id}") });
                }
                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Back", "menu_main") });

                await UpdateBotInterface(chatId, "Search completed. Select an entry to add to your library:", cancellationToken, new InlineKeyboardMarkup(buttons));
            }
            catch (Exception)
            {
                await UpdateBotInterface(chatId, "Error connecting to the database. Try again later.", cancellationToken, GetBackKeyboard());
            }
        }

        private async Task ShowRemoveList(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

            try
            {
                var watchedIds = await _apiService.GetWatchedAnimes(chatId);

                if (watchedIds == null || !watchedIds.Any())
                {
                    await UpdateBotInterface(chatId, "Your library is currently empty.", cancellationToken, GetBackKeyboard());
                    return;
                }

                var buttons = new List<InlineKeyboardButton[]>();

                foreach (var id in watchedIds.TakeLast(5))
                {
                    var anime = await _apiService.GetAnime(id);
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"Delete: {anime.Name}", $"del:{anime.Id}") });
                }

                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Back", "menu_main") });

                await UpdateBotInterface(chatId, "Select an entry to remove from your library (showing latest):", cancellationToken, new InlineKeyboardMarkup(buttons));
            }
            catch (Exception)
            {
                await UpdateBotInterface(chatId, "Failed to retrieve library data.", cancellationToken, GetBackKeyboard());
            }
        }

        private async Task AddToWatched(long chatId, int animeId, CancellationToken cancellationToken)
        {
            try
            {
                var anime = await _apiService.GetAnime(animeId);
                await _apiService.AddAnimeToWatched(chatId, animeId, anime.Name);
                await ShowMainMenu(chatId, $"Success. \"{anime.Name}\" was added to your library.", cancellationToken);
            }
            catch (BotException ex) when (ex.StatusCode == 409)
            {
                await ShowMainMenu(chatId, "Conflict. This entry already exists in your library.", cancellationToken);
            }
            catch (Exception)
            {
                await ShowMainMenu(chatId, "Error. Failed to add the entry.", cancellationToken);
            }
        }

        private async Task RemoveFromWatched(long chatId, int animeId, CancellationToken cancellationToken)
        {
            try
            {
                await _apiService.RemoveAnimeFromWatched(chatId, animeId);
                await ShowMainMenu(chatId, "Success. Entry removed from your library.", cancellationToken);
            }
            catch (Exception)
            {
                await ShowMainMenu(chatId, "Error. Failed to remove the entry.", cancellationToken);
            }
        }

        private async Task UpdateBotInterface(long chatId, string text, CancellationToken cancellationToken, InlineKeyboardMarkup keyboard = null)
        {
            if (_lastBotMessageIds.TryGetValue(chatId, out int messageId))
            {
                try
                {
                    await _botClient.EditMessageText(
                        chatId: chatId,
                        messageId: messageId,
                        text: text,
                        replyMarkup: keyboard,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken
                    );
                    return;
                }
                catch
                {
                    _lastBotMessageIds.Remove(chatId);
                }
            }

            var newMessage = await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: keyboard,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken
            );

            _lastBotMessageIds[chatId] = newMessage.MessageId;
        }

        private InlineKeyboardMarkup GetBackKeyboard()
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Cancel", "menu_main") }
            });
        }

        private async Task SafeDeleteMessage(long chatId, int messageId, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            try
            {
                await botClient.DeleteMessage(chatId, messageId, cancellationToken);
            }
            catch
            {
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Telegram API Error.");
            return Task.CompletedTask;
        }
    }
}