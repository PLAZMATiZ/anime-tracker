using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TelegramBot.DataTransferObjects
{
    public class AddWatchedDto
    {
        public long UserTelegramId { get; set; }
        public string AnimeName { get; set; } = string.Empty;
        public int MyAnimeListId { get; set; }
    }
}