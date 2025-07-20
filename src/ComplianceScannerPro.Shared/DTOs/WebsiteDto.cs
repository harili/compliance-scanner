using System.ComponentModel.DataAnnotations;

namespace ComplianceScannerPro.Shared.DTOs;

public class WebsiteDto
{
    public int Id { get; set; }
    
    [Required]
    [Url]
    [StringLength(2000)]
    public string Url { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? LastScanAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    [Range(1, 5)]
    public int MaxDepth { get; set; } = 3;
    
    public bool IncludeSubdomains { get; set; } = false;
    public string UserId { get; set; } = string.Empty;
}

public class CreateWebsiteDto
{
    [Required]
    [Url]
    [StringLength(2000)]
    public string Url { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Range(1, 5)]
    public int MaxDepth { get; set; } = 3;
    
    public bool IncludeSubdomains { get; set; } = false;
}

public class UpdateWebsiteDto
{
    [StringLength(200)]
    public string? Name { get; set; }
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [Range(1, 5)]
    public int? MaxDepth { get; set; }
    
    public bool? IncludeSubdomains { get; set; }
    public bool? IsActive { get; set; }
}