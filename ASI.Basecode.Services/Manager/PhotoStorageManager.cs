using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Manager;
public abstract class PhotoStorageManager
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;
    private readonly Uri _storageUrl;
    private readonly string _bucket;

    protected PhotoStorageManager(HttpClient httpClient, IOptions<SupabaseStorageOptions> options, string bucket)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _bucket = bucket;

        if (!Uri.TryCreate(_options.ProjectUrl, UriKind.Absolute, out var projectUrl)
            || projectUrl.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(_options.SecretKey)
            || string.IsNullOrWhiteSpace(_bucket))
        {
            throw new InvalidOperationException("Supabase storage configuration is missing or invalid.");
        }

        _storageUrl = new Uri(projectUrl.GetLeftPart(UriPartial.Authority) + "/storage/v1/");
    }

    public async Task<string> CreateReadUrlAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var path = EncodePath(storagePath);

        using var request = CreateRequest(HttpMethod.Post, $"object/sign/{Uri.EscapeDataString(_bucket)}/{path}");

        request.Content = JsonContent.Create(new
        {
            expiresIn = 3600
        });

        // Send the request and handle potential timeout exceptions.

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        // Check if the response indicates success and throw an exception if not.

        EnsureSuccess(response, "create a photo URL");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("signedURL", out var value)
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException("Supabase did not return a photo URL.");
        }

        var signedUrl = value.GetString();

        if (Uri.TryCreate(signedUrl, UriKind.Absolute, out var absoluteUrl))
        {
            return absoluteUrl.AbsoluteUri;
        }

        return new Uri(_storageUrl, signedUrl.TrimStart('/')).AbsoluteUri;
    }

    public async Task<Dictionary<string, string>> CreateReadUrlsAsync(IEnumerable<string> storagePaths, CancellationToken cancellationToken = default)
    {
        var paths = storagePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var urls = new Dictionary<string, string>(StringComparer.Ordinal);

        if (paths.Count == 0)
            return urls;

        // Validate the paths. JSON needs the original, unencoded paths.
        foreach (var path in paths)
            EncodePath(path);

        using var request = CreateRequest(HttpMethod.Post, $"object/sign/{Uri.EscapeDataString(_bucket)}");

        request.Content = JsonContent.Create(new
        {
            expiresIn = 3600,
            paths
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        EnsureSuccess(response, "create photo URLs");

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Supabase returned an invalid photo URL response.");

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("path", out var pathValue)
                || !entry.TryGetProperty("signedURL", out var urlValue))
                continue;

            var path = pathValue.GetString();
            var signedUrl = urlValue.GetString();

            // Missing or failed photos are left out for placeholder display.
            if (string.IsNullOrWhiteSpace(path)
                || string.IsNullOrWhiteSpace(signedUrl)
                || !paths.Contains(path, StringComparer.Ordinal))
                continue;

            urls[path] = Uri.TryCreate(
                signedUrl, UriKind.Absolute, out var absoluteUrl)
                    ? absoluteUrl.AbsoluteUri
                    : new Uri(_storageUrl, signedUrl.TrimStart('/')).AbsoluteUri;
        }

        return urls;
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        // Validate the path before sending it in the JSON body.
        EncodePath(storagePath);

        using var request = CreateRequest(HttpMethod.Delete, $"object/{Uri.EscapeDataString(_bucket)}");

        request.Content = JsonContent.Create(new
        {
            prefixes = new[] { storagePath }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        EnsureSuccess(response, "delete");
    }

    public async Task UploadAsync(string storagePath, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var path = EncodePath(storagePath);

        using var request = CreateRequest(HttpMethod.Post, $"object/{Uri.EscapeDataString(_bucket)}/{path}");

        request.Headers.Add("x-upsert", "false");
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        EnsureSuccess(response, "upload");
    }

    private static string EncodePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("A photo path is required.");

        var segments = storagePath.Split('/');

        if (segments.Any(segment =>
            string.IsNullOrWhiteSpace(segment)
            || segment == "."
            || segment == ".."
            || segment.Contains('\\')))
        {
            throw new ArgumentException("The photo path is invalid.");
        }

        return string.Join("/", segments.Select(Uri.EscapeDataString));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, new Uri(_storageUrl, relativePath));

        request.Headers.Add("apikey", _options.SecretKey);

        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string operation)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Unable to {operation} photo. " +
                $"Storage returned HTTP {(int)response.StatusCode}.");
        }
    }

    protected static (string Extension, string ContentType)? GetPhotoFormat(Stream content)
    {
        var originalPosition = content.Position;

        try
        {
            using var reader = new BinaryReader(content, System.Text.Encoding.UTF8, leaveOpen: true);

            var bytes = reader.ReadBytes(12);

            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF &&
                bytes[1] == 0xD8 &&
                bytes[2] == 0xFF)
            {
                return (".jpg", "image/jpeg");
            }

            if (bytes.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            {
                return (".png", "image/png");
            }

            if (bytes.Length == 12 &&
                bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            {
                return (".webp", "image/webp");
            }

            return null;
        }
        finally
        {
            content.Position = originalPosition;
        }
    }
}

