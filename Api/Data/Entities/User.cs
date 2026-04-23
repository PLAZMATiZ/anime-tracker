using System.ComponentModel.DataAnnotations;

namespace AnimeTracker.Data.Entities;

public class User
{
    [Key] public long Id { get; set; }
    [Required] public long TelegramId { get; set; }

    public DateTimeOffset CreateAt { get; set; } = DateTimeOffset.Now;

    public ICollection<WatchHistory> Watched { get; set; } = new List<WatchHistory>();
}