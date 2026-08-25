using System.ComponentModel.DataAnnotations;


namespace PriceRadar.Web.Models;

public class AddProductViewModel
{
    [RegularExpression(
       @"^https:\/\/(www\.)?(hepsiburada\.com|trendyol\.com)\/[^\s]+$",
       ErrorMessage = "Please enter a valid Hepsiburada or Trendyol link."
    )]
    public string Url { get; set; } = string.Empty;
    public string Marketplace { get; set; } = string.Empty;
}
