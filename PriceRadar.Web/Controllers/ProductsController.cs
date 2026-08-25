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

    private readonly HBScraper _scraper;
    private readonly PriceRadarDbContext _context;

    public ProductsController(HBScraper scraper, PriceRadarDbContext context)
    {
        _scraper = scraper;
        _context = context;
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
        return View();
    }


    [HttpPost]
    public async Task<IActionResult> Add(AddProductViewModel productUrl)
    {
        if (!ModelState.IsValid)
        {
            return View(productUrl);
        }

        ScrapedProduct scrapedProduct;
        try { scrapedProduct = await _scraper.ScrapeProductAsync(productUrl.Url); }

        catch
        {
            return View(productUrl);
        }

        await _context.TrackedProducts.AddAsync(new TrackedProduct
        {
            ProductName = scrapedProduct.ProductName,
            CurrentPrice = scrapedProduct.Price,
            Url = productUrl.Url,
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
