using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PriceRadar.Data.Context;
using PriceRadar.Data.Entities;
using PriceRadar.Web.Models;

namespace PriceRadar.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly PriceRadarDbContext _context;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public AccountController(
        PriceRadarDbContext context,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string userName = model.UserName.Trim().ToLowerInvariant();

        if (await _context.ApplicationUsers
                .AnyAsync(user => user.UserName == userName))
        {
            ModelState.AddModelError(
                nameof(model.UserName),
                "This username is already in use.");

            return View(model);
        }

        ApplicationUser user = new()
        {
            Name = model.Name.Trim(),
            UserName = userName,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            model.Password);

        await _context.ApplicationUsers.AddAsync(user);
        await _context.SaveChangesAsync();
        await SignInAsync(user, isPersistent: false);

        return RedirectToAction("Index", "Products");
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string userName = model.UserName.Trim().ToLowerInvariant();
        ApplicationUser? user = await _context.ApplicationUsers
            .SingleOrDefaultAsync(account => account.UserName == userName);

        if (user is null
            || _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                model.Password) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(
                string.Empty,
                "The username or password is incorrect.");

            return View(model);
        }

        await SignInAsync(user, model.RememberMe);

        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Products");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Login));
    }

    private async Task SignInAsync(
        ApplicationUser user,
        bool isPersistent)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim(ClaimTypes.GivenName, user.Name)
        ];

        ClaimsIdentity identity = new(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = new(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = isPersistent
            });
    }
}
