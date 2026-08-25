using HBTracker.Scraping.Models;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HBTracker.Scraping.Services;

public class HBScraper
{
    private static readonly Regex PricePattern = new(
        @"\d+(?:\.\d{3})*(?:,\d{2})?\s*TL",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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






        ILocator priceContainer = page.Locator(
            "[data-test-id='price'], [data-test-id='non-premium-price']")
            .First;

        await priceContainer.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

        ILocator checkoutPrice =
            priceContainer
                .Locator("[data-test-id='checkout-price']")
                .First;

        ILocator defaultPrice =
            priceContainer
                .Locator("[data-test-id='default-price']")
                .First;

        string? priceString = null;
        Match? priceMatch = null;

        if (await checkoutPrice.CountAsync() > 0
            && await checkoutPrice.IsVisibleAsync())
        {
            priceString = await checkoutPrice.InnerTextAsync();
            priceMatch = PricePattern.Match(priceString);
        }

        if (priceMatch is null || !priceMatch.Success)
        {
            await defaultPrice.WaitForAsync(
                new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

            priceString = await defaultPrice.InnerTextAsync();
            priceMatch = PricePattern.Match(priceString);
        }

        if (priceString is null
            || priceMatch is null
            || !priceMatch.Success)
        {
            throw new FormatException(
                $"Could not find a valid price in the current price section: {priceString}");
        }

        string cleanPrice =
            priceMatch.Value
                .Replace("TL", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

        decimal priceDecimal =
            decimal.Parse(
                cleanPrice,
                CultureInfo.GetCultureInfo("tr-TR"));



        return new ScrapedProduct
        {
            ProductName = name,
            Price = priceDecimal,
            Url = url
        };

    }

}
