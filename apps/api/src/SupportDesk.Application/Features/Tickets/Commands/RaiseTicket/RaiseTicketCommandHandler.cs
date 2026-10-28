using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    IClock clock,
    SlaOptions slaOptions,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var customer = await customers.GetByIdAsync(request.CustomerId, ct);
        if (customer is null)
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var category = await categories.GetByIdAsync(request.CategoryId, ct);
        if (category is null)
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var priority = category.ForcesCriticalPriority
            ? TicketPriority.Critical
            : request.RequestedPriority ?? TicketPriority.Medium;
        var now = clock.UtcNow;
        var windowHours = slaOptions.GetWindowHours(
            priority,
            customer.Tier == SupportDesk.Domain.Aggregates.Customers.CustomerTier.Premium);

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            priority,
            now,
            now.AddHours(windowHours));

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        return await getTicket.HandleAsync(ticket.Id, ct);
    }
}
