using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;

namespace ComplianceScannerPro.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService,
        ILogger<SubscriptionsController> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// Récupère les informations d'abonnement de l'utilisateur connecté
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserSubscriptionInfoDto>>> GetMySubscription()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<UserSubscriptionInfoDto>.ErrorResult("Utilisateur non authentifié"));

            var currentSubscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
            var availablePlans = await _subscriptionService.GetAvailablePlansAsync();
            var (websitesUsed, websitesLimit) = await _subscriptionService.GetWebsiteUsageAsync(userId);
            var (scansUsed, scansLimit) = await _subscriptionService.GetScanUsageAsync(userId);

            var response = new UserSubscriptionInfoDto
            {
                CurrentSubscription = currentSubscription != null ? MapToDto(currentSubscription) : null,
                WebsitesUsed = websitesUsed,
                ScansThisMonth = scansUsed,
                CanAddWebsite = await _subscriptionService.CanUserAddWebsiteAsync(userId),
                CanStartScan = await _subscriptionService.CanUserStartScanAsync(userId),
                AvailablePlans = availablePlans.Select(MapToDto).ToList()
            };

            return Ok(ApiResponse<UserSubscriptionInfoDto>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des informations d'abonnement pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            return StatusCode(500, ApiResponse<UserSubscriptionInfoDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère tous les plans d'abonnement disponibles
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SubscriptionDto>>>> GetAvailablePlans()
    {
        try
        {
            var plans = await _subscriptionService.GetAvailablePlansAsync();
            var planDtos = plans.Select(MapToDto).ToList();

            return Ok(ApiResponse<List<SubscriptionDto>>.SuccessResult(planDtos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des plans disponibles");
            return StatusCode(500, ApiResponse<List<SubscriptionDto>>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère l'usage actuel de l'utilisateur (sites web et scans)
    /// </summary>
    [HttpGet("usage")]
    public async Task<ActionResult<ApiResponse<object>>> GetUsage()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.ErrorResult("Utilisateur non authentifié"));

            var (websitesUsed, websitesLimit) = await _subscriptionService.GetWebsiteUsageAsync(userId);
            var (scansUsed, scansLimit) = await _subscriptionService.GetScanUsageAsync(userId);

            var usage = new
            {
                Websites = new
                {
                    Used = websitesUsed,
                    Limit = websitesLimit,
                    Percentage = websitesLimit > 0 ? (int)((double)websitesUsed / websitesLimit * 100) : 0
                },
                Scans = new
                {
                    Used = scansUsed,
                    Limit = scansLimit,
                    Percentage = scansLimit > 0 ? (int)((double)scansUsed / scansLimit * 100) : 0,
                    ResetDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1)
                },
                Features = new
                {
                    ApiAccess = await _subscriptionService.HasApiAccessAsync(userId),
                    BrandedReports = await _subscriptionService.HasBrandedReportsAsync(userId)
                }
            };

            return Ok(ApiResponse<object>.SuccessResult(usage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de l'usage pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Vérifie si l'utilisateur peut effectuer une action spécifique
    /// </summary>
    [HttpGet("can/{action}")]
    public async Task<ActionResult<ApiResponse<bool>>> CanPerformAction(string action)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<bool>.ErrorResult("Utilisateur non authentifié"));

            var canPerform = action.ToLower() switch
            {
                "add-website" => await _subscriptionService.CanUserAddWebsiteAsync(userId),
                "start-scan" => await _subscriptionService.CanUserStartScanAsync(userId),
                "access-api" => await _subscriptionService.HasApiAccessAsync(userId),
                "branded-reports" => await _subscriptionService.HasBrandedReportsAsync(userId),
                _ => false
            };

            return Ok(ApiResponse<bool>.SuccessResult(canPerform));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification de l'action {Action} pour l'utilisateur {UserId}", action, _userManager.GetUserId(User));
            return StatusCode(500, ApiResponse<bool>.ErrorResult("Erreur interne du serveur"));
        }
    }

    private static SubscriptionDto MapToDto(Core.Entities.Subscription subscription)
    {
        return new SubscriptionDto
        {
            Id = subscription.Id,
            PlanName = subscription.PlanName,
            Price = subscription.Price,
            MaxWebsites = subscription.MaxWebsites,
            MaxScansPerMonth = subscription.MaxScansPerMonth,
            ApiAccess = subscription.ApiAccess,
            BrandedReports = subscription.BrandedReports,
            PrioritySupport = subscription.PrioritySupport,
            CreatedAt = subscription.CreatedAt,
            ExpiresAt = subscription.ExpiresAt,
            IsActive = subscription.IsActive,
            StripeSubscriptionId = subscription.StripeSubscriptionId,
            StripePriceId = subscription.StripePriceId,
            UserId = subscription.UserId
        };
    }
}