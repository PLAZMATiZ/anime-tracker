using System.Text.Json.Serialization;
using AnimeTracker.Exceptions;

namespace AnimeTracker.Services
{
    public class JikanApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<JikanApiService> _logger;

        public JikanApiService(HttpClient httpClient, ILogger<JikanApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://api.jikan.moe/v4/");
        }

        public async Task<List<AnimeShortInfo>> FindAnimeAsync(string name)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<JikanResponse>($"anime?q={Uri.EscapeDataString(name)}&limit=5");
                if (response?.Data == null) return new List<AnimeShortInfo>();

                return response.Data.Select(a => new AnimeShortInfo
                {
                    Id = a.MalId,
                    Name = a.Title,
                    ImageUrl = a.Images?.Jpg?.LargeImageUrl, // Дістаємо картинку
                    Synopsis = a.Synopsis
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при запиті до Jikan API (Find)");
                throw new ApiException("Помилка при запиті до Jikan API", 500);
            }
        }

        public async Task<AnimeShortInfo> GetAnimeAsync(int id)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<JikanSingleResponse>($"anime/{id}");
                if (response?.Data == null) throw new ApiException("Аніме не знайдено", 404);

                return new AnimeShortInfo
                {
                    Id = response.Data.MalId,
                    Name = response.Data.Title,
                    ImageUrl = response.Data.Images?.Jpg?.LargeImageUrl, // Дістаємо картинку
                    Synopsis = response.Data.Synopsis
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при запиті до Jikan API (Get)");
                throw new ApiException("Помилка при запиті до Jikan API", 500);
            }
        }

        private class JikanResponse { [JsonPropertyName("data")] public List<JikanAnimeData> Data { get; set; } }
        private class JikanSingleResponse { [JsonPropertyName("data")] public JikanAnimeData Data { get; set; } }
        
        private class JikanAnimeData
        {
            [JsonPropertyName("mal_id")] public int MalId { get; set; }
            [JsonPropertyName("title")] public string Title { get; set; }
            [JsonPropertyName("synopsis")] public string Synopsis { get; set; }
            [JsonPropertyName("images")] public JikanImages Images { get; set; }
        }

        private class JikanImages { [JsonPropertyName("jpg")] public JikanJpg Jpg { get; set; } }
        private class JikanJpg { [JsonPropertyName("large_image_url")] public string LargeImageUrl { get; set; } }}

    public class AnimeShortInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Name { get; set; } = null!;

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("synopsis")]
        public string? Synopsis { get; set; }
    }
}