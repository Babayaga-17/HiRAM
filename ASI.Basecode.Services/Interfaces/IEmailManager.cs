using System.Threading;
using System.Threading.Tasks;
namespace ASI.Basecode.Services.Interfaces;
public interface IEmailManager
{
    Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken = default);
}
