using Pail.Models;

namespace Pail.Services;

public interface IS3Service
{
	public Task InitializeAsync(IAwsCredentials credentials);

	public Task<List<S3BucketItem>> GetBucketsAsync();

	public Task<S3ObjectPage> GetObjectsAsync(string bucketName, string prefix = "", string? prefixFilter = null, int pageSize = 1000, string? continuationToken = null);

	public Task DownloadObjectAsync(string bucketName, string key, string destinationPath, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);

	public Task DownloadFolderAsync(string bucketName, string prefix, string destinationFolder, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
}
