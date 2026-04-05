using Pail.Models;

namespace Pail.Services;

public interface IS3Service
{
	public Task InitializeAsync(IAwsCredentials credentials);

	public Task<List<S3BucketItem>> GetBucketsAsync();

	public Task<List<S3ObjectItem>> GetObjectsAsync(string bucketName, string prefix = "");

	public Task DownloadObjectAsync(string bucketName, string key, string destinationPath);

	public Task DownloadObjectsAsync(string bucketName, IEnumerable<string> keys, string destinationFolder);

	public Task DownloadFolderAsync(string bucketName, string prefix, string destinationFolder);
}
