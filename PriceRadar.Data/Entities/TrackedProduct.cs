namespace PriceRadar.Data.Entities;

public class TrackedProduct
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SellerName { get; set; }
    public string Url { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public bool IsActive { get; set; } = false;
    public DateTime? LastCheckedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

}




