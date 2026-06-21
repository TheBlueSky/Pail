using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using Pail.Extensions;
using Pail.Models;

namespace Pail.Services;

public sealed class S3Service : IS3Service
{
	private const int MinimumRequestedItemCount = 1;
	private const int MaximumRequestPageSize = 1000;
	private readonly IAwsClientFactory _awsClientFactory;

	public S3Service(IAwsClientFactory awsClientFactory)
	{
		ArgumentNullException.ThrowIfNull(awsClientFactory);

		_awsClientFactory = awsClientFactory;
	}

	public Task InitializeAsync(IAwsCredentials credentials)
	{
		S3Client = _awsClientFactory.CreateS3Client(credentials);

		return Task.CompletedTask;
	}

	private IAmazonS3 S3Client
	{
		get => field ?? throw new InvalidOperationException("S3 Client is not initialized.");
		set;
	}

	public async Task<List<S3BucketItem>> GetBucketsAsync()
	{
		var response = await S3Client.ListBucketsAsync();
		return [.. response.Buckets.Select(b => new S3BucketItem(b.BucketName, b.CreationDate))];
	}

	public async Task<S3ObjectPage> GetObjectsAsync(string bucketName, string prefix = "", string? prefixFilter = null, int pageSize = MaximumRequestPageSize, string? continuationToken = null)
	{
		var requestPrefix = string.IsNullOrEmpty(prefixFilter) ? prefix : prefix + prefixFilter;
		var requestedItemCount = Math.Max(MinimumRequestedItemCount, pageSize);
		var items = new List<S3ObjectItem>();
		var nextContinuationToken = string.IsNullOrWhiteSpace(continuationToken) ? null : continuationToken;
		var hasMoreItems = false;

		while (items.Count < requestedItemCount)
		{
			var request = new ListObjectsV2Request
			{
				BucketName = bucketName,
				Prefix = requestPrefix,
				Delimiter = "/",
				MaxKeys = Math.Min(requestedItemCount - items.Count, MaximumRequestPageSize),
				ContinuationToken = nextContinuationToken,
			};

			var response = await S3Client.ListObjectsV2Async(request);
			AddObjectItems(items, response, prefix);

			nextContinuationToken = string.IsNullOrWhiteSpace(response.NextContinuationToken) ? null : response.NextContinuationToken;
			hasMoreItems = (response.IsTruncated ?? false) && nextContinuationToken is not null;

			if (hasMoreItems is false)
			{
				break;
			}
		}

		return new S3ObjectPage(items, hasMoreItems, nextContinuationToken);
	}

	public async Task DownloadObjectAsync(string bucketName, string key, string destinationPath, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
	{
		var directory = Path.GetDirectoryName(destinationPath);

		if (!string.IsNullOrEmpty(directory))
		{
			Directory.CreateDirectory(directory);
		}

		var request = new GetObjectRequest
		{
			BucketName = bucketName,
			Key = key,
		};

		try
		{
			using var response = await S3Client.GetObjectAsync(request, cancellationToken);
			using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81_920, useAsync: true);

			await response.ResponseStream.CopyToWithProgressAsync(
				fileStream,
				fileName: Path.GetFileName(key),
				totalBytes: response.ContentLength,
				progress,
				cancellationToken);
		}
		catch
		{
			// Any failure (cancellation, network, disk full, etc.) leaves a partial file behind.
			// Delete it so retries start clean, then re-throw the original exception.
			TryDeletePartialFile(destinationPath);
			throw;
		}
	}

	private static void TryDeletePartialFile(string path)
	{
		if (!File.Exists(path))
		{
			return;
		}

		try
		{
			File.Delete(path);
		}
		catch (IOException)
		{
			// Best-effort cleanup; nothing actionable if the file is locked or temporarily unavailable.
		}
		catch (UnauthorizedAccessException)
		{
			// Best-effort cleanup; user lacks permission to delete the partial file.
		}
	}

