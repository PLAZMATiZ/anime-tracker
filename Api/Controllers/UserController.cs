using AnimeTracker.Exceptions;
using AnimeTracker.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        private readonly ILogger<MyLibraryController> _logger;
        private readonly UserService _userService;

        public UserController(ILogger<MyLibraryController> logger, UserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        [HttpGet("{telegramId}")]
        public async Task<IActionResult> GetUser(long telegramId)
        {
            try
            {
                var user = await _userService.FindUser(telegramId);
                return Ok(user);
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

        

        [HttpPost("{telegramId}")]
        public async Task<IActionResult> CreateUser(long telegramId)
        {
            try
            {
                await _userService.CreateUser(telegramId);
                return Ok("User created");
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

        [HttpDelete("{telegramId}")]
        public async Task<IActionResult> DeleteUser(long telegramId)
        {
            try
            {
                await _userService.DeleteUser(telegramId);
                return Ok("User deleted");
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


    }
}