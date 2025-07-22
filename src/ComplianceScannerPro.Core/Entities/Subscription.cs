namespace ComplianceScannerPro.Core.Entities;

public class Subscription
{
    public int Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int MaxWebsites { get; set; }
    public int MaxScansPerMonth { get; set; }
    public bool ApiAccess { get; set; }
    public bool BrandedReports { get; set; }
    public bool PrioritySupport { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? StripeSubscriptionId { get; set; }
    public string? StripePriceId { get; set; }
    public string? StripeCustomerId { get; set; }
    public ComplianceScannerPro.Shared.Enums.SubscriptionStatus Status { get; set; } = ComplianceScannerPro.Shared.Enums.SubscriptionStatus.Inactive;
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public DateTime? CanceledAt { get; set; }
    
    public string? UserId { get; set; }
}