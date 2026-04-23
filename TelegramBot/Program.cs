using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Telegram.Bot;
using TelegramBot.Services;

namespace TelegramBot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    // 1. Отримуємо токен бота з конфігурації
                    var token = hostContext.Configuration.GetValue<string>("BOT_TOKEN");

                    // 2. СТВОРЮЄМО ЗМІННУ apiUrl (саме на неї сварився ReSharper)
                    var apiUrl = hostContext.Configuration.GetValue<string>("API_BASE_URL")
                                 ?? "http://api:8080/";

                    // 3. Реєструємо клієнта бота
                    services.AddSingleton<ITelegramBotClient>(sp =>
                        new TelegramBotClient(token));

                    // 4. Реєструємо ApiService та передаємо йому apiUrl
                    services.AddHttpClient<ApiService>(client => { client.BaseAddress = new Uri(apiUrl); });

                    // 5. Реєструємо бекграунд-сервіс бота
                    services.AddHostedService<MyBotService>();
                });
    }
}