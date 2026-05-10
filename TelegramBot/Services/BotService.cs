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
        private readonly Dictionary<long, string> _lastSearchQueries = new();

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

                _lastSearchQueries[chatId] = text;

                await PerformSearch(chatId, text, cancellationToken);
            }
        }

        private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            long chatId = callbackQuery.Message!.Chat.Id;
            int messageId = callbackQuery.Message.MessageId;
            string data = callbackQuery.Data ?? string.Empty;

            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            if (data == "menu_main")
            {
                _userStates.Remove(chatId);
                await SafeDeleteMessage(chatId, messageId, botClient, cancellationToken);
                await ShowMainMenu(chatId, "Main Menu. Select an action.", cancellationToken);
            }
            else if (data.StartsWith("cmd_my_list:"))
            {
                _userStates.Remove(chatId);
                int page = int.Parse(data.Split(':')[1]);
                await ShowMyList(chatId, page, cancellationToken);
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
            else if (data.StartsWith("view:"))
            {
                int animeId = int.Parse(data.Split(':')[1]);
                await SafeDeleteMessage(chatId, messageId, botClient, cancellationToken);
                await ShowAnimeDetails(chatId, animeId, cancellationToken);
            }
            else if (data.StartsWith("add:"))
            {
                int animeId = int.Parse(data.Split(':')[1]);
                await SafeDeleteMessage(chatId, messageId, botClient, cancellationToken);
                await AddToWatched(chatId, animeId, cancellationToken);
            }
            else if (data.StartsWith("del:"))
            {
                int animeId = int.Parse(data.Split(':')[1]);
                await SafeDeleteMessage(chatId, messageId, botClient, cancellationToken);
                await RemoveFromWatched(chatId, animeId, cancellationToken);
            }
            else if (data == "back_to_search")
            {
                await SafeDeleteMessage(chatId, messageId, botClient, cancellationToken);

                if (_lastSearchQueries.TryGetValue(chatId, out var lastQuery))
                {
                    await PerformSearch(chatId, lastQuery, cancellationToken);
                }
                else
                {
                    await ShowMainMenu(chatId, "Session expired. Returned to main menu.", cancellationToken);
                }
            }
        }

        private async Task ShowMainMenu(long chatId, string text, CancellationToken cancellationToken)
        {
            var keyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("My Library", "cmd_my_list:0") }, // 0 - це стартова сторінка
                new[] { InlineKeyboardButton.WithCallbackData("Add Anime", "cmd_add") },
                new[] { InlineKeyboardButton.WithCallbackData("Remove Anime", "cmd_remove_list") }
            });

            await UpdateBotInterface(chatId, text, cancellationToken, keyboard);
        }

        private async Task ShowMyList(long chatId, int page, CancellationToken cancellationToken)
        {
            await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

            try
            {
                var watchedAnimes = await _apiService.GetWatchedAnimes(chatId);

                if (watchedAnimes == null || !watchedAnimes.Any())
                {
                    await UpdateBotInterface(chatId, "Your library is currently empty.", cancellationToken, GetBackKeyboard());
                    return;
                }

                int pageSize = 10;
                int totalItems = watchedAnimes.Count;
                int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

                if (page < 0) page = 0;
                if (page >= totalPages) page = totalPages - 1;

                var pageItems = watchedAnimes.Skip(page * pageSize).Take(pageSize).ToList();

                var messageLines = new List<string>
                {
                    $"Library Database (Page {page + 1} of {totalPages}):",
                    "----------------------------------------"
                };

                foreach (var anime in pageItems)
                {
                    messageLines.Add($"- {anime.Name}");
                }

                string text = string.Join("\n", messageLines);

                var navigationButtons = new List<InlineKeyboardButton>();

                if (page > 0)
                {
                    navigationButtons.Add(InlineKeyboardButton.WithCallbackData("< Previous", $"cmd_my_list:{page - 1}"));
                }

                if (page < totalPages - 1)
                {
                    navigationButtons.Add(InlineKeyboardButton.WithCallbackData("Next >", $"cmd_my_list:{page + 1}"));
                }

                var keyboardRows = new List<InlineKeyboardButton[]>();

                if (navigationButtons.Any())
                {
                    keyboardRows.Add(navigationButtons.ToArray());
                }

                keyboardRows.Add(new[] { InlineKeyboardButton.WithCallbackData("Back to Menu", "menu_main") });

                await UpdateBotInterface(chatId, text, cancellationToken, new InlineKeyboardMarkup(keyboardRows));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving anime list.");
                await UpdateBotInterface(chatId, "System error. Failed to retrieve the library data.", cancellationToken, GetBackKeyboard());
            }
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
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(anime.Name, $"view:{anime.Id}") });
                }
                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Cancel Search", "menu_main") });

                await UpdateBotInterface(chatId, "Search completed. Select an entry to view details:", cancellationToken, new InlineKeyboardMarkup(buttons));
            }
            catch (Exception)
            {
                await UpdateBotInterface(chatId, "Error connecting to the database. Try again later.", cancellationToken, GetBackKeyboard());
            }
        }

        private async Task ShowAnimeDetails(long chatId, int animeId, CancellationToken cancellationToken)
        {
            await _botClient.SendChatAction(chatId, ChatAction.UploadPhoto, cancellationToken: cancellationToken);

            try
            {
                var anime = await _apiService.GetAnime(animeId);

                string synopsis = anime.Synopsis ?? "No description available.";
                if (synopsis.Length > 800) synopsis = synopsis.Substring(0, 800) + "...";

                string caption = $"🎬 <b>{anime.Name}</b>\n\n📝 <b>Synopsis:</b>\n{synopsis}";

                var keyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("✅ Add to Library", $"add:{anime.Id}") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔙 Back to Search", "back_to_search") },
                    new[] { InlineKeyboardButton.WithCallbackData("🏠 Main Menu", "menu_main") }
                });

                Message newMessage;

                if (!string.IsNullOrEmpty(anime.ImageUrl))
                {
                    newMessage = await _botClient.SendPhoto(
                        chatId: chatId,
                        photo: InputFile.FromUri(anime.ImageUrl),
                        caption: caption,
                        parseMode: ParseMode.Html,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken
                    );
                }
                else
                {
                    newMessage = await _botClient.SendMessage(
                        chatId: chatId,
                        text: caption,
                        parseMode: ParseMode.Html,
                        replyMarkup: keyboard,
                        cancellationToken: cancellationToken
                    );
                }

                _lastBotMessageIds[chatId] = newMessage.MessageId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching anime details.");
                await ShowMainMenu(chatId, "Error loading anime details.", cancellationToken);
            }
        }

        private async Task ShowRemoveList(long chatId, CancellationToken cancellationToken)
        {
            await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

            try
            {
                var watchedAnimes = await _apiService.GetWatchedAnimes(chatId);

                if (watchedAnimes == null || !watchedAnimes.Any())
                {
                    await UpdateBotInterface(chatId, "Your library is currently empty.", cancellationToken, GetBackKeyboard());
                    return;
                }

                var buttons = new List<InlineKeyboardButton[]>();

                foreach (var anime in watchedAnimes.TakeLast(10))
                {
                    buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"Delete: {anime.Name}", $"del:{anime.Id}") });
                }

                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Back", "menu_main") });

                await UpdateBotInterface(chatId, "Select an entry to remove from your library (showing latest):", cancellationToken, new InlineKeyboardMarkup(buttons));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve library data for removal.");
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