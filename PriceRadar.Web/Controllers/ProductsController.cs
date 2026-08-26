using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PriceRadar.Web.Models;
using PriceRadar.Data.Context;
using PriceRadar.Data.Entities;
using PriceRadar.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace PriceRadar.Web.Controllers;

[Authorize]
public class ProductsController : Controller
{

    private readonly PriceRadarDbContext _context;
    private readonly ILogger<ProductsController> _logger;
    private readonly GitHubActionsDispatcher _actionsDispatcher;

    public ProductsController(
        PriceRadarDbContext context,
        ILogger<ProductsController> logger,
        GitHubActionsDispatcher actionsDispatcher)
    {
        _context = context;
        _logger = logger;
        _actionsDispatcher = actionsDispatcher;
    }



    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        var trackedProducts = await _context.TrackedProducts
            .Where(product => product.UserId == userId)
            .ToListAsync();

        return View(trackedProducts);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int Id)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        TrackedProduct? trackedProduct = await _context.TrackedProducts
            .AsNoTracking()
            .Include(product => product.PriceHistories)
            .SingleOrDefaultAsync(product =>
                product.Id == Id && product.UserId == userId);

        if (trackedProduct is null)
        {
            return NotFound();
        }

        return View(trackedProduct);
    }


    [HttpGet]
    public async Task<IActionResult> Status(int Id)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Unauthorized();
        }

        TrackedProduct? trackedProduct = await _context.TrackedProducts
            .AsNoTracking()
            .SingleOrDefaultAsync(product =>
                product.Id == Id && product.UserId == userId);

        if (trackedProduct is null)
        {
            return NotFound();
        }

        return Json(new
        {
            isReady = trackedProduct.CurrentPrice.HasValue,
            initialScrapeFailed = !trackedProduct.CurrentPrice.HasValue
                && trackedProduct.InitialScrapeFailed,
            productName = trackedProduct.ProductName,
            currentPrice = trackedProduct.CurrentPrice.HasValue
                ? trackedProduct.CurrentPrice.Value.ToString(
                    "N2",
                    System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))
                : null,
            lastChecked = trackedProduct.LastCheckedAt.HasValue
                ? trackedProduct.LastCheckedAt.Value.ToLocalTime().ToString("dd MMM HH:mm")
                : "—"
        });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryInitialScrape(int Id)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        TrackedProduct? trackedProduct = await _context.TrackedProducts
            .SingleOrDefaultAsync(product =>
                product.Id == Id && product.UserId == userId);

        if (trackedProduct is null)
        {
            return NotFound();
        }

        if (trackedProduct.CurrentPrice.HasValue)
        {
            return RedirectToAction(nameof(Index));
        }

        trackedProduct.InitialScrapeFailed = false;
        trackedProduct.LastCheckedAt = null;
        trackedProduct.IsActive = true;

        await _context.SaveChangesAsync();

        bool workflowDispatched =
            await _actionsDispatcher.TryDispatchPriceCheckAsync();

        TempData["AddProductMessage"] = workflowDispatched
            ? "Initial price check started again."
            : "Retry scheduled for the next price check.";

        return RedirectToAction(nameof(Index));
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddProductViewModel productUrl)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(productUrl);
        }

        string marketplace = productUrl.Marketplace.Trim();

        if (marketplace != "Hepsiburada"
            && marketplace != "Trendyol")
        {
            ModelState.AddModelError(
                string.Empty,
                "Please choose a supported marketplace.");

            return View(productUrl);
        }

        string normalizedUrl = productUrl.Url.Trim().TrimEnd('/');
        bool duplicateExists = await _context.TrackedProducts
            .AnyAsync(product =>
                product.UserId == userId
                && product.Marketplace == marketplace
                && product.Url == normalizedUrl);

        if (duplicateExists)
        {
            ModelState.AddModelError(
                nameof(productUrl.Url),
                "This product is already being tracked. Resume it from your product list if it is paused.");

            return View(productUrl);
        }

        TrackedProduct trackedProduct = new()
        {
            ProductName = null,
            CurrentPrice = null,
            Url = normalizedUrl,
            Marketplace = marketplace,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        await _context.TrackedProducts.AddAsync(trackedProduct);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Saved product as pending initial scrape. ProductId={ProductId}; " +
            "Marketplace={Marketplace}; Url={Url}",
            trackedProduct.Id,
            trackedProduct.Marketplace,
            trackedProduct.Url);

        bool workflowDispatched =
            await _actionsDispatcher.TryDispatchPriceCheckAsync();

        TempData["AddProductMessage"] = workflowDispatched
            ? "Product added. Initial price check started."
            : "Product added. Initial price check will run with the next scheduled check.";

        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableTracking(int Id)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        TrackedProduct? trackedProduct = await _context.TrackedProducts
            .SingleOrDefaultAsync(product =>
                product.Id == Id && product.UserId == userId);

        if (trackedProduct is null)
        {
            return NotFound();
        }

        trackedProduct.IsActive = false;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeTracking(int Id)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        TrackedProduct? trackedProduct = await _context.TrackedProducts
            .SingleOrDefaultAsync(product =>
                product.Id == Id && product.UserId == userId);

        if (trackedProduct is null)
        {
            return NotFound();
        }

        trackedProduct.IsActive = true;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int Id)
    {
        if (!TryGetCurrentUserId(out int userId))
        {
            return Challenge();
        }

        TrackedProduct? trackedProduct = await _context.TrackedProducts
            .SingleOrDefaultAsync(product =>
                product.Id == Id && product.UserId == userId);
        if (trackedProduct is null)
        {
            return NotFound();
        }

        _context.TrackedProducts.Remove(trackedProduct);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));

    }

    private bool TryGetCurrentUserId(out int userId)
    {
        string? userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdValue, out userId);
    }


}
