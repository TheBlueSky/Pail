using NSubstitute;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class BucketListViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly ICopyActionService _copyActionService = Substitute.For<ICopyActionService>();

	[Fact]
	internal async Task LoadBuckets_PopulatesBucketsCollection()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService);

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
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService)
		{
			SelectedBucket = new S3BucketItem("my-bucket", null),
		};

		// Act
		await viewModel.CopyBucketNameCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"my-bucket",
			"Copied bucket name: my-bucket",
			"Failed to copy bucket name.");
	}

	[Fact]
	internal async Task CopyBucketNameCommand_NoSelection_DoesNotCopy()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService);

		// Act
		await viewModel.CopyBucketNameCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.DidNotReceive().CopyWithFeedbackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
	}
}
