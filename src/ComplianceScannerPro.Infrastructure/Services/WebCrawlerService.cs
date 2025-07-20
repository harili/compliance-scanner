using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ComplianceScannerPro.Core.Interfaces;

namespace ComplianceScannerPro.Infrastructure.Services;

public class WebCrawlerService : IWebCrawlerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebCrawlerService> _logger;
    private readonly HashSet<string> _crawledUrls = new();

    public WebCrawlerService(HttpClient httpClient, ILogger<WebCrawlerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "ComplianceScannerPro/1.0 (Accessibility Scanner)");
    }

    public async Task<List<string>> CrawlAsync(string url, int maxDepth = 3, bool includeSubdomains = false)
    {
        var urls = new List<string>();
        var urlsToProcess = new Queue<(string Url, int Depth)>();
        var baseUri = new Uri(url);
        
        urlsToProcess.Enqueue((url, 0));
        _crawledUrls.Clear();

        while (urlsToProcess.Count > 0 && urls.Count < 100) // Limite sécurité
        {
            var (currentUrl, depth) = urlsToProcess.Dequeue();
            
            if (depth > maxDepth || _crawledUrls.Contains(currentUrl))
                continue;

            try
            {
                var isAccessible = await IsUrlAccessibleAsync(currentUrl);
                if (!isAccessible)
                    continue;

                urls.Add(currentUrl);
                _crawledUrls.Add(currentUrl);

                if (depth < maxDepth)
                {
                    var content = await GetPageContentAsync(currentUrl);
                    var links = ExtractLinks(content, baseUri, includeSubdomains);
                    
                    foreach (var link in links.Take(10)) // Limite par page
                    {
                        if (!_crawledUrls.Contains(link))
                        {
                            urlsToProcess.Enqueue((link, depth + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erreur lors du crawling de {Url}", currentUrl);
            }
        }

        _logger.LogInformation("Crawling terminé: {Count} URLs trouvées", urls.Count);
        return urls;
    }

    public async Task<string> GetPageContentAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du contenu de {Url}", url);
            throw;
        }
    }

    public async Task<bool> IsUrlAccessibleAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private List<string> ExtractLinks(string html, Uri baseUri, bool includeSubdomains)
    {
        var links = new List<string>();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (linkNodes == null) return links;

        foreach (var linkNode in linkNodes)
        {
            var href = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href)) continue;

            try
            {
                var uri = new Uri(baseUri, href);
                var normalizedUrl = $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";

                // Vérifier si c'est le même domaine ou sous-domaine autorisé
                if (includeSubdomains)
                {
                    if (!uri.Host.EndsWith(baseUri.Host.TrimStart('w', 'w', 'w', '.')))
                        continue;
                }
                else
                {
                    if (uri.Host != baseUri.Host)
                        continue;
                }

                // Exclure certains types de fichiers
                var extension = Path.GetExtension(uri.AbsolutePath).ToLower();
                var excludedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".css", ".js", ".xml" };
                if (excludedExtensions.Contains(extension))
                    continue;

                if (!links.Contains(normalizedUrl))
                {
                    links.Add(normalizedUrl);
                }
            }
            catch (UriFormatException)
            {
                // Ignorer les URLs mal formées
            }
        }

        return links;
    }
}