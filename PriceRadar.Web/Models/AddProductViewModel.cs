using System.ComponentModel.DataAnnotations;


namespace PriceRadar.Web.Models;

public class AddProductViewModel
{
    [RegularExpression(
       @"^https:\/\/(www\.)?hepsiburada\.com\/[a-zA-Z0-9\-_./]+",
       ErrorMessage = "Please enter a valid 'hepsiburada.com' link."
   )]
    public string Url { get; set; } = string.Empty;
}
