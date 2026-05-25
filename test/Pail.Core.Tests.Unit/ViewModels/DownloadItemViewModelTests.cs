using System.ComponentModel;
using System.Globalization;
using NSubstitute;
using Pail.Core.Tests.Unit.TestInfrastructure;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class DownloadItemViewModelTests
{
	private readonly IDownloadManager _manager = Substitute.For<IDownloadManager>();
	private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

	public DownloadItemViewModelTests()
	{
		_manager.CancelAsync(Arg.Any<Guid>()).Returns(Task.CompletedTask);
		_manager.RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		_localizationService.ReturnsFallbackStrings();
	}

	[Fact]
	internal void ProgressText_KnownBytes_FormatsDownloadedAndTotalBytes()
	{
		// Arrange
		var item = CreateItem(totalBytes: 2048, bytesDownloaded: 1024);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:n1} KB of {1:n1} KB", 1, 2), viewModel.ProgressText);
	}

	[Fact]
	internal void ProgressText_FolderWithFileCountOnly_FormatsFileProgress()
	{
		// Arrange
		var item = CreateItem(isFolder: true, filesCompleted: 2, totalFiles: 4);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal("2 of 4 files", viewModel.ProgressText);
	}

	[Fact]
	internal void ProgressText_FolderWithFilesAndBytes_FormatsMixedProgress()
	{
		// Arrange
		var item = CreateItem(isFolder: true, bytesDownloaded: 1536, filesCompleted: 2, totalFiles: 4);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal(string.Format(CultureInfo.CurrentCulture, "2 of 4 files \u00b7 {0:n1} KB downloaded", 1.5), viewModel.ProgressText);
	}

	[Fact]
	internal void ProgressText_UnknownSizeFile_FormatsDownloadedBytesOnly()
	{
		// Arrange
		var item = CreateItem(bytesDownloaded: 512);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:n1} B downloaded", 512), viewModel.ProgressText);
	}

	[Theory]
	[InlineData(0, "B", 0)]
	[InlineData(1536, "KB", 1.5)]
	[InlineData(2097152, "MB", 2)]
	internal void SpeedText_FormatsBytesPerSecond(double speed, string suffix, double value)
	{
		// Arrange
		var item = CreateItem(speed: speed);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal(string.Format(CultureInfo.CurrentCulture, "{0:n1} {1}/s", value, suffix), viewModel.SpeedText);
	}

	[Fact]
	internal void TimeRemainingText_WhenRemainingTimeExists_FormatsDuration()
	{
		// Arrange
		var item = CreateItem(totalBytes: 2000, bytesDownloaded: 1000, speed: 250);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal("about 4s remaining", viewModel.TimeRemainingText);
	}

	[Fact]
	internal void TimeRemainingText_WhenRemainingTimeUnknown_IsBlank()
	{
		// Arrange
		var item = CreateItem(totalBytes: 2000, bytesDownloaded: 1000, speed: 0);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.Equal(string.Empty, viewModel.TimeRemainingText);
	}

	[Fact]
	internal void ProgressBarState_UnknownActiveDownload_IsIndeterminate()
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.True(viewModel.IsByteProgressIndeterminate);
		Assert.Equal(0, viewModel.ProgressBarValue);
	}

	[Fact]
	internal void ProgressBarState_UnknownCompletedDownload_IsCompleteNotIndeterminate()
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Completed);

		// Act
		var viewModel = CreateViewModel(item);

		// Assert
		Assert.False(viewModel.IsByteProgressIndeterminate);
		Assert.Equal(100, viewModel.ProgressBarValue);
	}

	[Fact]
	internal void CanCancel_TracksStatusTransitions()
	{
		// Arrange
		var item = CreateItem();
		var viewModel = CreateViewModel(item);

		// Act & Assert
		Assert.True(viewModel.CanCancel);
		Assert.True(viewModel.CancelCommand.CanExecute(null));

		item.TransitionTo(DownloadStatus.Downloading);
		viewModel.Refresh();
		Assert.True(viewModel.CanCancel);
		Assert.True(viewModel.CancelCommand.CanExecute(null));

		item.TransitionTo(DownloadStatus.Cancelled);
		viewModel.Refresh();
		Assert.False(viewModel.CanCancel);
		Assert.False(viewModel.CancelCommand.CanExecute(null));
	}

	[Fact]
	internal async Task CancelCommand_CallsManager()
	{
		// Arrange
		var item = CreateItem();
		var viewModel = CreateViewModel(item);

		// Act
		await viewModel.CancelCommand.ExecuteAsync(null);

		// Assert
		await _manager.Received(1).CancelAsync(item.Id);
	}

	[Fact]
	internal void RefreshFromItem_RaisesPropertyChangedForUpdatedValues()
	{
		// Arrange
		var item = CreateItem();
		var viewModel = CreateViewModel(item);
		var changedProperties = new HashSet<string?>();
		viewModel.PropertyChanged += OnPropertyChanged;

		item.TotalBytes = 1000;
		item.BytesDownloaded = 500;
		item.Speed = 100;
		item.TransitionTo(DownloadStatus.Failed, "disk full");

		// Act
		viewModel.Refresh();

		// Assert
		Assert.Contains(nameof(DownloadItemViewModel.Status), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.StatusText), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.CanCancel), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.ByteProgress), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.ProgressText), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.SpeedText), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.TimeRemainingText), changedProperties);
		Assert.Contains(nameof(DownloadItemViewModel.ErrorMessage), changedProperties);

		viewModel.PropertyChanged -= OnPropertyChanged;

		void OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
		{
			changedProperties.Add(args.PropertyName);
		}
	}

	[Fact]
	internal async Task RetryCommand_CallsManagerForFailedDownloads()
	{
		// Arrange
		var item = CreateItem();
		item.TransitionTo(DownloadStatus.Downloading);
		item.TransitionTo(DownloadStatus.Failed, "Could not finish download.");
		var viewModel = CreateViewModel(item);

		// Act
		await viewModel.RetryCommand.ExecuteAsync(null);

		// Assert
		Assert.True(viewModel.CanRetry);
		Assert.True(viewModel.RetryCommand.CanExecute(null));
		await _manager.Received(1).RetryAsync(item.Id, Arg.Any<CancellationToken>());
	}

	private DownloadItemViewModel CreateViewModel(DownloadItem item) => new(item, _manager, _localizationService);

	private static DownloadItem CreateItem(
		long? totalBytes = null,
		long bytesDownloaded = 0,
		double speed = 0,
		bool isFolder = false,
		int filesCompleted = 0,
		int totalFiles = 0) =>
		new()
		{
			BucketName = "bucket-a",
			Key = isFolder ? "logs/" : "file.bin",
			DestinationPath = Path.Combine(Path.GetTempPath(), "pail-test-" + Guid.NewGuid()),
			FileName = isFolder ? "logs" : "file.bin",
			TotalBytes = totalBytes,
			BytesDownloaded = bytesDownloaded,
			Speed = speed,
			IsFolder = isFolder,
			FilesCompleted = filesCompleted,
			TotalFiles = totalFiles,
		};
}
