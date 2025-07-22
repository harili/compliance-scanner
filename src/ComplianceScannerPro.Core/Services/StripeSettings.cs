namespace ComplianceScannerPro.Core.Services;

public class StripeSettings
{
    public string PublishableKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    
    // Prix des plans (Price IDs de Stripe)
    public string StarterPlanPriceId { get; set; } = "";
    public string ProfessionalPlanPriceId { get; set; } = "";
    public string EnterprisePlanPriceId { get; set; } = "";
    
    // URLs de retour
    public string SuccessUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
}