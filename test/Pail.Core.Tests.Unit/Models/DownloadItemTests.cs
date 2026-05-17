using Pail.Extensions;
using Pail.Models;

namespace Pail.Core.Tests.Unit.Models;

public sealed class DownloadItemTests
{
	[Fact]
	internal void TransitionTo_ValidTransitions_UpdatesStatus()
	{
		// Arrange
		var item = CreateItem();
		Assert.Equal(DownloadStatus.Queued, item.Status);

		// Act & Assert - Transition to Downloading
		item.TransitionTo(DownloadStatus.Downloading);
		Assert.Equal(DownloadStatus.Downloading, item.Status);
		Assert.NotNull(item.StartTime);
		Assert.Null(item.EndTime);

		// Act & Assert - Transition to Completed
		item.TransitionTo(DownloadStatus.Completed);
		Assert.Equal(DownloadStatus.Completed, item.Status);
		Assert.NotNull(item.EndTime);
	}

	[Fact]
	internal void TransitionTo_TerminalStateToDownloading_ThrowsInvalidOperationException()
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Completed);

		// Act
		var action = () => item.TransitionTo(DownloadStatus.Downloading);

		// Assert
		Assert.Throws<InvalidOperationException>(action);
	}

	[Fact]
	internal void TransitionTo_QueuedToCompleted_ThrowsInvalidOperationException()
	{
		// Arrange
		var item = CreateItem();

		// Act
		var action = () => item.TransitionTo(DownloadStatus.Completed);

		// Assert
		Assert.Throws<InvalidOperationException>(action);
	}

	[Theory]
	[InlineData(DownloadStatus.Cancelled)]
	[InlineData(DownloadStatus.Failed)]
	internal void TransitionTo_TerminalState_SetsEndTime(DownloadStatus finalStatus)
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);

		// Act
		item.TransitionTo(finalStatus, "Error occurred");

		// Assert
		Assert.Equal(finalStatus, item.Status);
		Assert.NotNull(item.EndTime);

		if (finalStatus == DownloadStatus.Failed)
		{
			Assert.Equal("Error occurred", item.ErrorMessage);
		}
	}

	[Theory]
	[InlineData(DownloadStatus.Cancelled)]
	[InlineData(DownloadStatus.Failed)]
	internal void TransitionTo_QueuedDirectlyToTerminal_SetsEndTimeAndLeavesStartTimeNull(DownloadStatus finalStatus)
	{
		// Arrange — queue-time cancel/fail: the download never started
		var item = CreateItem();

		// Act
		item.TransitionTo(finalStatus, "Cancelled before start");

		// Assert
		Assert.Equal(finalStatus, item.Status);
		Assert.Null(item.StartTime);
		Assert.NotNull(item.EndTime);
		Assert.Equal("Cancelled before start", item.ErrorMessage);
	}

	[Fact]
	internal void TransitionTo_QueuedWithErrorMessage_ThrowsArgumentException()
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Failed, "Boom");

		// Act
		var action = () => item.TransitionTo(DownloadStatus.Queued, "should not be allowed");

		// Assert
		Assert.Throws<ArgumentException>(action);
	}

	[Fact]
	internal void GetProgressPercentage_KnownTotalBytes_ReturnsCorrectValue()
	{
		// Arrange
		var item = CreateItem();
		item.TotalBytes = 1000;
		item.BytesDownloaded = 250;

		// Act
		var progress = item.GetByteProgressPercentage();

		// Assert
		Assert.Equal(25.0, progress);
	}

	[Fact]
	internal void TransitionTo_TerminalStateToQueued_ClearsAttemptState()
	{
		// Arrange
		var item = CreateItem();
		item.TotalBytes = 1024;
		item.TotalFiles = 4;
		item.BytesDownloaded = 512;
		item.Speed = 256;
		item.FilesCompleted = 2;
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Failed, "Error occurred");

		// Act
		item.TransitionTo(DownloadStatus.Queued);

		// Assert
		Assert.Equal(DownloadStatus.Queued, item.Status);
		Assert.Null(item.StartTime);
		Assert.Null(item.EndTime);
		Assert.Null(item.ErrorMessage);
		Assert.Equal(0, item.BytesDownloaded);
		Assert.Equal(0, item.Speed);
		Assert.Equal(0, item.FilesCompleted);
		Assert.Equal(1024, item.TotalBytes);
		Assert.Equal(4, item.TotalFiles);
	}

	[Fact]
	internal void TransitionTo_RetryThenDownloading_StartsFreshAttempt()
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Completed);
		item.TransitionTo(DownloadStatus.Queued);

		// Act
		item.TransitionTo(DownloadStatus.Downloading);

		// Assert
		Assert.Equal(DownloadStatus.Downloading, item.Status);
		Assert.NotNull(item.StartTime);
		Assert.Null(item.EndTime);
		Assert.Null(item.ErrorMessage);
	}

	[Fact]
	internal void GetByteProgressPercentage_UnknownTotalBytes_ReturnsNull()
	{
		// Arrange
		var item = CreateItem();

		// Act
		var progress = item.GetByteProgressPercentage();

		// Assert
		Assert.Null(progress);
	}

	[Fact]
	internal void GetByteProgressPercentage_ZeroByteFile_BeforeCompletionReturnsZero()
	{
		// Arrange
		var item = CreateItem();
		item.TotalBytes = 0;

		// Act
		var progress = item.GetByteProgressPercentage();

		// Assert
		Assert.Equal(0, progress);
	}

	[Fact]
	internal void GetByteProgressPercentage_ZeroByteFile_AfterCompletionReturnsHundred()
	{
		// Arrange
		var item = CreateItem();
		item.TotalBytes = 0;
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Completed);

		// Act
		var progress = item.GetByteProgressPercentage();

		// Assert
		Assert.Equal(100, progress);
	}

	[Fact]
	internal void GetByteProgressPercentage_FolderWithUnknownBytes_ReturnsNull()
	{
		// Arrange
		var item = new DownloadItem
		{
			BucketName = "test-bucket",
			Key = "test-key",
			DestinationPath = "C:\\test\\path",
			FileName = "test-folder",
			IsFolder = true,
			TotalBytes = null,
			TotalFiles = 10,
			FilesCompleted = 4,
		};

		// Act
		var progress = item.GetByteProgressPercentage();

		// Assert
		Assert.Null(progress);
	}

	[Fact]
	internal void GetFileProgressPercentage_KnownFileCounts_ReturnsCorrectValue()
	{
		// Arrange
		var item = new DownloadItem
		{
			BucketName = "test-bucket",
			Key = "test-key",
			DestinationPath = "C:\\test\\path",
			FileName = "test.txt",
			IsFolder = true,
			TotalBytes = null,
			TotalFiles = 10,
			FilesCompleted = 4,
		};

		// Act
		var progress = item.GetFileProgressPercentage();

		// Assert
		Assert.Equal(40.0, progress);
	}

	[Fact]
	internal void GetFileProgressPercentage_NoTotalFiles_ReturnsNull()
	{
		// Arrange
		var item = CreateItem();

		// Act
		var progress = item.GetFileProgressPercentage();

		// Assert
		Assert.Null(progress);
	}

	[Fact]
	internal void GetTimeRemaining_CalculatesCorrectly()
	{
		// Arrange
		var item = CreateItem();
		item.TotalBytes = 10000;
		item.BytesDownloaded = 5000;
		item.Speed = 1000; // 1000 bytes/sec

		// Act
		var remaining = item.GetTimeRemaining();

		// Assert
		Assert.NotNull(remaining);
		Assert.Equal(TimeSpan.FromSeconds(5), remaining);
	}

	private static DownloadItem CreateItem() =>
		new()
		{
			BucketName = "test-bucket",
			Key = "test-key",
			DestinationPath = "C:\\test\\path",
			FileName = "test.txt",
		};
}
