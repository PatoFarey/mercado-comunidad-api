namespace ApiMercadoComunidad.Models.DTOs;

// --- Payku internal DTOs (API communication) ---

public class PaykuCreateClientRequest
{
    public string name { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public string phone { get; set; } = string.Empty;
}

public class PaykuCreateClientResponse
{
    public string? id { get; set; }
    public string? client { get; set; }
    public string? status { get; set; }
    public string? message { get; set; }
    public string? message_error { get; set; }
    public string? ClientId => id ?? client;
}

public class PaykuCreatePlanRequest
{
    public string name { get; set; } = string.Empty;
    public string amount { get; set; } = string.Empty;
    public string interval { get; set; } = "1";
    public string interval_count { get; set; } = "1";
    public string trial_days { get; set; } = "0";
    public string currency { get; set; } = "CLP";
}

public class PaykuCreatePlanResponse
{
    public string? id { get; set; }
    public string? token { get; set; }
    public string? status { get; set; }
    public string? message { get; set; }
    public string? message_error { get; set; }
    // Devuelve el identificador del plan (puede ser 'id' o 'token' según endpoint)
    public string? PlanId => id ?? token;
}

public class PaykuCreateSubscriptionRequest
{
    public string plan { get; set; } = string.Empty;
    public string client { get; set; } = string.Empty;
    public string url_return { get; set; } = string.Empty;
    public string url_cancel { get; set; } = string.Empty;
    public string url_notify_suscription { get; set; } = string.Empty;
    public string url_notify_payment { get; set; } = string.Empty;
}

public class PaykuCreateSubscriptionResponse
{
    public string? token { get; set; }
    public string? url { get; set; }
    public string? message { get; set; }
}

public class PaykuWebhookActivation
{
    public string? token { get; set; }
    public string? status { get; set; }
    public string? client { get; set; }
    public string? plan { get; set; }
}

public class PaykuWebhookPayment
{
    public string? token { get; set; }
    public string? status { get; set; }
    public string? client { get; set; }
    public string? plan { get; set; }
    public string? amount { get; set; }
}

// --- Plan management DTOs (Super Admin) ---

public class CreatePaykuPlanRequest
{
    public string PlanId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int AmountClp { get; set; }
}

// --- Frontend-facing DTOs ---

public class StartSubscriptionRequest
{
    public string PlanId { get; set; } = string.Empty;
}

public class StartSubscriptionResponse
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
}

public class UserSubscriptionResponse
{
    public string Id { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
