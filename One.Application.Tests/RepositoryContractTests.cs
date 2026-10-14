using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using One.Application.Services.Sales;
using One.Domain.Base;
using One.Domain.DTOs.Sales;
using One.Domain.Tenancy;
using One.Infrastructure.DbContext.MainDbContext;

namespace One.Application.Tests;

public sealed class RepositoryContractTests
{
    [Fact]
    public void BaseFilter_PageSizeAboveLimit_FailsValidation()
    {
        var filter = new BaseFilter { PageNumber = 1, PageSize = BaseFilter.MaxPageSize + 1 };
        var validationResults = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            filter,
            new ValidationContext(filter),
            validationResults,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(validationResults, item => item.MemberNames.Contains(nameof(BaseFilter.PageSize)));
    }

    [Fact]
    public async Task CreateAsync_TooManyInvoiceLines_RejectsBeforeOpeningConnection()
    {
        var connectionFactory = new Mock<IDapperConnectionFactory>(MockBehavior.Strict);
        var tenant = new Mock<ITenantContext>(MockBehavior.Strict);
        tenant.SetupGet(item => item.CompanyId).Returns(1);
        var service = new SalesInvoiceApplicationService(
            connectionFactory.Object,
            tenant.Object,
            NullLogger<SalesInvoiceApplicationService>.Instance);
        var lines = Enumerable.Range(0, 201)
            .Select(_ => new CreateSalesInvoiceLineCommand(Guid.NewGuid(), 1, 10, 0, 0))
            .ToList();
        var command = new CreateSalesInvoiceCommand(
            "INV-TOO-LARGE",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow,
            lines);

        var result = await service.CreateAsync(
            command,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        connectionFactory.Verify(item => item.CreateConnection(), Times.Never);
    }
}
