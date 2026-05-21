using TheShop.Domain.Entities;

namespace TheShop.Application.Common.Interfaces;

public interface ICustomerRepository
{
    Task<bool> ExistsForEmailAsync(string email, CancellationToken ct);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);

    Task AddAsync(Customer customer, CancellationToken ct);
}
