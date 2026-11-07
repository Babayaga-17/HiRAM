using System.Threading;
using System.Threading.Tasks;
namespace ASI.Basecode.Services.Interfaces;
public interface INotificationEmailDispatcher
{
    Task<bool> DispatchNextAsync(CancellationToken token);
}
