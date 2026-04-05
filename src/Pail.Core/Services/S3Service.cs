using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Pail.Models;

namespace Pail.Services;

public sealed class S3Service : IS3Service
{
	public Task InitializeAsync(IAwsCredentials credentials)
	{
		var region = RegionEndpoint.GetBySystemName(credentials.Region);

		if (credentials is AwsSessionCredentials sessionCredentials)
		{
			S3Client = string.IsNullOrEmpty(sessionCredentials.SessionToken)
				? new AmazonS3Client(sessionCredentials.AccessKey, sessionCredentials.SecretKey, region)
				: new AmazonS3Client(sessionCredentials.AccessKey, sessionCredentials.SecretKey, sessionCredentials.SessionToken, region);
		}
		else if (credentials is AwsDefaultChainCredentials defaultChainCredentials)
		{
			S3Client = string.IsNullOrWhiteSpace(defaultChainCredentials.ProfileName)
				? new AmazonS3Client(region)
				: new AmazonS3Client(GetProfileCredentials(defaultChainCredentials.ProfileName), region);
		}
		else
		{
			throw new ArgumentOutOfRangeException(nameof(credentials), credentials, "Unsupported AWS credential type.");
		}

		return Task.CompletedTask;
	}

	private AmazonS3Client S3Client
	{
		get => field ?? throw new InvalidOperationException("S3 Client is not initialized.");
		set;
	}

	public async Task<List<S3BucketItem>> GetBucketsAsync()
	{
		var response = await S3Client.ListBucketsAsync();
		return [.. response.Buckets.Select(b => new S3BucketItem(b.BucketName, b.CreationDate))];
	}

	public async Task<List<S3ObjectItem>> GetObjectsAsync(string bucketName, string prefix = "")
	{
		var request = new ListObjectsV2Request
		{
			BucketName = bucketName,
			Prefix = prefix,
			Delimiter = "/",
		};

		var response = await S3Client.ListObjectsV2Async(request);
		var items = new List<S3ObjectItem>();

		foreach (var commonPrefix in response.CommonPrefixes ?? [])
		{
			items.Add(
				new S3ObjectItem
				{
					Key = commonPrefix,
					Name = commonPrefix[prefix.Length..].TrimEnd('/'),
					IsFolder = true,
				});
		}

		foreach (var s3Object in response.S3Objects ?? [])
		{
			if (s3Object.Key == prefix)
			{
				continue;
			}

			items.Add(
				new S3ObjectItem
				{
					Key = s3Object.Key,
					Name = s3Object.Key[prefix.Length..],
					Size = s3Object.Size ?? -1,
					LastModified = s3Object.LastModified ?? new DateTime(),
				});
		}

		return items;
	}

	public async Task DownloadObjectAsync(string bucketName, string key, string destinationPath)
	{
		var directory = Path.GetDirectoryName(destinationPath);
		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var request = new GetObjectRequest
		{
			BucketName = bucketName,
			Key = key
		};

		using var response = await S3Client.GetObjectAsync(request);
		await response.WriteResponseStreamToFileAsync(destinationPath, false, default);
	}

	public async Task DownloadObjectsAsync(string bucketName, IEnumerable<string> keys, string destinationFolder)
	{
		foreach (var key in keys)
		{
			var fileName = Path.GetFileName(key);

			if (string.IsNullOrEmpty(fileName))
			{
				continue; // It's a folder
			}

			var destinationPath = Path.Combine(destinationFolder, fileName);
			await DownloadObjectAsync(bucketName, key, destinationPath);
		}
	}

	public async Task DownloadFolderAsync(string bucketName, string prefix, string destinationFolder)
	{
		var request = new ListObjectsV2Request
		{
			BucketName = bucketName,
			Prefix = prefix,
		};

		ListObjectsV2Response response;

		do
		{
			response = await S3Client.ListObjectsV2Async(request);

			foreach (var s3Object in response.S3Objects)
			{
				var relativeKey = s3Object.Key[prefix.Length..];

				if (string.IsNullOrEmpty(relativeKey))
				{
					continue;
				}

				var destinationPath = Path.Combine(destinationFolder, relativeKey.Replace('/', Path.DirectorySeparatorChar));

				if (s3Object.Key.EndsWith('/'))
				{
					Directory.CreateDirectory(destinationPath);
				}
				else
				{
					await DownloadObjectAsync(bucketName, s3Object.Key, destinationPath);
				}
			}

			request.ContinuationToken = response.NextContinuationToken;
		} while (response.IsTruncated is true);
	}

	private static AWSCredentials GetProfileCredentials(string profileName)
	{
		var profileStore = new CredentialProfileStoreChain();

		return profileStore.TryGetAWSCredentials(profileName, out var credentials) ?
			credentials :
			throw new InvalidOperationException($"AWS profile '{profileName}' was not found or could not be loaded.");
	}
}
