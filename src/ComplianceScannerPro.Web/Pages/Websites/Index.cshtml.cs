using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Web.Pages.Websites;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;

    public IndexModel(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
    }

    public List<WebsiteViewModel> Websites { get; set; } = new();
    public string SubscriptionPlan { get; set; } = "Gratuit";
    public int MaxWebsites { get; set; } = 3;

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return;

        await LoadWebsites(user.Id);
        await LoadSubscriptionInfo(user.Id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        var website = await _unitOfWork.Websites
            .GetFirstOrDefaultAsync(w => w.Id == id && w.UserId == user.Id);

        if (website == null)
        {
            TempData["ErrorMessage"] = "Site web introuvable.";
            return RedirectToPage();
        }

        // Supprimer tous les scans associés et leurs problèmes
        var scans = await _unitOfWork.ScanResults
            .GetAllAsync(s => s.WebsiteId == id);

        foreach (var scan in scans)
        {
            var issues = await _unitOfWork.AccessibilityIssues
                .GetAllAsync(i => i.ScanResultId == scan.Id);
            
            foreach (var issue in issues)
            {
                _unitOfWork.AccessibilityIssues.Delete(issue);
            }
            
            _unitOfWork.ScanResults.Delete(scan);
        }

        _unitOfWork.Websites.Delete(website);
        await _unitOfWork.SaveAsync();

        TempData["SuccessMessage"] = $"Le site '{website.Name}' a été supprimé avec succès.";
        return RedirectToPage();
    }

    private async Task LoadWebsites(string userId)
    {
        var websites = await _unitOfWork.Websites
            .GetAllAsync(w => w.UserId == userId);

        var websiteViewModels = new List<WebsiteViewModel>();

        foreach (var website in websites.OrderBy(w => w.Name))
        {
            // Récupérer les scans pour ce site
            var scans = await _unitOfWork.ScanResults
                .GetAllAsync(s => s.WebsiteId == website.Id);

            var completedScans = scans.Where(s => s.Status == ScanStatus.Completed).ToList();
            var lastCompletedScan = completedScans.OrderByDescending(s => s.CompletedAt).FirstOrDefault();

            // Calculer le nombre total de problèmes
            int totalIssues = 0;
            if (completedScans.Any())
            {
                var scanIds = completedScans.Select(s => s.Id);
                var issues = await _unitOfWork.AccessibilityIssues
                    .GetAllAsync(i => scanIds.Contains(i.ScanResultId));
                totalIssues = issues.Count();
            }

            var viewModel = new WebsiteViewModel
            {
                Id = website.Id,
                Name = website.Name,
                Url = website.Url,
                Description = website.Description,
                TotalScans = scans.Count(),
                LastScore = lastCompletedScan?.Score,
                LastScanDate = lastCompletedScan?.CompletedAt,
                TotalIssues = totalIssues
            };

            websiteViewModels.Add(viewModel);
        }

        Websites = websiteViewModels;
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
}

public class WebsiteViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Description { get; set; }
    public int TotalScans { get; set; }
    public int? LastScore { get; set; }
    public DateTime? LastScanDate { get; set; }
    public int TotalIssues { get; set; }
}