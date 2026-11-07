using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace ASI.Basecode.Services.Services;

public class ProfilePhotoService : SupabasePhotoStorage, IProfilePhotoService
{
    private readonly ILogger<ProfilePhotoService> logger;
    public ProfilePhotoService(HttpClient client, IOptions<SupabaseStorageOptions> options, ILogger<ProfilePhotoService> logger)
        : base(client, options, options.Value.ProfileBucket) { this.logger = logger; }

    public async Task<string> UploadProfileAsync(Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
            throw new ValidationException("Choose a JPG or PNG profile picture.");
        using var source = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (source.Length + count > ProfilePhotoPolicy.MaxBytes) throw new ValidationException("Profile picture must be 2 MB or smaller.");
            source.Write(buffer, 0, count);
        }
        if (source.Length == 0) throw new ValidationException("The profile picture is empty.");
        source.Position = 0;
        using var normalized = Normalize(source);
        var path = $"profiles/{Guid.NewGuid():N}.png";
        try { await UploadAsync(path, normalized, "image/png", cancellationToken); }
        catch
        {
            // A timed-out upload may have reached storage. Never reuse the generated path.
            try { await DeleteAsync(path, CancellationToken.None); }
            catch (Exception exception) { logger.LogWarning(exception, "Unable to clean up an unsuccessful profile upload."); }
            throw;
        }
        return path;
    }
    private static MemoryStream Normalize(Stream content)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1)) throw new PlatformNotSupportedException("The current profile image decoder requires Windows.");
        try
        {
            using var image = Image.FromStream(content, false, true);
            if (image.RawFormat.Guid != ImageFormat.Jpeg.Guid && image.RawFormat.Guid != ImageFormat.Png.Guid)
                throw new ValidationException("Choose a valid JPG or PNG profile picture.");
            if (image.Width > ProfilePhotoPolicy.MaxDimension || image.Height > ProfilePhotoPolicy.MaxDimension)
                throw new ValidationException("Profile picture dimensions must not exceed 4096 by 4096 pixels.");
            var scale = Math.Min(1d, (double)ProfilePhotoPolicy.StoredDimension / Math.Max(image.Width, image.Height));
            using var bitmap = new Bitmap(Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
            using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
            var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            output.Position = 0;
            return output;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is ExternalException || exception is OutOfMemoryException)
        { throw new ValidationException("The profile picture could not be read. Choose another JPG or PNG image."); }
    }
}

