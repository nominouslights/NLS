using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Booking.Application.Abstractions;
using NorthernLink.Booking.Domain.Customers;

namespace NorthernLink.Booking.Application.Customers.Create;

public sealed class CreateCustomerCommandHandler(ICustomerRepository repository)
    : ICommandHandler<CreateCustomerCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customerResult = Customer.Create(
            command.TenantId,
            command.Name,
            command.Phone,
            command.Email,
            command.Notes);

        if (customerResult.IsFailure)
        {
            return Result.Failure<Guid>(customerResult.Error);
        }

        repository.Add(customerResult.Value);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(customerResult.Value.Id);
    }
}
