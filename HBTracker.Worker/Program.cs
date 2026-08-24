using HBTracker.Data.Context;
using HBTracker.Scraping.Services;
using HBTracker.Worker.Services;
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

builder.Services.AddDbContext<HBTrackerDbContext>(options => 
options.UseNpgsql(connectionString));

builder.Services.AddScoped<PriceCheckJob>();
builder.Services.AddScoped<HBScraper>();

builder.Services.AddHttpClient<TelegramNotifier>();

using IHost app = builder.Build();


using IServiceScope scope = app.Services.CreateScope();

PriceCheckJob priceCheckJob =
    scope.ServiceProvider.GetRequiredService<PriceCheckJob>();


await priceCheckJob.RunAsync(); 

