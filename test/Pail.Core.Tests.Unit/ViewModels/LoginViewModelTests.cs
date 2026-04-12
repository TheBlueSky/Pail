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
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();

	[Fact]
	internal void LoginViewModel_RegionOptions_AreSortedAndDefaultToEuWest1()
	{
		// Act
		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _statusMessageService);

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

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _statusMessageService);

		// Act
		await viewModel.LoadCredentialProfilesAsync();

		// Assert
		Assert.Equal([LoginViewModel.AutomaticProfileOption, "dev", "prod"], viewModel.AvailableProfiles);
		Assert.Equal(LoginViewModel.AutomaticProfileOption, viewModel.SelectedProfileName);
	}

	[Fact]
	internal async Task LoginCommand_Successful_NavigatesToMainPage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _statusMessageService);

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_navigationService.Received(1).NavigateTo("MainPage", null);
		Assert.False(viewModel.IsBusy);
	}

	[Fact]
	internal async Task LoginCommand_Failed_ShowsErrorMessage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Throws(new Exception("Invalid credentials"));

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _statusMessageService);

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(s => s.Contains("Login failed: Invalid credentials")));
		_navigationService.DidNotReceive().NavigateTo(Arg.Any<string>(), Arg.Any<object>());
	}

	[Fact]
	internal async Task LoginCommand_DefaultChainWithSelectedProfile_UsesProfileCredentials()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _statusMessageService)
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
