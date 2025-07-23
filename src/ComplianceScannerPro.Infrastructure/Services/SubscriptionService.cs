using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;

namespace ComplianceScannerPro.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public SubscriptionService(IUnitOfWork unitOfWork, ILogger<SubscriptionService> logger, UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _userManager = userManager;
    }

    public async Task<bool> CanUserAddWebsiteAsync(string userId)
    {
        try
        {
            // Vérifier si l'utilisateur est admin/développeur
            if (await IsUserAdminOrDeveloperAsync(userId))
            {
                _logger.LogDebug("Utilisateur admin/développeur {UserId}: limites bypassées pour les sites", userId);
                return true;
            }

            var subscription = await GetUserSubscriptionAsync(userId);
            if (subscription == null)
            {
                // Pas d'abonnement = plan gratuit par défaut
                subscription = await GetDefaultFreeSubscriptionAsync();
            }

            var websiteCount = await _unitOfWork.Websites.CountAsync(w => w.UserId == userId && w.IsActive);
            
            var canAdd = websiteCount < subscription.MaxWebsites;
            
            _logger.LogDebug("Utilisateur {UserId}: {WebsiteCount}/{MaxWebsites} sites, peut ajouter: {CanAdd}", 
                userId, websiteCount, subscription.MaxWebsites, canAdd);
            
            return canAdd;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification des limites de sites pour l'utilisateur {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> CanUserStartScanAsync(string userId)
    {
        try
        {
            // Vérifier si l'utilisateur est admin/développeur
            if (await IsUserAdminOrDeveloperAsync(userId))
            {
                _logger.LogDebug("Utilisateur admin/développeur {UserId}: limites bypassées pour les scans", userId);
                return true;
            }

            var subscription = await GetUserSubscriptionAsync(userId);
            if (subscription == null)
            {
                subscription = await GetDefaultFreeSubscriptionAsync();
            }

            // Compter les scans du mois en cours
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var scanCount = await _unitOfWork.ScanResults.CountAsync(s => 
                s.UserId == userId && 
                s.StartedAt >= startOfMonth);

            var canScan = scanCount < subscription.MaxScansPerMonth;
            
            _logger.LogDebug("Utilisateur {UserId}: {ScanCount}/{MaxScans} scans ce mois, peut scanner: {CanScan}", 
                userId, scanCount, subscription.MaxScansPerMonth, canScan);
            
            return canScan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification des limites de scans pour l'utilisateur {UserId}", userId);
            return false;
        }
    }

    public async Task<Subscription?> GetUserSubscriptionAsync(string userId)
    {
        try
        {
            return await _unitOfWork.Subscriptions.GetAsync(s => s.UserId == userId && s.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération de l'abonnement pour l'utilisateur {UserId}", userId);
            return null;
        }
    }

    public async Task<List<Subscription>> GetAvailablePlansAsync()
    {
        try
        {
            // Récupérer les plans système (templates)
            var plans = await _unitOfWork.Subscriptions.GetAllAsync(s => s.UserId == "system");
            return plans.OrderBy(p => p.Price).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des plans disponibles");
            return new List<Subscription>();
        }
    }

    public async Task<bool> UpgradeSubscriptionAsync(string userId, string stripePriceId)
    {
        try
        {
            // Récupérer le plan template correspondant au prix Stripe
            var planTemplate = await _unitOfWork.Subscriptions.GetAsync(s => 
                s.StripePriceId == stripePriceId && s.UserId == "system");
            
            if (planTemplate == null)
            {
                _logger.LogWarning("Plan non trouvé pour le prix Stripe {StripePriceId}", stripePriceId);
                return false;
            }

            // Désactiver l'abonnement actuel s'il existe
            var currentSubscription = await GetUserSubscriptionAsync(userId);
            if (currentSubscription != null)
            {
                currentSubscription.IsActive = false;
                await _unitOfWork.Subscriptions.UpdateAsync(currentSubscription);
            }

            // Créer le nouvel abonnement
            var newSubscription = new Subscription
            {
                PlanName = planTemplate.PlanName,
                Price = planTemplate.Price,
                MaxWebsites = planTemplate.MaxWebsites,
                MaxScansPerMonth = planTemplate.MaxScansPerMonth,
                ApiAccess = planTemplate.ApiAccess,
                BrandedReports = planTemplate.BrandedReports,
                PrioritySupport = planTemplate.PrioritySupport,
                UserId = userId,
                StripePriceId = stripePriceId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Subscriptions.AddAsync(newSubscription);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Abonnement mis à niveau pour l'utilisateur {UserId} vers le plan {PlanName}", 
                userId, planTemplate.PlanName);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à niveau de l'abonnement pour l'utilisateur {UserId}", userId);
            return false;
        }
    }

    public async Task<Subscription> GetOrCreateUserSubscriptionAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        
        if (subscription == null)
        {
            // Créer un abonnement gratuit par défaut
            var freeTemplate = await GetDefaultFreeSubscriptionAsync();
            
            subscription = new Subscription
            {
                PlanName = freeTemplate.PlanName,
                Price = freeTemplate.Price,
                MaxWebsites = freeTemplate.MaxWebsites,
                MaxScansPerMonth = freeTemplate.MaxScansPerMonth,
                ApiAccess = freeTemplate.ApiAccess,
                BrandedReports = freeTemplate.BrandedReports,
                PrioritySupport = freeTemplate.PrioritySupport,
                UserId = userId,
                StripePriceId = freeTemplate.StripePriceId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Subscriptions.AddAsync(subscription);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Abonnement gratuit créé pour l'utilisateur {UserId}", userId);
        }

        return subscription;
    }

    public async Task<bool> HasApiAccessAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId) ?? await GetDefaultFreeSubscriptionAsync();
        return subscription.ApiAccess;
    }

    public async Task<bool> HasBrandedReportsAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId) ?? await GetDefaultFreeSubscriptionAsync();
        return subscription.BrandedReports;
    }

    public async Task<(int used, int limit)> GetWebsiteUsageAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId) ?? await GetDefaultFreeSubscriptionAsync();
        var used = await _unitOfWork.Websites.CountAsync(w => w.UserId == userId && w.IsActive);
        return (used, subscription.MaxWebsites);
    }

    public async Task<(int used, int limit)> GetScanUsageAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId) ?? await GetDefaultFreeSubscriptionAsync();
        
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var used = await _unitOfWork.ScanResults.CountAsync(s => 
            s.UserId == userId && s.StartedAt >= startOfMonth);
        
        return (used, subscription.MaxScansPerMonth);
    }

    private async Task<Subscription> GetDefaultFreeSubscriptionAsync()
    {
        var freeSubscription = await _unitOfWork.Subscriptions.GetAsync(s => 
            s.UserId == "system" && s.StripePriceId == "free_plan");
            
        if (freeSubscription == null)
        {
            throw new InvalidOperationException("Plan gratuit par défaut non trouvé dans la base de données");
        }
        
        return freeSubscription;
    }

    private async Task<bool> IsUserAdminOrDeveloperAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            // Vérifier si l'utilisateur est marqué comme admin
            if (user.IsAdmin) return true;

            // Vérifier les comptes développeur spécifiques
            var developerEmails = new[] { "akhy.kays@gmail.com", "dev@compliancescannerpro.com" };
            if (developerEmails.Contains(user.Email, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification admin pour l'utilisateur {UserId}", userId);
            return false;
        }
    }
}