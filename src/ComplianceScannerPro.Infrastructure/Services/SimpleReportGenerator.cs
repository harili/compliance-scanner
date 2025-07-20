using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using System.Text;

namespace ComplianceScannerPro.Infrastructure.Services;

public class SimpleReportGenerator : IReportGenerator
{
    private readonly ILogger<SimpleReportGenerator> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _reportsPath;

    public SimpleReportGenerator(ILogger<SimpleReportGenerator> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _reportsPath = configuration["ScanSettings:ReportsStoragePath"] ?? "./storage/reports";
        
        Directory.CreateDirectory(_reportsPath);
    }

    public async Task<string> GeneratePdfReportAsync(ScanResult scanResult, bool brandedForAgency = false)
    {
        try
        {
            var fileName = $"rapport-rgaa-{scanResult.ScanId}-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
            var filePath = Path.Combine(_reportsPath, fileName);

            var report = GenerateTextReport(scanResult, brandedForAgency);
            await File.WriteAllTextAsync(filePath, report, Encoding.UTF8);
            
            _logger.LogInformation("Rapport texte généré: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la génération du rapport pour {ScanId}", scanResult.ScanId);
            throw;
        }
    }

    public async Task<byte[]> GetReportBytesAsync(string reportPath)
    {
        if (!File.Exists(reportPath))
            throw new FileNotFoundException($"Le rapport {reportPath} n'existe pas.");

        return await File.ReadAllBytesAsync(reportPath);
    }

    private string GenerateTextReport(ScanResult scanResult, bool brandedForAgency)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("=== RAPPORT D'AUDIT RGAA ===");
        sb.AppendLine($"Scan ID: {scanResult.ScanId}");
        sb.AppendLine($"Date: {scanResult.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En cours"}");
        sb.AppendLine($"Site: {scanResult.Website?.Url ?? "N/A"}");
        sb.AppendLine();
        
        sb.AppendLine("=== RÉSULTATS ===");
        sb.AppendLine($"Score: {scanResult.Score}/100");
        sb.AppendLine($"Grade: {scanResult.Grade}");
        sb.AppendLine($"Pages analysées: {scanResult.PagesScanned}");
        sb.AppendLine();
        
        sb.AppendLine("=== PROBLÈMES DÉTECTÉS ===");
        sb.AppendLine($"Total: {scanResult.TotalIssues}");
        sb.AppendLine($"Critiques: {scanResult.CriticalIssues}");
        sb.AppendLine($"Avertissements: {scanResult.WarningIssues}");
        sb.AppendLine($"Informations: {scanResult.InfoIssues}");
        sb.AppendLine();
        
        if (brandedForAgency)
        {
            sb.AppendLine("Rapport généré par votre agence avec ComplianceScannerPro");
        }
        else
        {
            sb.AppendLine("Rapport généré par ComplianceScannerPro");
        }
        
        return sb.ToString();
    }
}