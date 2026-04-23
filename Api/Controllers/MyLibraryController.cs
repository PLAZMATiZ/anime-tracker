using AnimeTracker.Exceptions;
using AnimeTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Controllers
{
    [ApiController]
    [Route("api/me/anime")]
    public class MyLibraryController : ControllerBase
    {
        private readonly ILogger<MyLibraryController> _logger;
        private readonly AnimeService _animeService;
        private readonly UserService _userService;

        public MyLibraryController(
            ILogger<MyLibraryController> logger,
            AnimeService animeService,
            UserService userService)
        {
            _logger = logger;
            _animeService = animeService;
            _userService = userService;
        }

        [HttpPut]
        public async Task<IActionResult> AddWatched([FromBody] AddWatchedDto dto)
        {
            try
            {
                var user = await _userService.FindUser(dto.UserTelegramId);
                var userId = user.Id;
                await _animeService.AddWatched(userId, dto.MyAnimeListId, dto.AnimeName);
                return Ok();
            }
            catch (ApiException e)
            {
                return StatusCode(e.StatusCode, e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);    
                return StatusCode(500, "Internal server eror");
            }
        }
        [HttpDelete]
        public async Task<IActionResult> RemoveWatched([FromBody] RemoveWatchedDto dto)
        {
            try
            {
                var user = await _userService.FindUser(dto.UserTelegramId);
                var userId = user.Id;
                await _animeService.RemoveWatched(userId, dto.MyAnimeListId);
                return Ok();
            }
            catch (ApiException e)
            {
                return StatusCode(e.StatusCode, e.Message);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                throw;
            }
        }

        [HttpGet("{telegramId}")]
        public async Task<IActionResult> GetWatchedAnimes(long telegramId)
        {
            try
            {
                // Тобі потрібно додати цей метод у свій UserService або AnimeService
                var animeIds = await _animeService.GetWatchedAnimeIds(telegramId);
                return Ok(animeIds);
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                return StatusCode(500, "Internal server error");
            }
        }
    }
    public class AddWatchedDto
    {
        public long UserTelegramId { get; set; }
        public string AnimeName { get; set; }
        public int MyAnimeListId { get; set; }
    }
    public class RemoveWatchedDto
    {
        public long UserTelegramId { get; set; }
        public string AnimeName { get; set; }
        public int MyAnimeListId { get; set; }
    }

}
