using NSubstitute;
using Pail.Core.Tests.Unit.TestInfrastructure;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.Core.Tests.Unit.ViewModels;

public sealed class ObjectBrowserViewModelTests
{
	private readonly IS3Service _s3Service = Substitute.For<IS3Service>();
	private readonly IDownloadManager _downloadManager = Substitute.For<IDownloadManager>();
	private readonly INavigationService _navigationService = Substitute.For<INavigationService>();
	private readonly ICopyActionService _copyActionService = Substitute.For<ICopyActionService>();
	private readonly IFolderPickerService _folderPickerService = Substitute.For<IFolderPickerService>();
	private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();
	private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
	private readonly List<DownloadItem> _enqueuedItems = [];
	private readonly string _defaultDownloadFolder = Path.Combine(Path.GetTempPath(), "Pail.Tests", "Downloads.Default");
	private readonly string _pickedDownloadFolder = Path.Combine(Path.GetTempPath(), "Pail.Tests", "Downloads.Picked");
	private readonly AppSettings _appSettings = new()
	{
		DownloadFolder = string.Empty,
		AlwaysPromptDownloadLocation = false,
		InitialObjectLoadCount = 2,
		LoadMoreObjectCount = 0,
	};

	public ObjectBrowserViewModelTests()
	{
		_appSettings.DownloadFolder = _defaultDownloadFolder;
		_s3Service
			.GetObjectsAsync(Arg.Any<string>(), Arg.Any<string>(), pageSize: Arg.Any<int>(), continuationToken: Arg.Any<string?>())
			.Returns(Task.FromResult(CreatePage()));
		_downloadManager
			.EnqueueBatchAsync(Arg.Any<IEnumerable<DownloadItem>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				_enqueuedItems.AddRange(callInfo.Arg<IEnumerable<DownloadItem>>());
				return Task.CompletedTask;
			});
		_settingsService.DownloadFolder.Returns(_ => _appSettings.DownloadFolder);
		_settingsService.AlwaysPromptDownloadLocation.Returns(_ => _appSettings.AlwaysPromptDownloadLocation);
		_settingsService.InitialObjectLoadCount.Returns(_ => _appSettings.InitialObjectLoadCount);
		_settingsService.LoadMoreObjectCount.Returns(_ => _appSettings.LoadMoreObjectCount);
		_settingsService
			.UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo =>
			{
				callInfo.Arg<Action<AppSettings>>().Invoke(_appSettings);
				return Task.CompletedTask;
			});

		// Keep the debounce window open so the S3 search does not fire during the test.
		_settingsService.ObjectSearchDebounceDelay.Returns(TimeSpan.FromHours(1));

		_localizationService.ReturnsFallbackStrings();
	}

	[Fact]
	internal async Task CopyObjectNameCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = CreateViewModel();
		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.CopyObjectNameCommand.ExecuteAsync(selectedItems);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"report.csv",
			"Copied object names.",
			"Failed to copy object names.");
	}

	[Fact]
	internal async Task CopyObjectFullKeyCommand_SelectedItem_CopiesAndShowsSuccessMessage()
	{
		// Arrange
		var viewModel = CreateViewModel();
		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.CopyObjectFullKeyCommand.ExecuteAsync(selectedItems);

		// Assert
		await _copyActionService.Received(1).CopyWithFeedbackAsync(
			"reports/report.csv",
			"Copied full keys.",
			"Failed to copy full keys.");
	}

	[Fact]
	internal async Task CopyCommands_NoSelection_DoNotCopy()
	{
		// Arrange
		var viewModel = CreateViewModel();

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
		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", Size = 123, IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		await _folderPickerService.DidNotReceive().PickFolderAsync();
		var item = Assert.Single(_enqueuedItems);
		Assert.Equal("bucket-a", item.BucketName);
		Assert.Equal("reports/report.csv", item.Key);
		Assert.Equal(Path.Combine(_defaultDownloadFolder, "report.csv"), item.DestinationPath);
		Assert.Equal("report.csv", item.FileName);
		Assert.Equal(123, item.TotalBytes);
		Assert.False(item.IsFolder);
		await _downloadManager.Received(1).EnqueueBatchAsync(Arg.Any<IEnumerable<DownloadItem>>(), Arg.Any<CancellationToken>());
		await _s3Service.DidNotReceive().DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>());
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		_statusMessageService.Received(1).ShowInfo($"Enqueued 1 download to: {_defaultDownloadFolder}");
	}

	[Fact]
	internal async Task DownloadSelectedCommand_AlwaysPromptEnabled_SavesPickedFolderAndQueuesDownload()
	{
		// Arrange
		_appSettings.AlwaysPromptDownloadLocation = true;
		_folderPickerService.PickFolderAsync().Returns(_pickedDownloadFolder);

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		Assert.Equal(_pickedDownloadFolder, _appSettings.DownloadFolder);
		var item = Assert.Single(_enqueuedItems);
		Assert.Equal(Path.Combine(_pickedDownloadFolder, "report.csv"), item.DestinationPath);
		await _settingsService.Received(1).UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		await _downloadManager.Received(1).EnqueueBatchAsync(Arg.Any<IEnumerable<DownloadItem>>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	internal async Task DownloadSelectedCommand_WhenPromptCancelled_DoesNotDownload()
	{
		// Arrange
		_appSettings.AlwaysPromptDownloadLocation = true;
		_folderPickerService.PickFolderAsync().Returns((string?)null);

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		await _downloadManager.DidNotReceive().EnqueueBatchAsync(Arg.Any<IEnumerable<DownloadItem>>(), Arg.Any<CancellationToken>());
		await _s3Service.DidNotReceive().DownloadObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<DownloadProgress>?>(), Arg.Any<CancellationToken>());
		await _settingsService.DidNotReceive().UpdateAsync(Arg.Any<Action<AppSettings>>(), Arg.Any<CancellationToken>());
		_statusMessageService.Received(1).ShowInfo("Download cancelled.");
	}

	[Fact]
	internal async Task DownloadSelectedCommand_MultipleSelection_QueuesSeparateDownloads()
	{
		// Arrange
		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", Size = 1024, IsFolder = false },
			new() { Name = "logs", Key = "logs/", IsFolder = true },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		Assert.Collection(
			_enqueuedItems,
			item =>
			{
				Assert.Equal("reports/report.csv", item.Key);
				Assert.Equal(Path.Combine(_defaultDownloadFolder, "report.csv"), item.DestinationPath);
				Assert.Equal(1024, item.TotalBytes);
				Assert.False(item.IsFolder);
			},
			item =>
			{
				Assert.Equal("logs/", item.Key);
				Assert.Equal(Path.Combine(_defaultDownloadFolder, "logs"), item.DestinationPath);
				Assert.Null(item.TotalBytes);
				Assert.True(item.IsFolder);
			});
		_statusMessageService.Received(1).ShowInfo($"Enqueued 2 downloads to: {_defaultDownloadFolder}");
	}

	[Fact]
	internal async Task DownloadSelectedCommand_UnknownSizeObject_QueuesWithoutByteTotal()
	{
		// Arrange
		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "unknown.bin", Key = "unknown.bin", Size = -1, IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		var item = Assert.Single(_enqueuedItems);
		Assert.Null(item.TotalBytes);
	}

	[Fact]
	internal async Task DownloadSelectedCommand_DoesNotSetBusyWhileQueueingDownloads()
	{
		// Arrange
		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		var selectedItems = new List<S3ObjectItem>
		{
			new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
		};

		// Act
		await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

		// Assert
		Assert.False(viewModel.IsBusy);
	}

	[Fact]
	internal async Task DownloadSelectedCommand_WhenDestinationCannotBePrepared_ShowsErrorAndQueuesNothing()
	{
		// Arrange
		var blockedPath = Path.Combine(Path.GetTempPath(), "pail-download-folder-" + Guid.NewGuid());
		File.WriteAllText(blockedPath, string.Empty);
		_appSettings.DownloadFolder = blockedPath;

		try
		{
			var viewModel = CreateViewModel();
			await viewModel.InitializeAsync("bucket-a");

			var selectedItems = new List<S3ObjectItem>
			{
				new() { Name = "report.csv", Key = "reports/report.csv", IsFolder = false },
			};

			// Act
			await viewModel.DownloadSelectedCommand.ExecuteAsync(selectedItems);

			// Assert
			Assert.Empty(_enqueuedItems);
			await _downloadManager.DidNotReceive().EnqueueBatchAsync(Arg.Any<IEnumerable<DownloadItem>>(), Arg.Any<CancellationToken>());
			_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.StartsWith("Failed to enqueue downloads:", StringComparison.Ordinal)));
		}
		finally
		{
			File.Delete(blockedPath);
		}
	}

	[Fact]
	internal async Task InitializeAsync_LoadsConfiguredInitialBatchAndUpdatesStatus()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null).Returns(
			Task.FromResult(
				CreatePage(
					[
						CreateObject("report-1.csv", "reports/report-1.csv"),
						CreateObject("report-2.csv", "reports/report-2.csv"),
					],
					hasMoreItems: true,
					nextContinuationToken: "page-2")));

		var viewModel = CreateViewModel();

		// Act
		await viewModel.InitializeAsync("bucket-a");

		// Assert
		Assert.Equal(2, viewModel.Items.Count);
		Assert.Equal("Loaded 2 items, more available", viewModel.LoadedItemsStatus);
		Assert.True(viewModel.HasMoreItems);
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null);
	}

	[Fact]
	internal async Task LoadMoreCommand_AppendsNextPageWithoutReplacingItems()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null).Returns(
			Task.FromResult(
				CreatePage(
					[
						CreateObject("report-1.csv", "reports/report-1.csv"),
						CreateObject("report-2.csv", "reports/report-2.csv"),
					],
					hasMoreItems: true,
					nextContinuationToken: "page-2")));
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: "page-2").Returns(
			Task.FromResult(
				CreatePage(
					[
						CreateObject("report-3.csv", "reports/report-3.csv"),
					],
					hasMoreItems: false,
					nextContinuationToken: null)));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.LoadMoreCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal(3, viewModel.Items.Count);
		Assert.Collection(
			viewModel.Items,
			item => Assert.Equal("report-1.csv", item.Name),
			item => Assert.Equal("report-2.csv", item.Name),
			item => Assert.Equal("report-3.csv", item.Name));
		Assert.Equal("Loaded 3 items", viewModel.LoadedItemsStatus);
		Assert.False(viewModel.HasMoreItems);
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: "page-2");
	}

	[Fact]
	internal async Task LoadMoreCommand_LoadMoreCountZero_ReusesInitialLoadCount()
	{
		// Arrange
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("report-1.csv", "reports/report-1.csv")], hasMoreItems: true, nextContinuationToken: "page-2")));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.LoadMoreCommand.ExecuteAsync(null);

		// Assert
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: "page-2");
	}

	[Fact]
	internal async Task LoadMoreCommand_UsesConfiguredLoadMoreCountWhenSpecified()
	{
		// Arrange
		_appSettings.LoadMoreObjectCount = 1500;
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("report-1.csv", "reports/report-1.csv")], hasMoreItems: true, nextContinuationToken: "page-2")));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.LoadMoreCommand.ExecuteAsync(null);

		// Assert
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", pageSize: 1500, continuationToken: "page-2");
	}

	[Fact]
	internal async Task InitializeAsync_AllowsLargeInitialLoadCounts()
	{
		// Arrange
		_appSettings.InitialObjectLoadCount = 2500;
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2500, continuationToken: null).Returns(Task.FromResult(CreatePage()));

		var viewModel = CreateViewModel();

		// Act
		await viewModel.InitializeAsync("bucket-a");

		// Assert
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", pageSize: 2500, continuationToken: null);
	}

	[Fact]
	internal async Task LoadMoreCommand_WhenLoadFails_KeepsExistingItems()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null).Returns(
			Task.FromResult(
				CreatePage(
					[
						CreateObject("report-1.csv", "reports/report-1.csv"),
						CreateObject("report-2.csv", "reports/report-2.csv"),
					],
					hasMoreItems: true,
					nextContinuationToken: "page-2")));
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: "page-2")
			.Returns<Task<S3ObjectPage>>(_ => throw new InvalidOperationException("network glitch"));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.LoadMoreCommand.ExecuteAsync(null);

		// Assert
		Assert.Equal(2, viewModel.Items.Count);
		Assert.Equal("Loaded 2 items, more available", viewModel.LoadedItemsStatus);
		Assert.True(viewModel.HasMoreItems);
		_statusMessageService.Received(1).ShowError(Arg.Is<string>(message => message.Contains("Failed to load more objects: network glitch")));
	}

	[Fact]
	internal async Task SearchText_FiltersLoadedItemsByContainsWithoutQueryingS3()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: null, pageSize: 2, continuationToken: null).Returns(
			Task.FromResult(
				CreatePage(
					[
						CreateObject("annual-report.csv", "annual-report.csv"),
						CreateObject("summary.txt", "summary.txt"),
					],
					hasMoreItems: false,
					nextContinuationToken: null)));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");

		// Act
		viewModel.SearchText = "REPORT";

		// Assert
		var item = Assert.Single(viewModel.Items);
		Assert.Equal("annual-report.csv", item.Name);
		// Status still reflects everything loaded, not just the filtered view.
		Assert.Equal("Loaded 2 items", viewModel.LoadedItemsStatus);
		await _s3Service.DidNotReceive().GetObjectsAsync(
			Arg.Any<string>(),
			Arg.Any<string>(),
			Arg.Any<string?>(),
			Arg.Any<int>(),
			Arg.Is<string?>(searchPrefix => searchPrefix != null));
	}

	[Fact]
	internal async Task SearchCommand_WithTerm_QueriesS3WithSearchPrefixAndReplacesItems()
	{
		// Arrange
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: null, pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("summary.txt", "summary.txt")])));
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: "rep", pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(
				CreatePage(
					[
						CreateObject("report-1.csv", "report-1.csv"),
						CreateObject("report-2.csv", "report-2.csv"),
					],
					hasMoreItems: true,
					nextContinuationToken: "page-2")));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");
		viewModel.SearchText = "rep";

		// Act
		await viewModel.SearchCommand.ExecuteAsync(null);

		// Assert
		Assert.Collection(
			viewModel.Items,
			item => Assert.Equal("report-1.csv", item.Name),
			item => Assert.Equal("report-2.csv", item.Name));
		Assert.True(viewModel.HasMoreItems);
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", prefixFilter: "rep", pageSize: 2, continuationToken: null);
	}

	[Fact]
	internal async Task SearchCommand_ClearedAfterSearch_ReloadsFolderWithoutSearchPrefix()
	{
		// Arrange
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: null, pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("summary.txt", "summary.txt")])));
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: "rep", pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("report-1.csv", "report-1.csv")])));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");
		viewModel.SearchText = "rep";
		await viewModel.SearchCommand.ExecuteAsync(null);

		// Act
		viewModel.SearchText = string.Empty;
		await viewModel.SearchCommand.ExecuteAsync(null);

		// Assert
		var item = Assert.Single(viewModel.Items);
		Assert.Equal("summary.txt", item.Name);
		await _s3Service.Received(2).GetObjectsAsync("bucket-a", prefix: "", prefixFilter: null, pageSize: 2, continuationToken: null);
	}

	[Fact]
	internal async Task LoadMoreCommand_DuringSearch_ContinuesWithSearchPrefix()
	{
		// Arrange
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: null, pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("summary.txt", "summary.txt")])));
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: "rep", pageSize: 2, continuationToken: null)
			.Returns(Task.FromResult(CreatePage([CreateObject("report-1.csv", "report-1.csv")], hasMoreItems: true, nextContinuationToken: "page-2")));
		_s3Service
			.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: "rep", pageSize: 2, continuationToken: "page-2")
			.Returns(Task.FromResult(CreatePage([CreateObject("report-2.csv", "report-2.csv")])));

		var viewModel = CreateViewModel();
		await viewModel.InitializeAsync("bucket-a");
		viewModel.SearchText = "rep";
		await viewModel.SearchCommand.ExecuteAsync(null);

		// Act
		await viewModel.LoadMoreCommand.ExecuteAsync(null);

		// Assert
		Assert.Collection(
			viewModel.Items,
			item => Assert.Equal("report-1.csv", item.Name),
			item => Assert.Equal("report-2.csv", item.Name));
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "", prefixFilter: "rep", pageSize: 2, continuationToken: "page-2");
	}

	[Fact]
	internal async Task OpenItemCommand_FolderSelected_UpdatesPathAndEnablesBucketBack()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", "", prefixFilter: null, pageSize: 2, continuationToken: null).Returns(Task.FromResult(CreatePage()));
		_s3Service.GetObjectsAsync("bucket-a", "reports/", prefixFilter: null, pageSize: 2, continuationToken: null).Returns(Task.FromResult(CreatePage()));

		var viewModel = CreateViewModel();
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
		await _s3Service.Received(1).GetObjectsAsync("bucket-a", prefix: "reports/", pageSize: 2, continuationToken: null);
	}

	[Fact]
	internal async Task GoBackCommand_WhenInsideBucket_GoesToParentWithoutLeavingPage()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", prefixFilter: null, pageSize: 2, continuationToken: null).Returns(Task.FromResult(CreatePage()));
		_s3Service.GetObjectsAsync("bucket-a", prefix: "reports/", prefixFilter: null, pageSize: 2, continuationToken: null).Returns(Task.FromResult(CreatePage()));
		_s3Service.GetObjectsAsync("bucket-a", prefix: "reports/2026/", prefixFilter: null, pageSize: 2, continuationToken: null).Returns(Task.FromResult(CreatePage()));

		var viewModel = CreateViewModel();
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
		await _s3Service.Received(2).GetObjectsAsync("bucket-a", prefix: "reports/", pageSize: 2, continuationToken: null);
	}

	[Fact]
	internal async Task GoBackCommand_AtBucketRoot_DelegatesToNavigationService()
	{
		// Arrange
		_s3Service.GetObjectsAsync("bucket-a", prefix: "", pageSize: 2, continuationToken: null).Returns(Task.FromResult(CreatePage()));

		var viewModel = CreateViewModel();

		await viewModel.InitializeAsync("bucket-a");

		// Act
		await viewModel.GoBackCommand.ExecuteAsync(null);

		// Assert
		Assert.False(viewModel.CanNavigateBackWithinBucket);
		_navigationService.Received(1).GoBack();
	}

	private ObjectBrowserViewModel CreateViewModel() =>
		new(_s3Service, _downloadManager, _navigationService, _copyActionService, _folderPickerService, _settingsService, _statusMessageService, _localizationService);

	private static S3ObjectItem CreateObject(string name, string key) =>
		new()
		{
			Name = name,
			Key = key,
			IsFolder = false,
		};

	private static S3ObjectPage CreatePage(
		IEnumerable<S3ObjectItem>? items = null,
		bool hasMoreItems = false,
		string? nextContinuationToken = null) =>
		new([.. items ?? []], hasMoreItems, nextContinuationToken);
}
