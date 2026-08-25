using PriceRadar.Data.Context;
using PriceRadar.Data.Entities;
using PriceRadar.Scraping.Models;
using PriceRadar.Scraping.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PriceRadar.Worker.Services;

public class PriceCheckJob
{

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

            if (string.Equals(
                    marketplace,
                    "Trendyol",
                    StringComparison.OrdinalIgnoreCase))
            {
                scrapedProduct =
                    await _TYScraper.ScrapeProductAsync(product.Url);
            }
            else if (string.Equals(
                         marketplace,
                         "Hepsiburada",
                         StringComparison.OrdinalIgnoreCase))
            {
                scrapedProduct =
                    await _HBScraper.ScrapeProductAsync(product.Url);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported marketplace: '{product.Marketplace}'");
            }
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

            string message = $@"
            Product: {scrapedProduct.ProductName}
            Old Price: {product.CurrentPrice}
            New Price: {scrapedProduct.Price}
            Url: {scrapedProduct.Url}          
            ";

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


}
