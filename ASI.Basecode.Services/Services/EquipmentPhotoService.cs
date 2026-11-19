using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Options;
using System.Net.Http;
namespace ASI.Basecode.Services.Services;

public class EquipmentPhotoService : PhotoStorageManager, IEquipmentPhotoService
{
    public EquipmentPhotoService(HttpClient httpClient, IOptions<SupabaseStorageOptions> options) : base(httpClient, options, options.Value.Bucket) 
    { 
    }
}
