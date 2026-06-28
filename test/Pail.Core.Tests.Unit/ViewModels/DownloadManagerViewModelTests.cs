using NSubstitute;
using Pail.Core.Tests.Unit.TestInfrastructure;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class DownloadManagerViewModelTests
{
	private readonly IDownloadManager _manager = Substitute.For<IDownloadManager>();
	private readonly IDispatcherService _dispatcherService = CreateSynchronousDispatcher();
	private readonly IFileManagerService _fileManagerService = Substitute.For<IFileManagerService>();
	private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();

	public DownloadManagerViewModelTests()
	{
		_manager.GetActiveDownloads().Returns([]);
		_manager.CancelAsync(Arg.Any<Guid>()).Returns(Task.CompletedTask);
		_manager.CancelAllAsync().Returns(Task.CompletedTask);
		_manager.RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		_fileManagerService.ShowInFileManagerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
		_localizationService.ReturnsFallbackStrings();
	}

	[Fact]
	internal void Constructor_HydratesActiveDownloads()
	{
		// Arrange
		var item = CreateItem();
		_manager.GetActiveDownloads().Returns([item]);

		// Act
		var viewModel = CreateViewModel();

		// Assert
		var row = Assert.Single(viewModel.Items);
		Assert.Equal(item.Id, row.Id);
		Assert.Equal(item.FileName, row.FileName);
		Assert.True(viewModel.HasItems);
		Assert.Equal(1, viewModel.ActiveCount);
	}

	[Fact]
	internal void ProgressChanged_NewIdAddsRow()
	{
		// Arrange
		var viewModel = CreateViewModel();
		var item = CreateItem(totalBytes: 1000, bytesDownloaded: 250);

		// Act
		RaiseProgressChanged(item);

		// Assert
		var row = Assert.Single(viewModel.Items);
		Assert.Equal(item.Id, row.Id);
		Assert.Equal(25, row.ByteProgress);
		Assert.True(viewModel.HasItems);
	}

	[Fact]
	internal void ProgressChanged_ExistingIdRefreshesExistingRow()
	{
		// Arrange
		var item = CreateItem(totalBytes: 1000);
		_manager.GetActiveDownloads().Returns([item]);
		var viewModel = CreateViewModel();
		var row = Assert.Single(viewModel.Items);

		item.BytesDownloaded = 500;
		item.Speed = 200;
		item.TransitionTo(DownloadStatus.Downloading);

		// Act
		RaiseProgressChanged(item);

		// Assert
		Assert.Same(row, Assert.Single(viewModel.Items));
		Assert.Equal(DownloadStatus.Downloading, row.Status);
		Assert.Equal(50, row.ByteProgress);
		Assert.Equal(200, viewModel.OverallSpeed);
	}

	[Fact]
	internal void DownloadRemoved_RemovesRow()
	{
		// Arrange
		var item = CreateItem();
		_manager.GetActiveDownloads().Returns([item]);
		var viewModel = CreateViewModel();

		// Act
		RaiseDownloadRemoved(item);

		// Assert
		Assert.Empty(viewModel.Items);
		Assert.False(viewModel.HasItems);
		Assert.Equal(0, viewModel.ActiveCount);
	}

	[Fact]
	internal void Constructor_RecomputesAggregates()
	{
		// Arrange
		var queued = CreateItem(totalBytes: 1000);
		var downloading = CreateItem(status: DownloadStatus.Downloading, totalBytes: 2000, bytesDownloaded: 1000, speed: 300);
		var completed = CreateItem(status: DownloadStatus.Completed, totalBytes: 1000, bytesDownloaded: 1000, speed: 100);
		var failed = CreateItem(status: DownloadStatus.Failed);
		var cancelled = CreateItem(status: DownloadStatus.Cancelled);
		_manager.GetActiveDownloads().Returns([queued, downloading, completed, failed, cancelled]);

		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.Equal(2, viewModel.ActiveCount);
		Assert.Equal(1, viewModel.CompletedCount);
		Assert.Equal(1, viewModel.FailedCount);
		Assert.Equal(1, viewModel.CancelledCount);
		Assert.Equal(3, viewModel.FinishedCount);
		Assert.Equal(50, viewModel.OverallProgress);
		Assert.Equal(300, viewModel.OverallSpeed);
		Assert.Equal("2 active", viewModel.ActiveCountText);
		Assert.True(viewModel.HasActiveDownloads);
		Assert.True(viewModel.HasFinishedDownloads);
		Assert.True(viewModel.CanCancelAll);
		Assert.True(viewModel.CanClearFinished);
		Assert.True(viewModel.HasOverallByteProgress);
		Assert.False(viewModel.IsOverallProgressIndeterminate);
		Assert.True(viewModel.HasItems);
	}

	[Fact]
	internal void Constructor_OrdersActiveThenFailedCancelledThenCompleted()
	{
		// Arrange
		var completed = CreateItem(status: DownloadStatus.Completed);
		var failed = CreateItem(status: DownloadStatus.Failed);
		var downloading = CreateItem(status: DownloadStatus.Downloading);
		var queued = CreateItem(status: DownloadStatus.Queued);
		_manager.GetActiveDownloads().Returns([completed, failed, downloading, queued]);

		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.Equal(
			[downloading.Id, queued.Id, failed.Id, completed.Id],
			viewModel.Items.Select(item => item.Id));
	}

	[Fact]
	internal void Commands_ExposeCanExecuteFromQueueState()
	{
		// Arrange
		var active = CreateItem(status: DownloadStatus.Downloading);
		var finished = CreateItem(status: DownloadStatus.Completed);
		_manager.GetActiveDownloads().Returns([active, finished]);

		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.True(viewModel.CancelAllCommand.CanExecute(null));
		Assert.True(viewModel.ClearCompletedCommand.CanExecute(null));
	}

	[Fact]
	internal void AggregateProgress_UnknownBytesWhileActive_IsIndeterminate()
	{
		// Arrange
		var downloading = CreateItem(status: DownloadStatus.Downloading);
		_manager.GetActiveDownloads().Returns([downloading]);

		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.False(viewModel.HasOverallByteProgress);
		Assert.True(viewModel.IsOverallProgressIndeterminate);
		Assert.Equal("Progress unavailable", viewModel.OverallProgressText);
	}

	[Fact]
	internal async Task Commands_CallThroughToManager()
	{
		// Arrange
		var viewModel = CreateViewModel();
		var id = Guid.NewGuid();

		// Act
		await viewModel.CancelCommand.ExecuteAsync(id);
		await viewModel.CancelAllCommand.ExecuteAsync(null);
		viewModel.ClearCompletedCommand.Execute(null);

		// Assert
		await _manager.Received(1).CancelAsync(id);
		await _manager.Received(1).CancelAllAsync();
		_manager.Received(1).ClearCompleted();
	}

	[Fact]
	internal void Constructor_WithLargeQueue_HydratesWithoutDroppingItems()
	{
		// Arrange
		var items = Enumerable.Range(0, 150)
			.Select(index => CreateItem(
				status: index % 3 == 0 ? DownloadStatus.Downloading : DownloadStatus.Queued,
				totalBytes: 1000,
				bytesDownloaded: index % 3 == 0 ? 500 : 0,
				speed: index % 3 == 0 ? 100 : 0))
			.ToArray();
		_manager.GetActiveDownloads().Returns(items);

		// Act
		var viewModel = CreateViewModel();

		// Assert
		Assert.Equal(150, viewModel.Items.Count);
		Assert.Equal(150, viewModel.ActiveCount);
		Assert.True(viewModel.HasItems);
		Assert.True(viewModel.HasOverallByteProgress);
	}

	[Fact]
	internal void Dispose_UnsubscribesFromManagerEvents()
	{
		// Arrange
		var viewModel = CreateViewModel();
		var item = CreateItem();

		// Act
		viewModel.Dispose();
		RaiseProgressChanged(item);

		// Assert
		Assert.Empty(viewModel.Items);
		Assert.False(viewModel.HasItems);
	}

	private DownloadManagerViewModel CreateViewModel() => new(_manager, _dispatcherService, _fileManagerService, _localizationService, _statusMessageService);

	private void RaiseProgressChanged(DownloadItem item) =>
		_manager.ProgressChanged += Raise.Event<EventHandler<DownloadProgressEventArgs>>(_manager, new DownloadProgressEventArgs(item));

	private void RaiseDownloadRemoved(DownloadItem item) =>
		_manager.DownloadRemoved += Raise.Event<EventHandler<DownloadItemRemovedEventArgs>>(_manager, new DownloadItemRemovedEventArgs(item.Id, item));

	private static DownloadItem CreateItem(
		DownloadStatus status = DownloadStatus.Queued,
		long? totalBytes = null,
		long bytesDownloaded = 0,
		double speed = 0)
	{
		var item = new DownloadItem
		{
			BucketName = "bucket-a",
			Key = "file.bin",
			DestinationPath = Path.Combine(Path.GetTempPath(), "pail-test-" + Guid.NewGuid()),
			FileName = "file.bin",
			TotalBytes = totalBytes,
			BytesDownloaded = bytesDownloaded,
			Speed = speed,
		};

		ApplyStatus(item, status);
		return item;
	}

	private static void ApplyStatus(DownloadItem item, DownloadStatus status)
	{
		if (status is DownloadStatus.Queued)
		{
			return;
		}

		if (status is DownloadStatus.Downloading)
		{
			item.TransitionTo(DownloadStatus.Downloading);
			return;
		}

		if (status is DownloadStatus.Completed)
		{
			item.TransitionTo(DownloadStatus.Downloading);
			item.TransitionTo(DownloadStatus.Completed);
			return;
		}

		item.TransitionTo(status, status is DownloadStatus.Failed ? "download failed" : null);
	}

	private static IDispatcherService CreateSynchronousDispatcher()
	{
		var dispatcher = Substitute.For<IDispatcherService>();
		dispatcher.When(d => d.Run(Arg.Any<Action>())).Do(callInfo => callInfo.Arg<Action>()());
		return dispatcher;
	}
}
