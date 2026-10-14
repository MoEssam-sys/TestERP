using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using One.Domain.Base;
using One.Domain.DTOs.Sales;
using One.Domain.Tenancy;
using One.Infrastructure.DbContext.MainDbContext;
using One.Infrastructure.Interfaces;

namespace One.Application.Services.Sales
{
    public sealed class SalesInvoiceApplicationService(
        IDapperConnectionFactory connectionFactory,
        ITenantContext tenantContext,
        ILogger<SalesInvoiceApplicationService> logger) : ISalesInvoiceApplicationService
    {
        private const int MaxLines = 200;
        private const decimal MaxDatabaseAmount = 9_999_999_999_999_999.99m;

        public async Task<Result<SalesInvoiceCreatedDto>> CreateAsync(
            CreateSalesInvoiceCommand? command,
            Guid currentUserId,
            CancellationToken cancellationToken = default)
        {
            if (command is null)
                return Result<SalesInvoiceCreatedDto>.Fail(["An invoice request is required."], 400);

            var validation = Validate(command, currentUserId);
            if (validation is not null)
                return Result<SalesInvoiceCreatedDto>.Fail([validation], 400);

            var companyId = tenantContext.CompanyId;
            var now = DateTime.UtcNow;
            var invoiceId = Guid.NewGuid();
            List<InvoiceLineRow> lines;
            decimal totalAmount;
            decimal discountAmount;
            decimal taxAmount;
            decimal netAmount;
            try
            {
                lines = command.Lines
                    .Select(line => CalculateLine(line, invoiceId, companyId, currentUserId, now))
                    .ToList();
                totalAmount = Money(lines.Sum(line => line.GrossAmount));
                discountAmount = Money(lines.Sum(line => line.Discount));
                taxAmount = Money(lines.Sum(line => line.Tax));
                netAmount = Money(lines.Sum(line => line.LineTotal));
                if (new[] { totalAmount, discountAmount, taxAmount, netAmount }
                    .Any(amount => amount > MaxDatabaseAmount))
                {
                    return Result<SalesInvoiceCreatedDto>.Fail(["Invoice totals exceed the supported amount."], 400);
                }
            }
            catch (OverflowException)
            {
                return Result<SalesInvoiceCreatedDto>.Fail(["Invoice amounts exceed the supported range."], 400);
            }

            using var connection = connectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open)
                connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                var relatedDataValid = await ValidateRelatedDataAsync(
                    connection,
                    transaction,
                    companyId,
                    command,
                    cancellationToken);
                if (!relatedDataValid.Success)
                {
                    transaction.Rollback();
                    return Result<SalesInvoiceCreatedDto>.Fail(
                        relatedDataValid.Messages ?? ["Invoice references are invalid."],
                        relatedDataValid.StatusCode ?? 400);
                }

                const string insertInvoiceSql = """
                    INSERT INTO [SalesInvoices]
                    (
                        [CompanyId], [Id], [BranchId], [WarehouseId], [CustomerId],
                        [Number], [TotalAmount], [DiscountAmount], [TaxAmount], [NetAmount],
                        [Date], [CreatedAt], [CreatedBy]
                    )
                    OUTPUT INSERTED.[RowVersion]
                    VALUES
                    (
                        @CompanyId, @Id, @BranchId, @WarehouseId, @CustomerId,
                        @Number, @TotalAmount, @DiscountAmount, @TaxAmount, @NetAmount,
                        @Date, @CreatedAt, @CreatedBy
                    );
                    """;
                var rowVersion = await connection.ExecuteScalarAsync<byte[]>(new CommandDefinition(
                    insertInvoiceSql,
                    new
                    {
                        CompanyId = companyId,
                        Id = invoiceId,
                        command.BranchId,
                        command.WarehouseId,
                        command.CustomerId,
                        Number = command.Number.Trim(),
                        TotalAmount = totalAmount,
                        DiscountAmount = discountAmount,
                        TaxAmount = taxAmount,
                        NetAmount = netAmount,
                        Date = command.Date,
                        CreatedAt = now,
                        CreatedBy = currentUserId
                    },
                    transaction,
                    cancellationToken: cancellationToken));
                if (rowVersion is not { Length: > 0 })
                    throw new DataException("The inserted invoice did not return a row version.");

                const string insertLinesSql = """
                    INSERT INTO [SalesInvoiceDetails]
                    (
                        [CompanyId], [Id], [InvoiceId], [ProductId], [Quantity],
                        [UnitPrice], [Discount], [Tax], [CreatedAt], [CreatedBy]
                    )
                    VALUES
                    (
                        @CompanyId, @Id, @InvoiceId, @ProductId, @Quantity,
                        @UnitPrice, @Discount, @Tax, @CreatedAt, @CreatedBy
                    );
                    """;
                await connection.ExecuteAsync(new CommandDefinition(
                    insertLinesSql,
                    lines,
                    transaction,
                    cancellationToken: cancellationToken));

                transaction.Commit();
                return Result<SalesInvoiceCreatedDto>.Ok(new SalesInvoiceCreatedDto(
                    invoiceId,
                    command.Number.Trim(),
                    totalAmount,
                    discountAmount,
                    taxAmount,
                    netAmount,
                    rowVersion));
            }
            catch (OperationCanceledException)
            {
                transaction.Rollback();
                throw;
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627)
            {
                transaction.Rollback();
                logger.LogInformation(
                    "Duplicate sales invoice number for company {CompanyId}. SQL error {SqlErrorNumber}.",
                    companyId,
                    exception.Number);
                return Result<SalesInvoiceCreatedDto>.Fail(["The invoice number already exists."], 409);
            }
            catch (Exception exception)
            {
                transaction.Rollback();
                logger.LogError(exception, "Failed to create a sales invoice for company {CompanyId}.", companyId);
                return Result<SalesInvoiceCreatedDto>.Fail(["Unable to create the sales invoice."], 500);
            }
        }

        private static string? Validate(CreateSalesInvoiceCommand command, Guid currentUserId)
        {
            if (currentUserId == Guid.Empty)
                return "An authenticated user is required.";
            if (string.IsNullOrWhiteSpace(command.Number) || command.Number.Trim().Length > 50)
                return "Invoice number must contain between 1 and 50 characters.";
            if (command.BranchId == Guid.Empty || command.WarehouseId == Guid.Empty || command.CustomerId == Guid.Empty)
                return "Branch, warehouse, and customer are required.";
            if (command.Date == default || command.Date.ToUniversalTime() > DateTime.UtcNow.AddDays(1))
                return "Invoice date is invalid.";
            if (command.Lines is null || command.Lines.Count == 0 || command.Lines.Count > MaxLines)
                return $"An invoice must contain between 1 and {MaxLines} lines.";
            if (command.Lines.GroupBy(line => line.ProductId).Any(group => group.Key == Guid.Empty || group.Count() > 1))
                return "Each invoice product must be valid and appear once.";
            if (command.Lines.Any(line =>
                line.Quantity <= 0 || line.UnitPrice < 0 || line.Discount < 0 || line.Tax < 0 ||
                line.Quantity > MaxDatabaseAmount || line.UnitPrice > MaxDatabaseAmount ||
                line.Discount > MaxDatabaseAmount || line.Tax > MaxDatabaseAmount ||
                !HasDatabaseScale(line.Quantity) || !HasDatabaseScale(line.UnitPrice) ||
                !HasDatabaseScale(line.Discount) || !HasDatabaseScale(line.Tax)))
            {
                return "Invoice line amounts are invalid.";
            }

            try
            {
                if (command.Lines.Any(line => line.Discount > line.Quantity * line.UnitPrice))
                    return "Invoice line amounts are invalid.";
            }
            catch (OverflowException)
            {
                return "Invoice line amounts exceed the supported range.";
            }

            return null;
        }

        private static async Task<Result<bool>> ValidateRelatedDataAsync(
            IDbConnection connection,
            IDbTransaction transaction,
            int companyId,
            CreateSalesInvoiceCommand command,
            CancellationToken cancellationToken)
        {
            const string headerReferencesSql = """
                SELECT
                    CASE WHEN EXISTS
                    (
                        SELECT 1 FROM [Branches]
                        WHERE [CompanyId] = @CompanyId AND [Id] = @BranchId AND [IsActive] = 1
                    ) THEN 1 ELSE 0 END AS BranchExists,
                    CASE WHEN EXISTS
                    (
                        SELECT 1 FROM [Warehouses]
                        WHERE [CompanyId] = @CompanyId AND [Id] = @WarehouseId
                          AND [BranchId] = @BranchId AND [IsActive] = 1
                    ) THEN 1 ELSE 0 END AS WarehouseExists,
                    CASE WHEN EXISTS
                    (
                        SELECT 1 FROM [Customers]
                        WHERE [CompanyId] = @CompanyId AND [Id] = @CustomerId AND [IsActive] = 1
                    ) THEN 1 ELSE 0 END AS CustomerExists;
                """;
            var references = await connection.QuerySingleAsync<HeaderReferenceState>(new CommandDefinition(
                headerReferencesSql,
                new { CompanyId = companyId, command.BranchId, command.WarehouseId, command.CustomerId },
                transaction,
                cancellationToken: cancellationToken));
            if (!references.BranchExists || !references.WarehouseExists || !references.CustomerExists)
                return Result<bool>.Fail(["The branch, warehouse, or customer is invalid or inactive."], 400);

            var productIds = command.Lines.Select(line => line.ProductId).ToArray();
            var productCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(*) FROM [Products] WHERE [CompanyId] = @CompanyId AND [Id] IN @ProductIds AND [IsActive] = 1;",
                new { CompanyId = companyId, ProductIds = productIds },
                transaction,
                cancellationToken: cancellationToken));
            return productCount == productIds.Length
                ? Result<bool>.Ok(true)
                : Result<bool>.Fail(["One or more products are invalid or inactive."], 400);
        }

        private static InvoiceLineRow CalculateLine(
            CreateSalesInvoiceLineCommand line,
            Guid invoiceId,
            int companyId,
            Guid currentUserId,
            DateTime createdAt)
        {
            var gross = Money(line.Quantity * line.UnitPrice);
            var discount = Money(line.Discount);
            var tax = Money(line.Tax);
            return new InvoiceLineRow(
                companyId,
                Guid.NewGuid(),
                invoiceId,
                line.ProductId,
                line.Quantity,
                Money(line.UnitPrice),
                discount,
                tax,
                Money(gross - discount + tax),
                gross,
                createdAt,
                currentUserId);
        }

        private static decimal Money(decimal value)
            => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

        private static bool HasDatabaseScale(decimal value)
            => decimal.Round(value, 2) == value;

        private sealed class HeaderReferenceState
        {
            public bool BranchExists { get; set; }

            public bool WarehouseExists { get; set; }

            public bool CustomerExists { get; set; }
        }

        private sealed record InvoiceLineRow(
            int CompanyId,
            Guid Id,
            Guid InvoiceId,
            Guid ProductId,
            decimal Quantity,
            decimal UnitPrice,
            decimal Discount,
            decimal Tax,
            decimal LineTotal,
            decimal GrossAmount,
            DateTime CreatedAt,
            Guid CreatedBy);
    }
}
