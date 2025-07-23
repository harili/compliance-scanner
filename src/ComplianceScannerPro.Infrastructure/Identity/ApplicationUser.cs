using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Entities;

namespace ComplianceScannerPro.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; }
    public bool IsAgency { get; set; }
    public bool IsAdmin { get; set; }
    public string? AgencyLogo { get; set; }
    
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    
    public ICollection<Website> Websites { get; set; } = new List<Website>();
    public ICollection<ScanResult> ScanResults { get; set; } = new List<ScanResult>();
}