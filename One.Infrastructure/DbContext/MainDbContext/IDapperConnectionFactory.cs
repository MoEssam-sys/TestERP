using System.Data;

namespace One.Infrastructure.DbContext.MainDbContext
{
    public interface IDapperConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}