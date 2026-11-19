using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
namespace ASI.Basecode.Services.Services;

public class EquipmentPhotoService : PhotoStorageManager, IEquipmentPhotoService
{
    public EquipmentPhotoService(HttpClient httpClient, IOptions<SupabaseStorageOptions> options) : base(httpClient, options, options.Value.Bucket) 
    { 
    }

    public (List<(IFormFile File, string Path, string ContentType)> Uploads, string Error) PreparePhotoUploads(List<IFormFile> photos, int availableSlots)
    {
        var uploads = new List<(IFormFile File, string Path, string ContentType)>();

        if (photos == null || photos.Count == 0)
            return (uploads, null);

        if (photos.Count > availableSlots)
        {
            var error = availableSlots <= 0
                ? "This equipment already has the maximum number of photos."
                : $"Choose up to {availableSlots} photos.";

            return (uploads, error);
        }

        foreach (var photo in photos)
        {
            if (photo.Length == 0 || photo.Length > EquipmentPhotoPolicy.MaxPhotoBytes)
            {
                return (uploads, $"Each photo must be nonempty and at most {EquipmentPhotoPolicy.MaxPhotoSizeMb} MB.");
            }

            using var stream = photo.OpenReadStream();
            var format = GetPhotoFormat(stream);

            if (format == null)
                return (uploads, "Choose JPG, PNG, or WebP images.");

            var path = $"equipment/{Guid.NewGuid():N}{format.Value.Extension}";

            uploads.Add((photo, path, format.Value.ContentType));
        }

        return (uploads, null);
    }

    /*
     * Checks the file signature and returns the extension and MIME type used for the upload.
     * 
     * This catches files renamed to look like images. It checks the header, so a corrupt image
     * with a valid header can still pass.
     */

}
