using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using ComplianceScannerPro.Infrastructure.Identity;
using ComplianceScannerPro.Shared.DTOs;

namespace ComplianceScannerPro.Web.Controllers.Api;

[ApiController]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(UserManager<ApplicationUser> userManager, ILogger<AdminController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint temporaire pour donner les privilèges admin (DÉVELOPPEMENT UNIQUEMENT)
    /// </summary>
    [HttpPost("make-developer-admin")]
    public async Task<ActionResult<ApiResponse<string>>> MakeDeveloperAdmin([FromBody] MakeAdminRequest request)
    {
        try
        {
            // Sécurité : seuls les emails de développement autorisés
            var allowedEmails = new[] { "akhy.kays@gmail.com", "dev@compliancescannerpro.com" };
            
            if (!allowedEmails.Contains(request.Email, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(ApiResponse<string>.ErrorResult("Email non autorisé pour les privilèges admin"));
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return NotFound(ApiResponse<string>.ErrorResult("Utilisateur non trouvé"));
            }

            user.IsAdmin = true;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                _logger.LogInformation("Privilèges admin accordés à l'utilisateur {Email}", request.Email);
                return Ok(ApiResponse<string>.SuccessResult(
                    $"Privilèges admin accordés à {request.Email}", 
                    "Admin créé avec succès"));
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(ApiResponse<string>.ErrorResult($"Erreur lors de la mise à jour: {errors}"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'attribution des privilèges admin pour {Email}", request.Email);
            return StatusCode(500, ApiResponse<string>.ErrorResult("Erreur interne du serveur"));
        }
    }

    /// <summary>
    /// Vérifier si un utilisateur est admin
    /// </summary>
    [HttpGet("check-admin/{email}")]
    public async Task<ActionResult<ApiResponse<AdminCheckResponse>>> CheckAdmin(string email)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return NotFound(ApiResponse<AdminCheckResponse>.ErrorResult("Utilisateur non trouvé"));
            }

            var response = new AdminCheckResponse
            {
                Email = user.Email!,
                IsAdmin = user.IsAdmin,
                FirstName = user.FirstName,
                LastName = user.LastName
            };

            return Ok(ApiResponse<AdminCheckResponse>.SuccessResult(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la vérification admin pour {Email}", email);
            return StatusCode(500, ApiResponse<AdminCheckResponse>.ErrorResult("Erreur interne du serveur"));
        }
    }
}

public class MakeAdminRequest
{
    public string Email { get; set; } = string.Empty;
}

public class AdminCheckResponse
{
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}