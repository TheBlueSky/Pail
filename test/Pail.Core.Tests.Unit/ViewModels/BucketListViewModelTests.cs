using NSubstitute;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class BucketListViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly IClipboardService _clipboardService = Substitute.For<IClipboardService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();

	[Fact]
	internal async Task LoadBuckets_PopulatesBucketsCollection()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _clipboardService, _statusMessageService);

		var buckets = new List<S3BucketItem>
		{
			new(Name: "bucket1", null),
			new(Name: "bucket2", null),
		};

		_s3Service.GetBucketsAsync().Returns(buckets);

		// Act
		await viewModel.LoadBucketsCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal(2, viewModel.Buckets.Count);
		Assert.Equal("bucket1", viewModel.Buckets[0].Name);
		Assert.Equal("bucket2", viewModel.Buckets[1].Name);
	}

	[Fact]
	internal async Task CopyBucketNameCommand_SelectedBucket_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _clipboardService, _statusMessageService)
		{
			SelectedBucket = new S3BucketItem("my-bucket", null),
		};

		_clipboardService.CopyTextAsync("my-bucket").Returns(true);

		// Act
		await viewModel.CopyBucketNameCommand.ExecuteAsync(null);

		// Assert
		await _clipboardService.Received(1).CopyTextAsync("my-bucket");
		_statusMessageService.Received(1).ShowInfo("Copied bucket name: my-bucket");
	}

	[Fact]
	internal async Task CopyBucketNameCommand_NoSelection_DoesNotCopy()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _clipboardService, _statusMessageService);

		// Act
		await viewModel.CopyBucketNameCommand.ExecuteAsync(null);

		// Assert
		await _clipboardService.DidNotReceive().CopyTextAsync(Arg.Any<string>());
		_statusMessageService.DidNotReceive().ShowInfo(Arg.Any<string>());
	}
}
