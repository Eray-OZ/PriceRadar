using PriceRadar.Data.Context;
using PriceRadar.Scraping.Services;
using PriceRadar.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;





HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new ArgumentException("Connection String Cannot Be Null");
}

builder.Services.AddDbContext<PriceRadarDbContext>(options => 
options.UseNpgsql(connectionString));

builder.Services.AddScoped<PriceCheckJob>();
builder.Services.AddScoped<HBScraper>();
builder.Services.AddScoped<TYScraper>();

builder.Services.AddHttpClient<TelegramNotifier>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Logging.AddFilter(
    "System.Net.Http.HttpClient.TelegramNotifier",
    LogLevel.None);

using IHost app = builder.Build();


await using AsyncServiceScope scope = app.Services.CreateAsyncScope();

PriceCheckJob priceCheckJob =
    scope.ServiceProvider.GetRequiredService<PriceCheckJob>();


await priceCheckJob.RunAsync(); 
