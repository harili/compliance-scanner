using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Services;
using ComplianceScannerPro.Infrastructure.Identity;

namespace ComplianceScannerPro.Web.Pages.Subscriptions;

[Authorize]
public class IndexModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IPaymentService _paymentService;
    private readonly StripeSettings _stripeSettings;

    public IndexModel(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService,
        IPaymentService paymentService,
        IOptions<StripeSettings> stripeSettings)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _paymentService = paymentService;
        _stripeSettings = stripeSettings.Value;
    }

    public Subscription? CurrentSubscription { get; set; }
    public bool IsFreePlan => CurrentSubscription == null;
    public int ScansUsed { get; set; }
    public int WebsitesCount { get; set; }
    public string CustomerPortalUrl { get; set; } = "";

    // Prix Stripe des plans
    public string StarterPriceId => _stripeSettings.StarterPlanPriceId;
    public string ProfessionalPriceId => _stripeSettings.ProfessionalPlanPriceId;
    public string EnterprisePriceId => _stripeSettings.EnterprisePlanPriceId;

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return;

        await LoadCurrentSubscription(user.Id);
        await LoadUsageData(user.Id);
        await LoadCustomerPortalUrl(user);
    }

    public async Task<IActionResult> OnPostSubscribeAsync(string planName, string priceId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        try
        {
            var successUrl = Url.PageLink("/Subscriptions/Success", null, new { }, Request.Scheme) ?? "";
            var cancelUrl = Url.PageLink("/Subscriptions/Index", null, new { }, Request.Scheme) ?? "";

            var checkoutUrl = await _paymentService.CreateCheckoutSessionAsync(
                user.Id, planName, priceId, successUrl, cancelUrl);

            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Erreur lors de la création de la session de paiement : " + ex.Message;
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostCancelAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        var subscription = await _subscriptionService.GetUserSubscriptionAsync(user.Id);
        if (subscription?.StripeSubscriptionId == null)
        {
            TempData["ErrorMessage"] = "Aucun abonnement actif à annuler.";
            return RedirectToPage();
        }

        try
        {
            var success = await _paymentService.CancelSubscriptionAsync(subscription.StripeSubscriptionId);
            if (success)
            {
                TempData["SuccessMessage"] = "Votre abonnement a été annulé avec succès. Il reste actif jusqu'à la fin de la période de facturation.";
            }
            else
            {
                TempData["ErrorMessage"] = "Erreur lors de l'annulation de l'abonnement.";
            }
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = "Erreur lors de l'annulation : " + ex.Message;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDowngradeAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Forbid();

        var subscription = await _subscriptionService.GetUserSubscriptionAsync(user.Id);
        if (subscription?.StripeSubscriptionId != null)
        {
            try
            {
                await _paymentService.CancelSubscriptionAsync(subscription.StripeSubscriptionId);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Erreur lors de la rétrogradation : " + ex.Message;
                return RedirectToPage();
            }
        }

        // Supprimer l'abonnement de la base de données (retour au plan gratuit)
        if (subscription != null)
        {
            _unitOfWork.Subscriptions.Delete(subscription);
            await _unitOfWork.SaveAsync();
        }

        TempData["SuccessMessage"] = "Vous êtes maintenant sur le plan gratuit.";
        return RedirectToPage();
    }

    private async Task LoadCurrentSubscription(string userId)
    {
        CurrentSubscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
    }

    private async Task LoadUsageData(string userId)
    {
        // Compter les sites web
        var websites = await _unitOfWork.Websites
            .GetAllAsync(w => w.UserId == userId);
        WebsitesCount = websites.Count();

        // Compter les scans ce mois
        var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var websiteIds = websites.Select(w => w.Id);
        var scansThisMonth = await _unitOfWork.ScanResults
            .GetAllAsync(s => websiteIds.Contains(s.WebsiteId) && s.StartedAt >= startOfMonth);
        ScansUsed = scansThisMonth.Count();
    }

    private async Task LoadCustomerPortalUrl(ApplicationUser user)
    {
        if (CurrentSubscription?.StripeCustomerId != null)
        {
            try
            {
                var returnUrl = Url.PageLink("/Subscriptions/Index", null, new { }, Request.Scheme) ?? "";
                CustomerPortalUrl = await _paymentService.CreateCustomerPortalSessionAsync(
                    CurrentSubscription.StripeCustomerId, returnUrl);
            }
            catch
            {
                CustomerPortalUrl = "";
            }
        }
    }
}