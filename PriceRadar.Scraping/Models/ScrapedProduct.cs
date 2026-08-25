namespace PriceRadar.Scraping.Models;

public class ScrapedProduct
{
    public string ProductName { get; set; } = string.Empty;
    public string? SellerName { get; set; }
    public string Marketplace { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
