using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Interfaces;

public interface IEquipmentPhotoService
{
    Task UploadAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task<string> CreateReadUrlAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> CreateReadUrlsAsync(IEnumerable<string> storagePaths, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
    (List<(IFormFile File, string Path, string ContentType)> Uploads, string Error) PreparePhotoUploads(List<IFormFile> photos, int availableSlots);
}
