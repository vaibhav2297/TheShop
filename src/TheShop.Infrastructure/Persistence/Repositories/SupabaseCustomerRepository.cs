using TheShop.Application.Common.Interfaces;
using TheShop.Domain.Entities;
using TheShop.Infrastructure.Persistence.Mappers;
using TheShop.Infrastructure.Persistence.Records;

namespace TheShop.Infrastructure.Persistence.Repositories;

/// <summary>
/// Supabase-backed implementation of <see cref="ICustomerRepository"/>.
/// Maps between <see cref="CustomerRecord"/> rows and <see cref="Customer"/> domain entities.
/// </summary>
public sealed class SupabaseCustomerRepository(Supabase.Client client) : ICustomerRepository
{
    private const string CustomerExistsRpc = "customer_exists";

    public async Task<bool> ExistsForEmailAsync(string email, CancellationToken ct)
    {
        return await client.Rpc<bool>(
            CustomerExistsRpc,
            new { p_email = email });
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var response = await client
            .From<CustomerRecord>()
            .Where(c => c.Id == id)
            .Single(ct);

        return response is null ? null : CustomerMapper.ToDomain(response);
    }

    public async Task AddAsync(Customer customer, CancellationToken ct)
    {
        var record = CustomerMapper.ToRecord(customer);
        await client.From<CustomerRecord>().Insert(record, cancellationToken: ct);
    }
}
