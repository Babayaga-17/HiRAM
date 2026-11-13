using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ServiceModels;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASI.Basecode.Services.Services;
public abstract class SupabasePhotoStorage
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;
    private readonly Uri _storageUrl;
    private readonly string _bucket;

    protected SupabasePhotoStorage(HttpClient httpClient, IOptions<SupabaseStorageOptions> options, string bucket)
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

        using var response = await _httpClient.SendAsync(request, cancellationToken); // exception

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
}

