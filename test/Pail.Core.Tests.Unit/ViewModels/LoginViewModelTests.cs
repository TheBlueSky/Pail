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
	private readonly IClipboardService _clipboardService = Substitute.For<IClipboardService>();
	private readonly IAwsConsoleCredentialsParser _awsConsoleCredentialsParser = Substitute.For<IAwsConsoleCredentialsParser>();
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
		var viewModel = CreateViewModel();

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

		var viewModel = CreateViewModel();

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

		var viewModel = CreateViewModel();

		// Act
		await viewModel.LoadCredentialProfilesAsync();

		// Assert
		Assert.Equal(LoginViewModel.AutomaticProfileOption, viewModel.SelectedProfileName);
	}

	[Fact]
	internal async Task LoadCredentialProfilesCommand_RefreshesProfilesAndPreservesSelection()
	{
		// Arrange
		_awsProfileService.GetProfileNamesAsync().Returns(["dev", "prod"], ["dev", "stage", "prod"]);
		_appSettings.LastProfileName = "prod";

		var viewModel = CreateViewModel();
		viewModel.UseDefaultChain = true;
		await viewModel.LoadCredentialProfilesAsync();

		// Act
		await viewModel.LoadCredentialProfilesCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal([LoginViewModel.AutomaticProfileOption, "dev", "stage", "prod"], viewModel.AvailableProfiles);
		Assert.Equal("prod", viewModel.SelectedProfileName);
	}

	[Fact]
	internal async Task LoadCredentialProfilesAsync_TracksRefreshAvailability()
	{
		// Arrange
		var profilesLoaded = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
		_awsProfileService.GetProfileNamesAsync().Returns(_ => profilesLoaded.Task);

		var viewModel = CreateViewModel();
		viewModel.UseDefaultChain = true;

		// Act
		var loadTask = viewModel.LoadCredentialProfilesAsync();

		// Assert
		Assert.False(viewModel.CanRefreshProfiles);
		Assert.True(viewModel.IsLoadingCredentialProfiles);

		profilesLoaded.SetResult(["dev"]);
		await loadTask;

		Assert.True(viewModel.CanRefreshProfiles);
		Assert.False(viewModel.IsLoadingCredentialProfiles);

		viewModel.UseDefaultChain = false;

		Assert.False(viewModel.CanRefreshProfiles);
	}

	[Fact]
	internal async Task LoadCredentialProfilesCommand_Failed_ShowsErrorAndRestoresRefreshAvailability()
	{
		// Arrange
		_awsProfileService.GetProfileNamesAsync().Throws(new Exception("Profile store unavailable"));

		var viewModel = CreateViewModel();
		viewModel.UseDefaultChain = true;

		// Act
		await viewModel.LoadCredentialProfilesCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Failed to load AWS profiles: Profile store unavailable", StringComparison.Ordinal)));
		Assert.False(viewModel.IsLoadingCredentialProfiles);
		Assert.True(viewModel.CanRefreshProfiles);
	}

	[Fact]
	internal async Task LoginCommand_Successful_NavigatesToMainPage()
	{
		// Arrange
		_s3Service.GetBucketsAsync().Returns([]);
		_appSettings.DefaultRegion = "eu-west-1";
		_appSettings.UseCredentialChainByDefault = false;
		_appSettings.LastProfileName = "saved-default";

		var viewModel = CreateViewModel();
		viewModel.Region = "ap-southeast-2";
		viewModel.UseDefaultChain = true;
		viewModel.SelectedProfileName = "dev-profile";

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_navigationService.Received(1).NavigateTo("MainPage", null, true);
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

		var viewModel = CreateViewModel();

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

		var viewModel = CreateViewModel();
		viewModel.UseDefaultChain = true;
		viewModel.SelectedProfileName = "dev-profile";

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

		var viewModel = CreateViewModel();
		viewModel.UseDefaultChain = false;
		viewModel.SelectedProfileName = "dev-profile";

		// Act
		await viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("stale-profile", _appSettings.LastProfileName);
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	internal async Task PasteCredentialsCommand_ValidClipboard_PopulatesFieldsAndDisablesDefaultChain()
	{
		// Arrange
		const string clipboardText = "[ProfileName]\naws_access_key_id=key_id\naws_secret_access_key=secret\naws_session_token=token==";
		var parsedCredentials = new AwsConsoleCredentials("key_id", "secret", "token==");
		_clipboardService.ReadTextAsync().Returns(clipboardText);
		_awsConsoleCredentialsParser.Parse(clipboardText).Returns(parsedCredentials);

		var viewModel = CreateViewModel();
		viewModel.UseDefaultChain = true;

		// Act
		await viewModel.PasteCredentialsCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal(parsedCredentials.AccessKey, viewModel.AccessKey);
		Assert.Equal(parsedCredentials.SecretKey, viewModel.SecretKey);
		Assert.Equal(parsedCredentials.SessionToken, viewModel.SessionToken);
		Assert.False(viewModel.UseDefaultChain);
		_statusMessageService.DidNotReceive().ShowError(Arg.Any<string>());
	}

	[Fact]
	internal async Task PasteCredentialsCommand_EmptyClipboard_ShowsErrorWithoutParsing()
	{
		// Arrange
		_clipboardService.ReadTextAsync().Returns((string?)null);

		var viewModel = CreateViewModel();

		// Act
		await viewModel.PasteCredentialsCommand.ExecuteAsync(null);

		// Assert
		await _clipboardService.Received(1).ReadTextAsync();
		_awsConsoleCredentialsParser.DidNotReceive().Parse(Arg.Any<string>());
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Clipboard does not contain AWS Console credentials", StringComparison.Ordinal)));
	}

	[Fact]
	internal async Task PasteCredentialsCommand_InvalidClipboard_ShowsErrorAndPreservesFields()
	{
		// Arrange
		const string clipboardText = "not-aws-credentials";
		_clipboardService.ReadTextAsync().Returns(clipboardText);
		_awsConsoleCredentialsParser.Parse(clipboardText).Returns((AwsConsoleCredentials?)null);

		var viewModel = CreateViewModel();
		viewModel.AccessKey = "existing-access";
		viewModel.SecretKey = "existing-secret";
		viewModel.SessionToken = "existing-token";
		viewModel.UseDefaultChain = true;

		// Act
		await viewModel.PasteCredentialsCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("existing-access", viewModel.AccessKey);
		Assert.Equal("existing-secret", viewModel.SecretKey);
		Assert.Equal("existing-token", viewModel.SessionToken);
		Assert.True(viewModel.UseDefaultChain);
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Clipboard text is not in the expected AWS Console credential format", StringComparison.Ordinal)));
	}

	private LoginViewModel CreateViewModel() =>
		new(_awsProfileService, _s3Service, _navigationService, _settingsService, _clipboardService, _awsConsoleCredentialsParser, _statusMessageService);
}
