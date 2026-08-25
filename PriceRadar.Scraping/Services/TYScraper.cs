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

        try
        {
            await priceContainer.WaitForAsync(
                new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
        }
        catch (TimeoutException)
        {
            await SaveTrendyolDiagnosticAsync(page, name);
            throw;
        }


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
        }


        string? priceString = null;
        Match? priceMatch = null;



        if (typlusVisible)
        {
            priceString = await typlusOriginalPrice.InnerTextAsync();
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
            Url = url,
            Marketplace = "Trendyol"
        };

    }

    private static async Task SaveTrendyolDiagnosticAsync(
        IPage page,
        string productName)
    {
        try
        {
            string currentUrl = page.Url;
            string pageTitle = await page.TitleAsync();
            string bodyText = await page.Locator("body").InnerTextAsync();

            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] Product: {productName}");
            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] URL: {currentUrl}");
            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] Title: {pageTitle}");
            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] Body text:\n" +
                bodyText[..Math.Min(bodyText.Length, 3000)]);

            string diagnosticDirectory =
                Path.Combine("artifacts", "trendyol");

            Directory.CreateDirectory(diagnosticDirectory);

            string diagnosticFileId = Guid.NewGuid().ToString("N");
            string screenshotPath = Path.Combine(
                diagnosticDirectory,
                $"trendyol-{diagnosticFileId}.png");
            string htmlPath = Path.Combine(
                diagnosticDirectory,
                $"trendyol-{diagnosticFileId}.html");

            await page.ScreenshotAsync(
                new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });

            string html = await page.ContentAsync();
            await File.WriteAllTextAsync(htmlPath, html);

            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] Screenshot saved: {screenshotPath}");
            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] HTML saved: {htmlPath}");
        }
        catch (Exception diagnosticException)
        {
            Console.WriteLine(
                $"[TRENDYOL DIAGNOSTIC] Could not save page diagnostics: " +
                diagnosticException.Message);
        }
    }

}
