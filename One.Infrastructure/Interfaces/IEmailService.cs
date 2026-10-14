using System.Threading.Tasks;

namespace One.Infrastructure.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
