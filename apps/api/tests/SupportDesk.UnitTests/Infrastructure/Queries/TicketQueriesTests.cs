using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_FiltersByExactPriorityBeforeCountingAndPaging()
    {
        await using var fixture = await QueryFixture.CreateAsync();

        var result = await fixture.Queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High, PageSize = 1 },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
        Assert.Equal(TicketPriority.High, result.Items[0].Priority);
    }

    [Fact]
    public async Task GetPagedAsync_CombinesSuppliedFiltersWithAnd()
    {
        await using var fixture = await QueryFixture.CreateAsync();

        var result = await fixture.Queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Status = TicketStatus.InProgress,
                CategoryId = fixture.BillingCategoryId,
                CustomerId = fixture.ContosoCustomerId
            },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0041", item.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_SearchesTrimmedCustomerNameInDatabase()
    {
        await using var fixture = await QueryFixture.CreateAsync();

        var result = await fixture.Queries.GetPagedAsync(
            new TicketQuery { Search = "  Contoso  " },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.Equal("Contoso Support", item.Customer.Name));
    }

    [Theory]
    [InlineData("Invoice", 2)]
    [InlineData("0042", 1)]
    public async Task GetPagedAsync_SearchesTitleAndReference(string search, int expectedCount)
    {
        await using var fixture = await QueryFixture.CreateAsync();

        var result = await fixture.Queries.GetPagedAsync(
            new TicketQuery { Search = search },
            CancellationToken.None);

        Assert.Equal(expectedCount, result.TotalCount);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByAssignedAgent()
    {
        await using var fixture = await QueryFixture.CreateAsync();

        var result = await fixture.Queries.GetPagedAsync(
            new TicketQuery { AssignedAgentId = fixture.AgentId },
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("TCK-0041", item.Reference);
    }

    [Fact]
    public async Task GetPagedAsync_UnassignedOnlyAndBlankSearchPreserveFilteredCount()
    {
        await using var fixture = await QueryFixture.CreateAsync();

        var result = await fixture.Queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true, Search = "   ", PageSize = 1 },
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Items);
        Assert.Null(result.Items[0].AssignedAgent);
    }

    private sealed class QueryFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly SupportDbContext _db;

        private QueryFixture(SqliteConnection connection, SupportDbContext db, TicketQueries queries)
        {
            _connection = connection;
            _db = db;
            Queries = queries;
        }

        public TicketQueries Queries { get; }

        public int BillingCategoryId { get; private set; }

        public int ContosoCustomerId { get; private set; }

        public int AgentId { get; private set; }

        public static async Task<QueryFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SupportDbContext>()
                .UseSqlite(connection)
                .Options;
            var db = new SupportDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer("Contoso Support", "support@contoso.example", null, CustomerTier.Standard, FixedClock.DefaultNow);
            var otherCustomer = new Customer("Fabrikam", "support@fabrikam.example", null, CustomerTier.Standard, FixedClock.DefaultNow);
            var billing = new Category("Billing", true, false);
            var general = new Category("General", false, false);
            var agent = new Agent("Alex Turner", "alex@example.test", 10, FixedClock.DefaultNow);

            db.AddRange(customer, otherCustomer, billing, general, agent);
            await db.SaveChangesAsync();

            db.AddRange(
                Ticket.Raise("TCK-0041", "Invoice issue", "Invoice details", customer.Id, billing.Id, TicketPriority.High, FixedClock.DefaultNow),
                Ticket.Raise("TCK-0042", "Invoice follow-up", "Invoice details", customer.Id, billing.Id, TicketPriority.High, FixedClock.DefaultNow.AddMinutes(1)),
                Ticket.Raise("TCK-0043", "General question", "Question", otherCustomer.Id, general.Id, TicketPriority.Low, FixedClock.DefaultNow.AddMinutes(2)));
            await db.SaveChangesAsync();

            var assignedTicket = await db.Set<Ticket>().SingleAsync(t => t.Reference == "TCK-0041");
            assignedTicket.AssignTo(agent.Id, FixedClock.DefaultNow);
            assignedTicket.ChangeStatus(TicketStatus.InProgress, FixedClock.DefaultNow);
            await db.SaveChangesAsync();

            var clock = new FixedClock();
            var fixture = new QueryFixture(connection, db, new TicketQueries(db, clock))
            {
                BillingCategoryId = billing.Id,
                ContosoCustomerId = customer.Id,
                AgentId = agent.Id
            };
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await _db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
