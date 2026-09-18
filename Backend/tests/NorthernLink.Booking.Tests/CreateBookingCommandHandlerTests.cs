using NorthernLink.Booking.Application.Bookings;
using NorthernLink.Booking.Application.Bookings.Create;
using NorthernLink.Booking.Application.Integration;
using NorthernLink.Booking.Domain.Bookings;
using NorthernLink.Booking.Domain.Customers;
using Xunit;

namespace NorthernLink.Booking.Tests;

/// <summary>
/// The create handler's reference allocation: a fresh NL- reference is stamped on the saved
/// booking, a collision regenerates, and the attempt cap turns into a retryable conflict
/// with nothing saved.
/// </summary>
public class CreateBookingCommandHandlerTests
{
    private readonly InMemoryBookingRepository _bookings = new();
    private readonly InMemoryBookingDayRepository _days = new();
    private readonly InMemoryCustomerRepository _customers = new();
    private readonly InMemoryCorridorLookupRepository _corridors = new();
    private readonly InMemoryBookingPolicyRepository _policies = new();
    private readonly Customer _customer;

    public CreateBookingCommandHandlerTests()
    {
        _customer = Customer.Create(TestBookings.TenantId, "Doris Spence", "204-555-0199", "doris@example.com", null).Value;
        _customers.Add(_customer);
        _corridors.Corridors.Add(new CorridorLookup
        {
            CorridorId = TestBookings.CorridorId,
            TenantId = TestBookings.TenantId,
            Name = "Thompson ↔ Lynn Lake",
            Origin = "Thompson",
            Destination = "Lynn Lake",
            Active = true,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
    }

    private CreateBookingCommandHandler Handler() => new(_bookings, _days, _customers, _corridors, _policies);

    private CreateBookingCommand Command() => new(
        TestBookings.TenantId,
        _customer.Id,
        TestBookings.CorridorId,
        new DateOnly(2026, 9, 15),
        new BookingLocationInput(Guid.NewGuid(), "Thompson Depot", null),
        new BookingLocationInput(Guid.NewGuid(), "Lynn Lake Terminal", null),
        [new BookingPassengerInput("Doris Spence", "204-555-0199", true), new BookingPassengerInput("Sam Spence", null, false)],
        PaymentMethod.ETransfer,
        null);

    [Fact]
    public async Task Created_booking_carries_a_freshly_generated_reference()
    {
        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_bookings.Bookings);
        Assert.Equal(result.Value, stored.Id);
        Assert.True(BookingReference.Create(stored.Reference.Value).IsSuccess);
        Assert.Equal(stored.Reference, Assert.Single(_bookings.ReferenceChecks)); // one draw, checked once
        Assert.Equal(1, _bookings.SaveChangesCallCount);
    }

    [Fact]
    public async Task One_collision_regenerates_and_saves_the_second_draw()
    {
        var collisions = 0;
        _bookings.ReferenceCollides = _ => collisions++ == 0; // first draw taken, second free

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, _bookings.ReferenceChecks.Count);
        Assert.NotEqual(_bookings.ReferenceChecks[0], _bookings.ReferenceChecks[1]);
        var stored = Assert.Single(_bookings.Bookings);
        Assert.Equal(_bookings.ReferenceChecks[1], stored.Reference);
    }

    [Fact]
    public async Task Three_collisions_return_ReferenceExhausted_and_save_nothing()
    {
        _bookings.ReferenceCollides = _ => true;

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BookingErrors.ReferenceExhausted, result.Error);
        Assert.Equal(CreateBookingCommandHandler.ReferenceAttempts, _bookings.ReferenceChecks.Count);
        Assert.Empty(_bookings.Bookings);
        Assert.Equal(0, _bookings.SaveChangesCallCount);
        Assert.Empty(_days.Days); // the day is materialized only once a reference is in hand
    }
}
