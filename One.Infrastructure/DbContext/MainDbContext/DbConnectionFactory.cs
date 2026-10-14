using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using One.Domain.Tenancy;

namespace One.Infrastructure.DbContext.MainDbContext
{
    public class DbConnectionFactory : IDapperConnectionFactory
    {
        private readonly IConfiguration _config;
        private readonly ITenantContext _tenantContext;
        private const string ConnectionStringName = "MainConnection";

        public DbConnectionFactory(IConfiguration config, ITenantContext tenantContext)
        {
            _config = config;
            _tenantContext = tenantContext;
        }

        public IDbConnection CreateConnection()
        {
            var connString = _config.GetConnectionString(ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connString))
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is missing.");

            var connection = new SqlConnection(connString);
            try
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "EXEC sys.sp_set_session_context @key=N'CompanyId', @value=@CompanyId;";
                command.Parameters.Add("@CompanyId", SqlDbType.Int).Value = _tenantContext.CompanyId;
                command.ExecuteNonQuery();
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }
}
