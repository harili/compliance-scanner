using ComplianceScannerPro.Core.Entities;

namespace ComplianceScannerPro.Core.Interfaces;

public interface IReportGenerator
{
    Task<string> GeneratePdfReportAsync(ScanResult scanResult, bool brandedForAgency = false);
    Task<byte[]> GetReportBytesAsync(string reportPath);
}