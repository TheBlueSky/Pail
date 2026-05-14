using NSubstitute;
using Pail.Core.Tests.Unit.TestInfrastructure;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class BucketListViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly ICopyActionService _copyActionService = Substitute.For<ICopyActionService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();
	private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

	public BucketListViewModelTests()
	{
		_localizationService.ReturnsFallbackStrings();
	}

	[Fact]
	internal async Task LoadBuckets_PopulatesBucketsCollection()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService);

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
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService)
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
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService);

		// Act
		await viewModel.CopyBucketNameCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.DidNotReceive().CopyWithFeedbackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
	}

	[Fact]
	internal async Task SearchText_FiltersBucketsCaseInsensitively()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService);

		_s3Service
			.GetBucketsAsync()
			.Returns(
			[
				new S3BucketItem("alpha-logs", null),
				new S3BucketItem("Beta-Archive", null),
				new S3BucketItem("prod-data", null),
			]);

		await viewModel.LoadBucketsCommand.ExecuteAsync(null);

		// Act
		viewModel.SearchText = "arch";

		// Assert
		Assert.Single(viewModel.Buckets);
		Assert.Equal("Beta-Archive", viewModel.Buckets[0].Name);
	}

	[Fact]
	internal async Task SearchText_EmptyString_RestoresAllBuckets()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService);

		_s3Service
			.GetBucketsAsync()
			.Returns(
			[
				new S3BucketItem("alpha", null),
				new S3BucketItem("beta", null),
				new S3BucketItem("gamma", null),
			]);

		await viewModel.LoadBucketsCommand.ExecuteAsync(null);
		viewModel.SearchText = "be";

		// Act
		viewModel.SearchText = string.Empty;

		// Assert
		Assert.Equal(3, viewModel.Buckets.Count);
		Assert.Equal("alpha", viewModel.Buckets[0].Name);
		Assert.Equal("beta", viewModel.Buckets[1].Name);
		Assert.Equal("gamma", viewModel.Buckets[2].Name);
	}

	[Fact]
	internal async Task LoadBuckets_PreservesActiveSearchFilter()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService);

		_s3Service
			.GetBucketsAsync()
			.Returns(
			[
				new S3BucketItem("prod-east", null),
				new S3BucketItem("dev-west", null),
			],
			[
				new S3BucketItem("prod-central", null),
				new S3BucketItem("stage-east", null),
			]);

		await viewModel.LoadBucketsCommand.ExecuteAsync(null);
		viewModel.SearchText = "prod";

		// Act
		await viewModel.LoadBucketsCommand.ExecuteAsync(null);

		// Assert
		Assert.Single(viewModel.Buckets);
		Assert.Equal("prod-central", viewModel.Buckets[0].Name);
	}

	[Fact]
	internal async Task SearchText_ClearsSelectionWhenSelectedBucketIsFilteredOut()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService, _copyActionService, _statusMessageService, _localizationService);

		_s3Service
			.GetBucketsAsync()
			.Returns(
			[
				new S3BucketItem("alpha", null),
				new S3BucketItem("beta", null),
			]);

		await viewModel.LoadBucketsCommand.ExecuteAsync(null);
		viewModel.SelectedBucket = viewModel.Buckets[1];

		// Act
		viewModel.SearchText = "alpha";

		// Assert
		Assert.Null(viewModel.SelectedBucket);
		Assert.False(viewModel.CopyBucketNameCommand.CanExecute(null));
	}
}
