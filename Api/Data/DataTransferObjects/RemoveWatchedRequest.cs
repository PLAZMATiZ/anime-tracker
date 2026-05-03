using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Api.Data.DataTransferObjects
{
    public class RemoveWatchedRequest
    {
        public long UserTelegramId { get; set; }
        public string AnimeName { get; set; }
        public int MyAnimeListId { get; set; }
    }
}