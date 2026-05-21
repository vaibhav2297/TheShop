using TheShop.Domain.Entities;

namespace TheShop.Application.Common.Interfaces;

/// <summary>
/// Persistence contract for <see cref="Customer"/>. Implementations live in the
/// Infrastructure layer.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Returns <c>true</c> when a customer record already exists for the given email address.
    /// </summary>
    Task<bool> ExistsForEmailAsync(string email, CancellationToken ct);

    /// <summary>
    /// Returns the customer with the given ID, or <c>null</c> if no match is found.
    /// </summary>
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Persists a newly registered customer for the first time.
    /// </summary>
    Task AddAsync(Customer customer, CancellationToken ct);
}
