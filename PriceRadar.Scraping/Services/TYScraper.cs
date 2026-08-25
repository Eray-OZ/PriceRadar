using PriceRadar.Scraping.Models;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PriceRadar.Scraping.Services;

public class TYScraper
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







        ILocator priceContainer = page.Locator("div.price-wrapper").First;

        await priceContainer.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });


        ILocator typlusOriginalPrice =
            priceContainer
                .Locator("div.ty-plus-price-original-price")
                .First;



        ILocator defaultPrice =
            priceContainer
                .Locator("span.discounted")
                .First;


        bool typlusVisible = false;


        try
        {
            await typlusOriginalPrice.WaitForAsync(
                new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });

            typlusVisible = true;
        }
        catch (TimeoutException)
        {
            Console.WriteLine(
            $"[SCRAPER DIAGNOSTIC] {name}: checkout-price did not become " +
            "visible within 10000ms. Default-price will be used if available.");
        }


        string? priceString = null;
        Match? priceMatch = null;



        if (typlusVisible)
        {
            priceString = await typlusOriginalPrice.InnerTextAsync();
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
            Url = url,
            Marketplace = "Trendyol"
        };

    }

}
