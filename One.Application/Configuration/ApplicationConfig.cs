using Microsoft.Extensions.DependencyInjection;
using One.Application.Base;
using One.Application.Services.Mapping;
using One.Application.Services.Sales;
using One.Domain.Services.Attributes;
using One.Infrastructure.Interfaces;
using System.Reflection;

namespace One.Application.Configuration
{
    public static class ApplicationConfig
    {
        /// <summary>
        /// يسجل كل Repository أو Service في Assembly معين أو Namespace معين تلقائيًا في الـ DI
        /// </summary>
        public static IServiceCollection AddOneApplication(this IServiceCollection services, Assembly? assembly = null)
        {
            assembly ??= Assembly.GetExecutingAssembly();

            // يسجل أي كلاس concrete بيرث من BaseRepository<,>
            var repoBaseType = typeof(BaseRepository<,>);
            var repoTypes = assembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    IsSubclassOfRawGeneric(repoBaseType, t)
                ).ToList();

            foreach (var repoType in repoTypes)
            {
                ValidateRepositoryContract(repoBaseType, repoType);
                services.AddScoped(repoType);
            }

            services.AddSingleton<IDynamicMappingService, DynamicMappingService>();
            services.AddScoped<ISalesInvoiceApplicationService, SalesInvoiceApplicationService>();

            return services;
        }

        private static void ValidateRepositoryContract(Type repositoryBaseType, Type repositoryType)
        {
            var closedBaseType = FindClosedGenericBase(repositoryBaseType, repositoryType)
                ?? throw new InvalidOperationException($"Unable to resolve the repository contract for {repositoryType.Name}.");
            var entityType = closedBaseType.GetGenericArguments()[0];
            var rowVersion = entityType.GetProperty("RowVersion");

            if (entityType.GetCustomAttribute<ReferenceDataAttribute>() is null ||
                entityType.GetProperty("CompanyId")?.PropertyType != typeof(int) ||
                entityType.GetProperty("IsActive")?.PropertyType != typeof(bool) ||
                rowVersion?.PropertyType != typeof(byte[]) ||
                rowVersion.GetCustomAttribute<ConcurrencyFieldAttribute>() is null)
            {
                throw new InvalidOperationException(
                    $"{repositoryType.Name} can only target tenant-owned reference data with IsActive and RowVersion.");
            }
        }

        private static Type? FindClosedGenericBase(Type generic, Type toCheck)
        {
            while (toCheck != typeof(object))
            {
                if (toCheck.IsGenericType && toCheck.GetGenericTypeDefinition() == generic)
                    return toCheck;

                toCheck = toCheck.BaseType!;
            }

            return null;
        }

        // Helper function to detect generic base class
        private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
            => FindClosedGenericBase(generic, toCheck) is not null;
    }
}
