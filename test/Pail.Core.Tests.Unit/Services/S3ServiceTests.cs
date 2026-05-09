using System.Reflection;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
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
		var page = await service.GetObjectsAsync("bucket-a", "logs/", 1500);

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
		var page = await service.GetObjectsAsync("bucket-a", "logs/", 1500);

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
		var page = await service.GetObjectsAsync("bucket-a", "logs/", 200, "page-6");

		// Assert
		Assert.Equal(200, page.Items.Count);
		Assert.False(page.HasMoreItems);
		Assert.Null(page.NextContinuationToken);
		Assert.Single(requests);
		Assert.Equal("page-6", requests[0].ContinuationToken);
		Assert.Equal(200, requests[0].MaxKeys);
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
}
