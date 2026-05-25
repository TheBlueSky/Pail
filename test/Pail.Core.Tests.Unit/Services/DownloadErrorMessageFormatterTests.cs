using System.Net;
using System.Net.Sockets;
using Amazon.S3;
using NSubstitute;
using Pail.Core.Tests.Unit.TestInfrastructure;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class DownloadErrorMessageFormatterTests
{
	private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

	public DownloadErrorMessageFormatterTests()
	{
		_localizationService.ReturnsFallbackStrings();
	}

	[Fact]
	internal void Format_S3PermissionError_ReturnsFriendlyPermissionMessageAndDetails()
	{
		// Arrange
		var exception = new AmazonS3Exception("Access denied by bucket policy")
		{
			ErrorCode = "AccessDenied",
			StatusCode = HttpStatusCode.Forbidden,
		};

		// Act
		var message = DownloadErrorMessageFormatter.Format(exception, _localizationService);

		// Assert
		Assert.Equal("Pail could not read this object from S3. Check permissions and try again.", message.Summary);
		Assert.Equal("Access denied by bucket policy", message.Details);
	}

	[Fact]
	internal void Format_S3RegionMismatch_ReturnsFriendlyRegionMessage()
	{
		// Arrange
		var exception = new AmazonS3Exception("The bucket is in another region")
		{
			ErrorCode = "PermanentRedirect",
			StatusCode = HttpStatusCode.MovedPermanently,
		};

		// Act
		var message = DownloadErrorMessageFormatter.Format(exception, _localizationService);

		// Assert
		Assert.Equal("This bucket is in a different region. Reconnect using the bucket's region, then try again.", message.Summary);
	}

	[Fact]
	internal void Format_S3Throttling_ReturnsFriendlyThrottleMessage()
	{
		// Arrange
		var exception = new AmazonS3Exception("Please reduce your request rate")
		{
			ErrorCode = "SlowDown",
			StatusCode = HttpStatusCode.ServiceUnavailable,
		};

		// Act
		var message = DownloadErrorMessageFormatter.Format(exception, _localizationService);

		// Assert
		Assert.Equal("Amazon S3 is throttling download requests. Try again in a moment.", message.Summary);
	}

	[Fact]
	internal void Format_NetworkError_ReturnsFriendlyNetworkMessage()
	{
		// Arrange
		var exception = new HttpRequestException("connection lost", new SocketException());

		// Act
		var message = DownloadErrorMessageFormatter.Format(exception, _localizationService);

		// Assert
		Assert.Equal("The network connection was interrupted. Check your connection and try again.", message.Summary);
		Assert.Equal("connection lost", message.Details);
	}

	[Fact]
	internal void Format_IoError_ReturnsFriendlyStorageOrNetworkMessage()
	{
		// Arrange
		var exception = new IOException("disk full");

		// Act
		var message = DownloadErrorMessageFormatter.Format(exception, _localizationService);

		// Assert
		Assert.Equal("Pail could not finish the download. Check your connection, free disk space, and folder permissions, then try again.", message.Summary);
		Assert.Equal("disk full", message.Details);
	}
}
