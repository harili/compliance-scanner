using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Shared.Enums;
using ComplianceScannerPro.Core.Interfaces;

namespace ComplianceScannerPro.Infrastructure.Services;

public class ScanService : IScanService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWebCrawlerService _webCrawler;
    private readonly IAccessibilityAnalyzer _accessibilityAnalyzer;
    private readonly IReportGenerator _reportGenerator;
    private readonly ILogger<ScanService> _logger;
    private readonly IConfiguration _configuration;

    public ScanService(
        IUnitOfWork unitOfWork,
        IWebCrawlerService webCrawler,
        IAccessibilityAnalyzer accessibilityAnalyzer,
        IReportGenerator reportGenerator,
        ILogger<ScanService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _webCrawler = webCrawler;
        _accessibilityAnalyzer = accessibilityAnalyzer;
        _reportGenerator = reportGenerator;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ScanResult> StartScanAsync(int websiteId, string userId)
    {
        var website = await _unitOfWork.Websites.GetByIdAsync(websiteId);
        if (website == null)
            throw new ArgumentException("Site web non trouvé", nameof(websiteId));

        if (website.UserId != userId)
            throw new UnauthorizedAccessException("Accès non autorisé au site web");

        var scanResult = new ScanResult
        {
            ScanId = Guid.NewGuid().ToString(),
            WebsiteId = websiteId,
            Website = website,
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            Status = ScanStatus.Pending
        };

        await _unitOfWork.ScanResults.AddAsync(scanResult);
        await _unitOfWork.SaveChangesAsync();

        // Démarrer le scan en arrière-plan
        _ = Task.Run(async () => await ExecuteScanAsync(scanResult.Id));

        _logger.LogInformation("Scan {ScanId} créé pour le site {WebsiteId}", scanResult.ScanId, websiteId);
        
        return scanResult;
    }

    public async Task<ScanResult?> GetScanResultAsync(string scanId)
    {
        return await _unitOfWork.ScanResults.GetAsync(s => s.ScanId == scanId);
    }

    public async Task<List<ScanResult>> GetUserScanHistoryAsync(string userId, int take = 10)
    {
        var scans = await _unitOfWork.ScanResults.GetAllAsync(s => s.UserId == userId);
        return scans.OrderByDescending(s => s.StartedAt).Take(take).ToList();
    }

    public async Task<bool> CanUserStartScanAsync(string userId)
    {
        // Vérifier les scans en cours
        var runningScans = await _unitOfWork.ScanResults.GetAllAsync(s => 
            s.UserId == userId && 
            (s.Status == ScanStatus.Pending || s.Status == ScanStatus.Running));

        var maxConcurrentScans = int.Parse(_configuration["ScanSettings:MaxConcurrentScans"] ?? "2");
        
        if (runningScans.Count >= maxConcurrentScans)
        {
            _logger.LogWarning("Utilisateur {UserId} a atteint la limite de scans simultanés", userId);
            return false;
        }

        // Vérifier les quotas d'abonnement (sera implémenté avec SubscriptionService)
        return true;
    }

    private async Task ExecuteScanAsync(int scanResultId)
    {
        ScanResult? scanResult = null;
        
        try
        {
            scanResult = await _unitOfWork.ScanResults.GetByIdAsync(scanResultId);
            if (scanResult == null)
            {
                _logger.LogError("ScanResult {ScanResultId} non trouvé", scanResultId);
                return;
            }

            var website = await _unitOfWork.Websites.GetByIdAsync(scanResult.WebsiteId);
            if (website == null)
            {
                await UpdateScanStatus(scanResult, ScanStatus.Failed, "Site web non trouvé");
                return;
            }

            _logger.LogInformation("Démarrage du scan {ScanId} pour {WebsiteUrl}", scanResult.ScanId, website.Url);

            // Mettre à jour le statut
            await UpdateScanStatus(scanResult, ScanStatus.Running);

            // Phase 1: Crawling
            _logger.LogInformation("Phase 1: Crawling du site {WebsiteUrl}", website.Url);
            
            var urls = await _webCrawler.CrawlAsync(
                website.Url, 
                website.MaxDepth, 
                website.IncludeSubdomains);

            if (!urls.Any())
            {
                await UpdateScanStatus(scanResult, ScanStatus.Failed, "Aucune page accessible trouvée");
                return;
            }

            _logger.LogInformation("Crawling terminé: {UrlCount} URLs trouvées", urls.Count);

            // Phase 2: Analyse d'accessibilité
            _logger.LogInformation("Phase 2: Analyse d'accessibilité");
            
            var allIssues = new List<AccessibilityIssue>();
            var pagesAnalyzed = 0;

            foreach (var url in urls.Take(50)) // Limiter à 50 pages pour éviter les timeouts
            {
                try
                {
                    var content = await _webCrawler.GetPageContentAsync(url);
                    var issues = await _accessibilityAnalyzer.AnalyzePageAsync(url, content);
                    
                    // Associer les problèmes au scan
                    foreach (var issue in issues)
                    {
                        issue.ScanResultId = scanResult.Id;
                        allIssues.Add(issue);
                    }

                    pagesAnalyzed++;
                    
                    // Mettre à jour le progrès périodiquement
                    if (pagesAnalyzed % 5 == 0)
                    {
                        scanResult.PagesScanned = pagesAnalyzed;
                        await _unitOfWork.SaveChangesAsync();
                    }

                    _logger.LogDebug("Page analysée: {Url} - {IssueCount} problèmes", url, issues.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Erreur lors de l'analyse de {Url}", url);
                    // Continuer avec les autres pages
                }
            }

            // Sauvegarder tous les problèmes
            foreach (var issue in allIssues)
            {
                await _unitOfWork.AccessibilityIssues.AddAsync(issue);
            }

            // Phase 3: Calcul du score
            _logger.LogInformation("Phase 3: Calcul du score");
            
            var score = await _accessibilityAnalyzer.CalculateScoreAsync(allIssues, pagesAnalyzed);
            var grade = await _accessibilityAnalyzer.GetGradeFromScoreAsync(score);

            // Mettre à jour les résultats
            scanResult.PagesScanned = pagesAnalyzed;
            scanResult.Score = score;
            scanResult.Grade = grade;
            scanResult.TotalIssues = allIssues.Count;
            scanResult.CriticalIssues = allIssues.Count(i => i.Severity == IssueSeverity.Critical);
            scanResult.WarningIssues = allIssues.Count(i => i.Severity == IssueSeverity.Warning);
            scanResult.InfoIssues = allIssues.Count(i => i.Severity == IssueSeverity.Info);
            scanResult.CompletedAt = DateTime.UtcNow;
            scanResult.Status = ScanStatus.Completed;

            // Mettre à jour la date du dernier scan du site
            website.LastScanAt = DateTime.UtcNow;
            await _unitOfWork.Websites.UpdateAsync(website);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Scan {ScanId} terminé avec succès. Score: {Score}/100, Grade: {Grade}", 
                scanResult.ScanId, score, grade);

            // Phase 4: Génération du rapport PDF (en arrière-plan)
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation("Génération du rapport PDF pour le scan {ScanId}", scanResult.ScanId);
                    
                    // Le rapport sera généré à la demande ou lors du premier accès
                    // pour optimiser les performances
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors de la génération du rapport pour le scan {ScanId}", scanResult.ScanId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'exécution du scan {ScanId}", scanResult?.ScanId ?? "inconnu");
            
            if (scanResult != null)
            {
                await UpdateScanStatus(scanResult, ScanStatus.Failed, $"Erreur interne: {ex.Message}");
            }
        }
    }

    private async Task UpdateScanStatus(ScanResult scanResult, ScanStatus status, string? errorMessage = null)
    {
        scanResult.Status = status;
        
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            scanResult.ErrorMessage = errorMessage;
        }

        if (status == ScanStatus.Failed || status == ScanStatus.Completed)
        {
            scanResult.CompletedAt = DateTime.UtcNow;
        }

        await _unitOfWork.ScanResults.UpdateAsync(scanResult);
        await _unitOfWork.SaveChangesAsync();
    }
}