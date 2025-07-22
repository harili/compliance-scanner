using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;

namespace ComplianceScannerPro.Web.Pages.Subscriptions;

[Authorize]
public class SuccessModel : PageModel
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public SuccessModel(
        ISubscriptionService subscriptionService,
        UserManager<ApplicationUser> userManager)
    {
        _subscriptionService = subscriptionService;
        _userManager = userManager;
    }

    public string PlanName { get; set; } = "";
    public decimal Amount { get; set; }
    public DateTime CurrentPeriodStart { get; set; }
    public DateTime CurrentPeriodEnd { get; set; }
    public int MaxWebsites { get; set; }
    public int MaxScansPerMonth { get; set; }
    public bool HasBranding { get; set; }
    public bool HasApiAccess { get; set; }

    public async Task<IActionResult> OnGetAsync(string? sessionId = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        var subscription = await _subscriptionService.GetUserSubscriptionAsync(user.Id);
        if (subscription == null)
        {
            TempData["ErrorMessage"] = "Aucun abonnement trouvé.";
            return RedirectToPage("/Subscriptions/Index");
        }

        // Remplir les données de la page
        PlanName = subscription.PlanName;
        Amount = subscription.Price;
        CurrentPeriodStart = subscription.CurrentPeriodStart;
        CurrentPeriodEnd = subscription.CurrentPeriodEnd;
        MaxWebsites = subscription.MaxWebsites;
        MaxScansPerMonth = subscription.MaxScansPerMonth;
        
        // Déterminer les fonctionnalités selon le plan
        HasBranding = PlanName != "Gratuit";
        HasApiAccess = PlanName == "Professional" || PlanName == "Enterprise";

        return Page();
    }
}