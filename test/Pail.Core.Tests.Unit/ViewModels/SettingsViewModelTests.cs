using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class SettingsViewModelTests
{
	private readonly IAppThemeService _appThemeService = Substitute.For<IAppThemeService>();
	private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
	private readonly IFolderPickerService _folderPickerService = Substitute.For<IFolderPickerService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly AppSettings _settings = new()
	{
		AppTheme = AppThemeMode.Dark,
		DownloadFolder = "D:\\Downloads",
		AlwaysPromptDownloadLocation = true,
		StatusOverlayDurationSeconds = 5,
		DefaultRegion = "us-east-1",
		UseCredentialChainByDefault = false,
		LastProfileName = "dev",
	};

	public SettingsViewModelTests()
	{
		_settingsService.AppTheme.Returns(_ => _settings.AppTheme);
		_settingsService.DownloadFolder.Returns(_ => _settings.DownloadFolder);
		_settingsService.AlwaysPromptDownloadLocation.Returns(_ => _settings.AlwaysPromptDownloadLocation);
		_settingsService.StatusOverlayDurationSeconds.Returns(_ => _settings.StatusOverlayDurationSeconds);
		_settingsService.DefaultRegion.Returns(_ => _settings.DefaultRegion);
		_settingsService.UseCredentialChainByDefault.Returns(_ => _settings.UseCredentialChainByDefault);
		_settingsService.LastProfileName.Returns(_ => _settings.LastProfileName);
		_settingsService.UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				callInfo.Arg<Action<AppSettings>>().Invoke(_settings);
				return Task.CompletedTask;
			});
	}

	[Fact]
	internal void Constructor_LoadsValuesFromSettingsService()
	{
		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.Equal(AppThemeMode.Dark, viewModel.AppTheme);
		Assert.Equal("D:\\Downloads", viewModel.DownloadFolder);
		Assert.True(viewModel.AlwaysPromptDownloadLocation);
		Assert.Equal(5, viewModel.StatusOverlayDurationSeconds);
		Assert.Equal("us-east-1", viewModel.DefaultRegion);
		Assert.False(viewModel.UseCredentialChainByDefault);
		Assert.Equal("dev", viewModel.LastProfileName);
		Assert.Contains(AppThemeMode.System, viewModel.AvailableThemes);
		Assert.Contains(AppThemeMode.Light, viewModel.AvailableThemes);
		Assert.Contains(AppThemeMode.Dark, viewModel.AvailableThemes);
		Assert.Contains("eu-west-1", viewModel.AvailableRegions);
	}

	[Fact]
	internal async Task SaveCommand_UpdatesSettingsAndPersistsThem()
	{
		// Arrange
		var viewModel = CreateViewModel();
		viewModel.AppTheme = AppThemeMode.System;
		viewModel.DownloadFolder = "E:\\Exports";
		viewModel.AlwaysPromptDownloadLocation = false;
		viewModel.StatusOverlayDurationSeconds = 8;
		viewModel.DefaultRegion = "ap-south-1";
		viewModel.UseCredentialChainByDefault = true;
		viewModel.LastProfileName = "prod";

		// Act
		await viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal(AppThemeMode.System, _settings.AppTheme);
		Assert.Equal("E:\\Exports", _settings.DownloadFolder);
		Assert.False(_settings.AlwaysPromptDownloadLocation);
		Assert.Equal(8, _settings.StatusOverlayDurationSeconds);
		Assert.Equal("ap-south-1", _settings.DefaultRegion);
		Assert.True(_settings.UseCredentialChainByDefault);
		Assert.Equal("prod", _settings.LastProfileName);
		await _settingsService.Received(1).UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		_appThemeService.Received(1).ApplyTheme(AppThemeMode.System);
		_statusMessageService.Received(1).ShowInfo("Settings saved.");
	}

	[Fact]
	internal async Task SaveCommand_BlankProfile_ClearsStoredProfile()
	{
		// Arrange
		var viewModel = CreateViewModel();
		viewModel.LastProfileName = "   ";

		// Act
		await viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		Assert.Null(_settings.LastProfileName);
	}

	[Fact]
	internal async Task SaveCommand_Failure_ShowsErrorMessage()
	{
		// Arrange
		_settingsService.UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("disk full"));
		var viewModel = CreateViewModel();

		// Act
		await viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Failed to save settings: disk full")));
	}

	[Fact]
	internal async Task SaveCommand_ThemeApplyFailure_ShowsThemeErrorMessage()
	{
		// Arrange
		const string themeErrorMessage = "theme unavailable";
		_appThemeService
			.When(service => service.ApplyTheme(AppThemeMode.Dark))
			.Do(_ => throw new InvalidOperationException(themeErrorMessage));
		var viewModel = CreateViewModel();

		// Act
		await viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal(AppThemeMode.Dark, _settings.AppTheme);
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(actualMessage => actualMessage.Contains($"Settings saved, but failed to apply theme: {themeErrorMessage}")));
	}

	[Fact]
	internal async Task BrowseDownloadFolderCommand_WhenFolderSelected_UpdatesDownloadFolder()
	{
		// Arrange
		_folderPickerService.PickFolderAsync().Returns("F:\\Chosen");
		var viewModel = CreateViewModel();

		// Act
		await viewModel.BrowseDownloadFolderCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("F:\\Chosen", viewModel.DownloadFolder);
	}

	[Fact]
	internal async Task BrowseDownloadFolderCommand_WhenCancelled_KeepsDownloadFolder()
	{
		// Arrange
		_folderPickerService.PickFolderAsync().Returns((string?)null);
		var viewModel = CreateViewModel();

		// Act
		await viewModel.BrowseDownloadFolderCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("D:\\Downloads", viewModel.DownloadFolder);
	}

	[Fact]
	internal async Task BrowseDownloadFolderCommand_OnFailure_ShowsError()
	{
		// Arrange
		_folderPickerService
			.PickFolderAsync()
			.ThrowsAsync(new InvalidOperationException("picker unavailable"));
		var viewModel = CreateViewModel();

		// Act
		await viewModel.BrowseDownloadFolderCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Failed to select folder: picker unavailable")));
	}

	private SettingsViewModel CreateViewModel() => new(_appThemeService, _settingsService, _folderPickerService, _statusMessageService, _navigationService);
}
