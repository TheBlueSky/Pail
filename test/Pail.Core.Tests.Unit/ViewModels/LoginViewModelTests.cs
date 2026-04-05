using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class LoginViewModelTests
{
	private readonly IAwsProfileService _awsProfileService = Substitute.For<IAwsProfileService>();
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

	[Fact]
	internal void LoginViewModel_RegionOptions_AreSortedAndDefaultToEuWest1()
	{
		// Act
		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService);

		// Assert
		Assert.Equal("eu-west-1", viewModel.Region);
		Assert.Equal(viewModel.AvailableRegions.OrderBy(region => region, StringComparer.Ordinal), viewModel.AvailableRegions);
		Assert.Contains("eu-west-1", viewModel.AvailableRegions);
	}

	[Fact]
	internal async Task LoadCredentialProfilesAsync_LoadsAutomaticOptionAndAvailableProfiles()
	{
		// Arrange
		_awsProfileService.GetProfileNamesAsync().Returns(["dev", "prod"]);

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService);

		// Act
		await viewModel.LoadCredentialProfilesAsync();

		// Assert
		Assert.Equal([LoginViewModel.AutomaticProfileOption, "dev", "prod"], viewModel.AvailableProfiles);
		Assert.Equal(LoginViewModel.AutomaticProfileOption, viewModel.SelectedProfileName);
	}

	[Fact]
	internal async Task LoginCommand_Successful_NavigatesToBucketListPage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService);

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_navigationService.Received(1).NavigateTo("BucketListPage", null);
		Assert.False(viewModel.IsBusy);
		Assert.Empty(viewModel.ErrorMessage);
	}

	[Fact]
	internal async Task LoginCommand_Failed_SetsErrorMessage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Throws(new Exception("Invalid credentials"));

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService);

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		Assert.Contains("Login failed: Invalid credentials", viewModel.ErrorMessage);
		_navigationService.DidNotReceive().NavigateTo(Arg.Any<string>(), Arg.Any<object>());
	}

	[Fact]
	internal async Task LoginCommand_DefaultChainWithSelectedProfile_UsesProfileCredentials()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService)
		{
			UseDefaultChain = true,
			SelectedProfileName = "dev-profile",
		};

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		await _s3Service.Received(1).InitializeAsync(Arg.Any<IAwsCredentials>());

		var initializeCall = _s3Service.ReceivedCalls().Single(call => call.GetMethodInfo().Name == nameof(IS3Service.InitializeAsync));
		var credentials = Assert.IsType<AwsDefaultChainCredentials>(initializeCall.GetArguments()[0]);

		Assert.Equal("eu-west-1", credentials.Region);
		Assert.Equal("dev-profile", credentials.ProfileName);
	}
}
