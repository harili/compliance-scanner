using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;

namespace ComplianceScannerPro.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ScansController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScanService _scanService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IReportGenerator _reportGenerator;
    private readonly ILogger<ScansController> _logger;

    public ScansController(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IScanService scanService,
        ISubscriptionService subscriptionService,
        IReportGenerator reportGenerator,
        ILogger<ScansController> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _scanService = scanService;
        _subscriptionService = subscriptionService;
        _reportGenerator = reportGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Démarre un nouveau scan d'accessibilité
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<ApiResponse<ScanResultDto>>> StartScan([FromBody] StartScanDto startDto)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ScanResultDto>.ErrorResult("Utilisateur non authentifié"));

            // Vérifier les limitations d'abonnement
            var canScan = await _subscriptionService.CanUserStartScanAsync(userId);
            if (!canScan)
                return BadRequest(ApiResponse<ScanResultDto>.ErrorResult("Limite de scans atteinte pour votre abonnement"));

            // Vérifier que le site appartient à l'utilisateur
            var website = await _unitOfWork.Websites.GetAsync(w => w.Id == startDto.WebsiteId && w.UserId == userId);
            if (website == null)
                return NotFound(ApiResponse<ScanResultDto>.ErrorResult("Site web non trouvé"));

            if (!website.IsActive)
                return BadRequest(ApiResponse<ScanResultDto>.ErrorResult("Le site web est désactivé"));

            var scanResult = await _scanService.StartScanAsync(startDto.WebsiteId, userId);

            _logger.LogInformation("Scan démarré: {ScanId} pour le site {WebsiteId}", scanResult.ScanId, startDto.WebsiteId);

            return Ok(ApiResponse<ScanResultDto>.SuccessResult(
                MapToDto(scanResult, website), 
                "Scan démarré avec succès"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du démarrage du scan pour le site {WebsiteId}", startDto.WebsiteId);
            return StatusCode(500, ApiResponse<ScanResultDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère le statut d'un scan en cours
    /// </summary>
    [HttpGet("{scanId}/status")]
    public async Task<ActionResult<ApiResponse<ScanProgressDto>>> GetScanStatus(string scanId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var scanResult = await _scanService.GetScanResultAsync(scanId);

            if (scanResult == null || scanResult.UserId != userId)
                return NotFound(ApiResponse<ScanProgressDto>.ErrorResult("Scan non trouvé"));

            var progress = new ScanProgressDto
            {
                ScanId = scanResult.ScanId,
                Status = scanResult.Status,
                PagesScanned = scanResult.PagesScanned,
                CurrentPage = scanResult.PagesScanned,
                CurrentUrl = "En cours...",
                ProgressPercentage = CalculateProgress(scanResult),
                ErrorMessage = scanResult.ErrorMessage
            };

            return Ok(ApiResponse<ScanProgressDto>.SuccessResult(progress));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du statut du scan {ScanId}", scanId);
            return StatusCode(500, ApiResponse<ScanProgressDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère les résultats d'un scan terminé
    /// </summary>
    [HttpGet("{scanId}")]
    public async Task<ActionResult<ApiResponse<ScanResultDto>>> GetScanResult(string scanId)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var scanResult = await _scanService.GetScanResultAsync(scanId);

            if (scanResult == null || scanResult.UserId != userId)
                return NotFound(ApiResponse<ScanResultDto>.ErrorResult("Scan non trouvé"));

            var website = await _unitOfWork.Websites.GetByIdAsync(scanResult.WebsiteId);

            return Ok(ApiResponse<ScanResultDto>.SuccessResult(MapToDto(scanResult, website)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du scan {ScanId}", scanId);
            return StatusCode(500, ApiResponse<ScanResultDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère l'historique des scans de l'utilisateur
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<ScanResultDto>>>> GetScanHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? websiteId = null)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<PaginatedResponse<ScanResultDto>>.ErrorResult("Utilisateur non authentifié"));

            var scans = await _scanService.GetUserScanHistoryAsync(userId, pageSize * 5); // Récupérer plus pour la pagination

            if (websiteId.HasValue)
            {
                scans = scans.Where(s => s.WebsiteId == websiteId.Value).ToList();
            }

            var totalCount = scans.Count;
            var paginatedScans = scans
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var scanDtos = new List<ScanResultDto>();
            foreach (var scan in paginatedScans)
            {
                var website = await _unitOfWork.Websites.GetByIdAsync(scan.WebsiteId);
                scanDtos.Add(MapToDto(scan, website));
            }

            var response = new PaginatedResponse<ScanResultDto>
            {
                Items = scanDtos,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

            return Ok(ApiResponse<PaginatedResponse<ScanResultDto>>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de l'historique des scans pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            return StatusCode(500, ApiResponse<PaginatedResponse<ScanResultDto>>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère les problèmes d'accessibilité d'un scan
    /// </summary>
    [HttpGet("{scanId}/issues")]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<AccessibilityIssueDto>>>> GetScanIssues(
        string scanId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? severity = null,
        [FromQuery] string? rgaaRule = null)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var scanResult = await _scanService.GetScanResultAsync(scanId);

            if (scanResult == null || scanResult.UserId != userId)
                return NotFound(ApiResponse<PaginatedResponse<AccessibilityIssueDto>>.ErrorResult("Scan non trouvé"));

            var issues = await _unitOfWork.AccessibilityIssues.GetAllAsync(i => i.ScanResultId == scanResult.Id);

            // Filtrer par sévérité si spécifiée
            if (!string.IsNullOrWhiteSpace(severity) && Enum.TryParse<Shared.Enums.IssueSeverity>(severity, true, out var severityEnum))
            {
                issues = issues.Where(i => i.Severity == severityEnum).ToList();
            }

            // Filtrer par règle RGAA si spécifiée
            if (!string.IsNullOrWhiteSpace(rgaaRule))
            {
                issues = issues.Where(i => i.RgaaRule.Equals(rgaaRule, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalCount = issues.Count;
            var paginatedIssues = issues
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapIssueToDto)
                .ToList();

            var response = new PaginatedResponse<AccessibilityIssueDto>
            {
                Items = paginatedIssues,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

            return Ok(ApiResponse<PaginatedResponse<AccessibilityIssueDto>>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des problèmes du scan {ScanId}", scanId);
            return StatusCode(500, ApiResponse<PaginatedResponse<AccessibilityIssueDto>>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Télécharge le rapport PDF d'un scan
    /// </summary>
    [HttpGet("{scanId}/report")]
    public async Task<IActionResult> DownloadReport(string scanId)
    {
        try
        {
            _logger.LogInformation("Tentative de téléchargement du rapport pour le scan {ScanId}", scanId);
            
            var userId = _userManager.GetUserId(User);
            var scanResult = await _scanService.GetScanResultAsync(scanId);

            if (scanResult == null || scanResult.UserId != userId)
            {
                _logger.LogWarning("Scan {ScanId} non trouvé ou accès non autorisé pour l'utilisateur {UserId}", scanId, userId);
                return NotFound();
            }

            _logger.LogInformation("Scan trouvé: {ScanId}, Status: {Status}, ReportPath: {ReportPath}", scanId, scanResult.Status, scanResult.ReportPdfPath);

            if (string.IsNullOrWhiteSpace(scanResult.ReportPdfPath) || !System.IO.File.Exists(scanResult.ReportPdfPath))
            {
                _logger.LogInformation("Génération du rapport PDF nécessaire pour le scan {ScanId}", scanId);
                
                // Charger les données nécessaires pour le rapport
                var website = await _unitOfWork.Websites.GetByIdAsync(scanResult.WebsiteId);
                var user = await _userManager.FindByIdAsync(userId);
                var isAgency = user?.IsAgency == true;
                
                _logger.LogInformation("Données chargées - Website: {WebsiteName}, IsAgency: {IsAgency}", website?.Name, isAgency);
                
                // Assigner le site web au scan result pour le rapport
                scanResult.Website = website;
                
                _logger.LogInformation("Début de la génération du rapport PDF...");
                scanResult.ReportPdfPath = await _reportGenerator.GeneratePdfReportAsync(scanResult, isAgency);
                _logger.LogInformation("Rapport PDF généré: {ReportPath}", scanResult.ReportPdfPath);
                
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Chemin du rapport sauvegardé en base");
            }

            _logger.LogInformation("Lecture du fichier PDF: {ReportPath}", scanResult.ReportPdfPath);
            var fileBytes = await _reportGenerator.GetReportBytesAsync(scanResult.ReportPdfPath);
            var fileName = $"rapport-rgaa-{scanResult.ScanId}.pdf";

            _logger.LogInformation("Envoi du fichier PDF ({FileSize} bytes) avec le nom {FileName}", fileBytes.Length, fileName);
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du téléchargement du rapport pour le scan {ScanId}", scanId);
            return StatusCode(500, new { error = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    private static ScanResultDto MapToDto(ScanResult scanResult, Website? website)
    {
        return new ScanResultDto
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
    }

    private static AccessibilityIssueDto MapIssueToDto(AccessibilityIssue issue)
    {
        return new AccessibilityIssueDto
        {
            Id = issue.Id,
            RgaaRule = issue.RgaaRule,
            Title = issue.Title,
            Description = issue.Description,
            Severity = issue.Severity,
            PageUrl = issue.PageUrl,
            ElementSelector = issue.ElementSelector,
            ElementHtml = issue.ElementHtml,
            FixSuggestion = issue.FixSuggestion,
            CodeExample = issue.CodeExample,
            DetectedAt = issue.DetectedAt,
            ScanResultId = issue.ScanResultId
        };
    }

    private static int CalculateProgress(ScanResult scanResult)
    {
        return scanResult.Status switch
        {
            Shared.Enums.ScanStatus.Pending => 5, // Initialisation
            Shared.Enums.ScanStatus.Running => CalculateRunningProgress(scanResult),
            Shared.Enums.ScanStatus.Completed => 100,
            Shared.Enums.ScanStatus.Failed => 0,
            Shared.Enums.ScanStatus.Cancelled => 0,
            _ => 0
        };
    }

    private static int CalculateRunningProgress(ScanResult scanResult)
    {
        // Phase 1: Crawling (10-40%) - estimé selon les pages trouvées
        // Phase 2: Analyse (40-90%) - basé sur les pages scannées
        // Phase 3: Finalisation (90-95%) - calcul du score
        
        var pagesScanned = scanResult.PagesScanned;
        
        if (pagesScanned == 0)
        {
            // Encore en phase de crawling
            return 15; // Entre 10-40%
        }
        
        // En phase d'analyse - progression basée sur les pages scannées
        // Supposons un maximum de 50 pages (limite backend)
        var maxPages = 50;
        var analysisProgress = Math.Min(pagesScanned, maxPages);
        
        // Mapping: 1-50 pages = 40-90% de progression
        var progressPercent = 40 + (analysisProgress * 50 / maxPages);
        
        return Math.Min(95, Math.Max(15, (int)progressPercent));
    }
}