	public async Task DownloadFolderAsync(string bucketName, string prefix, string destinationFolder, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
	{
		var request = new ListObjectsV2Request
		{
			BucketName = bucketName,
			Prefix = prefix,
		};

		var itemsToDownload = new List<S3Object>();
		var totalBytesKnown = 0L;
		var totalFiles = 0;

		await foreach (var response in S3Client.Paginators.ListObjectsV2(request).Responses.WithCancellation(cancellationToken))
		{
			foreach (var s3Object in response.S3Objects ?? [])
			{
				var relativeKey = s3Object.Key[prefix.Length..];

				if (string.IsNullOrEmpty(relativeKey))
				{
					continue;
				}

				itemsToDownload.Add(s3Object);

				if (!s3Object.Key.EndsWith('/'))
				{
					totalFiles++;
					totalBytesKnown += s3Object.Size ?? 0;
				}
			}
		}

		var filesCompleted = 0;
		var totalBytesDownloaded = 0L;
		var folderStopwatch = Stopwatch.StartNew();
		var folderThrottleInterval = TimeSpan.FromMilliseconds(100);
		var lastFolderReportTime = TimeSpan.Zero;
		var lastReportedFilesCompleted = -1;
		var reportedTotalBytes = totalBytesKnown > 0 ? totalBytesKnown : null as long?;

		foreach (var s3Object in itemsToDownload)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var relativeKey = s3Object.Key[prefix.Length..];
			var destinationPath = Path.Combine(destinationFolder, relativeKey.Replace('/', Path.DirectorySeparatorChar));

			if (s3Object.Key.EndsWith('/'))
			{
				Directory.CreateDirectory(destinationPath);
			}
			else
			{
				var fileInitialDownloadedBytes = totalBytesDownloaded;

				var fileProgress = new SyncProgress<DownloadProgress>(
					downloadProgress =>
					{
						if (progress is null)
						{
							return;
						}

						var elapsed = folderStopwatch.Elapsed;
						var fileCountChanged = filesCompleted != lastReportedFilesCompleted;

						if (elapsed - lastFolderReportTime < folderThrottleInterval && !fileCountChanged)
						{
							return;
						}

						lastFolderReportTime = elapsed;
						lastReportedFilesCompleted = filesCompleted;

						var currentTotalBytesDownloaded = fileInitialDownloadedBytes + downloadProgress.BytesDownloaded;
						var speed = elapsed.TotalSeconds > 0 ? currentTotalBytesDownloaded / elapsed.TotalSeconds : 0;
						var remainingTime = speed > 0 && totalBytesKnown > 0
							? TimeSpan.FromSeconds(Math.Max(0, totalBytesKnown - currentTotalBytesDownloaded) / speed)
							: null as TimeSpan?;

						progress.Report(
							new DownloadProgress(
								BytesDownloaded: currentTotalBytesDownloaded,
								TotalBytes: reportedTotalBytes,
								FileName: downloadProgress.FileName,
								Speed: speed,
								ElapsedTime: elapsed,
								RemainingTime: remainingTime,
								FilesCompleted: filesCompleted,
								TotalFiles: totalFiles));
					});

				await DownloadObjectAsync(bucketName, s3Object.Key, destinationPath, fileProgress, cancellationToken);

				filesCompleted++;
				totalBytesDownloaded += s3Object.Size ?? 0;
			}
		}

		if (progress is not null && totalFiles > 0)
		{
			var elapsed = folderStopwatch.Elapsed;
			var speed = elapsed.TotalSeconds > 0 ? totalBytesDownloaded / elapsed.TotalSeconds : 0;

			progress.Report(
				new DownloadProgress(
					BytesDownloaded: totalBytesDownloaded,
					TotalBytes: reportedTotalBytes,
					FileName: string.Empty,
					Speed: speed,
					ElapsedTime: elapsed,
					RemainingTime: TimeSpan.Zero,
					FilesCompleted: filesCompleted,
					TotalFiles: totalFiles));
		}
	}

	private static void AddObjectItems(List<S3ObjectItem> items, ListObjectsV2Response response, string prefix)
	{
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
	}

}
