using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
        
        // Configuration QuestPDF
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GeneratePdfReportAsync(ScanResult scanResult, bool brandedForAgency = false)
    {
        try
        {
            var fileName = $"rapport-rgaa-{scanResult.ScanId}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
            var filePath = Path.Combine(_reportsPath, fileName);

            // Génération du PDF avec QuestPDF
            var pdfBytes = await Task.Run(() => GeneratePdfDocument(scanResult, brandedForAgency));
            await File.WriteAllBytesAsync(filePath, pdfBytes);
            
            _logger.LogInformation("Rapport PDF généré: {FilePath}", filePath);
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

    private byte[] GeneratePdfDocument(ScanResult scanResult, bool brandedForAgency)
    {
        try 
        {
            _logger.LogInformation("Début génération PDF pour scan {ScanId}", scanResult?.ScanId);
            
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    BuildHeader(page);
                    if (scanResult != null)
                        BuildContent(page, scanResult);
                    BuildFooter(page, brandedForAgency);
                });
            });

            _logger.LogInformation("Document PDF créé, génération des bytes...");
            var pdfBytes = document.GeneratePdf();
            _logger.LogInformation("PDF généré avec succès, taille: {Size} bytes", pdfBytes.Length);
            
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la génération PDF pour scan {ScanId}", scanResult?.ScanId);
            throw;
        }
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(2, Unit.Centimetre);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontSize(12));
    }

    private static void BuildHeader(PageDescriptor page)
    {
        page.Header().Height(50).Background(Colors.Grey.Lighten3).Padding(10).Column(headerCol =>
        {
            headerCol.Item().Text("RAPPORT D'AUDIT RGAA").FontSize(18).SemiBold();
            headerCol.Item().Text($"Généré le {DateTime.UtcNow:dd/MM/yyyy à HH:mm}").FontSize(10);
        });
    }

    private static void BuildContent(PageDescriptor page, ScanResult scanResult)
    {
        page.Content().Padding(20).Column(contentCol =>
        {
            BuildScanInformationSection(contentCol, scanResult);
            BuildResultsSection(contentCol, scanResult);
            BuildIssuesSection(contentCol, scanResult);
            BuildRecommendationsSection(contentCol);
        });
    }

    private static void BuildScanInformationSection(ColumnDescriptor contentCol, ScanResult scanResult)
    {
        contentCol.Item().PaddingBottom(10).Text("Informations du scan").FontSize(16).SemiBold();
        
        contentCol.Item().PaddingBottom(5).Text($"Identifiant: {scanResult?.ScanId ?? "N/A"}");
        contentCol.Item().PaddingBottom(5).Text($"Site web: {scanResult?.Website?.Url ?? "N/A"}");
        contentCol.Item().PaddingBottom(20).Text($"Date: {scanResult?.CompletedAt?.ToString("dd/MM/yyyy HH:mm") ?? "En cours"}");
    }

    private static void BuildResultsSection(ColumnDescriptor contentCol, ScanResult scanResult)
    {
        contentCol.Item().PaddingBottom(10).Text("Résultats de l'audit").FontSize(16).SemiBold();
        
        contentCol.Item().PaddingBottom(5).Text($"Score global: {scanResult?.Score ?? 0}/100").FontSize(14).SemiBold();
        contentCol.Item().PaddingBottom(5).Text($"Grade obtenu: {scanResult?.Grade.ToString() ?? "N/A"}");
        contentCol.Item().PaddingBottom(20).Text($"Pages analysées: {scanResult?.PagesScanned ?? 0}");
    }

    private static void BuildIssuesSection(ColumnDescriptor contentCol, ScanResult scanResult)
    {
        contentCol.Item().PaddingBottom(10).Text("Problèmes détectés").FontSize(16).SemiBold();
        
        contentCol.Item().PaddingBottom(3).Text($"• Critiques: {scanResult?.CriticalIssues ?? 0}");
        contentCol.Item().PaddingBottom(3).Text($"• Avertissements: {scanResult?.WarningIssues ?? 0}");
        contentCol.Item().PaddingBottom(3).Text($"• Informatifs: {scanResult?.InfoIssues ?? 0}");
        contentCol.Item().PaddingBottom(20).Text($"• Total: {scanResult?.TotalIssues ?? 0}").SemiBold();
    }

    private static void BuildRecommendationsSection(ColumnDescriptor contentCol)
    {
        contentCol.Item().PaddingBottom(10).Text("Prochaines étapes").FontSize(16).SemiBold();
        
        contentCol.Item().PaddingBottom(3).Text("1. Corriger en priorité les problèmes critiques");
        contentCol.Item().PaddingBottom(3).Text("2. Effectuer des tests avec des technologies d'assistance");
        contentCol.Item().PaddingBottom(3).Text("3. Former l'équipe aux bonnes pratiques RGAA");
        contentCol.Item().Text("4. Programmer des audits réguliers");
    }

    private static void BuildFooter(PageDescriptor page, bool brandedForAgency)
    {
        page.Footer().Height(30).Background(Colors.Grey.Lighten4).Padding(10).AlignCenter().Text(
            brandedForAgency ? 
            "Rapport généré par votre agence avec ComplianceScannerPro" : 
            "Rapport généré par ComplianceScannerPro"
        ).FontSize(9);
    }
    
    
    private static string GetStatusText(Shared.Enums.ScanStatus status)
    {
        return status switch
        {
            Shared.Enums.ScanStatus.Completed => "✅ Terminé",
            Shared.Enums.ScanStatus.Running => "⏳ En cours",
            Shared.Enums.ScanStatus.Failed => "❌ Échec",
            Shared.Enums.ScanStatus.Cancelled => "⏹️ Annulé",
            _ => "⏳ En attente"
        };
    }
    
    private static string GetComplianceText(int score)
    {
        return score switch
        {
            >= 80 => "✅ Excellent niveau de conformité RGAA. Le site respecte la majorité des critères d'accessibilité et offre une bonne expérience aux utilisateurs en situation de handicap.",
            >= 60 => "⚠️ Niveau de conformité partiel. Des améliorations sont nécessaires pour atteindre un niveau satisfaisant d'accessibilité RGAA.",
            >= 40 => "❌ Niveau de conformité insuffisant. Des corrections importantes sont requises pour respecter les standards d'accessibilité.",
            _ => "🚨 Niveau de conformité très faible. Une refonte majeure de l'accessibilité est nécessaire pour se mettre en conformité avec le RGAA."
        };
    }
}