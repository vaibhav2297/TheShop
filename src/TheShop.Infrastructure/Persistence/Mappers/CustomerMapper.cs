using TheShop.Domain.Entities;
using TheShop.Domain.ValueObjects;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Mappers;

internal static class CustomerMapper
{
    public static CustomerRecord ToRecord(this Customer customer) => new()
    {
        Id = customer.Id,
        FirstName = customer.FirstName,
        LastName = customer.LastName,
        DateOfBirth = customer.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue),
        Email = customer.Email.Value,
        CreatedAt = customer.CreatedAt.UtcDateTime,
    };

    public static Customer ToDomain(this CustomerRecord record)
    {
        var email = Email.Create(record.Email);
        var dob = DateOfBirth.Create(DateOnly.FromDateTime(record.DateOfBirth));

        return Customer.Rehydrate(
            record.Id,
            record.FirstName,
            record.LastName,
            dob,
            email,
            new DateTimeOffset(DateTime.SpecifyKind(record.CreatedAt, DateTimeKind.Utc)));
    }
}
