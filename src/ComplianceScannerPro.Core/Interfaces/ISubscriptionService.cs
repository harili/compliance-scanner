using ComplianceScannerPro.Core.Entities;

namespace ComplianceScannerPro.Core.Interfaces;

public interface ISubscriptionService
{
    Task<bool> CanUserAddWebsiteAsync(string userId);
    Task<bool> CanUserStartScanAsync(string userId);
    Task<Subscription?> GetUserSubscriptionAsync(string userId);
    Task<List<Subscription>> GetAvailablePlansAsync();
    Task<bool> UpgradeSubscriptionAsync(string userId, string stripePriceId);
    Task<bool> HasApiAccessAsync(string userId);
    Task<bool> HasBrandedReportsAsync(string userId);
    Task<(int used, int limit)> GetWebsiteUsageAsync(string userId);
    Task<(int used, int limit)> GetScanUsageAsync(string userId);
}