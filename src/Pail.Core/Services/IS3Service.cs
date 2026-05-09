using Pail.Models;

namespace Pail.Services;

public interface IS3Service
{
	public Task InitializeAsync(IAwsCredentials credentials);

	public Task<List<S3BucketItem>> GetBucketsAsync();

	public Task<S3ObjectPage> GetObjectsAsync(string bucketName, string prefix = "", int pageSize = 1000, string? continuationToken = null);

	public Task DownloadObjectAsync(string bucketName, string key, string destinationPath);

	public Task DownloadObjectsAsync(string bucketName, IEnumerable<string> keys, string destinationFolder);

	public Task DownloadFolderAsync(string bucketName, string prefix, string destinationFolder);
}
