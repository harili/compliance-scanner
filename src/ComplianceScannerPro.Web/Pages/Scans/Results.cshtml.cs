using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;
using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Web.Pages.Scans;

[Authorize]
public class ResultsModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScanService _scanService;
    private readonly ILogger<ResultsModel> _logger;

    public ResultsModel(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IScanService scanService,
        ILogger<ResultsModel> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _scanService = scanService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string ScanId { get; set; } = string.Empty;

    public ScanResultDto? ScanResult { get; set; }
    public List<string> AvailableRules { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            if (string.IsNullOrWhiteSpace(ScanId))
            {
                ErrorMessage = "Identifiant de scan manquant";
                return Page();
            }

            // Récupérer le résultat du scan
            var scanResult = await _scanService.GetScanResultAsync(ScanId);
            
            if (scanResult == null || scanResult.UserId != userId)
            {
                ErrorMessage = "Scan non trouvé ou accès non autorisé";
                return Page();
            }

            // Vérifier que le scan est terminé
            if (scanResult.Status != ScanStatus.Completed)
            {
                return RedirectToPage("/Scans/Start");
            }

            // Récupérer les informations du site web
            var website = await _unitOfWork.Websites.GetByIdAsync(scanResult.WebsiteId);
            
            ScanResult = new ScanResultDto
            {
                Id = scanResult.Id,
                ScanId = scanResult.ScanId,
                StartedAt = scanResult.StartedAt,
                CompletedAt = scanResult.CompletedAt,
                Status = scanResult.Status,
                Score = scanResult.Score,
                Grade = scanResult.Grade,
                PagesScanned = scanResult.PagesScanned,
                TotalIssues = scanResult.TotalIssues,
                CriticalIssues = scanResult.CriticalIssues,
                WarningIssues = scanResult.WarningIssues,
                InfoIssues = scanResult.InfoIssues,
                ErrorMessage = scanResult.ErrorMessage,
                ReportPdfPath = scanResult.ReportPdfPath,
                WebsiteId = scanResult.WebsiteId,
                WebsiteName = website?.Name ?? "Site inconnu",
                WebsiteUrl = website?.Url ?? "",
                UserId = scanResult.UserId
            };

            // Récupérer les règles RGAA disponibles pour les filtres
            var issues = await _unitOfWork.AccessibilityIssues.GetAllAsync(i => i.ScanResultId == scanResult.Id);
            AvailableRules = issues.Select(i => i.RgaaRule)
                                 .Distinct()
                                 .OrderBy(r => r)
                                 .ToList();

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du chargement des résultats du scan {ScanId} pour l'utilisateur {UserId}", 
                ScanId, _userManager.GetUserId(User));
            ErrorMessage = "Une erreur est survenue lors du chargement des résultats.";
            return Page();
        }
    }

    public string GetGradeDescription(AccessibilityGrade grade)
    {
        return grade switch
        {
            AccessibilityGrade.A => "Excellent - Pleinement conforme",
            AccessibilityGrade.B => "Bon - Largement conforme",
            AccessibilityGrade.C => "Moyen - Partiellement conforme",
            AccessibilityGrade.D => "Médiocre - Non conforme",
            AccessibilityGrade.F => "Échec - Totalement non conforme",
            _ => "Non évalué"
        };
    }

    public string GetSeverityBadgeClass(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Critical => "severity-critical",
            IssueSeverity.Warning => "severity-warning",
            IssueSeverity.Info => "severity-info",
            _ => "bg-secondary"
        };
    }

    public string GetSeverityText(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Critical => "Critique",
            IssueSeverity.Warning => "Avertissement",
            IssueSeverity.Info => "Information",
            _ => "Inconnu"
        };
    }
}