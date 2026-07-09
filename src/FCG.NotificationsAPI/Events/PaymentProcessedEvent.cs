namespace FCG.NotificationsAPI.Events;

/// <summary>
/// Publicado pela PaymentsAPI após processar um pagamento.
/// </summary>
public class PaymentProcessedEvent
{
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string GameTitle { get; set; } = string.Empty;
    public decimal Price { get; set; }

    /// <summary>
    /// "Approved" ou "Rejected"
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
