namespace ComplianceScannerPro.Core.Interfaces;

public interface IPaymentService
{
    Task<string> CreateCheckoutSessionAsync(string userId, string planName, string priceId, string successUrl, string cancelUrl);
    Task<string> CreateCustomerPortalSessionAsync(string customerId, string returnUrl);
    Task HandleWebhookAsync(string json, string signature);
    Task<bool> CancelSubscriptionAsync(string subscriptionId);
}