using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

public sealed class SlaOptions
{
    public Dictionary<string, double> BaseWindowHours { get; set; } = new()
    {
        [nameof(TicketPriority.Low)] = 72,
        [nameof(TicketPriority.Medium)] = 24,
        [nameof(TicketPriority.High)] = 8,
        [nameof(TicketPriority.Critical)] = 4
    };

    public double PremiumCustomerMultiplier { get; set; } = 0.5;

    public double MinimumWindowHours { get; set; } = 1;

    public double GetWindowHours(TicketPriority priority, bool isPremium)
    {
        if (!BaseWindowHours.TryGetValue(priority.ToString(), out var baseHours) || baseHours <= 0)
        {
            throw new InvalidOperationException($"A positive SLA window is required for {priority}.");
        }

        if (!isPremium)
        {
            return baseHours;
        }

        if (PremiumCustomerMultiplier <= 0 || PremiumCustomerMultiplier > 1 || MinimumWindowHours <= 0)
        {
            throw new InvalidOperationException("Premium SLA settings must have a multiplier in (0, 1] and a positive minimum window.");
        }

        return Math.Max(baseHours * PremiumCustomerMultiplier, MinimumWindowHours);
    }
}
