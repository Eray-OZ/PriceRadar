using Microsoft.AspNetCore.Mvc;
using PriceRadar.Web.Models;
using PriceRadar.Scraping.Services;
using PriceRadar.Data.Context;
using PriceRadar.Data.Entities;
using PriceRadar.Scraping.Models;
using Microsoft.EntityFrameworkCore;

namespace PriceRadar.Web.Controllers;

public class ProductsController : Controller
{

    private readonly HBScraper _HBScraper;
    private readonly TYScraper _TYScraper;
    private readonly PriceRadarDbContext _context;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        HBScraper hbscraper,
        TYScraper tyscraper,
        PriceRadarDbContext context,
        ILogger<ProductsController> logger)
    {
        _HBScraper = hbscraper;
        _TYScraper = tyscraper;
        _context = context;
        _logger = logger;
    }



    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var trackedProducts = await _context.TrackedProducts.ToListAsync();
        return View(trackedProducts);
    }


    [HttpGet]
    public IActionResult Add()
    {
        return View(new AddProductViewModel
        {
            Marketplace = "Hepsiburada"
        });
    }


    [HttpPost]
    public async Task<IActionResult> Add(AddProductViewModel productUrl)
    {
        if (!ModelState.IsValid)
        {
            return View(productUrl);
        }

        ScrapedProduct scrapedProduct;
        try
        {
            _logger.LogInformation(
                "Starting scrape. Marketplace={Marketplace}; Url={Url}",
                productUrl.Marketplace,
                productUrl.Url);

            if (productUrl.Marketplace == "Trendyol")
            {
                scrapedProduct =
                    await _TYScraper.ScrapeProductAsync(productUrl.Url);
            }
            else if (productUrl.Marketplace == "Hepsiburada")
            {
                scrapedProduct =
                    await _HBScraper.ScrapeProductAsync(productUrl.Url);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported marketplace: {productUrl.Marketplace}");
            }

            _logger.LogInformation(
                "Scrape completed. Marketplace={Marketplace}; ProductName={ProductName}; Price={Price}",
                productUrl.Marketplace,
                scrapedProduct.ProductName,
                scrapedProduct.Price);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Scraping failed. Marketplace={Marketplace}; Url={Url}",
                productUrl.Marketplace,
                productUrl.Url);

            ModelState.AddModelError(
                string.Empty,
                "The product could not be scraped. Check the URL and try again.");

            return View(productUrl);
        }

        await _context.TrackedProducts.AddAsync(new TrackedProduct
        {
            ProductName = scrapedProduct.ProductName,
            CurrentPrice = scrapedProduct.Price,
            Url = productUrl.Url,
            Marketplace = productUrl.Marketplace,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    public async Task<IActionResult> DisableTracking(int Id)
    {
        TrackedProduct? trackedProduct = await _context.TrackedProducts.FindAsync(Id);

        if (trackedProduct is null)
        {
            return NotFound();
        }

        trackedProduct.IsActive = false;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }



}
