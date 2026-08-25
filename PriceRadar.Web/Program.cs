using PriceRadar.Data.Context;
using Microsoft.EntityFrameworkCore;
using PriceRadar.Scraping.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new ArgumentException("Connection String Cannot Be Null");
}
builder.Services.AddDbContext<PriceRadarDbContext>(options => 
options.UseNpgsql(connectionString));


builder.Services.AddScoped<HBScraper>();
builder.Services.AddScoped<TYScraper>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Products}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
