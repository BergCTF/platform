using MediatR;

namespace Berg.Api.Notifications;

public record SolveNotification : INotification
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public string PlayerFederatedId { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public Guid? TeamId { get; set; }
    public string? TeamName { get; set; }
    public DateTime SolvedAt { get; set; }
    public string Challenge { get; set; } = "";
    public bool IsFrozen { get; set; } = false;
    public bool IsAdmin { get; set; } = false;
}
