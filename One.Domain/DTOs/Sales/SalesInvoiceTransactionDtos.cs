namespace One.Domain.DTOs.Sales
{
    public sealed record CreateSalesInvoiceLineCommand(
        Guid ProductId,
        decimal Quantity,
        decimal UnitPrice,
        decimal Discount,
        decimal Tax);

    public sealed record CreateSalesInvoiceCommand(
        string Number,
        Guid BranchId,
        Guid WarehouseId,
        Guid CustomerId,
        DateTime Date,
        IReadOnlyList<CreateSalesInvoiceLineCommand> Lines);

    public sealed record SalesInvoiceCreatedDto(
        Guid Id,
        string Number,
        decimal TotalAmount,
        decimal DiscountAmount,
        decimal TaxAmount,
        decimal NetAmount,
        byte[] RowVersion);
}
