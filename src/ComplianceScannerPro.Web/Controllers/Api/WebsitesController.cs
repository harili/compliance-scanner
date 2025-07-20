using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Core.Interfaces;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;

namespace ComplianceScannerPro.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class WebsitesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<WebsitesController> _logger;

    public WebsitesController(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ISubscriptionService subscriptionService,
        ILogger<WebsitesController> logger)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    /// <summary>
    /// Récupère tous les sites web de l'utilisateur connecté
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<WebsiteDto>>>> GetWebsites(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<PaginatedResponse<WebsiteDto>>.ErrorResult("Utilisateur non authentifié"));

            var websites = await _unitOfWork.Websites.GetAllAsync(w => w.UserId == userId);
            
            if (!string.IsNullOrWhiteSpace(search))
            {
                websites = websites.Where(w => 
                    w.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    w.Url.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var totalCount = websites.Count;
            var paginatedWebsites = websites
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            var response = new PaginatedResponse<WebsiteDto>
            {
                Items = paginatedWebsites,
                TotalCount = totalCount,
                PageNumber = page,
                PageSize = pageSize
            };

            return Ok(ApiResponse<PaginatedResponse<WebsiteDto>>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des sites web pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            return StatusCode(500, ApiResponse<PaginatedResponse<WebsiteDto>>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Récupère un site web par son ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> GetWebsite(int id)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var website = await _unitOfWork.Websites.GetAsync(w => w.Id == id && w.UserId == userId);

            if (website == null)
                return NotFound(ApiResponse<WebsiteDto>.ErrorResult("Site web non trouvé"));

            return Ok(ApiResponse<WebsiteDto>.SuccessResult(MapToDto(website)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du site web {WebsiteId}", id);
            return StatusCode(500, ApiResponse<WebsiteDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Crée un nouveau site web
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> CreateWebsite([FromBody] CreateWebsiteDto createDto)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<WebsiteDto>.ErrorResult("Utilisateur non authentifié"));

            // Vérifier les limitations d'abonnement
            var canAdd = await _subscriptionService.CanUserAddWebsiteAsync(userId);
            if (!canAdd)
                return BadRequest(ApiResponse<WebsiteDto>.ErrorResult("Limite de sites web atteinte pour votre abonnement"));

            // Vérifier si l'URL n'existe pas déjà
            var existingWebsite = await _unitOfWork.Websites.GetAsync(w => w.Url == createDto.Url && w.UserId == userId);
            if (existingWebsite != null)
                return BadRequest(ApiResponse<WebsiteDto>.ErrorResult("Cette URL existe déjà dans vos sites web"));

            var website = new Website
            {
                Url = createDto.Url.Trim(),
                Name = createDto.Name.Trim(),
                Description = createDto.Description?.Trim(),
                MaxDepth = createDto.MaxDepth,
                IncludeSubdomains = createDto.IncludeSubdomains,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Websites.AddAsync(website);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Site web créé: {WebsiteId} pour l'utilisateur {UserId}", website.Id, userId);

            return CreatedAtAction(nameof(GetWebsite), new { id = website.Id }, 
                ApiResponse<WebsiteDto>.SuccessResult(MapToDto(website), "Site web créé avec succès"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du site web pour l'utilisateur {UserId}", _userManager.GetUserId(User));
            return StatusCode(500, ApiResponse<WebsiteDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Met à jour un site web existant
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<WebsiteDto>>> UpdateWebsite(int id, [FromBody] UpdateWebsiteDto updateDto)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var website = await _unitOfWork.Websites.GetAsync(w => w.Id == id && w.UserId == userId);

            if (website == null)
                return NotFound(ApiResponse<WebsiteDto>.ErrorResult("Site web non trouvé"));

            // Mettre à jour uniquement les propriétés fournies
            if (!string.IsNullOrWhiteSpace(updateDto.Name))
                website.Name = updateDto.Name.Trim();
            
            if (updateDto.Description != null)
                website.Description = updateDto.Description.Trim();
            
            if (updateDto.MaxDepth.HasValue)
                website.MaxDepth = updateDto.MaxDepth.Value;
            
            if (updateDto.IncludeSubdomains.HasValue)
                website.IncludeSubdomains = updateDto.IncludeSubdomains.Value;
            
            if (updateDto.IsActive.HasValue)
                website.IsActive = updateDto.IsActive.Value;

            await _unitOfWork.Websites.UpdateAsync(website);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Site web mis à jour: {WebsiteId}", id);

            return Ok(ApiResponse<WebsiteDto>.SuccessResult(MapToDto(website), "Site web mis à jour avec succès"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour du site web {WebsiteId}", id);
            return StatusCode(500, ApiResponse<WebsiteDto>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Supprime un site web
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWebsite(int id)
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var website = await _unitOfWork.Websites.GetAsync(w => w.Id == id && w.UserId == userId);

            if (website == null)
                return NotFound(ApiResponse<object>.ErrorResult("Site web non trouvé"));

            await _unitOfWork.Websites.DeleteAsync(website);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Site web supprimé: {WebsiteId}", id);

            return Ok(ApiResponse<object>.SuccessResult(null, "Site web supprimé avec succès"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression du site web {WebsiteId}", id);
            return StatusCode(500, ApiResponse<object>.ErrorResult("Erreur interne du serveur"));
        }
    }

    private static WebsiteDto MapToDto(Website website)
    {
        return new WebsiteDto
        {
            Id = website.Id,
            Url = website.Url,
            Name = website.Name,
            Description = website.Description,
            CreatedAt = website.CreatedAt,
            LastScanAt = website.LastScanAt,
            IsActive = website.IsActive,
            MaxDepth = website.MaxDepth,
            IncludeSubdomains = website.IncludeSubdomains,
            UserId = website.UserId
        };
    }
}