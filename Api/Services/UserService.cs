using AnimeTracker.Data;
using AnimeTracker.Data.Context;
using AnimeTracker.Data.Entities;
using AnimeTracker.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Services;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateUser(long telegramId)
    {
        var userTable = _context.Set<User>();

        var user = new User()
        {
            TelegramId = telegramId
        };

        if(userTable.Any(x => x.TelegramId == telegramId))
            throw new ApiException("User already exists", 400);

        try
        {
            await userTable.AddAsync(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw new ApiException("Internal server eror", 500);
        }
    }
    
    public async Task DeleteUser(long telegramId)
    {
        var userTable = _context.Set<User>();

        var user = await userTable.FirstOrDefaultAsync(x => x.TelegramId == telegramId);

        if (user is null)
            throw new ApiException("User not found", 404);

        try
        {
            userTable.Remove(user);
            await _context.SaveChangesAsync();
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw new ApiException("Internal server eror", 500);
        }
    }

    public async Task<User> FindUser(long telegramId)
    {
        var userTable = _context.Set<User>();

        var user = await userTable.FirstOrDefaultAsync(x => x.TelegramId == telegramId);

        if (user is null)
            throw new ApiException("User not found", 404);
        return user;
    }
}