using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace FocusFun.Api.Storage;

public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>R2 endpoint: https://&lt;account-id&gt;.r2.cloudflarestorage.com</summary>
    public string ServiceUrl { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;

    /// <summary>Folder inside the bucket, so Focus Fun files stay separate from anything else stored there.</summary>
    public string KeyPrefix { get; set; } = "focusfun/";
    public TimeSpan UrlLifetime { get; set; } = TimeSpan.FromHours(1);
}

public interface IFileStorage
{
    Task PutTextAsync(string key, string content, string contentType, CancellationToken ct = default);
    Task PutBytesAsync(string key, byte[] content, string contentType, CancellationToken ct = default);
    Task<byte[]> GetBytesAsync(string key, CancellationToken ct = default);
    Task<string> GetDownloadUrlAsync(string key, CancellationToken ct = default);
}

/// <summary>Cloudflare R2 through its S3-compatible API. The bucket stays private; clients get short-lived presigned URLs.</summary>
public sealed class R2FileStorage : IFileStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly StorageOptions _options;

    public R2FileStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
        var config = new AmazonS3Config
        {
            ServiceURL = _options.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            // R2 does not support the newer default checksum headers.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKeyId, _options.SecretAccessKey), config);
    }

    public async Task PutTextAsync(string key, string content, string contentType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = _options.KeyPrefix + key,
            ContentBody = content,
            ContentType = contentType,
            DisablePayloadSigning = true,
        }, ct);
    }

    public async Task PutBytesAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = _options.KeyPrefix + key,
            InputStream = stream,
            ContentType = contentType,
            DisablePayloadSigning = true,
        }, ct);
    }

    public async Task<byte[]> GetBytesAsync(string key, CancellationToken ct = default)
    {
        using var response = await _client.GetObjectAsync(_options.Bucket, _options.KeyPrefix + key, ct);
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    public Task<string> GetDownloadUrlAsync(string key, CancellationToken ct = default) =>
        _client.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = _options.KeyPrefix + key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(_options.UrlLifetime),
        });

    public void Dispose() => _client.Dispose();
}
