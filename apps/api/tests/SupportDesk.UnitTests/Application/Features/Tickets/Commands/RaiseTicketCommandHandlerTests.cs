using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class RaiseTicketCommandHandlerTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task HandleAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 72, 36)]
    [InlineData(TicketPriority.Medium, 24, 12)]
    [InlineData(TicketPriority.High, 8, 4)]
    [InlineData(TicketPriority.Critical, 4, 2)]
    public async Task HandleAsync_UsesPriorityAndCustomerTierSlaWindows(
        TicketPriority priority,
        double standardWindowHours,
        double premiumWindowHours)
    {
        var context = new TicketCommandTestContext();

        await context.RaiseTicket.HandleAsync(Request(priority), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(priority, context.AddedTicket.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(standardWindowHours), context.AddedTicket.DueAtUtc);

        context.Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, context.Clock.UtcNow));
        await context.RaiseTicket.HandleAsync(Request(priority), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(premiumWindowHours), context.AddedTicket?.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_ForcesCriticalPriorityForFlaggedCategory()
    {
        var context = new TicketCommandTestContext();
        context.Categories.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category("Security", true, true));

        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Critical, context.AddedTicket.Priority);
        Assert.Equal(FixedClock.DefaultNow.AddHours(4), context.AddedTicket.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_HonorsOneHourFloorForPremiumWindow()
    {
        var context = new TicketCommandTestContext();
        context.Customers.Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer("Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, context.Clock.UtcNow));

        context.SlaOptions.PremiumCustomerMultiplier = 0.01;
        await context.RaiseTicket.HandleAsync(Request(TicketPriority.Low), CancellationToken.None);

        Assert.Equal(FixedClock.DefaultNow.AddHours(1), context.AddedTicket?.DueAtUtc);
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();
        context.Customers
            .Setup(c => c.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Customer?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.RaiseTicket.HandleAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.VerifySaved(Times.Never());
    }
}
