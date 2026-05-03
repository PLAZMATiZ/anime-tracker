using AnimeTracker.Data;
using AnimeTracker.Data.Context;
using AnimeTracker.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AnimeTracker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            UseDependencyInjection(builder);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            var enableSwagger = builder.Configuration.GetValue<bool>("ENABLE_SWAGGER");
            if (app.Environment.IsDevelopment() || enableSwagger)
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }

        private static void UseDependencyInjection(WebApplicationBuilder builder)
        {
            string connectionString = builder.Configuration["DB_CONNECTION_STRING"] ??
                                      throw new Exception("Env var DB_CONNECTION_STRING is missing");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            var dataSource = dataSourceBuilder.Build();

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(dataSource));

            builder.Services.AddHttpClient<JikanApiService>();
            builder.Services.AddScoped<JikanApiService>();
            builder.Services.AddScoped<AnimeService>();
            builder.Services.AddScoped<UserService>();
        }
    }
}
