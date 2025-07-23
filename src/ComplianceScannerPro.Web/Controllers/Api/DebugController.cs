using Microsoft.AspNetCore.Mvc;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Shared.DTOs;
using System.Diagnostics;

namespace ComplianceScannerPro.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class DebugController : ControllerBase
{
    private readonly IWebCrawlerService _webCrawler;
    private readonly IAccessibilityAnalyzer _analyzer;
    private readonly ILogger<DebugController> _logger;

    public DebugController(IWebCrawlerService webCrawler, IAccessibilityAnalyzer analyzer, ILogger<DebugController> logger)
    {
        _webCrawler = webCrawler;
        _analyzer = analyzer;
        _logger = logger;
    }

    /// <summary>
    /// Test du service WebCrawler
    /// </summary>
    [HttpPost("test-crawler")]
    public async Task<ActionResult<ApiResponse<object>>> TestCrawler([FromBody] TestUrlRequest request)
    {
        try
        {
            _logger.LogInformation("🧪 [DEBUG] Test WebCrawler pour {Url}", request.Url);
            
            var stopwatch = Stopwatch.StartNew();
            
            // Test d'accessibilité de l'URL
            var isAccessible = await _webCrawler.IsUrlAccessibleAsync(request.Url);
            if (!isAccessible)
            {
                return BadRequest(ApiResponse<object>.ErrorResult("URL non accessible"));
            }
            
            // Test de crawling
            var urls = await _webCrawler.CrawlAsync(request.Url, 2, false);
            
            stopwatch.Stop();
            
            var result = new
            {
                url = request.Url,
                isAccessible = isAccessible,
                urlCount = urls.Count,
                duration = stopwatch.ElapsedMilliseconds,
                urls = urls.Take(10).ToList() // Premiers 10 pour debug
            };
            
            _logger.LogInformation("✅ [DEBUG] WebCrawler OK: {UrlCount} URLs en {Duration}ms", urls.Count, stopwatch.ElapsedMilliseconds);
            
            return Ok(ApiResponse<object>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DEBUG] Erreur WebCrawler pour {Url}", request.Url);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Erreur WebCrawler: {ex.Message}"));
        }
    }

    /// <summary>
    /// Test du service AccessibilityAnalyzer
    /// </summary>
    [HttpPost("test-analyzer")]
    public async Task<ActionResult<ApiResponse<object>>> TestAnalyzer([FromBody] TestUrlRequest request)
    {
        try
        {
            _logger.LogInformation("🧪 [DEBUG] Test AccessibilityAnalyzer pour {Url}", request.Url);
            
            var stopwatch = Stopwatch.StartNew();
            
            // Récupérer le contenu de la page
            var content = await _webCrawler.GetPageContentAsync(request.Url);
            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(ApiResponse<object>.ErrorResult("Impossible de récupérer le contenu de la page"));
            }
            
            // Analyser l'accessibilité
            var issues = await _analyzer.AnalyzePageAsync(request.Url, content);
            
            stopwatch.Stop();
            
            var result = new
            {
                url = request.Url,
                contentLength = content.Length,
                issueCount = issues.Count,
                duration = stopwatch.ElapsedMilliseconds,
                issuesSummary = issues.GroupBy(i => i.Severity)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                sampleIssues = issues.Take(5).Select(i => new
                {
                    rgaaRule = i.RgaaRule,
                    title = i.Title,
                    severity = i.Severity.ToString(),
                    elementSelector = i.ElementSelector,
                    description = i.Description?.Length > 100 ? i.Description.Substring(0, 100) + "..." : i.Description
                }).ToList()
            };
            
            _logger.LogInformation("✅ [DEBUG] AccessibilityAnalyzer OK: {IssueCount} problèmes en {Duration}ms", issues.Count, stopwatch.ElapsedMilliseconds);
            
            return Ok(ApiResponse<object>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DEBUG] Erreur AccessibilityAnalyzer pour {Url}", request.Url);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Erreur AccessibilityAnalyzer: {ex.Message}"));
        }
    }

    /// <summary>
    /// Test complet du pipeline de scan
    /// </summary>
    [HttpPost("test-full-pipeline")]
    public async Task<ActionResult<ApiResponse<object>>> TestFullPipeline([FromBody] TestUrlRequest request)
    {
        try
        {
            _logger.LogInformation("🧪 [DEBUG] Test complet du pipeline pour {Url}", request.Url);
            
            var overallStopwatch = Stopwatch.StartNew();
            var steps = new List<object>();
            
            // Étape 1: Test d'accessibilité
            var step1Stopwatch = Stopwatch.StartNew();
            var isAccessible = await _webCrawler.IsUrlAccessibleAsync(request.Url);
            step1Stopwatch.Stop();
            
            steps.Add(new
            {
                step = "1_accessibility_check",
                duration = step1Stopwatch.ElapsedMilliseconds,
                success = isAccessible,
                details = isAccessible ? "URL accessible" : "URL non accessible"
            });
            
            if (!isAccessible)
            {
                overallStopwatch.Stop();
                return BadRequest(ApiResponse<object>.ErrorResult("URL non accessible", new { steps, totalDuration = overallStopwatch.ElapsedMilliseconds }));
            }
            
            // Étape 2: Crawling
            var step2Stopwatch = Stopwatch.StartNew();
            var urls = await _webCrawler.CrawlAsync(request.Url, 1, false); // Limiter à depth 1 pour le test
            step2Stopwatch.Stop();
            
            steps.Add(new
            {
                step = "2_crawling",
                duration = step2Stopwatch.ElapsedMilliseconds,
                success = urls.Count > 0,
                details = $"{urls.Count} URLs trouvées"
            });
            
            // Étape 3: Analyse d'accessibilité sur la première URL
            var step3Stopwatch = Stopwatch.StartNew();
            var targetUrl = urls.FirstOrDefault() ?? request.Url;
            var content = await _webCrawler.GetPageContentAsync(targetUrl);
            var issues = await _analyzer.AnalyzePageAsync(targetUrl, content);
            step3Stopwatch.Stop();
            
            steps.Add(new
            {
                step = "3_accessibility_analysis",
                duration = step3Stopwatch.ElapsedMilliseconds,
                success = true,
                details = $"{issues.Count} problèmes trouvés sur {targetUrl}"
            });
            
            overallStopwatch.Stop();
            
            var result = new
            {
                url = request.Url,
                totalDuration = overallStopwatch.ElapsedMilliseconds,
                steps = steps,
                summary = new
                {
                    urlsFound = urls.Count,
                    issuesFound = issues.Count,
                    contentLength = content?.Length ?? 0
                }
            };
            
            _logger.LogInformation("✅ [DEBUG] Pipeline complet OK en {Duration}ms", overallStopwatch.ElapsedMilliseconds);
            
            return Ok(ApiResponse<object>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DEBUG] Erreur pipeline complet pour {Url}", request.Url);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Erreur pipeline: {ex.Message}"));
        }
    }

    /// <summary>
    /// Récupère les informations détaillées d'un scan pour debug
    /// </summary>
    [HttpGet("scan-details/{scanId}")]
    public async Task<ActionResult<ApiResponse<object>>> GetScanDetails(string scanId)
    {
        try
        {
            _logger.LogInformation("🧪 [DEBUG] Récupération détails scan {ScanId}", scanId);
            
            // Récupérer le scan
            var scan = await HttpContext.RequestServices.GetRequiredService<IUnitOfWork>()
                .ScanResults.GetAsync(s => s.ScanId == scanId);
            
            if (scan == null)
            {
                return NotFound(ApiResponse<object>.ErrorResult("Scan non trouvé"));
            }
            
            // Récupérer le website associé
            var website = await HttpContext.RequestServices.GetRequiredService<IUnitOfWork>()
                .Websites.GetByIdAsync(scan.WebsiteId);
            
            // Récupérer les problèmes trouvés
            var issues = await HttpContext.RequestServices.GetRequiredService<IUnitOfWork>()
                .AccessibilityIssues.GetAllAsync(i => i.ScanResultId == scan.Id);
            
            var result = new
            {
                scan = new
                {
                    id = scan.Id,
                    scanId = scan.ScanId,
                    status = scan.Status.ToString(),
                    startedAt = scan.StartedAt,
                    completedAt = scan.CompletedAt,
                    pagesScanned = scan.PagesScanned,
                    totalIssues = scan.TotalIssues,
                    score = scan.Score,
                    grade = scan.Grade.ToString(),
                    errorMessage = scan.ErrorMessage
                },
                website = website != null ? new
                {
                    id = website.Id,
                    name = website.Name,
                    url = website.Url,
                    isActive = website.IsActive,
                    maxDepth = website.MaxDepth,
                    includeSubdomains = website.IncludeSubdomains
                } : null,
                issues = issues.Take(10).Select(i => new
                {
                    rgaaRule = i.RgaaRule,
                    title = i.Title,
                    severity = i.Severity.ToString(),
                    elementSelector = i.ElementSelector,
                    description = i.Description?.Length > 100 ? i.Description.Substring(0, 100) + "..." : i.Description,
                    pageUrl = i.PageUrl
                }).ToList(),
                statistics = new
                {
                    totalIssues = issues.Count,
                    issuesBySeverity = issues.GroupBy(i => i.Severity).ToDictionary(g => g.Key.ToString(), g => g.Count()),
                    issuesByRule = issues.GroupBy(i => i.RgaaRule).ToDictionary(g => g.Key, g => g.Count())
                }
            };
            
            return Ok(ApiResponse<object>.SuccessResult(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [DEBUG] Erreur récupération détails scan {ScanId}", scanId);
            return StatusCode(500, ApiResponse<object>.ErrorResult($"Erreur: {ex.Message}"));
        }
    }
}

public class TestUrlRequest
{
    public string Url { get; set; } = string.Empty;
}