using PriceRadar.Data.Context;
using PriceRadar.Data.Entities;
using PriceRadar.Scraping.Models;
using PriceRadar.Scraping.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace PriceRadar.Worker.Services;

public class PriceCheckJob
{
    private const int MaxScrapeAttempts = 3;
    private static readonly TimeSpan ScrapeRetryDelay =
        TimeSpan.FromSeconds(2);

    private readonly PriceRadarDbContext _context;
    private readonly ILogger<PriceCheckJob> _logger;
    private readonly HBScraper _HBScraper;
    private readonly TYScraper _TYScraper;
    private readonly TelegramNotifier _telegram;
    public PriceCheckJob(PriceRadarDbContext context, ILogger<PriceCheckJob> logger, HBScraper hbscraper, TYScraper tyscraper, TelegramNotifier telegram)
    {
        _context = context;
        _logger = logger;
        _HBScraper = hbscraper;
        _TYScraper = tyscraper;
        _telegram = telegram;
    }



    public async Task RunAsync()
    {
        List<TrackedProduct> products =
            await LoadActiveProductsAsync();

        _logger.LogInformation(
            "Found {Count} active tracked products.",
            products.Count);

        foreach (TrackedProduct product in products)
        {
            await CheckAndRecordPriceChangeAsync(product);
        }



    }



    private Task<List<TrackedProduct>> LoadActiveProductsAsync()
    {
        CancellationToken cancellationToken = default;
        var products = _context.TrackedProducts
        .Where(p => p.IsActive)
        .ToListAsync(cancellationToken);
        return products;
    }


    private async Task CheckAndRecordPriceChangeAsync(TrackedProduct product)
    {
        ScrapedProduct scrapedProduct;
        string marketplace = product.Marketplace?.Trim() ?? string.Empty;

        _logger.LogInformation(
            "[PRICE CHECK START] ProductId={ProductId}; Marketplace='{Marketplace}'; " +
            "StoredPrice={StoredPrice}; ProductName='{ProductName}'; Url={Url}",
            product.Id,
            marketplace,
            product.CurrentPrice,
            product.ProductName,
            product.Url);

        try
        {
            scrapedProduct =
                await ScrapeProductWithRetryAsync(product, marketplace);
        }

        catch (TimeoutException ex)
        {
            _logger.LogWarning(
                ex,
                "[PRICE CHECK SKIPPED] Timeout; ProductId={ProductId}; " +
                "Marketplace='{Marketplace}'; ProductName='{ProductName}'; Url={Url}",
                product.Id,
                marketplace,
                product.ProductName,
                product.Url);

            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[PRICE CHECK SKIPPED] Scraping error; ProductId={ProductId}; " +
                "Marketplace='{Marketplace}'; ProductName='{ProductName}'; Url={Url}",
                product.Id,
                marketplace,
                product.ProductName,
                product.Url);

            return;
        }

        _logger.LogInformation(
            "[PRICE CHECK SCRAPED] ProductId={ProductId}; Marketplace='{Marketplace}'; " +
            "StoredPrice={StoredPrice}; ScrapedPrice={ScrapedPrice}; " +
            "ScrapedName='{ScrapedName}'; Url={Url}",
            product.Id,
            marketplace,
            product.CurrentPrice,
            scrapedProduct.Price,
            scrapedProduct.ProductName,
            scrapedProduct.Url);

        DateTime t = DateTime.UtcNow;
        decimal oldPrice = product.CurrentPrice;

        if (scrapedProduct.Price != oldPrice)
        {
            CultureInfo turkishCulture =
                CultureInfo.GetCultureInfo("tr-TR");

            string oldPriceText = oldPrice.ToString("N2", turkishCulture);
            string newPriceText =
                scrapedProduct.Price.ToString("N2", turkishCulture);

            string message =
                $"📉 Fiyat Değişikliği\n\n" +
                $"Ürün: {scrapedProduct.ProductName}\n" +
                $"Pazaryeri: {marketplace}\n" +
                $"Eski fiyat: {oldPriceText} TL\n" +
                $"Yeni fiyat: {newPriceText} TL\n\n" +
                $"Ürünü görüntüle: {scrapedProduct.Url}";

            await _context.PriceHistories.AddAsync(
                new PriceHistory
                {
                    TrackedProductId = product.Id,
                    Price = scrapedProduct.Price,
                    CheckedAt = t
                }
            );
            product.CurrentPrice = scrapedProduct.Price;
            product.LastCheckedAt = t;
            await _context.SaveChangesAsync();
            await _telegram.SendPriceChangeNotificationAsync(message);

            _logger.LogInformation(
                "[PRICE CHECK CHANGED] ProductId={ProductId}; OldPrice={OldPrice}; " +
                "NewPrice={NewPrice}; PriceHistorySaved=true; TelegramRequested=true",
                product.Id,
                oldPrice,
                scrapedProduct.Price);
        }
        else
        {
            product.LastCheckedAt = t;
            _logger.LogInformation(
                "[PRICE CHECK UNCHANGED] ProductId={ProductId}; ProductName='{ProductName}'; " +
                "StoredPrice={StoredPrice}; ScrapedPrice={ScrapedPrice}",
                product.Id,
                product.ProductName,
                product.CurrentPrice,
                scrapedProduct.Price);
            await _context.SaveChangesAsync();

        }

    }

    private async Task<ScrapedProduct> ScrapeProductWithRetryAsync(
        TrackedProduct product,
        string marketplace)
    {
        bool isTrendyol = string.Equals(
            marketplace,
            "Trendyol",
            StringComparison.OrdinalIgnoreCase);

        bool isHepsiburada = string.Equals(
            marketplace,
            "Hepsiburada",
            StringComparison.OrdinalIgnoreCase);

        if (!isTrendyol && !isHepsiburada)
        {
            throw new InvalidOperationException(
                $"Unsupported marketplace: '{product.Marketplace}'");
        }

        for (int attempt = 1; attempt <= MaxScrapeAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "[SCRAPE ATTEMPT] ProductId={ProductId}; Attempt={Attempt}/{MaxAttempts}; " +
                    "Marketplace='{Marketplace}'; Url={Url}",
                    product.Id,
                    attempt,
                    MaxScrapeAttempts,
                    marketplace,
                    product.Url);

                if (isTrendyol)
                {
                    return await _TYScraper.ScrapeProductAsync(product.Url);
                }

                return await _HBScraper.ScrapeProductAsync(product.Url);
            }
            catch (Exception ex) when (attempt < MaxScrapeAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "[SCRAPE RETRY] ProductId={ProductId}; Attempt={Attempt}/{MaxAttempts} " +
                    "failed. Retrying in {RetryDelaySeconds} seconds.",
                    product.Id,
                    attempt,
                    MaxScrapeAttempts,
                    ScrapeRetryDelay.TotalSeconds);

                await Task.Delay(ScrapeRetryDelay);
            }
        }

        throw new InvalidOperationException(
            $"Scraping failed after {MaxScrapeAttempts} attempts for product {product.Id}.");
    }

}
