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

        ILocator visiblePriceSection =
            priceContainer
                .Locator(
                    "[data-test-id='checkout-price']:visible, " +
                    "[data-test-id='default-price']:visible")
                .First;

        try
        {
            await visiblePriceSection.WaitForAsync(
                new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
                $"[SCRAPER DIAGNOSTIC] {name}: no visible checkout-price or " +
                "default-price appeared within 10000ms.");

            throw;
        }

        int checkoutPriceCount = await checkoutPrice.CountAsync();
        bool checkoutPriceVisible = checkoutPriceCount > 0
            && await checkoutPrice.IsVisibleAsync();
        int defaultPriceCount = await defaultPrice.CountAsync();

        Console.WriteLine(
            $"[SCRAPER DIAGNOSTIC] {name}: checkout-price count={checkoutPriceCount}, " +
            $"visible={checkoutPriceVisible}; default-price count={defaultPriceCount}.");

        string? priceString = null;
        Match? priceMatch = null;

        if (checkoutPriceVisible)
        {
            priceString = await checkoutPrice.InnerTextAsync();
            priceMatch = PricePattern.Match(priceString);

            Console.WriteLine(
                $"[SCRAPER DIAGNOSTIC] {name}: selected checkout-price. " +
                $"Raw text: {priceString}");
        }

        if (priceMatch is null || !priceMatch.Success)
        {
            Console.WriteLine(
                $"[SCRAPER DIAGNOSTIC] {name}: checkout-price was unavailable or " +
                "did not contain a TL price. Falling back to default-price.");

            await defaultPrice.WaitForAsync(
                new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

            priceString = await defaultPrice.InnerTextAsync();
            priceMatch = PricePattern.Match(priceString);

            Console.WriteLine(
                $"[SCRAPER DIAGNOSTIC] {name}: selected default-price. " +
                $"Raw text: {priceString}");
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
