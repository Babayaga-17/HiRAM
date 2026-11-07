using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace ASI.Basecode.Services.Interfaces;
public interface IProfilePhotoService
{
    Task<string> UploadProfileAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
    Task<string> CreateReadUrlAsync(string storagePath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
