using Microsoft.Extensions.DependencyInjection;
using One.Infrastructure.DbContext.MainDbContext;

namespace One.Infrastructure.Configuration
{
    public static class InfrastructureConfig
    {
        public static IServiceCollection AddOneInfrastructure(this IServiceCollection services)
        {
            // سجل DbConnectionFactory كـ Scoped
            services.AddScoped<IDapperConnectionFactory, DbConnectionFactory>();

            // Email Service
            services.AddTransient<One.Infrastructure.Interfaces.IEmailService, One.Infrastructure.Services.EmailService>();

            return services;
        }
    }
}