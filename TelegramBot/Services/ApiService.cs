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
            var response = await _httpClient.PostAsync($"api/user/{telegramId}", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task AddAnimeToWatched(long telegramId, int animeId, string animeName)
        {
            var dto = new AddWatchedDto
            {
                UserTelegramId = telegramId,
                AnimeName = animeName,
                MyAnimeListId = animeId
            };

            var response = await _httpClient.PutAsJsonAsync("api/me/anime", dto);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                throw new BotException("Anime already exists", 409);
            }

            response.EnsureSuccessStatusCode();
        }

        public async Task RemoveAnimeFromWatched(long telegramId, int animeId)
        {
            var dto = new
            {
                UserTelegramId = telegramId,
                AnimeName = "Unknown",
                MyAnimeListId = animeId
            };

            var request = new HttpRequestMessage(HttpMethod.Delete, "api/me/anime")
            {
                Content = JsonContent.Create(dto)
            };

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new BotException("Anime not found", 404);
            }

            response.EnsureSuccessStatusCode();
        }

        public async Task<List<int>> GetWatchedAnimes(long telegramId)
        {
            var response = await _httpClient.GetAsync($"api/me/anime/{telegramId}");
            if (!response.IsSuccessStatusCode) return new List<int>();

            return await response.Content.ReadFromJsonAsync<List<int>>() ?? new List<int>();
        }

        public async Task<List<AnimeShortInfo>> FindAnime(string name)
        {
            var response = await _httpClient.GetAsync($"api/anime/find/{name}");
            if (!response.IsSuccessStatusCode) return new List<AnimeShortInfo>();

            return await response.Content.ReadFromJsonAsync<List<AnimeShortInfo>>() ?? new List<AnimeShortInfo>();
        }

        public async Task<AnimeShortInfo> GetAnime(int id)
        {
            var response = await _httpClient.GetAsync($"api/anime/{id}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AnimeShortInfo>() ?? new AnimeShortInfo();
        }
    }

    public class AnimeShortInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Name { get; set; } = string.Empty;
    }

    public class AddWatchedDto
    {
        public long UserTelegramId { get; set; }
        public string AnimeName { get; set; } = string.Empty;
        public int MyAnimeListId { get; set; }
    }
}