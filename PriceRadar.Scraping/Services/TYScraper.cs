using PriceRadar.Scraping.Models;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PriceRadar.Scraping.Services;

public class TYScraper : IAsyncDisposable
{
    private static readonly Regex PricePattern = new(
        @"\d+(?:\.\d{3})*(?:,\d{2})?\s*TL",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    public async Task<ScrapedProduct> ScrapeProductAsync(string url)
    {
        await EnsureBrowserContextAsync();

        IPage page = await _context!.NewPageAsync();

        try
        {
            return await ScrapeProductPageAsync(page, url);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    private async Task<ScrapedProduct> ScrapeProductPageAsync(
        IPage page,
        string url)
    {
        Console.WriteLine(
            $"[TRENDYOL FLOW] Requested product URL: {url}");

        await page.GotoAsync(
            url,
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            });

        Console.WriteLine(
            $"[TRENDYOL FLOW] Initial navigation completed. Page URL: {page.Url}");

        await SelectTurkeyIfRequiredAsync(page, url);

        Console.WriteLine(
            $"[TRENDYOL FLOW] Country step completed. Page URL: {page.Url}");



        ILocator productHeading = page.Locator("h1").First;


        try
        {
            await productHeading.WaitForAsync(
                new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 30000
                });
        }
        catch (TimeoutException)
        {
            await SaveTrendyolDiagnosticAsync(page, "product-page");
            throw;
        }
        var name = await productHeading.InnerTextAsync();





        ILocator productSeller = page.Locator("[data-testid='buyBox-seller']").First;

        await productSeller.WaitForAsync(
        new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 10000
        });
        var sellerName = await productSeller.InnerTextAsync();




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


        (string priceString, string selectedSelector) =
            await FindPriceTextAsync(page, priceContainer);
        Match priceMatch = PricePattern.Match(priceString);

        Console.WriteLine(
            $"[TRENDYOL DIAGNOSTIC] {name}: selected selector='{selectedSelector}'; " +
            $"Raw text: {priceString}");

        string cleanPrice =
            priceMatch.Value
                .Replace("TL", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

        decimal priceDecimal =
            decimal.Parse(
                cleanPrice,
                CultureInfo.GetCultureInfo("tr-TR"));

        Console.WriteLine(
            $"[TRENDYOL DIAGNOSTIC] {name}: parsed price={priceDecimal}; URL={url}");



        return new ScrapedProduct
        {
            ProductName = name,
            Price = priceDecimal,
            Url = url,
            Marketplace = "Trendyol",
            SellerName = sellerName
        };

    }

    private static async Task<(string PriceText, string Selector)> FindPriceTextAsync(
        IPage page,
        ILocator priceContainer)
    {
        string[] priceSelectors =
        {
            ".ty-plus-price-original-price",
            ".campaign-price .new-price",
            "span.discounted",
            ".new-price"
        };

        DateTime deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            foreach (string selector in priceSelectors)
            {
                ILocator candidate = priceContainer.Locator(selector).First;

                if (await candidate.CountAsync() == 0
                    || !await candidate.IsVisibleAsync())
                {
                    continue;
                }

                string candidateText = await candidate.InnerTextAsync();

                if (PricePattern.IsMatch(candidateText))
                {
                    return (candidateText, selector);
                }
            }

            await page.WaitForTimeoutAsync(250);
        }

        throw new TimeoutException(
            "Could not find a visible current price in the Trendyol price section.");
    }

    private async Task EnsureBrowserContextAsync()
    {
        if (_context is not null)
        {
            return;
        }

        _playwright = await Playwright.CreateAsync();

        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = true,
                Channel = "chrome",
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled"
                }
            });

        _context = await _browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                UserAgent =
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
                    "AppleWebKit/537.36 (KHTML, like Gecko) " +
                    "Chrome/125.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize
                {
                    Width = 1920,
                    Height = 1080
                },
                Locale = "tr-TR",
                TimezoneId = "Europe/Istanbul"
            });
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

    private static async Task SelectTurkeyIfRequiredAsync(
        IPage page,
        string productUrl)
    {
        if (!page.Url.Contains(
                "/select-country",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Console.WriteLine(
            $"[TRENDYOL COUNTRY] Country page detected. " +
            $"Requested product URL: {productUrl}; Current URL: {page.Url}");

        ILocator countrySelect =
            page.Locator("select[data-testid='country-select']");

        await countrySelect.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

        await countrySelect.SelectOptionAsync(
            new SelectOptionValue { Value = "Türkiye" });

        string selectedCountry = await countrySelect.InputValueAsync();

        ILocator selectButton =
            page.Locator("button[data-testid='country-select-btn-desktop']");

        await selectButton.WaitForAsync(
            new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

        string? disabledAttribute =
            await selectButton.GetAttributeAsync("disabled");
        string? buttonClass =
            await selectButton.GetAttributeAsync("class");
        IReadOnlyList<string> visibleButtonTexts =
            await page.Locator("button:visible").AllInnerTextsAsync();

        Console.WriteLine(
            $"[TRENDYOL COUNTRY] Selection applied. " +
            $"SelectedValue='{selectedCountry}'; " +
            $"ButtonEnabled={await selectButton.IsEnabledAsync()}; " +
            $"ButtonDisabledAttribute='{disabledAttribute}'; " +
            $"ButtonClass='{buttonClass}'; " +
            $"VisibleButtons='{string.Join(" | ", visibleButtonTexts)}'");

        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (await selectButton.IsEnabledAsync())
            {
                break;
            }

            await page.WaitForTimeoutAsync(100);
        }

        if (!await selectButton.IsEnabledAsync())
        {
            Console.WriteLine(
                "[TRENDYOL COUNTRY] Select button stayed disabled after 10 seconds.");
            await SaveTrendyolDiagnosticAsync(page, "country-selection");
            throw new TimeoutException(
                "Trendyol country selection button did not become enabled.");
        }

        Console.WriteLine(
            "[TRENDYOL COUNTRY] Clicking enabled country selection button.");

        await selectButton.ClickAsync();

        Console.WriteLine(
            $"[TRENDYOL COUNTRY] Button click completed. Page URL immediately after click: {page.Url}");

        try
        {
            await page.GotoAsync(
                productUrl,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded
                });

            Console.WriteLine(
                $"[TRENDYOL COUNTRY] Product re-navigation completed. Final URL: {page.Url}");
        }
        catch (PlaywrightException navigationException)
        {
            Console.WriteLine(
                $"[TRENDYOL COUNTRY] Product re-navigation failed. " +
                $"Current URL: {page.Url}; Error: {navigationException.Message}");

            await SaveTrendyolDiagnosticAsync(page, "country-after-click-navigation-error");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            await _context.CloseAsync();
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }

}
