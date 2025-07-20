namespace ComplianceScannerPro.Core.Interfaces;

public interface IWebCrawlerService
{
    Task<List<string>> CrawlAsync(string url, int maxDepth = 3, bool includeSubdomains = false);
    Task<string> GetPageContentAsync(string url);
    Task<bool> IsUrlAccessibleAsync(string url);
}