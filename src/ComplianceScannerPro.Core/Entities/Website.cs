namespace ComplianceScannerPro.Core.Entities;

public class Website
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastScanAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int MaxDepth { get; set; } = 3;
    public bool IncludeSubdomains { get; set; } = false;
    
    public string UserId { get; set; } = string.Empty;
    
    public ICollection<ScanResult> ScanResults { get; set; } = new List<ScanResult>();
}