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
                    var token = hostContext.Configuration.GetValue<string>("BOT_TOKEN");

                    var apiUrl = hostContext.Configuration.GetValue<string>("API_BASE_URL")
                                 ?? "http://api:8080/";

                    services.AddSingleton<ITelegramBotClient>(sp =>
                        new TelegramBotClient(token));

                    services.AddHttpClient<ApiService>(client => { client.BaseAddress = new Uri(apiUrl); });

                    services.AddHostedService<MyBotService>();
                });
    }
}