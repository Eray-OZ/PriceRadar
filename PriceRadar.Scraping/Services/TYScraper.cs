using PriceRadar.Scraping.Models;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PriceRadar.Scraping.Services;

public class TYScraper
{

    public async Task<ScrapedProduct> ScrapeProductAsync(string url)
    {
        using IPlaywright playwright =
            await Playwright.CreateAsync();

        await using IBrowser browser =
            await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Channel = "chrome",
                    Args = new[] {
                    "--disable-blink-features=AutomationControlled",
                }
                });


        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "tr-TR",
            TimezoneId = "Europe/Istanbul"
        });




        IPage page = await context.NewPageAsync();

        await page.GotoAsync(
            url,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });



        ILocator productHeading = page.Locator("h1").First;


        await productHeading.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });
        var name = await productHeading.InnerTextAsync();







        ILocator priceContainer = page.Locator("div.price-wrapper").First;

        await priceContainer.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });


        ILocator defaultPrice =
            priceContainer
                .Locator("span.discounted")
                .First;



        return new ScrapedProduct
        {
            ProductName = name,
            Price = 10,
            Url = url,
            Marketplace = "Trendyol"
        };

    }

}
