using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;
using ComplianceScannerPro.Core.Entities;

namespace ComplianceScannerPro.Web.Pages.Scans;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScanService _scanService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IScanService scanService,
        ILogger<IndexModel> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _scanService = scanService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int? WebsiteId { get; set; }

    public Website? Website { get; set; }
    public List<ScanResultDto> Scans { get; set; } = new();
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

            // Si WebsiteId est spécifié, vérifier qu'il appartient à l'utilisateur
            if (WebsiteId.HasValue)
            {
                Website = await _unitOfWork.Websites.GetAsync(w => w.Id == WebsiteId.Value && w.UserId == userId);
                if (Website == null)
                {
                    ErrorMessage = "Site web non trouvé ou accès non autorisé";
                    return Page();
                }
            }

            // Récupérer les scans de l'utilisateur
            var scanResults = await _scanService.GetUserScanHistoryAsync(userId, 100); // Limite à 100 scans récents

            // Filtrer par site si spécifié
            if (WebsiteId.HasValue)
            {
                scanResults = scanResults.Where(s => s.WebsiteId == WebsiteId.Value).ToList();
            }

            // Mapper vers DTOs avec informations des sites web
            Scans = new List<ScanResultDto>();
            foreach (var scan in scanResults.OrderByDescending(s => s.StartedAt))
            {
                var website = Website ?? await _unitOfWork.Websites.GetByIdAsync(scan.WebsiteId);
                
                Scans.Add(new ScanResultDto
                {
                    Id = scan.Id,
                    ScanId = scan.ScanId,
                    StartedAt = scan.StartedAt,
                    CompletedAt = scan.CompletedAt,
                    Status = scan.Status,
                    Score = scan.Score,
                    Grade = scan.Grade,
                    PagesScanned = scan.PagesScanned,
                    TotalIssues = scan.TotalIssues,
                    CriticalIssues = scan.CriticalIssues,
                    WarningIssues = scan.WarningIssues,
                    InfoIssues = scan.InfoIssues,
                    ErrorMessage = scan.ErrorMessage,
                    ReportPdfPath = scan.ReportPdfPath,
                    WebsiteId = scan.WebsiteId,
                    WebsiteName = website?.Name ?? "Site inconnu",
                    WebsiteUrl = website?.Url ?? "",
                    UserId = scan.UserId
                });
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des scans pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            ErrorMessage = "Erreur lors du chargement des scans";
            return Page();
        }
    }
}