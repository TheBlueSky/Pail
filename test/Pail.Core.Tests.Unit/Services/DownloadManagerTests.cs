using NSubstitute;
using Pail.Core.Tests.Unit.TestInfrastructure;
using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class DownloadManagerTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
	private readonly IDispatcherService _dispatcherService = CreateSynchronousDispatcher();
	private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

	public DownloadManagerTests()
	{
		_settingsService.MaxParallelDownloads.Returns(3);
		_settingsService.AutoClearCompletedDownloads.Returns(false);
		_settingsService.AutoClearCompletedDownloadsDelaySeconds.Returns(0);
		_localizationService.ReturnsFallbackStrings();
	}

	[Fact]
	internal async Task EnqueueAsync_FileSucceeds_TransitionsThroughDownloadingToCompleted()
	{
		// Arrange
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var manager = CreateManager();
		var item = CreateItem();
		var statuses = new List<DownloadStatus>();
		manager.ProgressChanged += (_, args) => statuses.Add(args.Item.Status);

		// Act
		await manager.EnqueueAsync(item);
		await WaitForStatusAsync(item, DownloadStatus.Completed);

		// Assert
		Assert.Equal(DownloadStatus.Completed, item.Status);
		Assert.Contains(DownloadStatus.Queued, statuses);
		Assert.Contains(DownloadStatus.Downloading, statuses);
		Assert.Contains(DownloadStatus.Completed, statuses);
		Assert.NotNull(item.StartTime);
		Assert.NotNull(item.EndTime);
	}

	[Fact]
	internal async Task EnqueueAsync_S3ServiceThrows_TransitionsToFailedWithMessage()
	{
		// Arrange
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromException(new IOException("disk full")));

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);
		await WaitForStatusAsync(item, DownloadStatus.Failed);

		// Assert
		Assert.Equal(DownloadStatus.Failed, item.Status);
		Assert.Equal("Pail could not finish the download. Check your connection, free disk space, and folder permissions, then try again.", item.ErrorMessage);
		Assert.Equal("disk full", item.ErrorDetails);
	}

	[Fact]
	internal async Task EnqueueAsync_S3ServiceTimesOut_TransitionsToFailed()
	{
		// Arrange
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromException(new TimeoutException("request timed out")));

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);
		await WaitForStatusAsync(item, DownloadStatus.Failed);

		// Assert
		Assert.Equal(DownloadStatus.Failed, item.Status);
		Assert.Equal("The network connection was interrupted. Check your connection and try again.", item.ErrorMessage);
		Assert.Equal("request timed out", item.ErrorDetails);
	}

	[Fact]
	internal async Task RetryAsync_FailedDownload_ResetsAndRunsAgain()
	{
		// Arrange
		var attempts = 0;
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(_ =>
			{
				var attempt = Interlocked.Increment(ref attempts);
				return attempt == 1 ? Task.FromException(new IOException("disk full")) : Task.CompletedTask;
			});

		var manager = CreateManager();
		var item = CreateItem();

		await manager.EnqueueAsync(item);
		await WaitForStatusAsync(item, DownloadStatus.Failed);

		// Act
		await manager.RetryAsync(item.Id);
		await WaitForStatusAsync(item, DownloadStatus.Completed);

		// Assert
		Assert.Equal(2, attempts);
		Assert.Equal(DownloadStatus.Completed, item.Status);
		Assert.Null(item.ErrorMessage);
		Assert.Null(item.ErrorDetails);
	}

	[Fact]
	internal async Task EnqueueAsync_ReturnsBeforeDownloadCompletesAndRegistersItem()
	{
		// Arrange
		var releaseDownload = new TaskCompletionSource();
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo => await releaseDownload.Task.WaitAsync(callInfo.Arg<CancellationToken>()));

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);

		// Assert
		Assert.Contains(manager.GetActiveDownloads(), download => download.Id == item.Id);

		releaseDownload.SetResult();
		await WaitForStatusAsync(item, DownloadStatus.Completed);
	}

	[Fact]
	internal async Task EnqueueBatchAsync_RespectsMaxParallelDownloads()
	{
		// Arrange
		_settingsService.MaxParallelDownloads.Returns(2);

		var inFlight = 0;
		var peak = 0;
		var releaseDownloads = new TaskCompletionSource();

		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo =>
			{
				var current = Interlocked.Increment(ref inFlight);
				InterlockedMax(ref peak, current);
				await releaseDownloads.Task.WaitAsync(callInfo.Arg<CancellationToken>());
				Interlocked.Decrement(ref inFlight);
			});

		var manager = CreateManager();
		var items = Enumerable.Range(0, 4).Select(_ => CreateItem()).ToArray();

		// Act
		await manager.EnqueueBatchAsync(items);

		var spinwait = new SpinWait();
		while (Volatile.Read(ref inFlight) < 2)
		{
			spinwait.SpinOnce();
		}

		releaseDownloads.SetResult();

		foreach (var item in items)
		{
			await WaitForStatusAsync(item, DownloadStatus.Completed);
		}

		// Assert
		Assert.Equal(2, peak);
	}

	[Fact]
	internal async Task EnqueueAsync_CancelledExternally_TransitionsToCancelled()
	{
		// Arrange
		var releaseDownload = new TaskCompletionSource();
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo =>
			{
				var ct = callInfo.Arg<CancellationToken>();
				await releaseDownload.Task.WaitAsync(ct);
			});

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);
		await Task.Yield();
		await manager.CancelAsync(item.Id);
		await WaitForStatusAsync(item, DownloadStatus.Cancelled);

		// Assert
		Assert.Equal(DownloadStatus.Cancelled, item.Status);
		releaseDownload.TrySetResult();
	}

	[Fact]
	internal async Task EnqueueAsync_ProgressReported_PropagatesToItemAndEvent()
	{
		// Arrange
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				var progress = callInfo.Arg<IProgress<DownloadProgress>?>();
				progress?.Report(new DownloadProgress(
					BytesDownloaded: 500,
					TotalBytes: 1000,
					FileName: "file.bin",
					Speed: 250,
					ElapsedTime: TimeSpan.FromSeconds(2),
					RemainingTime: TimeSpan.FromSeconds(2),
					FilesCompleted: 0,
					TotalFiles: 1));

				return Task.CompletedTask;
			});

		var manager = CreateManager();
		var item = CreateItem();
		var progressEvents = new List<long>();

		manager.ProgressChanged += (_, args) =>
		{
			if (args.Item.Status is DownloadStatus.Downloading)
			{
				progressEvents.Add(args.Item.BytesDownloaded);
			}
		};

		// Act
		await manager.EnqueueAsync(item);
		await WaitForStatusAsync(item, DownloadStatus.Completed);

		// Assert — at least one event during Downloading reported the bytes from S3Service
		Assert.Contains(500, progressEvents);
		Assert.Equal(1000, item.TotalBytes);
	}

	[Fact]
	internal async Task CancelAllAsync_CancelsAllInFlightDownloads()
	{
		// Arrange
		var releaseDownload = new TaskCompletionSource();
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo =>
			{
				var ct = callInfo.Arg<CancellationToken>();
				await releaseDownload.Task.WaitAsync(ct);
			});

		var manager = CreateManager();
		var items = Enumerable.Range(0, 3).Select(_ => CreateItem()).ToArray();
		await manager.EnqueueBatchAsync(items);
		await Task.Yield();

		// Act
		await manager.CancelAllAsync();
		foreach (var item in items)
		{
			await WaitForStatusAsync(item, DownloadStatus.Cancelled);
		}

		// Assert
		Assert.All(items, i => Assert.Equal(DownloadStatus.Cancelled, i.Status));
		releaseDownload.TrySetResult();
	}

	[Fact]
	internal async Task ClearCompleted_RemovesOnlyTerminalDownloads()
	{
		// Arrange — one completed, one in progress.
		var holdDownload = new TaskCompletionSource();
		var firstCallIsCompleted = true;

		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo =>
			{
				if (firstCallIsCompleted)
				{
					firstCallIsCompleted = false;
					return;
				}

				await holdDownload.Task.WaitAsync(callInfo.Arg<CancellationToken>());
			});

		var manager = CreateManager();
		var completedItem = CreateItem();
		var inFlightItem = CreateItem();

		await manager.EnqueueAsync(completedItem);
		await WaitForStatusAsync(completedItem, DownloadStatus.Completed);
		await manager.EnqueueAsync(inFlightItem);
		await Task.Yield();

		// Act
		manager.ClearCompleted();
		var active = manager.GetActiveDownloads();

		// Assert
		Assert.DoesNotContain(active, i => i.Id == completedItem.Id);
		Assert.Contains(active, i => i.Id == inFlightItem.Id);

		holdDownload.SetResult();
		await WaitForStatusAsync(inFlightItem, DownloadStatus.Completed);
	}

	[Fact]
	internal async Task DownloadRemoved_RaisedOnClearCompleted()
	{
		// Arrange
		var holdDownload = new TaskCompletionSource();
		var firstCallIsCompleted = true;

		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo =>
			{
				if (firstCallIsCompleted)
				{
					firstCallIsCompleted = false;
					return;
				}

				await holdDownload.Task.WaitAsync(callInfo.Arg<CancellationToken>());
			});

		var manager = CreateManager();
		var completedItem = CreateItem();
		var inFlightItem = CreateItem();
		var removedItems = new List<DownloadItemRemovedEventArgs>();
		manager.DownloadRemoved += (_, args) => removedItems.Add(args);

		await manager.EnqueueAsync(completedItem);
		await WaitForStatusAsync(completedItem, DownloadStatus.Completed);
		await manager.EnqueueAsync(inFlightItem);
		await Task.Yield();

		// Act
		manager.ClearCompleted();

		// Assert
		var removed = Assert.Single(removedItems);
		Assert.Equal(completedItem.Id, removed.Id);
		Assert.Same(completedItem, removed.Item);

		holdDownload.SetResult();
		await WaitForStatusAsync(inFlightItem, DownloadStatus.Completed);
	}

	[Fact]
	internal async Task GetActiveDownloads_BeforeStart_IncludesQueuedItem()
	{
		// Arrange — block the download so the item stays in flight.
		var release = new TaskCompletionSource();
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo => await release.Task.WaitAsync(callInfo.Arg<CancellationToken>()));

		var manager = CreateManager();
		var item = CreateItem();
		await manager.EnqueueAsync(item);

		// Act
		var active = manager.GetActiveDownloads();

		// Assert
		Assert.Contains(active, i => i.Id == item.Id);

		release.SetResult();
		await WaitForStatusAsync(item, DownloadStatus.Completed);
	}

	[Fact]
	internal async Task EnqueueAsync_FolderItem_RoutesToDownloadFolderAsync()
	{
		// Arrange
		_s3Service
			.DownloadFolderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var manager = CreateManager();
		var folder = new DownloadItem
		{
			BucketName = "b",
			Key = "logs/",
			DestinationPath = Path.Combine(Path.GetTempPath(), "logs"),
			FileName = "logs",
			IsFolder = true,
		};

		// Act
		await manager.EnqueueAsync(folder);
		await WaitForStatusAsync(folder, DownloadStatus.Completed);

		// Assert
		await _s3Service.Received(1).DownloadFolderAsync("b", "logs/", folder.DestinationPath, Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>());
		await _s3Service.DidNotReceive().DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>());
		Assert.Equal(DownloadStatus.Completed, folder.Status);
	}

	[Fact]
	internal async Task EnqueueAsync_AutoClearEnabled_RemovesAfterDelay()
	{
		// Arrange
		_settingsService.AutoClearCompletedDownloads.Returns(true);
		_settingsService.AutoClearCompletedDownloadsDelaySeconds.Returns(0);

		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);

		// Wait briefly for the auto-clear continuation to run.
		var deadline = DateTime.UtcNow.AddSeconds(2);
		while (manager.GetActiveDownloads().Any(i => i.Id == item.Id) && DateTime.UtcNow < deadline)
		{
			await Task.Delay(20);
		}

		// Assert
		Assert.DoesNotContain(manager.GetActiveDownloads(), i => i.Id == item.Id);
	}

	[Fact]
	internal async Task EnqueueAsync_AutoClearEnabled_RemovesCancelledAfterDelay()
	{
		// Arrange
		_settingsService.AutoClearCompletedDownloads.Returns(true);
		_settingsService.AutoClearCompletedDownloadsDelaySeconds.Returns(0);

		var releaseDownload = new TaskCompletionSource();
		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(async callInfo => await releaseDownload.Task.WaitAsync(callInfo.Arg<CancellationToken>()));

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);
		await Task.Yield();
		await manager.CancelAsync(item.Id);
		await WaitForStatusAsync(item, DownloadStatus.Cancelled);

		var deadline = DateTime.UtcNow.AddSeconds(2);
		while (manager.GetActiveDownloads().Any(i => i.Id == item.Id) && DateTime.UtcNow < deadline)
		{
			await Task.Delay(20);
		}

		// Assert
		Assert.DoesNotContain(manager.GetActiveDownloads(), i => i.Id == item.Id);
		releaseDownload.TrySetResult();
	}

	[Fact]
	internal async Task EnqueueAsync_AutoClearEnabled_KeepsFailedDownloads()
	{
		// Arrange
		_settingsService.AutoClearCompletedDownloads.Returns(true);
		_settingsService.AutoClearCompletedDownloadsDelaySeconds.Returns(0);

		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(_ => Task.FromException(new IOException("disk full")));

		var manager = CreateManager();
		var item = CreateItem();

		// Act
		await manager.EnqueueAsync(item);
		await WaitForStatusAsync(item, DownloadStatus.Failed);
		await Task.Delay(100);

		// Assert
		Assert.Contains(manager.GetActiveDownloads(), i => i.Id == item.Id);
	}

	[Fact]
	internal async Task DownloadRemoved_RaisedOnAutoClear()
	{
		// Arrange
		_settingsService.AutoClearCompletedDownloads.Returns(true);
		_settingsService.AutoClearCompletedDownloadsDelaySeconds.Returns(0);

		_s3Service
			.DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var manager = CreateManager();
		var item = CreateItem();
		var removedItem = new TaskCompletionSource<DownloadItemRemovedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
		manager.DownloadRemoved += (_, args) => removedItem.TrySetResult(args);

		// Act
		await manager.EnqueueAsync(item);
		var removed = await removedItem.Task.WaitAsync(TimeSpan.FromSeconds(2));

		// Assert
		Assert.Equal(item.Id, removed.Id);
		Assert.Same(item, removed.Item);
	}

	private DownloadManager CreateManager() => new(_s3Service, _settingsService, _dispatcherService, _localizationService);

	private static DownloadItem CreateItem() => new()
	{
		BucketName = "bucket-a",
		Key = "object-key",
		DestinationPath = Path.Combine(Path.GetTempPath(), "pail-test-" + Guid.NewGuid()),
		FileName = "file.bin",
	};

	private static IDispatcherService CreateSynchronousDispatcher()
	{
		var dispatcher = Substitute.For<IDispatcherService>();
		dispatcher.When(d => d.Run(Arg.Any<Action>())).Do(callInfo => callInfo.Arg<Action>()());
		return dispatcher;
	}

	private static void InterlockedMax(ref int target, int value)
	{
		int snapshot;

		do
		{
			snapshot = Volatile.Read(ref target);

			if (value <= snapshot)
			{
				return;
			}
		}
		while (Interlocked.CompareExchange(ref target, value, snapshot) != snapshot);
	}

	private static async Task WaitForStatusAsync(DownloadItem item, DownloadStatus expectedStatus)
	{
		var deadline = DateTimeOffset.UtcNow.AddSeconds(2);

		while (item.Status != expectedStatus && DateTimeOffset.UtcNow < deadline)
		{
			await Task.Delay(20);
		}

		Assert.Equal(expectedStatus, item.Status);
	}
}
