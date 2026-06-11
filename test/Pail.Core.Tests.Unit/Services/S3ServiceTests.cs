using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class S3ServiceTests
{
	private readonly IAmazonS3 _s3Client = Substitute.For<IAmazonS3>();

	[Fact]
	internal async Task GetObjectsAsync_PageSizeExceedsS3Limit_BatchesRequestsAndCarriesContinuationToken()
	{
		// Arrange
		var requests = new List<ListObjectsV2Request>();
		_s3Client
			.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var request = callInfo.Arg<ListObjectsV2Request>();
				requests.Add(CloneRequest(request));

				return Task.FromResult(requests.Count switch
				{
					1 => CreateResponse(CreateS3Objects("logs/", 0, 1000), isTruncated: true, nextContinuationToken: "page-2"),
					2 => CreateResponse(CreateS3Objects("logs/", 1000, 500), isTruncated: true, nextContinuationToken: "page-3"),
					_ => throw new InvalidOperationException("Unexpected request count."),
				});
			});

		var service = CreateService(_s3Client);

		// Act
		var page = await service.GetObjectsAsync("bucket-a", "logs/", pageSize: 1500);

		// Assert
		Assert.Equal(1500, page.Items.Count);
		Assert.Equal("file-0000.txt", page.Items[0].Name);
		Assert.Equal("file-1499.txt", page.Items[^1].Name);
		Assert.True(page.HasMoreItems);
		Assert.Equal("page-3", page.NextContinuationToken);
		Assert.Collection(
			requests,
			request =>
			{
				Assert.Equal("bucket-a", request.BucketName);
				Assert.Equal("logs/", request.Prefix);
				Assert.Equal("/", request.Delimiter);
				Assert.Equal(1000, request.MaxKeys);
				Assert.Null(request.ContinuationToken);
			},
			request =>
			{
				Assert.Equal("bucket-a", request.BucketName);
				Assert.Equal("logs/", request.Prefix);
				Assert.Equal("/", request.Delimiter);
				Assert.Equal(500, request.MaxKeys);
				Assert.Equal("page-2", request.ContinuationToken);
			});
	}

	[Fact]
	internal async Task GetObjectsAsync_PageSizeAtS3Limit_UsesSingleRequestAndPreservesHasMoreState()
	{
		// Arrange
		var requests = new List<ListObjectsV2Request>();
		_s3Client
			.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var request = callInfo.Arg<ListObjectsV2Request>();
				requests.Add(CloneRequest(request));

				return Task.FromResult(CreateResponse(CreateS3Objects(string.Empty, 0, 1000), isTruncated: true, nextContinuationToken: "page-2"));
			});

		var service = CreateService(_s3Client);

		// Act
		var page = await service.GetObjectsAsync("bucket-a", pageSize: 1000);

		// Assert
		Assert.Equal(1000, page.Items.Count);
		Assert.True(page.HasMoreItems);
		Assert.Equal("page-2", page.NextContinuationToken);
		Assert.Single(requests);
		Assert.Equal(1000, requests[0].MaxKeys);
		Assert.Null(requests[0].ContinuationToken);
	}

	[Fact]
	internal async Task GetObjectsAsync_WhenS3RunsOutOfItemsBeforeRequestedCount_ReturnsAvailableItemsAndStops()
	{
		// Arrange
		var requests = new List<ListObjectsV2Request>();
		_s3Client
			.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var request = callInfo.Arg<ListObjectsV2Request>();
				requests.Add(CloneRequest(request));

				return Task.FromResult(CreateResponse(CreateS3Objects("logs/", 0, 700), isTruncated: false, nextContinuationToken: null));
			});

		var service = CreateService(_s3Client);

		// Act
		var page = await service.GetObjectsAsync("bucket-a", "logs/", pageSize: 1500);

		// Assert
		Assert.Equal(700, page.Items.Count);
		Assert.False(page.HasMoreItems);
		Assert.Null(page.NextContinuationToken);
		Assert.Single(requests);
		Assert.Equal(1000, requests[0].MaxKeys);
		Assert.Null(requests[0].ContinuationToken);
	}

	[Fact]
	internal async Task GetObjectsAsync_UsesIncomingContinuationTokenOnFirstRequest()
	{
		// Arrange
		var requests = new List<ListObjectsV2Request>();
		_s3Client
			.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var request = callInfo.Arg<ListObjectsV2Request>();
				requests.Add(CloneRequest(request));

				return Task.FromResult(CreateResponse(CreateS3Objects("logs/", 500, 200), isTruncated: false, nextContinuationToken: null));
			});

		var service = CreateService(_s3Client);

		// Act
		var page = await service.GetObjectsAsync("bucket-a", "logs/", pageSize: 200, continuationToken: "page-6");

		// Assert
		Assert.Equal(200, page.Items.Count);
		Assert.False(page.HasMoreItems);
		Assert.Null(page.NextContinuationToken);
		Assert.Single(requests);
		Assert.Equal("page-6", requests[0].ContinuationToken);
		Assert.Equal(200, requests[0].MaxKeys);
	}

	[Fact]
	internal async Task GetObjectsAsync_WithPrefixFilter_QueriesFolderPlusSearchTermButStripsNamesToFolder()
	{
		// Arrange
		var requests = new List<ListObjectsV2Request>();
		_s3Client
			.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var request = callInfo.Arg<ListObjectsV2Request>();
				requests.Add(CloneRequest(request));

				return Task.FromResult(
					CreateResponse(
						[
							new S3Object { Key = "logs/report-1.txt", Size = 1, LastModified = DateTime.UtcNow },
							new S3Object { Key = "logs/report-2.txt", Size = 2, LastModified = DateTime.UtcNow },
						],
						isTruncated: false,
						nextContinuationToken: null));
			});

		var service = CreateService(_s3Client);

		// Act
		var page = await service.GetObjectsAsync("bucket-a", "logs/", "rep", 1000, null);

		// Assert
		var request = Assert.Single(requests);
		Assert.Equal("logs/rep", request.Prefix);
		Assert.Equal("/", request.Delimiter);
		Assert.Collection(
			page.Items,
			item => Assert.Equal("report-1.txt", item.Name),
			item => Assert.Equal("report-2.txt", item.Name));
	}

	[Fact]
	internal async Task GetObjectsAsync_WithoutPrefixFilter_UsesFolderPrefixUnchanged()
	{
		// Arrange
		var requests = new List<ListObjectsV2Request>();
		_s3Client
			.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				requests.Add(CloneRequest(callInfo.Arg<ListObjectsV2Request>()));
				return Task.FromResult(CreateResponse(CreateS3Objects("logs/", 0, 3), isTruncated: false, nextContinuationToken: null));
			});

		var service = CreateService(_s3Client);

		// Act
		await service.GetObjectsAsync("bucket-a", "logs/", pageSize: 1000);

		// Assert
		var request = Assert.Single(requests);
		Assert.Equal("logs/", request.Prefix);
	}

	[Fact]
	internal async Task DownloadObjectAsync_ReportsProgressAndThrottles()
	{
		// Arrange
		const int bufferSize = 81_920; // 80 KB, as configured in S3Service
		const int contentLength = 204_800; // 200 KB

		var bucketName = "bucket-a";
		var key = "test.txt";
		var tempFile = Path.Combine(Path.GetTempPath(), "pail_test_" + Guid.NewGuid() + ".txt");

		var testStream = new MemoryStream(new byte[contentLength]);
		var response = new GetObjectResponse
		{
			ContentLength = contentLength,
			ResponseStream = testStream,
		};

		_s3Client
			.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(response));

		var service = CreateService(_s3Client);
		var progressUpdates = new List<DownloadProgress>();
		var progress = new SyncProgress<DownloadProgress>(progressUpdates.Add);

		try
		{
			// Act
			await service.DownloadObjectAsync(bucketName, key, tempFile, progress);

			// Assert
			Assert.NotEmpty(progressUpdates);

			var finalUpdate = progressUpdates[^1];
			Assert.Equal(contentLength, finalUpdate.BytesDownloaded);
			Assert.Equal(contentLength, finalUpdate.TotalBytes);
			Assert.Equal("test.txt", finalUpdate.FileName);

			// Check throttling (81920 buffer means 3 reads for 200 KB)
			// Meaning we expect <= 3 updates
			Assert.True(progressUpdates.Count <= contentLength / bufferSize);
		}
		finally
		{
			if (File.Exists(tempFile))
			{
				File.Delete(tempFile);
			}
		}
	}

	[Fact]
	internal async Task DownloadObjectAsync_FailureMidway_CleansUpPartialFile()
	{
		// Arrange
		var bucketName = "bucket-a";
		var key = "test.txt";
		var tempFile = Path.Combine(Path.GetTempPath(), "pail_test_" + Guid.NewGuid() + ".txt");

		var throwingStream = new DelegateStream(onRead: _ => throw new IOException("simulated disk failure"));

		var response = new GetObjectResponse
		{
			ContentLength = 204_800,
			ResponseStream = throwingStream,
		};

		_s3Client
			.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(response));

		var service = CreateService(_s3Client);

		// Act
		var action = () => service.DownloadObjectAsync(bucketName, key, tempFile);

		// Assert
		await Assert.ThrowsAsync<IOException>(action);

		Assert.False(File.Exists(tempFile), "Partial file should be cleaned up on any failure, not only cancellation.");
	}

	[Fact]
	internal async Task DownloadObjectAsync_CancelledMidway_CleansUpPartialFile()
	{
		// Arrange
		var bucketName = "bucket-a";
		var key = "test.txt";
		var tempFile = Path.Combine(Path.GetTempPath(), "pail_test_" + Guid.NewGuid() + ".txt");

		var cancellationTokenSource = new CancellationTokenSource();
		var throwingStream = new DelegateStream(
			onRead: ct =>
			{
				cancellationTokenSource.Cancel();
				cancellationTokenSource.Token.ThrowIfCancellationRequested();
				return 0;
			});

		var response = new GetObjectResponse
		{
			ContentLength = 204_800,
			ResponseStream = throwingStream,
		};

		_s3Client
			.GetObjectAsync(Arg.Any<GetObjectRequest>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(response));

		var service = CreateService(_s3Client);

		// Act
		var action = () => service.DownloadObjectAsync(bucketName, key, tempFile, null, cancellationTokenSource.Token);

		// Assert
		await Assert.ThrowsAsync<OperationCanceledException>(action);

		Assert.False(File.Exists(tempFile), "Partial file should be cleaned up on cancellation.");
	}

	private static S3Service CreateService(IAmazonS3 s3Client)
	{
		var service = new S3Service();
		var property = typeof(S3Service).GetProperty("S3Client", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("S3Client property was not found.");
		var setter = property.GetSetMethod(nonPublic: true)
			?? throw new InvalidOperationException("S3Client setter was not found.");

		setter.Invoke(service, [s3Client]);
		return service;
	}

	private static ListObjectsV2Request CloneRequest(ListObjectsV2Request request) =>
		new()
		{
			BucketName = request.BucketName,
			Prefix = request.Prefix,
			Delimiter = request.Delimiter,
			MaxKeys = request.MaxKeys,
			ContinuationToken = request.ContinuationToken,
		};

	private static ListObjectsV2Response CreateResponse(
		IEnumerable<S3Object> objects,
		bool isTruncated,
		string? nextContinuationToken) =>
		new()
		{
			S3Objects = [.. objects],
			IsTruncated = isTruncated,
			NextContinuationToken = nextContinuationToken,
		};

	private static IEnumerable<S3Object> CreateS3Objects(string prefix, int startIndex, int count)
	{
		for (var index = 0; index < count; index++)
		{
			var sequence = startIndex + index;
			yield return new S3Object
			{
				Key = $"{prefix}file-{sequence:D4}.txt",
				Size = sequence,
				LastModified = DateTime.UtcNow.AddMinutes(sequence),
			};
		}
	}

	private sealed class DelegateStream(Func<CancellationToken, int> onRead) : Stream
	{
		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;
		public override long Length => throw new NotSupportedException();
		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
		public override void Flush() { }
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return ValueTask.FromCanceled<int>(cancellationToken);
			}
			try
			{
				return new ValueTask<int>(onRead(cancellationToken));
			}
			catch (Exception ex)
			{
				return ValueTask.FromException<int>(ex);
			}
		}
	}
}
