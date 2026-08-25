using PriceRadar.Data.Context;
using PriceRadar.Scraping.Services;
using PriceRadar.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;





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

builder.Services.AddHttpClient<TelegramNotifier>();

using IHost app = builder.Build();


using IServiceScope scope = app.Services.CreateScope();

PriceCheckJob priceCheckJob =
    scope.ServiceProvider.GetRequiredService<PriceCheckJob>();


await priceCheckJob.RunAsync(); 

