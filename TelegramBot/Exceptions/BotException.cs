using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TelegramBot.Exceptions
{
    public class BotException : Exception
    {
        public int StatusCode { get; }

        public BotException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}