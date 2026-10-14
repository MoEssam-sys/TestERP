using One.Domain.Base;
using One.Domain.DTOs.Sales;

namespace One.Infrastructure.Interfaces
{
    public interface ISalesInvoiceApplicationService
    {
        Task<Result<SalesInvoiceCreatedDto>> CreateAsync(
            CreateSalesInvoiceCommand? command,
            Guid currentUserId,
            CancellationToken cancellationToken = default);
    }
}
