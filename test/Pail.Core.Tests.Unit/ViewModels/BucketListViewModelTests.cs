using NSubstitute;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class BucketListViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

	[Fact]
	internal async Task LoadBuckets_PopulatesBucketsCollection()
	{
		// Arrange
		var viewModel = new BucketListViewModel(_s3Service, _navigationService);

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
}
