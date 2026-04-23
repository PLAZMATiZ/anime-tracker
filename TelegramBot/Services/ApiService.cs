using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TelegramBot.Exceptions;

namespace TelegramBot.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApiService> _logger;

        public ApiService(HttpClient httpClient, ILogger<ApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<bool> IsUserExists(long telegramId)
        {
            var response = await _httpClient.GetAsync($"api/user/{telegramId}");
            return response.IsSuccessStatusCode;
        }

        public async Task CreateUser(long telegramId)
        {
            try
            {
                var response = await _httpClient.PostAsync($"api/user/{telegramId}", null);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error creating user {TelegramId}", telegramId);
                throw;
            }
        }

        public async Task DeleteUser(long telegramId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/user/{telegramId}");
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error deleting user {TelegramId}", telegramId);
                throw;
            }
        }

        // ДОПИСАНО: Передача даних через Body (PUT запит)
        public async Task AddAnimeToWatched(long telegramId, int animeId, string animeName)
        {
            try
            {
                var dto = new AddWatchedDto
                {
                    UserTelegramId = telegramId,
                    AnimeName = animeName,
                    MyAnimeListId = animeId
                };
                var Json = JsonSerializer.Serialize(dto);
                _logger.LogInformation("Json: {Json}", Json);

                var response = await _httpClient.PutAsJsonAsync("api/me/anime", dto);
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation("Anime {AnimeId} already exists for user {TelegramId}", animeId, telegramId);
                    throw new BotException("Anime already exists", 409);
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error adding anime {AnimeId} for user {TelegramId}", animeId, telegramId);
                throw;
            }
        }

        public async Task RemoveAnimeFromWatched(long telegramId, int animeId)
        {
            try
            {
                var dto = new
                {
                    UserId = telegramId,
                    AnimeName =
                        "",
                    MyAnimeListId = animeId
                };

                var request = new HttpRequestMessage(HttpMethod.Delete, "api/me/anime")
                {
                    Content = JsonContent.Create(dto)
                };

                var response = await _httpClient.SendAsync(request);
                if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Anime {AnimeId} not found for user {TelegramId}", animeId, telegramId);
                    throw new BotException("Anime not found", 404);
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error removing anime {AnimeId} from user {TelegramId}", animeId, telegramId);
                throw;
            }
        }

        public async Task<List<int>> GetWatchedAnimes(long telegramId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/me/anime/{telegramId}");
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<List<int>>() ?? new List<int>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error getting watched animes for user {TelegramId}", telegramId);
                throw;
            }
        }

        public async Task<List<AnimeShortInfo>> FindAnime(string name)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/anime/find/{name}");
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<List<AnimeShortInfo>>() ?? new List<AnimeShortInfo>();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error finding anime {AnimeName}", name);
                throw;
            }
        }

        public async Task<AnimeShortInfo> GetAnime(int id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/anime/{id}");
    
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<AnimeShortInfo>() ?? new AnimeShortInfo();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error getting anime {AnimeId}", id);
                throw;
            }
        }
    }

public class AnimeShortInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Name { get; set; } = null!;
}

public class AddWatchedDto
{
    public long UserTelegramId { get; set; }
    public string AnimeName { get; set; }
    public int MyAnimeListId { get; set; }
}
}