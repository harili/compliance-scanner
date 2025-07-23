using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Shared.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using Colors = QuestPDF.Helpers.Colors;

namespace ComplianceScannerPro.Infrastructure.Services;

public class QuestPdfReportGenerator : IReportGenerator
{
    private readonly ILogger<QuestPdfReportGenerator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _reportsPath;

    // Configuration des couleurs et styles
    private static readonly string PrimaryColor = "#0d6efd";
    private static readonly string DangerColor = "#dc3545";
    private static readonly string WarningColor = "#ffc107";
    private static readonly string InfoColor = "#0dcaf0";
    private static readonly string SuccessColor = "#198754";
    private static readonly string LightGrayColor = "#f8f9fa";
    private static readonly string DarkGrayColor = "#6c757d";

    public QuestPdfReportGenerator(
        ILogger<QuestPdfReportGenerator> logger, 
        IConfiguration configuration,
        IUnitOfWork unitOfWork)
    {
        _logger = logger;
        _configuration = configuration;
        _unitOfWork = unitOfWork;
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

            // Récupérer les données complètes
            var website = await _unitOfWork.Websites.GetByIdAsync(scanResult.WebsiteId);
            var issues = await _unitOfWork.AccessibilityIssues.GetAllAsync(i => i.ScanResultId == scanResult.Id);
            var issuesList = issues.OrderByDescending(i => i.Severity).ThenBy(i => i.RgaaRule).ToList();

            // Génération du PDF
            var document = CreatePdfDocument(scanResult, website, issuesList, brandedForAgency);
            document.GeneratePdf(filePath);
            
            _logger.LogInformation("Rapport PDF généré: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la génération du rapport PDF pour {ScanId}", scanResult.ScanId);
            throw;
        }
    }

    public async Task<byte[]> GetReportBytesAsync(string reportPath)
    {
        if (!File.Exists(reportPath))
            throw new FileNotFoundException($"Le rapport {reportPath} n'existe pas.");

        return await File.ReadAllBytesAsync(reportPath);
    }

    private Document CreatePdfDocument(ScanResult scanResult, Website? website, List<AccessibilityIssue> issues, bool brandedForAgency)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                page.Header().Element(header => ComposeHeader(header, scanResult, website, brandedForAgency));
                page.Content().Element(content => ComposeContent(content, scanResult, website, issues));
                page.Footer().Element(footer => ComposeFooter(footer, scanResult));
            });
        });
    }

    private void ComposeHeader(IContainer container, ScanResult scanResult, Website? website, bool brandedForAgency)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                if (brandedForAgency)
                {
                    column.Item().Text("RAPPORT D'AUDIT D'ACCESSIBILITÉ").FontSize(20).Bold().FontColor(PrimaryColor);
                    column.Item().Text("Conformité RGAA 4.1").FontSize(14).FontColor(DarkGrayColor);
                }
                else
                {
                    column.Item().Text("ComplianceScannerPro").FontSize(16).Bold().FontColor(PrimaryColor);
                    column.Item().Text("Rapport d'audit d'accessibilité RGAA").FontSize(12).FontColor(DarkGrayColor);
                }
                
                column.Item().PaddingTop(10).Text($"Site web: {website?.Name ?? "Non spécifié"}").FontSize(12).Bold();
                column.Item().Text($"URL: {website?.Url ?? "N/A"}").FontSize(10).FontColor(DarkGrayColor);
            });

            row.ConstantItem(120).Column(column =>
            {
                var grade = GetGradeInfo(scanResult.Grade);
                column.Item().AlignRight().Width(80).Height(80).Background(grade.Color).Padding(5)
                    .AlignCenter().AlignMiddle().Column(gradeColumn =>
                    {
                        gradeColumn.Item().Text("SCORE").FontSize(8).Bold().FontColor(Colors.White);
                        gradeColumn.Item().Text($"{scanResult.Score}").FontSize(24).Bold().FontColor(Colors.White);
                        gradeColumn.Item().Text("/100").FontSize(10).FontColor(Colors.White);
                        gradeColumn.Item().Text($"Grade {scanResult.Grade}").FontSize(10).Bold().FontColor(Colors.White);
                    });
            });
        });

        container.PaddingTop(20).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
    }

    private void ComposeContent(IContainer container, ScanResult scanResult, Website? website, List<AccessibilityIssue> issues)
    {
        container.PaddingTop(20).Column(column =>
        {
            // Résumé exécutif
            column.Item().Element(content => ComposeExecutiveSummary(content, scanResult));
            
            // Métriques détaillées
            column.Item().PaddingTop(20).Element(content => ComposeMetrics(content, scanResult));
            
            // Répartition des problèmes
            column.Item().PaddingTop(20).Element(content => ComposeIssuesBreakdown(content, issues));
            
            // Recommandations prioritaires
            column.Item().PaddingTop(20).Element(content => ComposePriorityRecommendations(content, scanResult, issues));
            
            // Liste détaillée des problèmes
            column.Item().PageBreak();
            column.Item().Element(content => ComposeDetailedIssues(content, issues));
        });
    }

    private void ComposeExecutiveSummary(IContainer container, ScanResult scanResult)
    {
        container.Column(column =>
        {
            column.Item().Text("RÉSUMÉ EXÉCUTIF").FontSize(14).Bold().FontColor(PrimaryColor);
            column.Item().PaddingTop(10).BorderLeft(3).BorderColor(PrimaryColor).PaddingLeft(10)
                .Column(summaryColumn =>
                {
                    var gradeDescription = GetGradeDescription(scanResult.Grade);
                    var complianceLevel = GetComplianceLevel(scanResult.Score);
                    
                    summaryColumn.Item().Text($"Ce site web obtient un score de {scanResult.Score}/100 avec une note {scanResult.Grade} ({gradeDescription}).")
                        .FontSize(11).LineHeight(1.4f);
                    
                    summaryColumn.Item().PaddingTop(5)
                        .Text($"Niveau de conformité: {complianceLevel}")
                        .FontSize(11).LineHeight(1.4f);
                    
                    summaryColumn.Item().PaddingTop(5)
                        .Text($"Sur {scanResult.PagesScanned} pages analysées, {scanResult.TotalIssues} problèmes d'accessibilité ont été détectés, dont {scanResult.CriticalIssues} critiques nécessitant une correction immédiate.")
                        .FontSize(11).LineHeight(1.4f);
                    
                    if (scanResult.Score < 60)
                    {
                        summaryColumn.Item().PaddingTop(5)
                            .Text("⚠️ Actions recommandées: Correction prioritaire des problèmes critiques avant mise en production.")
                            .FontSize(11).Bold().FontColor(DangerColor);
                    }
                    else if (scanResult.Score < 80)
                    {
                        summaryColumn.Item().PaddingTop(5)
                            .Text("📋 Actions recommandées: Amélioration progressive pour atteindre une conformité totale.")
                            .FontSize(11).Bold().FontColor(WarningColor);
                    }
                    else
                    {
                        summaryColumn.Item().PaddingTop(5)
                            .Text("✅ Bon niveau de conformité. Maintenir les bonnes pratiques et corriger les problèmes restants.")
                            .FontSize(11).Bold().FontColor(SuccessColor);
                    }
                });
        });
    }

    private void ComposeMetrics(IContainer container, ScanResult scanResult)
    {
        container.Column(column =>
        {
            column.Item().Text("MÉTRIQUES DÉTAILLÉES").FontSize(14).Bold().FontColor(PrimaryColor);
            
            column.Item().PaddingTop(10).Row(row =>
            {
                // Score général
                row.RelativeItem().Background(LightGrayColor).Padding(15).Column(col =>
                {
                    col.Item().AlignCenter().Text("SCORE RGAA").FontSize(10).Bold().FontColor(DarkGrayColor);
                    col.Item().AlignCenter().Text($"{scanResult.Score}/100").FontSize(18).Bold().FontColor(PrimaryColor);
                    col.Item().AlignCenter().Text($"Grade {scanResult.Grade}").FontSize(10).FontColor(DarkGrayColor);
                });
                
                row.ConstantItem(10);
                
                // Pages analysées
                row.RelativeItem().Background(LightGrayColor).Padding(15).Column(col =>
                {
                    col.Item().AlignCenter().Text("PAGES").FontSize(10).Bold().FontColor(DarkGrayColor);
                    col.Item().AlignCenter().Text($"{scanResult.PagesScanned}").FontSize(18).Bold().FontColor(PrimaryColor);
                    col.Item().AlignCenter().Text("analysées").FontSize(10).FontColor(DarkGrayColor);
                });
                
                row.ConstantItem(10);
                
                // Total problèmes
                row.RelativeItem().Background(LightGrayColor).Padding(15).Column(col =>
                {
                    col.Item().AlignCenter().Text("PROBLÈMES").FontSize(10).Bold().FontColor(DarkGrayColor);
                    col.Item().AlignCenter().Text($"{scanResult.TotalIssues}").FontSize(18).Bold().FontColor(DangerColor);
                    col.Item().AlignCenter().Text("détectés").FontSize(10).FontColor(DarkGrayColor);
                });
            });
            
            // Répartition par sévérité
            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Border(1).BorderColor(DangerColor).Padding(10).Column(col =>
                {
                    col.Item().Text("CRITIQUES").FontSize(9).Bold().FontColor(DangerColor);
                    col.Item().Text($"{scanResult.CriticalIssues}").FontSize(16).Bold().FontColor(DangerColor);
                    col.Item().Text("À corriger immédiatement").FontSize(8).FontColor(DarkGrayColor);
                });
                
                row.ConstantItem(5);
                
                row.RelativeItem().Border(1).BorderColor(WarningColor).Padding(10).Column(col =>
                {
                    col.Item().Text("AVERTISSEMENTS").FontSize(9).Bold().FontColor("#f7931e");
                    col.Item().Text($"{scanResult.WarningIssues}").FontSize(16).Bold().FontColor("#f7931e");
                    col.Item().Text("À améliorer").FontSize(8).FontColor(DarkGrayColor);
                });
                
                row.ConstantItem(5);
                
                row.RelativeItem().Border(1).BorderColor(InfoColor).Padding(10).Column(col =>
                {
                    col.Item().Text("INFORMATIONS").FontSize(9).Bold().FontColor("#0ea5e9");
                    col.Item().Text($"{scanResult.InfoIssues}").FontSize(16).Bold().FontColor("#0ea5e9");
                    col.Item().Text("Bonnes pratiques").FontSize(8).FontColor(DarkGrayColor);
                });
            });
        });
    }

    private void ComposeIssuesBreakdown(IContainer container, List<AccessibilityIssue> issues)
    {
        container.Column(column =>
        {
            column.Item().Text("RÉPARTITION PAR RÈGLE RGAA").FontSize(14).Bold().FontColor(PrimaryColor);
            
            var issuesByRule = issues.GroupBy(i => i.RgaaRule)
                                   .Select(g => new { Rule = g.Key, Count = g.Count(), Severity = g.Max(x => x.Severity) })
                                   .OrderByDescending(x => x.Severity)
                                   .ThenByDescending(x => x.Count)
                                   .Take(10);
            
            column.Item().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                    columns.ConstantColumn(80);
                    columns.ConstantColumn(80);
                });
                
                // En-tête
                table.Header(header =>
                {
                    header.Cell().Background(PrimaryColor).Padding(8)
                        .Text("Règle").FontSize(10).Bold().FontColor(Colors.White);
                    header.Cell().Background(PrimaryColor).Padding(8)
                        .Text("Description").FontSize(10).Bold().FontColor(Colors.White);
                    header.Cell().Background(PrimaryColor).Padding(8).AlignCenter()
                        .Text("Occurrences").FontSize(10).Bold().FontColor(Colors.White);
                    header.Cell().Background(PrimaryColor).Padding(8).AlignCenter()
                        .Text("Priorité").FontSize(10).Bold().FontColor(Colors.White);
                });
                
                // Lignes
                foreach (var ruleGroup in issuesByRule)
                {
                    var severityInfo = GetSeverityInfo(ruleGroup.Severity);
                    var ruleDescription = GetRgaaRuleDescription(ruleGroup.Rule);
                    
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6)
                        .Text($"RGAA {ruleGroup.Rule}").FontSize(9).Bold();
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6)
                        .Text(ruleDescription).FontSize(9);
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter()
                        .Text($"{ruleGroup.Count}").FontSize(9).Bold().FontColor(severityInfo.Color);
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignCenter()
                        .Background(severityInfo.Color).Padding(2)
                        .Text(severityInfo.Text).FontSize(8).Bold().FontColor(Colors.White);
                }
            });
        });
    }

    private void ComposePriorityRecommendations(IContainer container, ScanResult scanResult, List<AccessibilityIssue> issues)
    {
        container.Column(column =>
        {
            column.Item().Text("RECOMMANDATIONS PRIORITAIRES").FontSize(14).Bold().FontColor(PrimaryColor);
            
            column.Item().PaddingTop(10).Column(recommendations =>
            {
                if (scanResult.CriticalIssues > 0)
                {
                    recommendations.Item().PaddingBottom(10).Row(row =>
                    {
                        row.ConstantItem(20).AlignTop().Text("🔴").FontSize(12);
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Corriger les problèmes critiques (priorité 1)").FontSize(11).Bold();
                            col.Item().Text($"{scanResult.CriticalIssues} problèmes bloquent l'accessibilité et doivent être corrigés immédiatement.")
                                .FontSize(10).FontColor(DarkGrayColor);
                        });
                    });
                }
                
                if (scanResult.Score < 70)
                {
                    recommendations.Item().PaddingBottom(10).Row(row =>
                    {
                        row.ConstantItem(20).AlignTop().Text("🟡").FontSize(12);
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Améliorer le score global (priorité 2)").FontSize(11).Bold();
                            col.Item().Text("Focus sur les images, formulaires et navigation pour une amélioration rapide du score.")
                                .FontSize(10).FontColor(DarkGrayColor);
                        });
                    });
                }
                
                recommendations.Item().PaddingBottom(10).Row(row =>
                {
                    row.ConstantItem(20).AlignTop().Text("📚").FontSize(12);
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Formation de l'équipe").FontSize(11).Bold();
                        col.Item().Text("Sensibiliser les développeurs aux bonnes pratiques RGAA pour éviter les régressions.")
                            .FontSize(10).FontColor(DarkGrayColor);
                    });
                });
                
                recommendations.Item().Row(row =>
                {
                    row.ConstantItem(20).AlignTop().Text("🔄").FontSize(12);
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Audit régulier").FontSize(11).Bold();
                        col.Item().Text("Programmer des scans hebdomadaires pour maintenir le niveau de conformité.")
                            .FontSize(10).FontColor(DarkGrayColor);
                    });
                });
            });
        });
    }

    private void ComposeDetailedIssues(IContainer container, List<AccessibilityIssue> issues)
    {
        container.Column(column =>
        {
            column.Item().Text("DÉTAIL DES PROBLÈMES D'ACCESSIBILITÉ").FontSize(14).Bold().FontColor(PrimaryColor);
            
            var criticalIssues = issues.Where(i => i.Severity == IssueSeverity.Critical).ToList();
            var warningIssues = issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();
            var infoIssues = issues.Where(i => i.Severity == IssueSeverity.Info).ToList();
            
            if (criticalIssues.Any())
            {
                column.Item().PaddingTop(15).Element(content => ComposeIssueSection(content, "PROBLÈMES CRITIQUES", criticalIssues, DangerColor));
            }
            
            if (warningIssues.Any())
            {
                column.Item().PaddingTop(15).Element(content => ComposeIssueSection(content, "AVERTISSEMENTS", warningIssues, "#f7931e"));
            }
            
            if (infoIssues.Any())
            {
                column.Item().PaddingTop(15).Element(content => ComposeIssueSection(content, "INFORMATIONS", infoIssues, "#0ea5e9"));
            }
        });
    }

    private void ComposeIssueSection(IContainer container, string title, List<AccessibilityIssue> issues, string color)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(12).Bold().FontColor(color);
            
            foreach (var issue in issues.Take(20)) // Limiter pour éviter des rapports trop longs
            {
                column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10)
                    .Column(issueColumn =>
                    {
                        // En-tête de l'issue
                        issueColumn.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"RGAA {issue.RgaaRule}: {issue.Title}").FontSize(10).Bold();
                            row.ConstantItem(60).AlignRight()
                                .Background(color).Padding(2).AlignCenter()
                                .Text(GetSeverityText(issue.Severity)).FontSize(8).Bold().FontColor(Colors.White);
                        });
                        
                        // Description
                        issueColumn.Item().PaddingTop(5).Text(issue.Description).FontSize(9).LineHeight(1.3f);
                        
                        // Page concernée
                        if (!string.IsNullOrWhiteSpace(issue.PageUrl))
                        {
                            issueColumn.Item().PaddingTop(5)
                                .Text($"Page: {TruncateUrl(issue.PageUrl)}")
                                .FontSize(8).FontColor(DarkGrayColor);
                        }
                        
                        // Suggestion de correction
                        if (!string.IsNullOrWhiteSpace(issue.FixSuggestion))
                        {
                            issueColumn.Item().PaddingTop(5).BorderLeft(2).BorderColor(color).PaddingLeft(8)
                                .Column(fixColumn =>
                                {
                                    fixColumn.Item().Text("💡 Suggestion de correction:").FontSize(8).Bold().FontColor(color);
                                    fixColumn.Item().Text(issue.FixSuggestion).FontSize(8).LineHeight(1.3f);
                                });
                        }
                    });
            }
            
            if (issues.Count > 20)
            {
                column.Item().PaddingTop(10).AlignCenter()
                    .Text($"... et {issues.Count - 20} autres problèmes de cette catégorie")
                    .FontSize(9).Italic().FontColor(DarkGrayColor);
            }
        });
    }

    private void ComposeFooter(IContainer container, ScanResult scanResult)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(10)
            .Row(row =>
            {
                row.RelativeItem().Text($"Rapport généré le {DateTime.Now:dd/MM/yyyy à HH:mm}")
                    .FontSize(8).FontColor(DarkGrayColor);
                row.RelativeItem().AlignCenter().Text($"Scan ID: {scanResult.ScanId}")
                    .FontSize(8).FontColor(DarkGrayColor);
                row.RelativeItem().AlignRight().Text("ComplianceScannerPro - Audit RGAA automatisé")
                    .FontSize(8).FontColor(DarkGrayColor);
            });
    }

    // Méthodes utilitaires
    private (string Color, string Text) GetGradeInfo(AccessibilityGrade grade)
    {
        return grade switch
        {
            AccessibilityGrade.A => (SuccessColor, "A"),
            AccessibilityGrade.B => (PrimaryColor, "B"),
            AccessibilityGrade.C => (WarningColor, "C"),
            AccessibilityGrade.D => ("#fd7e14", "D"),
            AccessibilityGrade.F => (DangerColor, "F"),
            _ => (DarkGrayColor, "?")
        };
    }

    private (string Color, string Text) GetSeverityInfo(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Critical => (DangerColor, "CRITIQUE"),
            IssueSeverity.Warning => ("#f7931e", "MOYEN"),
            IssueSeverity.Info => ("#0ea5e9", "INFO"),
            _ => (DarkGrayColor, "INCONNU")
        };
    }

    private string GetGradeDescription(AccessibilityGrade grade)
    {
        return grade switch
        {
            AccessibilityGrade.A => "Excellent",
            AccessibilityGrade.B => "Bon",
            AccessibilityGrade.C => "Moyen",
            AccessibilityGrade.D => "Médiocre",
            AccessibilityGrade.F => "Échec",
            _ => "Non évalué"
        };
    }

    private string GetComplianceLevel(int score)
    {
        return score switch
        {
            >= 90 => "Pleinement conforme RGAA",
            >= 75 => "Largement conforme RGAA",
            >= 50 => "Partiellement conforme RGAA",
            _ => "Non conforme RGAA"
        };
    }

    private string GetSeverityText(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Critical => "CRITIQUE",
            IssueSeverity.Warning => "MOYEN",
            IssueSeverity.Info => "INFO",
            _ => "INCONNU"
        };
    }

    private string GetRgaaRuleDescription(string rgaaRule)
    {
        var descriptions = new Dictionary<string, string>
        {
            { "1.1", "Images avec alternative textuelle" },
            { "1.2", "Images décoratives ignorées" },
            { "3.2", "Contraste des couleurs suffisant" },
            { "6.1", "Liens explicites" },
            { "7.1", "Scripts compatibles" },
            { "8.3", "Langue de la page" },
            { "8.5", "Titre de page" },
            { "9.1", "Structure de titres" },
            { "9.3", "Listes structurées" },
            { "10.4", "Texte lisible au zoom" },
            { "11.1", "Étiquettes de formulaire" },
            { "11.2", "Étiquettes pertinentes" },
            { "12.6", "Zones de regroupement" },
            { "12.8", "Ordre de tabulation" },
            { "12.9", "Pas de piège au clavier" }
        };
        
        return descriptions.TryGetValue(rgaaRule, out var description) 
            ? description 
            : "Règle d'accessibilité";
    }

    private string TruncateUrl(string url, int maxLength = 80)
    {
        if (url.Length <= maxLength) return url;
        return url.Substring(0, maxLength - 3) + "...";
    }
}