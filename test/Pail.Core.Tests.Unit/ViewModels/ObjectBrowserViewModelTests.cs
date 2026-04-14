using NSubstitute;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class ObjectBrowserViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly ICopyActionService _copyActionService = Substitute.For<ICopyActionService>();
	private readonly IFolderPickerService _folderPickerService = Substitute.For<IFolderPickerService>();
	private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();
	private readonly string _defaultDownloadFolder = Path.Combine(Path.GetTempPath(), "Pail.Tests", "Downloads.Default");
	private readonly string _pickedDownloadFolder = Path.Combine(Path.GetTempPath(), "Pail.Tests", "Downloads.Picked");
	private readonly AppSettings _appSettings = new()
	{
		DownloadFolder = string.Empty,
		AlwaysPromptDownloadLocation = false,
	};

	public ObjectBrowserViewModelTests()
	{
		_appSettings.DownloadFolder = _defaultDownloadFolder;
		_settingsService.DownloadFolder.Returns(_ => _appSettings.DownloadFolder);
		_settingsService.AlwaysPromptDownloadLocation.Returns(_ => _appSettings.AlwaysPromptDownloadLocation);
		_settingsService.UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				callInfo.Arg<Action<AppSettings>>().Invoke(_appSettings);
				return Task.CompletedTask;
			});
	}

	[Fact]
	internal async Task CopyObjectNameCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService)
		{
			SelectedItem = new S3ObjectItem
			{
				Name = "report.csv",
				Key = "reports/2026/report.csv",
				IsFolder = false,
			},
		};

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"report.csv",
			"Copied object name: report.csv",
			"Failed to copy object name.");
	}

	[Fact]
	internal async Task CopyObjectFullKeyCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService)
		{
			SelectedItem = new S3ObjectItem
			{
				Name = "report.csv",
				Key = "reports/2026/report.csv",
				IsFolder = false,
			},
		};

		// Act
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"reports/2026/report.csv",
			"Copied full key: reports/2026/report.csv",
			"Failed to copy full key.");
	}

	[Fact]
	internal async Task CopyCommands_NoSelection_DoNotCopy()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(null);
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);

		// Assert
		await _copyActionService.DidNotReceive().CopyWithFeedbackAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
	}

	[Fact]
	internal async Task DownloadSelectedCommand_AlwaysPromptDisabled_UsesSavedDownloadFolder()
	{
		// Arrange
		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		await _folderPickerService.DidNotReceive().PickFolderAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>());
		await _s3Service.Received(1).DownloadObjectAsync("bucket-a", "reports/report.csv", Path.Combine(_defaultDownloadFolder, "report.csv"));
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	internal async Task DownloadSelectedCommand_AlwaysPromptEnabled_SavesPickedFolderAndDownloads()
	{
		// Arrange
		_appSettings.AlwaysPromptDownloadLocation = true;
		_folderPickerService.PickFolderAsync(_defaultDownloadFolder, Arg.Any<CancellationToken>()).Returns(_pickedDownloadFolder);

		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		Assert.Equal(_pickedDownloadFolder, _appSettings.DownloadFolder);
		await _settingsService.Received(1).UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		await _s3Service.Received(1).DownloadObjectAsync("bucket-a", "reports/report.csv", Path.Combine(_pickedDownloadFolder, "report.csv"));
	}

	[Fact]
	internal async Task DownloadSelectedCommand_WhenPromptCancelled_DoesNotDownload()
	{
		// Arrange
		_appSettings.AlwaysPromptDownloadLocation = true;
		_folderPickerService.PickFolderAsync(_defaultDownloadFolder, Arg.Any<CancellationToken>()).Returns((string?)null);

		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		await _s3Service.DidNotReceive().DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		_statusMessageService.Received(1).ShowInfo("Download cancelled.");
	}

	[Fact]
	internal async Task OpenItemCommand_FolderSelected_UpdatesPathAndEnablesBucketBack()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", "").Returns(new List<S3ObjectItem>());
		_s3Service.GetObjectsAsync("bucket-a", "reports/").Returns(new List<S3ObjectItem>());

		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);
		var folder = new S3ObjectItem
		{
			Name = "reports",
			Key = "reports/",
			IsFolder = true,
		};

		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.OpenItemCommand.ExecuteAsync(folder);

		// Assert
		Assert.Equal("reports/", viewModel.CurrentPath);
		Assert.True(viewModel.CanNavigateBackWithinBucket);
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", "reports/");
	}

	[Fact]
	internal async Task GoBackCommand_WhenInsideBucket_GoesToParentWithoutLeavingPage()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", "").Returns(new List<S3ObjectItem>());
		_s3Service.GetObjectsAsync("bucket-a", "reports/").Returns(new List<S3ObjectItem>());
		_s3Service.GetObjectsAsync("bucket-a", "reports/2026/").Returns(new List<S3ObjectItem>());

		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);
		var parentFolder = new S3ObjectItem
		{
			Name = "reports",
			Key = "reports/",
			IsFolder = true,
		};
		var childFolder = new S3ObjectItem
		{
			Name = "2026",
			Key = "reports/2026/",
			IsFolder = true,
		};

		await viewModel.InitializeAsync("bucket-a");
		await viewModel.OpenItemCommand.ExecuteAsync(parentFolder);
		await viewModel.OpenItemCommand.ExecuteAsync(childFolder);

		// Act
		await viewModel.GoBackCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal("reports/", viewModel.CurrentPath);
		Assert.True(viewModel.CanNavigateBackWithinBucket);
		_navigationService.DidNotReceive().GoBack();
		await _s3Service.Received(2).GetObjectsAsync("bucket-a", "reports/");
	}

	[Fact]
	internal async Task GoBackCommand_AtBucketRoot_DelegatesToNavigationService()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", "").Returns(new List<S3ObjectItem>());

		var viewModel = new ObjectBrowserViewModel(_s3Service, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService);

		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.GoBackCommand.ExecuteAsync(null);

		// Assert
		Assert.False(viewModel.CanNavigateBackWithinBucket);
		_navigationService.Received(1).GoBack();
	}
}
