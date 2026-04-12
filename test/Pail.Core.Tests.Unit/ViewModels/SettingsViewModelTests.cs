using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class SettingsViewModelTests
{
	private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
	private readonly IFolderPickerService _folderPickerService = Substitute.For<IFolderPickerService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();
	private readonly AppSettings _settings = new()
	{
		DownloadFolder = "D:\\Downloads",
		AlwaysPromptDownloadLocation = true,
		StatusOverlayDurationSeconds = 5,
		DefaultRegion = "us-east-1",
		UseCredentialChainByDefault = false,
		LastProfileName = "dev",
	};

	public SettingsViewModelTests()
	{
		_settingsService.Settings.Returns(_settings);
	}

	[Fact]
	internal void Constructor_LoadsValuesFromSettingsService()
	{
		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.Equal("D:\\Downloads", viewModel.DownloadFolder);
		Assert.True(viewModel.AlwaysPromptDownloadLocation);
		Assert.Equal(5, viewModel.StatusOverlayDurationSeconds);
		Assert.Equal("us-east-1", viewModel.DefaultRegion);
		Assert.False(viewModel.UseCredentialChainByDefault);
		Assert.Equal("dev", viewModel.LastProfileName);
		Assert.Contains("eu-west-1", viewModel.AvailableRegions);
	}

	[Fact]
	internal async Task SaveCommand_UpdatesSettingsAndPersistsThem()
	{
		// Arrange
		var viewModel = CreateViewModel();
		viewModel.DownloadFolder = "E:\\Exports";
		viewModel.AlwaysPromptDownloadLocation = false;
		viewModel.StatusOverlayDurationSeconds = 8;
		viewModel.DefaultRegion = "ap-south-1";
		viewModel.UseCredentialChainByDefault = true;
		viewModel.LastProfileName = "prod";

		// Act
		await viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("E:\\Exports", _settings.DownloadFolder);
		Assert.False(_settings.AlwaysPromptDownloadLocation);
		Assert.Equal(8, _settings.StatusOverlayDurationSeconds);
		Assert.Equal("ap-south-1", _settings.DefaultRegion);
		Assert.True(_settings.UseCredentialChainByDefault);
		Assert.Equal("prod", _settings.LastProfileName);
		await _settingsService.Received(1).SaveAsync();
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
		_settingsService.SaveAsync().ThrowsAsync(new InvalidOperationException("disk full"));
		var viewModel = CreateViewModel();

		// Act
		await viewModel.SaveCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Failed to save settings: disk full")));
	}

	[Fact]
	internal async Task BrowseDownloadFolderCommand_WhenFolderSelected_UpdatesDownloadFolder()
	{
		// Arrange
		_folderPickerService.PickFolderAsync("D:\\Downloads", Arg.Any<CancellationToken>()).Returns("F:\\Chosen");
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
		_folderPickerService.PickFolderAsync("D:\\Downloads", Arg.Any<CancellationToken>()).Returns((string?)null);
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
			.PickFolderAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.ThrowsAsync(new InvalidOperationException("picker unavailable"));
		var viewModel = CreateViewModel();

		// Act
		await viewModel.BrowseDownloadFolderCommand.ExecuteAsync(null);

		// Assert
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Failed to select folder: picker unavailable")));
	}

	private SettingsViewModel CreateViewModel() => new(_settingsService, _folderPickerService, _statusMessageService);
}
