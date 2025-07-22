using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Infrastructure.Identity;
using System.ComponentModel.DataAnnotations;

namespace ComplianceScannerPro.Web.Pages.Websites;

[Authorize]
public class CreateModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IScanService _scanService;

    public CreateModel(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService,
        IScanService scanService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _scanService = scanService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string SubscriptionPlan { get; set; } = "Gratuit";
    public int MaxWebsites { get; set; } = 3;
    public int CurrentWebsitesCount { get; set; }
    public bool IsQuotaExceeded => CurrentWebsitesCount >= MaxWebsites;

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return;

        await LoadSubscriptionInfo(user.Id);
        await LoadCurrentWebsitesCount(user.Id);
    }

    public async Task<IActionResult> OnPostAsync(bool saveAndScan = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        await LoadSubscriptionInfo(user.Id);
        await LoadCurrentWebsitesCount(user.Id);

        // Vérifier le quota
        if (IsQuotaExceeded)
        {
            ModelState.AddModelError("", $"Vous avez atteint la limite de {MaxWebsites} sites web pour votre plan {SubscriptionPlan}.");
            return Page();
        }

        // Vérifier si l'URL est déjà utilisée par ce utilisateur
        var existingWebsite = await _unitOfWork.Websites
            .GetFirstOrDefaultAsync(w => w.UserId == user.Id && w.Url.ToLower() == Input.Url.ToLower());

        if (existingWebsite != null)
        {
            ModelState.AddModelError("Input.Url", "Vous avez déjà ajouté ce site web.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Créer le nouveau site
        var website = new Website
        {
            Name = Input.Name,
            Url = Input.Url,
            Description = Input.Description,
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };

        _unitOfWork.Websites.Add(website);
        await _unitOfWork.SaveAsync();

        TempData["SuccessMessage"] = $"Le site '{website.Name}' a été ajouté avec succès.";

        // Si demandé, lancer immédiatement un scan
        if (saveAndScan)
        {
            try
            {
                await _scanService.StartScanAsync(website.Id, user.Id);
                TempData["SuccessMessage"] += " Un scan a été lancé automatiquement.";
                return RedirectToPage("/Scans/Index", new { websiteId = website.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Site ajouté mais impossible de lancer le scan : " + ex.Message;
            }
        }

        return RedirectToPage("Index");
    }

    private async Task LoadSubscriptionInfo(string userId)
    {
        var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
        
        if (subscription != null)
        {
            SubscriptionPlan = subscription.PlanName;
            MaxWebsites = subscription.MaxWebsites;
        }
        else
        {
            SubscriptionPlan = "Gratuit";
            MaxWebsites = 3;
        }
    }

    private async Task LoadCurrentWebsitesCount(string userId)
    {
        var websites = await _unitOfWork.Websites
            .GetAllAsync(w => w.UserId == userId);
        CurrentWebsitesCount = websites.Count();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Le nom du site est obligatoire")]
        [StringLength(200, ErrorMessage = "Le nom ne peut pas dépasser 200 caractères")]
        [Display(Name = "Nom du site")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "L'URL du site est obligatoire")]
        [Url(ErrorMessage = "Veuillez saisir une URL valide")]
        [StringLength(500, ErrorMessage = "L'URL ne peut pas dépasser 500 caractères")]
        [Display(Name = "URL du site")]
        public string Url { get; set; } = "";

        [StringLength(1000, ErrorMessage = "La description ne peut pas dépasser 1000 caractères")]
        [Display(Name = "Description")]
        public string? Description { get; set; }
    }
}