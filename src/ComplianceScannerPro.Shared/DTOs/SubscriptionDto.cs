namespace ComplianceScannerPro.Shared.DTOs;

public class SubscriptionDto
{
    public int Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int MaxWebsites { get; set; }
    public int MaxScansPerMonth { get; set; }
    public bool ApiAccess { get; set; }
    public bool BrandedReports { get; set; }
    public bool PrioritySupport { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? StripePriceId { get; set; }
    public string? UserId { get; set; }
}

public class UserSubscriptionInfoDto
{
    public SubscriptionDto? CurrentSubscription { get; set; }
    public int WebsitesUsed { get; set; }
    public int ScansThisMonth { get; set; }
    public bool CanAddWebsite { get; set; }
    public bool CanStartScan { get; set; }
    public List<SubscriptionDto> AvailablePlans { get; set; } = new();
}