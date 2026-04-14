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
	private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();
	private readonly AppSettings _appSettings = new();

	public LoginViewModelTests()
	{
		_settingsService.DefaultRegion.Returns(_ => _appSettings.DefaultRegion);
		_settingsService.UseCredentialChainByDefault.Returns(_ => _appSettings.UseCredentialChainByDefault);
		_settingsService.LastProfileName.Returns(_ => _appSettings.LastProfileName);
	}

	[Fact]
	internal void LoginViewModel_UsesSettingsForRegionAndCredentialChainDefaults()
	{
		// Arrange
		_appSettings.DefaultRegion = "us-west-2";
		_appSettings.UseCredentialChainByDefault = true;
		_appSettings.LastProfileName = "dev-profile";

		// Act
		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService);

		// Assert
		Assert.Equal("us-west-2", viewModel.Region);
		Assert.True(viewModel.UseDefaultChain);
		Assert.Equal("dev-profile", viewModel.SelectedProfileName);
		Assert.Equal(viewModel.AvailableRegions.OrderBy(region => region, StringComparer.Ordinal), viewModel.AvailableRegions);
		Assert.Contains("eu-west-1", viewModel.AvailableRegions);
	}

	[Fact]
	internal async Task LoadCredentialProfilesAsync_LoadsAutomaticOptionAndAvailableProfiles()
	{
		// Arrange
		_awsProfileService.GetProfileNamesAsync().Returns(["dev", "prod"]);
		_appSettings.LastProfileName = "prod";

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService);

		// Act
		await viewModel.LoadCredentialProfilesAsync();

		// Assert
		Assert.Equal([LoginViewModel.AutomaticProfileOption, "dev", "prod"], viewModel.AvailableProfiles);
		Assert.Equal("prod", viewModel.SelectedProfileName);
	}

	[Fact]
	internal async Task LoadCredentialProfilesAsync_MissingSavedProfile_FallsBackToAutomatic()
	{
		// Arrange
		_awsProfileService.GetProfileNamesAsync().Returns(["dev", "prod"]);
		_appSettings.LastProfileName = "unknown-profile";

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService);

		// Act
		await viewModel.LoadCredentialProfilesAsync();

		// Assert
		Assert.Equal(LoginViewModel.AutomaticProfileOption, viewModel.SelectedProfileName);
	}

	[Fact]
	internal async Task LoginCommand_Successful_NavigatesToMainPage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);
		_appSettings.DefaultRegion = "eu-west-1";
		_appSettings.UseCredentialChainByDefault = false;
		_appSettings.LastProfileName = "saved-default";

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService)
		{
			Region = "ap-southeast-2",
			UseDefaultChain = true,
			SelectedProfileName = "dev-profile",
		};

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_navigationService.Received(1).NavigateTo("MainPage", null);
		Assert.Equal("eu-west-1", _appSettings.DefaultRegion);
		Assert.False(_appSettings.UseCredentialChainByDefault);
		Assert.Equal("saved-default", _appSettings.LastProfileName);
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		Assert.False(viewModel.IsBusy);
	}

	[Fact]
	internal async Task LoginCommand_Failed_ShowsErrorMessage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Throws(new Exception("Invalid credentials"));

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService);

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(s => s.Contains("Login failed: Invalid credentials")));
		_navigationService.DidNotReceive().NavigateTo(Arg.Any<string>(), Arg.Any<object>());
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	internal async Task LoginCommand_DefaultChainWithSelectedProfile_UsesProfileCredentials()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService)
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

	[Fact]
	internal async Task LoginCommand_NotUsingDefaultChain_DoesNotChangeSavedProfileDefault()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);
		_appSettings.LastProfileName = "stale-profile";

		var viewModel = new LoginViewModel(_awsProfileService, _s3Service, _navigationService, _settingsService, _statusMessageService)
		{
			UseDefaultChain = false,
			SelectedProfileName = "dev-profile",
		};

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("stale-profile", _appSettings.LastProfileName);
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
	}
}
