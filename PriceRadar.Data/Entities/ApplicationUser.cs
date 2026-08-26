namespace PriceRadar.Data.Entities;

public class ApplicationUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public List<TrackedProduct> TrackedProducts { get; set; } = new();
}
