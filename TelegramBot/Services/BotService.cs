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
        private readonly Dictionary<long, string?> _lastUserCommands = new();
        private readonly Dictionary<long, Message> _lastUserMessages = new();
        private readonly Dictionary<long, Message> _lastBotMessages = new();

        public MyBotService(ILogger<MyBotService> logger, ITelegramBotClient botClient, ApiService apiService)
        {
            _logger = logger;
            _botClient = botClient;
            _apiService = apiService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Бот запускається...");

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
            _logger.LogInformation("Бот @{Username} запущений!", me.Username);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
            CancellationToken cancellationToken)
        {
            var chatId = update.Type switch
            {
                UpdateType.Message => update.Message.Chat.Id,
                UpdateType.CallbackQuery => update.CallbackQuery.Message.Chat.Id,
                UpdateType.EditedMessage => update.EditedMessage.Chat.Id,
                UpdateType.ChannelPost => update.ChannelPost.Chat.Id,
                _ => 0 // Якщо тип не підтримує чат (наприклад, опитування)
            };

            // 1. ОБРОБКА ТЕКСТОВИХ ПОВІДОМЛЕНЬ
            if (update.Message is { Text: { } messageText } message)
            {
                if (_lastUserCommands.TryGetValue(message.Chat.Id, out var lastCommand))
                {
                    
                    switch (lastCommand)
                    {
                        case "/find":
                            try
                            {
                                await FindAnime(chatId, messageText, botClient, cancellationToken);
                                ClearLastUserCommand(message.Chat.Id);
                            }
                            catch (Exception e)
                            {
                                await SendMessage(chatId, "Нічого не знайдено, спробуйте ще", botClient, cancellationToken);
                                _logger.LogError(e, "Помилка при пошуку аніме");
                                throw;
                            }
                            break;
                    }
                }

                if(!_lastUserMessages.TryAdd(message.Chat.Id, message))
                {
                    _lastUserMessages[message.Chat.Id] = message;
                }

                

                _logger.LogInformation("Отримано текст: {Text}", messageText);

                long userId = message.From?.Id ?? -1;

                if (messageText == "/start")
                {
                    var userExits = await _apiService.IsUserExists(userId);
                    if (!userExits) await _apiService.CreateUser(userId);

                    await SendMessage(chatId, "Привіт! Якщо ви хочете додати аніме, напишіть /find.", botClient, cancellationToken);                    
                    
                    await MainMenu(chatId, botClient, cancellationToken);
                    return;
                }

                if (messageText.StartsWith("/find"))
                {
                    var animeName = messageText.Replace("/find", "").Trim();
                    if (string.IsNullOrEmpty(animeName))
                    {
                        await SendMessage(chatId, "Напишіть назву аніме на англійській, щоб знайти його.", botClient, cancellationToken);
                        SetLastUserCommand(userId, "/find");
                        return;
                    }

                    await FindAnime(chatId, animeName, botClient, cancellationToken);
                }
                return;
            }

            if (update.CallbackQuery is { } callbackQuery)
            {
                _logger.LogInformation("Натиснуто кнопку: {Data}", callbackQuery.Data);

                long userId = callbackQuery.From.Id;
                string data = callbackQuery.Data ?? "";

                await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

                if (data == "add_anime")
                {
                    SetLastUserCommand(userId, "/find");
                    await botClient.SendMessage(chatId, "Напишіть назву аніме на англійській, щоб знайти його. ", cancellationToken: cancellationToken);
                }
                else if (data == "delete:")
                {
                    int animeId = int.Parse(data.Split(':')[1]);

                    await DeleteLastBotMessage(chatId, botClient, cancellationToken);
                    await DeleteLastUserMessage(chatId, botClient, cancellationToken);

                    await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

                    await RemoveFromWatched(userId, animeId, chatId, botClient, cancellationToken);

                    await MainMenu(chatId, botClient, cancellationToken);
                }
                else if (data == "menu_main")
                {
                    await MainMenu(chatId, botClient, cancellationToken);
                }
                else if (data.StartsWith("add:"))
                {
                    int animeId = int.Parse(data.Split(':')[1]);
                    
                    await DeleteLastBotMessage(chatId, botClient, cancellationToken);
                    await DeleteLastUserMessage(chatId, botClient, cancellationToken);

                    await _botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);   

                    await AddToWatched(userId, animeId, botClient, cancellationToken);

                    await MainMenu(chatId, botClient, cancellationToken);
                }
                else if (data == "remove:")
                {
                    int animeId = int.Parse(data.Split(':')[1]);
                    await RemoveFromWatched(userId, animeId, chatId, botClient, cancellationToken);
                }
            }
        }

        private void SetLastUserCommand(long chatId, string command)
        {
            if (!_lastUserCommands.TryAdd(chatId, command))
            {
                _lastUserCommands[chatId] = command;
            }
            else
            {
                _lastUserCommands[chatId] = command;
            }
        }
        private void ClearLastUserCommand(long chatId)
        {
            _lastUserCommands.Remove(chatId);
        }

        private async Task SendMessage(long chatId, string text, ITelegramBotClient botClient,
            CancellationToken cancellationToken, ReplyMarkup? replyMarkup = null)
        {
            var message = await botClient.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: replyMarkup,
                cancellationToken: cancellationToken
            );
            if(!_lastBotMessages.TryAdd(message.Chat.Id, message))
            {
                _lastBotMessages[message.Chat.Id] = message;
            }
        }

        private async Task DeleteLastUserMessage(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            if (_lastUserMessages.TryGetValue(chatId, out var lastMessage))
            {
                await botClient.DeleteMessage(chatId: chatId, messageId: lastMessage.MessageId,
                    cancellationToken: cancellationToken);
                _lastUserMessages.Remove(chatId);
            }
        }
        private async Task DeleteLastBotMessage(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            if (_lastBotMessages.TryGetValue(chatId, out var lastMessage))
            {
                await botClient.DeleteMessage(chatId: chatId, messageId: lastMessage.MessageId,
                    cancellationToken: cancellationToken);
                _lastBotMessages.Remove(chatId);
            }
        }

        private async Task MainMenu(long chatId, ITelegramBotClient botClient, CancellationToken cancellationToken)
        {
            var buttons = new List<InlineKeyboardButton[]>()
            {
                new InlineKeyboardButton[]
                {
                    InlineKeyboardButton.WithCallbackData("Додати аніме", "add_anime"),
                    InlineKeyboardButton.WithCallbackData("Видалити аніме", "remove_anime")
                }
            };

            await SendMessage(chatId, "Головне меню", botClient, cancellationToken, new InlineKeyboardMarkup(buttons));   
        }

        private async Task FindAnime(long chatId, string name, ITelegramBotClient botClient,
            CancellationToken cancellationToken)
        {
            var animes = await _apiService.FindAnime(name);

            if (animes == null || !animes.Any())
            {
                throw new Exception("Anime not found");
            }

            var buttons = new List<InlineKeyboardButton[]>();

            foreach (var anime in animes.Take(5))
            {
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData(anime.Name, $"add:{anime.Id}")
                });
            }

            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("Вийти в меню", "menu_main")
            });

            var keyboard = new InlineKeyboardMarkup(buttons);

            await SendMessage(chatId, "Ось що я знайшов. Обери аніме, щоб додати у переглянуті:", botClient, cancellationToken, keyboard);
        }

        private async Task AddToWatched(long userId, int animeId, ITelegramBotClient botClient,
            CancellationToken cancellationToken)
        {
            var anime = await _apiService.GetAnime(animeId);
            try
            {
                await _apiService.AddAnimeToWatched(userId, animeId, anime.Name);

                await SendMessage(userId, "Anime added to watched list", botClient, cancellationToken);
            }
            catch (BotException e)
            {
                await SendMessage(userId, e.Message, botClient, cancellationToken);
            }
        }

        private async Task RemoveFromWatched(long userId, int animeId, long chatId, ITelegramBotClient botClient,
            CancellationToken cancellationToken)
        {
            try
            {   
                await _apiService.RemoveAnimeFromWatched(userId, animeId);
            }
            catch (BotException e)
            {
                await SendMessage(userId, e.Message, botClient, cancellationToken);
                return;
            }

            await SendMessage(chatId, "Anime removed from watched list", botClient, cancellationToken);
            await Task.Delay(300, cancellationToken);
            await DeleteLastBotMessage(chatId, botClient, cancellationToken);
            await DeleteLastUserMessage(chatId, botClient, cancellationToken);

            await MainMenu(chatId, botClient, cancellationToken);
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Помилка Telegram API");
            return Task.CompletedTask;
        }
    }
}