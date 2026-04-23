using AnimeTracker.Data.Context;
using AnimeTracker.Data.Entities;
using AnimeTracker.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Services;

public class AnimeService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AnimeService> _logger;

    public AnimeService(AppDbContext context, ILogger<AnimeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<int>> GetWatchedAnimeIds(long userId)
    {
        return await _context.Set<WatchHistory>()
            .Where(x => x.UserId == userId)
            .Select(x => x.MyAnimeListId)
            .ToListAsync();
    }

    public async Task AddWatched(long userId, int animeId, string name)
    {
        try
        {
            var watchHistory = new WatchHistory
            {
                UserId = userId,
                MyAnimeListId = animeId,
                AnimeName = name
            };
            if(await _context.Set<WatchHistory>().AnyAsync(x => x.UserId == userId && x.MyAnimeListId == animeId))
                throw new ApiException("Anime already exists", 409);

            await _context.Set<WatchHistory>().AddAsync(watchHistory);
            await _context.SaveChangesAsync();
        }
        catch (ApiException e)
        {
            throw new ApiException(e.Message, e.StatusCode);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw new ApiException("Internal server eror", 500);
        }
    }

    public async Task RemoveWatched(long userId, int animeId)
    {
        try
        {
            var watchHistory = await _context.Set<WatchHistory>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.MyAnimeListId == animeId);
            if (watchHistory is null)
                throw new ApiException("Anime not found", 404);
            _context.Set<WatchHistory>().Remove(watchHistory);
            await _context.SaveChangesAsync();
        }
        catch (ApiException e)
        {
            throw new ApiException(e.Message, e.StatusCode);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
            throw new ApiException("Internal server eror", 500);
        }
    }
}