using ComplianceScannerPro.Core.Entities;

namespace ComplianceScannerPro.Core.Interfaces;

public interface IScanService
{
    Task<ScanResult> StartScanAsync(int websiteId, string userId);
    Task<ScanResult?> GetScanResultAsync(string scanId);
    Task<List<ScanResult>> GetUserScanHistoryAsync(string userId, int take = 10);
    Task<bool> CanUserStartScanAsync(string userId);
}