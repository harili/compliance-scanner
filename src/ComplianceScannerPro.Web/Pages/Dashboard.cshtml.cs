using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.Enums;

namespace ComplianceScannerPro.Web.Pages;

[Authorize]
public class DashboardModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;

    public DashboardModel(
        IUnitOfWork unitOfWork, 
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
    }

    // Métriques principales
    public int TotalWebsites { get; set; }
    public int TotalScans { get; set; }
    public int TotalIssues { get; set; }
    public decimal AverageScore { get; set; }

    // Informations d'abonnement
    public string SubscriptionPlan { get; set; } = "Gratuit";
    public int ScansThisMonth { get; set; }
    public int MaxScansPerMonth { get; set; }
    public int MaxWebsites { get; set; }
    public decimal ScansUsagePercentage => MaxScansPerMonth > 0 ? (ScansThisMonth * 100.0m) / MaxScansPerMonth : 0;
    public decimal WebsitesUsagePercentage => MaxWebsites > 0 ? (TotalWebsites * 100.0m) / MaxWebsites : 0;

    // Données pour graphiques et listes
    public List<WebsiteScoreDto> TopWebsitesByScore { get; set; } = new();
    public List<RecentScanDto> RecentScans { get; set; } = new();
    public List<ScoreHistoryDto> ScoreHistory { get; set; } = new();

    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return;

        await LoadDashboardData(user.Id);
    }

    private async Task LoadDashboardData(string userId)
    {
        // Charger les métriques principales
        var websites = await _unitOfWork.Websites
            .GetAllAsync(w => w.UserId == userId);
        
        TotalWebsites = websites.Count();

        var scans = await _unitOfWork.ScanResults
            .GetAllAsync(s => websites.Select(w => w.Id).Contains(s.WebsiteId));
        
        TotalScans = scans.Count();

        var completedScans = scans.Where(s => s.Status == ScanStatus.Completed && s.Score > 0);
        
        if (completedScans.Any())
        {
            AverageScore = (decimal)completedScans.Average(s => s.Score);
            
            // Calculer le total des problèmes détectés
            var scanIds = completedScans.Select(s => s.Id);
            var issues = await _unitOfWork.AccessibilityIssues
                .GetAllAsync(i => scanIds.Contains(i.ScanResultId));
            TotalIssues = issues.Count();
        }

        // Charger les informations d'abonnement
        await LoadSubscriptionData(userId);

        // Charger les top sites par score
        await LoadTopWebsitesByScore(websites, scans);

        // Charger les scans récents
        await LoadRecentScans(scans, websites);

        // Charger l'historique des scores pour le graphique
        LoadScoreHistory(completedScans);
    }

    private async Task LoadSubscriptionData(string userId)
    {
        var subscription = await _subscriptionService.GetUserSubscriptionAsync(userId);
        
        if (subscription != null)
        {
            SubscriptionPlan = subscription.PlanName;
            MaxScansPerMonth = subscription.MaxScansPerMonth;
            MaxWebsites = subscription.MaxWebsites;
        }
        else
        {
            // Plan gratuit par défaut
            SubscriptionPlan = "Gratuit";
            MaxScansPerMonth = 10;
            MaxWebsites = 3;
        }

        // Calculer l'utilisation ce mois
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var scansThisMonth = await _unitOfWork.ScanResults
            .GetAllAsync(s => s.StartedAt >= startOfMonth);
        ScansThisMonth = scansThisMonth.Count();
    }

    private async Task LoadTopWebsitesByScore(
        IEnumerable<ComplianceScannerPro.Core.Entities.Website> websites,
        IEnumerable<ComplianceScannerPro.Core.Entities.ScanResult> scans)
    {
        var topWebsites = websites
            .Select(w => new
            {
                Website = w,
                LastScan = scans.Where(s => s.WebsiteId == w.Id && s.Status == ScanStatus.Completed && s.Score > 0)
                              .OrderByDescending(s => s.CompletedAt)
                              .FirstOrDefault()
            })
            .Where(x => x.LastScan != null)
            .OrderByDescending(x => x.LastScan!.Score)
            .Take(5)
            .Select(x => new WebsiteScoreDto
            {
                Id = x.Website.Id,
                Name = x.Website.Name,
                Url = x.Website.Url,
                LastScore = x.LastScan!.Score
            })
            .ToList();

        TopWebsitesByScore = topWebsites;
    }

    private async Task LoadRecentScans(
        IEnumerable<ComplianceScannerPro.Core.Entities.ScanResult> scans,
        IEnumerable<ComplianceScannerPro.Core.Entities.Website> websites)
    {
        var recentScans = scans
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .Select(scan => new RecentScanDto
            {
                Id = scan.Id,
                WebsiteName = websites.FirstOrDefault(w => w.Id == scan.WebsiteId)?.Name ?? "Site inconnu",
                WebsiteUrl = websites.FirstOrDefault(w => w.Id == scan.WebsiteId)?.Url ?? "",
                Status = scan.Status,
                AccessibilityScore = scan.Score,
                CompletedAt = scan.CompletedAt,
                IssuesCount = 0 // À calculer séparément pour les performances
            })
            .ToList();

        // Calculer le nombre de problèmes pour chaque scan
        foreach (var scan in recentScans)
        {
            var issues = await _unitOfWork.AccessibilityIssues
                .GetAllAsync(i => i.ScanResultId == scan.Id);
            scan.IssuesCount = issues.Count();
        }

        RecentScans = recentScans;
    }

    private void LoadScoreHistory(IEnumerable<ComplianceScannerPro.Core.Entities.ScanResult> completedScans)
    {
        // Grouper par jour sur les 30 derniers jours
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        
        var scoresByDay = completedScans
            .Where(s => s.CompletedAt >= thirtyDaysAgo)
            .GroupBy(s => s.CompletedAt!.Value.Date)
            .Select(g => new ScoreHistoryDto
            {
                Date = g.Key,
                AverageScore = (decimal)g.Average(s => s.Score)
            })
            .OrderBy(x => x.Date)
            .ToList();

        // S'assurer qu'on a au moins quelques points de données
        if (!scoresByDay.Any())
        {
            scoresByDay.Add(new ScoreHistoryDto { Date = DateTime.UtcNow.Date, AverageScore = 0 });
        }

        ScoreHistory = scoresByDay;
    }
}

// DTOs pour le dashboard
public class WebsiteScoreDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public int LastScore { get; set; }
}

public class RecentScanDto
{
    public int Id { get; set; }
    public string WebsiteName { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public ScanStatus Status { get; set; }
    public int? AccessibilityScore { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int IssuesCount { get; set; }
}

public class ScoreHistoryDto
{
    public DateTime Date { get; set; }
    public decimal AverageScore { get; set; }
}