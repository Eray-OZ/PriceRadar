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
        var message = "";
        try
        {

            if (product.Marketplace == "Trendyol")
            {
                scrapedProduct =
                    await _TYScraper.ScrapeProductAsync(product.Url);
            }
            else if (product.Marketplace == "Hepsiburada")
            {
                scrapedProduct =
                    await _HBScraper.ScrapeProductAsync(product.Url);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported marketplace: {product.Marketplace}");
            }
        }

        catch (TimeoutException ex)
        {
            _logger.LogWarning(
                ex,
                "Scraping timed out for {ProductName}. Skipping this product.",
                product.ProductName);

            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected scraping error for {ProductName}. Skipping this product.",
                product.ProductName);

            return;
        }

        DateTime t = DateTime.UtcNow;

        if (scrapedProduct.Price != product.CurrentPrice)
        {

            message = $@"
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
        }
        else
        {
            product.LastCheckedAt = t;
            _logger.LogInformation("{ProductName} price unchanged.", product.ProductName);
            await _context.SaveChangesAsync();

        }

    }


}
