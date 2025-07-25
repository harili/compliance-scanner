using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;

namespace ComplianceScannerPro.Web.Pages.Scans;

[Authorize]
public class StartModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IScanService _scanService;
    private readonly ILogger<StartModel> _logger;

    public StartModel(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IScanService scanService,
        ILogger<StartModel> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _scanService = scanService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int? WebsiteId { get; set; }
    
    public List<WebsiteDto> Websites { get; set; } = new();
    public WebsiteDto? SelectedWebsite { get; set; }
    public bool CanStartScan { get; set; } = true;
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

            // Récupérer les sites de l'utilisateur
            var websites = await _unitOfWork.Websites.GetAllAsync(w => w.UserId == userId);
            
            Websites = websites.Select(w => new WebsiteDto
            {
                Id = w.Id,
                Name = w.Name,
                Url = w.Url,
                IsActive = w.IsActive,
                CreatedAt = w.CreatedAt,
                LastScanAt = w.LastScanAt,
                MaxDepth = w.MaxDepth,
                IncludeSubdomains = w.IncludeSubdomains,
                UserId = w.UserId
            }).OrderBy(w => w.Name).ToList();

            // Si un websiteId est spécifié, vérifier qu'il appartient à l'utilisateur et le sélectionner
            if (WebsiteId.HasValue)
            {
                SelectedWebsite = Websites.FirstOrDefault(w => w.Id == WebsiteId.Value);
                if (SelectedWebsite == null)
                {
                    ErrorMessage = "Site web non trouvé ou accès non autorisé";
                    return Page();
                }
            }

            // Vérifier si l'utilisateur peut démarrer un scan
            CanStartScan = await _scanService.CanUserStartScanAsync(userId);

            if (!CanStartScan)
            {
                ErrorMessage = "Vous avez atteint la limite de scans simultanés. Veuillez attendre qu'un scan se termine.";
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du chargement de la page de scan pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            ErrorMessage = "Une erreur est survenue lors du chargement de la page.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostStartScanAsync([FromBody] StartScanDto startDto)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return new JsonResult(new { success = false, message = "Utilisateur non authentifié" });
            }

            // Vérifier si l'utilisateur peut démarrer un scan
            if (!await _scanService.CanUserStartScanAsync(userId))
            {
                return new JsonResult(new { success = false, message = "Limite de scans simultanés atteinte" });
            }

            // Vérifier que le site appartient à l'utilisateur
            var website = await _unitOfWork.Websites.GetAsync(w => w.Id == startDto.WebsiteId && w.UserId == userId);
            if (website == null)
            {
                return new JsonResult(new { success = false, message = "Site web non trouvé" });
            }

            if (!website.IsActive)
            {
                return new JsonResult(new { success = false, message = "Le site web est désactivé" });
            }

            // Démarrer le scan
            var scanResult = await _scanService.StartScanAsync(startDto.WebsiteId, userId);

            _logger.LogInformation("Scan démarré via interface web: {ScanId} pour le site {WebsiteId}", scanResult.ScanId, startDto.WebsiteId);

            return new JsonResult(new 
            { 
                success = true, 
                scanId = scanResult.ScanId,
                message = "Scan démarré avec succès"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du démarrage du scan pour le site {WebsiteId}", startDto.WebsiteId);
            return new JsonResult(new { success = false, message = "Erreur interne du serveur" });
        }
    }
}