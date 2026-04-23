using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AnimeTracker.Data.Entities;

public class WatchHistory
{
   [Key] public int Id { get; set; }

   [Required] public long UserId { get; set; }

   [ForeignKey(nameof(UserId))] public User User { get; set; }

   [Required] public string AnimeName { get; set; }
   [Required] public int MyAnimeListId { get; set; }

   public DateTimeOffset CreateAt { get; set; } = DateTimeOffset.UtcNow;
}