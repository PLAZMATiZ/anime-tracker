using AnimeTracker.Exceptions;
using AnimeTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Controllers;

[ApiController]
[Route("api/anime")]
public class AnimeController : ControllerBase
{
    private readonly JikanApiService _jikanApiService;
    private readonly ILogger<AnimeController> _logger;

    public AnimeController(JikanApiService jikanApiService, ILogger<AnimeController> logger)
    {
        _jikanApiService = jikanApiService;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAnime(int id)
    {
        try
        {
            var anime = await _jikanApiService.GetAnimeAsync(id);
            return Ok(anime);
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
    [HttpGet("find/{animeName}")]
    public async Task<IActionResult> GetAnime(string animeName)
    {
        try
        {
            var animes = await _jikanApiService.FindAnimeAsync(animeName);
            return Ok(animes);
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
}