using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Shared.Enums;
using ComplianceScannerPro.Core.Interfaces;

namespace ComplianceScannerPro.Infrastructure.Services;

public class ScanService : IScanService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ScanService> _logger;
    private readonly IConfiguration _configuration;

    public ScanService(
        IUnitOfWork unitOfWork,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ScanService> logger,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _serviceScopeFactory = serviceScopeFactory;
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

        // Démarrer le scan en arrière-plan avec timeout et gestion d'erreurs
        _ = Task.Run(async () => 
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<ScanService>>();
            
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10)); // Timeout de 10 minutes
                await ExecuteScanAsync(scanResult.Id, cts.Token, scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                scopedLogger.LogError(ex, "❌ [SCAN-CRITICAL] Erreur critique dans la tâche de scan {ScanId}", scanResult.ScanId);
                try
                {
                    var scopedUnitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                    var failedScan = await scopedUnitOfWork.ScanResults.GetByIdAsync(scanResult.Id);
                    if (failedScan != null)
                    {
                        await UpdateScanStatus(failedScan, ScanStatus.Failed, $"Erreur critique: {ex.Message}", scopedUnitOfWork);
                    }
                }
                catch (Exception updateEx)
                {
                    scopedLogger.LogError(updateEx, "❌ [SCAN-CRITICAL] Impossible de mettre à jour le statut du scan échoué {ScanId}", scanResult.ScanId);
                }
            }
        });

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

    private async Task ExecuteScanAsync(int scanResultId, CancellationToken cancellationToken, IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<ScanService>>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var webCrawler = serviceProvider.GetRequiredService<IWebCrawlerService>();
        var accessibilityAnalyzer = serviceProvider.GetRequiredService<IAccessibilityAnalyzer>();
        
        ScanResult? scanResult = null;
        
        try
        {
            logger.LogInformation("🚀 [SCAN-START] Début ExecuteScanAsync pour scanResultId={ScanResultId}", scanResultId);
            
            scanResult = await unitOfWork.ScanResults.GetByIdAsync(scanResultId);
            if (scanResult == null)
            {
                logger.LogError("❌ [SCAN-ERROR] ScanResult {ScanResultId} non trouvé en base", scanResultId);
                return;
            }

            logger.LogInformation("✅ [SCAN-DB] ScanResult récupéré: {ScanId}, Status={Status}", scanResult.ScanId, scanResult.Status);

            var website = await unitOfWork.Websites.GetByIdAsync(scanResult.WebsiteId);
            if (website == null)
            {
                logger.LogError("❌ [SCAN-ERROR] Website {WebsiteId} non trouvé pour le scan {ScanId}", scanResult.WebsiteId, scanResult.ScanId);
                await UpdateScanStatus(scanResult, ScanStatus.Failed, "Site web non trouvé", unitOfWork);
                return;
            }

            logger.LogInformation("🌐 [SCAN-WEBSITE] Website trouvé: {WebsiteName} ({WebsiteUrl}), IsActive={IsActive}", 
                website.Name, website.Url, website.IsActive);

            if (!website.IsActive)
            {
                logger.LogWarning("⚠️ [SCAN-WARNING] Website {WebsiteId} est inactif, arrêt du scan", website.Id);
                await UpdateScanStatus(scanResult, ScanStatus.Failed, "Site web inactif", unitOfWork);
                return;
            }

            logger.LogInformation("🚀 [SCAN-START] Démarrage du scan {ScanId} pour {WebsiteUrl}", scanResult.ScanId, website.Url);

            // Mettre à jour le statut
            logger.LogInformation("📝 [SCAN-STATUS] Passage du statut à Running pour {ScanId}", scanResult.ScanId);
            await UpdateScanStatus(scanResult, ScanStatus.Running, null, unitOfWork);

            // Phase 1: Crawling
            logger.LogInformation("🕷️ [SCAN-PHASE-1] Début crawling du site {WebsiteUrl} (MaxDepth={MaxDepth}, Subdomains={IncludeSubdomains})", 
                website.Url, website.MaxDepth, website.IncludeSubdomains);
            
            var crawlStartTime = DateTime.UtcNow;
            List<string> urls;
            
            try
            {
                urls = await webCrawler.CrawlAsync(
                    website.Url, 
                    website.MaxDepth, 
                    website.IncludeSubdomains);
                
                var crawlDuration = DateTime.UtcNow - crawlStartTime;
                logger.LogInformation("✅ [SCAN-CRAWL] Crawling terminé en {Duration}ms: {UrlCount} URLs trouvées", 
                    crawlDuration.TotalMilliseconds, urls.Count);
                
                // Log des premières URLs pour debug
                var urlsToLog = urls.Take(5).ToList();
                logger.LogDebug("[SCAN-CRAWL-URLS] Premières URLs: {Urls}", string.Join(", ", urlsToLog));
            }
            catch (Exception crawlEx)
            {
                logger.LogError(crawlEx, "❌ [SCAN-CRAWL-ERROR] Erreur lors du crawling de {WebsiteUrl}", website.Url);
                await UpdateScanStatus(scanResult, ScanStatus.Failed, $"Erreur crawling: {crawlEx.Message}", unitOfWork);
                return;
            }

            if (!urls.Any())
            {
                logger.LogWarning("⚠️ [SCAN-CRAWL-EMPTY] Aucune page accessible trouvée pour {WebsiteUrl}", website.Url);
                await UpdateScanStatus(scanResult, ScanStatus.Failed, "Aucune page accessible trouvée", unitOfWork);
                return;
            }

            // Phase 2: Analyse d'accessibilité
            logger.LogInformation("🔍 [SCAN-PHASE-2] Début analyse d'accessibilité - {UrlCount} pages à analyser", urls.Count);
            
            var allIssues = new List<AccessibilityIssue>();
            var pagesAnalyzed = 0;
            var maxPagesToAnalyze = Math.Min(urls.Count, 50); // Limiter à 50 pages pour éviter les timeouts
            var analysisStartTime = DateTime.UtcNow;

            logger.LogInformation("📊 [SCAN-ANALYSIS] Analyse limitée à {MaxPages} pages sur {TotalPages} trouvées", 
                maxPagesToAnalyze, urls.Count);

            foreach (var url in urls.Take(maxPagesToAnalyze))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning("⏹️ [SCAN-CANCELLED] Scan annulé par timeout pour {ScanId}", scanResult.ScanId);
                    await UpdateScanStatus(scanResult, ScanStatus.Failed, "Scan annulé par timeout", unitOfWork);
                    return;
                }

                try
                {
                    logger.LogDebug("🔍 [SCAN-PAGE] Analyse de la page {PageNumber}/{MaxPages}: {Url}", 
                        pagesAnalyzed + 1, maxPagesToAnalyze, url);
                    
                    var pageStartTime = DateTime.UtcNow;
                    var content = await webCrawler.GetPageContentAsync(url);
                    var getContentDuration = DateTime.UtcNow - pageStartTime;
                    
                    logger.LogDebug("📄 [SCAN-CONTENT] Contenu récupéré en {Duration}ms pour {Url} ({ContentLength} chars)", 
                        getContentDuration.TotalMilliseconds, url, content?.Length ?? 0);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        logger.LogWarning("⚠️ [SCAN-CONTENT-EMPTY] Contenu vide pour {Url}", url);
                        pagesAnalyzed++;
                        continue;
                    }

                    var analyzeStartTime = DateTime.UtcNow;
                    var issues = await accessibilityAnalyzer.AnalyzePageAsync(url, content);
                    var analyzeDuration = DateTime.UtcNow - analyzeStartTime;
                    
                    logger.LogDebug("✅ [SCAN-ANALYZE] Page analysée en {Duration}ms: {Url} - {IssueCount} problèmes trouvés", 
                        analyzeDuration.TotalMilliseconds, url, issues.Count);
                    
                    // Associer les problèmes au scan
                    foreach (var issue in issues)
                    {
                        issue.ScanResultId = scanResult.Id;
                        allIssues.Add(issue);
                    }

                    pagesAnalyzed++;
                    
                    // Mettre à jour le progrès périodiquement
                    if (pagesAnalyzed % 5 == 0 || pagesAnalyzed == maxPagesToAnalyze)
                    {
                        scanResult.PagesScanned = pagesAnalyzed;
                        await unitOfWork.SaveChangesAsync();
                        
                        var progressPercent = (pagesAnalyzed * 100) / maxPagesToAnalyze;
                        logger.LogInformation("📈 [SCAN-PROGRESS] Progrès: {PagesAnalyzed}/{MaxPages} pages ({ProgressPercent}%) - {TotalIssues} problèmes trouvés", 
                            pagesAnalyzed, maxPagesToAnalyze, progressPercent, allIssues.Count);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "⚠️ [SCAN-PAGE-ERROR] Erreur lors de l'analyse de {Url}: {ErrorMessage}", url, ex.Message);
                    pagesAnalyzed++; // Compter même en cas d'erreur pour éviter un blocage
                    // Continuer avec les autres pages
                }
            }

            var totalAnalysisDuration = DateTime.UtcNow - analysisStartTime;
            logger.LogInformation("✅ [SCAN-ANALYSIS-COMPLETE] Analyse terminée en {Duration}s: {PagesAnalyzed} pages, {TotalIssues} problèmes", 
                totalAnalysisDuration.TotalSeconds, pagesAnalyzed, allIssues.Count);

            // Sauvegarder tous les problèmes
            foreach (var issue in allIssues)
            {
                await unitOfWork.AccessibilityIssues.AddAsync(issue);
            }

            // Phase 3: Calcul du score
            logger.LogInformation("🧮 [SCAN-PHASE-3] Calcul du score");
            
            var score = await accessibilityAnalyzer.CalculateScoreAsync(allIssues, pagesAnalyzed);
            var grade = await accessibilityAnalyzer.GetGradeFromScoreAsync(score);

            // Mettre à jour les résultats
            scanResult.PagesScanned = pagesAnalyzed;
            scanResult.Score = score;
            scanResult.Grade = grade;
            scanResult.TotalIssues = allIssues.Count;
            scanResult.CriticalIssues = allIssues.Count(i => i.Severity == IssueSeverity.Critical);
            scanResult.WarningIssues = allIssues.Count(i => i.Severity == IssueSeverity.Warning);
            scanResult.InfoIssues = allIssues.Count(i => i.Severity == IssueSeverity.Info);
            scanResult.CompletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
            scanResult.Status = ScanStatus.Completed;

            // Mettre à jour la date du dernier scan du site
            website.LastScanAt = DateTime.UtcNow;
            await unitOfWork.Websites.UpdateAsync(website);

            await unitOfWork.SaveChangesAsync();

            logger.LogInformation("🏁 [SCAN-COMPLETE] Scan {ScanId} terminé avec succès. Score: {Score}/100, Grade: {Grade}", 
                scanResult.ScanId, score, grade);

            // Phase 4: Génération du rapport PDF (en arrière-plan)
            _ = Task.Run(async () =>
            {
                try
                {
                    logger.LogInformation("📄 [SCAN-PDF] Génération du rapport PDF pour le scan {ScanId}", scanResult.ScanId);
                    
                    // Le rapport sera généré à la demande ou lors du premier accès
                    // pour optimiser les performances
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "❌ [SCAN-PDF-ERROR] Erreur lors de la génération du rapport pour le scan {ScanId}", scanResult.ScanId);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ [SCAN-EXECUTION-ERROR] Erreur lors de l'exécution du scan {ScanId}", scanResult?.ScanId ?? "inconnu");
            
            if (scanResult != null)
            {
                await UpdateScanStatus(scanResult, ScanStatus.Failed, $"Erreur interne: {ex.Message}", unitOfWork);
            }
        }
    }

    private async Task UpdateScanStatus(ScanResult scanResult, ScanStatus status, string? errorMessage, IUnitOfWork unitOfWork)
    {
        scanResult.Status = status;
        
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            scanResult.ErrorMessage = errorMessage;
        }

        if (status == ScanStatus.Failed || status == ScanStatus.Completed)
        {
            scanResult.CompletedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        }

        await unitOfWork.ScanResults.UpdateAsync(scanResult);
        await unitOfWork.SaveChangesAsync();
    }
}