namespace PriceRadar.Data.Entities;

public class PriceHistory
{
    public int Id { get; set; }
    public int TrackedProductId { get; set; }
    public TrackedProduct? TrackedProduct { get; set; }
    public decimal Price { get; set; }
    public DateTime CheckedAt { get; set; }

}
