using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class ObjectBrowserViewModel : ObservableObject
{
	private readonly IS3Service _s3Service;
	private readonly IDownloadManager _downloadManager;
	private readonly INavigationService _navigationService;
	private readonly ICopyActionService _copyActionService;
	private readonly IFolderPickerService _folderPickerService;
	private readonly ISettingsService _settingsService;
	private readonly IStatusMessageService _statusMessageService;
	private readonly ILocalizationService _localizationService;
	private readonly string _utcTimestampDisplaySuffix;
	private readonly Stack<string> _pathStack = new();
	private readonly List<ObjectBrowserItemViewModel> _loadedItems = [];
	private readonly TimeSpan _searchDebounceDelay;

	private string? _nextContinuationToken;
	private string? _activeSearchPrefix;
	private bool _canNavigateBackWithinBucket;
	private bool _isPreparingDownloads;
	private CancellationTokenSource? _searchDebounceCts;

	public ObjectBrowserViewModel(
		IS3Service s3Service,
		IDownloadManager downloadManager,
		INavigationService navigationService,
		ICopyActionService copyActionService,
		IFolderPickerService folderPickerService,
		ISettingsService settingsService,
		IStatusMessageService statusMessageService,
		ILocalizationService localizationService)
	{
		_s3Service = s3Service;
		_downloadManager = downloadManager;
		_navigationService = navigationService;
		_copyActionService = copyActionService;
		_folderPickerService = folderPickerService;
		_settingsService = settingsService;
		_statusMessageService = statusMessageService;
		_localizationService = localizationService;
		_utcTimestampDisplaySuffix = _localizationService.GetString("ObjectTimestampUtcSuffix", "UTC");

		_searchDebounceDelay = _settingsService.ObjectSearchDebounceDelay;

		UpdateLoadedItemsStatus();
	}

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(LocationBreadcrumb))]
	public partial string BucketName { get; private set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(LocationBreadcrumb))]
	public partial string CurrentPath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string SearchText { get; set; } = string.Empty;

	public string LocationBreadcrumb =>
		string.Join(
			" / ",
			new[] { BucketName }.Concat(CurrentPath.Split('/', StringSplitOptions.RemoveEmptyEntries)));

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopyObjectNameCommand))]
	[NotifyCanExecuteChangedFor(nameof(CopyObjectFullKeyCommand))]
	[NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial bool IsInitialLoadInProgress { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
	public partial bool IsLoadingMore { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
	public partial bool HasMoreItems { get; set; }

	[ObservableProperty]
	public partial string LoadedItemsStatus { get; set; } = string.Empty;

	public ObservableCollection<ObjectBrowserItemViewModel> Items { get; } = [];

	public bool CanNavigateBackWithinBucket
	{
		get => _canNavigateBackWithinBucket;
		private set => SetProperty(ref _canNavigateBackWithinBucket, value);
	}

	public async Task InitializeAsync(string bucketName)
	{
		BucketName = bucketName;
		_pathStack.Clear();
		UpdateCanNavigateBackWithinBucket();
		CurrentPath = string.Empty;
		await LoadItemsAsync();
	}

	[RelayCommand]
	public async Task LoadItemsAsync()
	{
		ExitSearch();
		await LoadAsync(searchPrefix: null);
	}

	private async Task LoadAsync(string? searchPrefix)
	{
		if (IsBusy)
		{
			return;
		}

		IsBusy = true;
		IsInitialLoadInProgress = true;
		_activeSearchPrefix = searchPrefix;
		ResetListingState();

		try
		{
			var page = await _s3Service.GetObjectsAsync(BucketName, CurrentPath, prefixFilter: searchPrefix, pageSize: GetInitialObjectLoadCount());

			AppendItems(page.Items);
			UpdatePagingState(page);
		}
		catch (Exception ex)
		{
			ClearPagingState();

			var message = ex is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.ErrorCode == "PermanentRedirect"
				? _localizationService.GetString("BucketRegionMismatch", "This bucket is in a different region than the one you connected with. Please reconnect with the correct region.")
				: _localizationService.FormatString("ObjectLoadFailed", "Failed to load objects: {0}", ex.Message);

			_statusMessageService.ShowError(message);
		}
		finally
		{
			UpdateLoadedItemsStatus();
			IsInitialLoadInProgress = false;
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanLoadMoreItems))]
	private async Task LoadMoreAsync()
	{
		if (CanLoadMoreItems() is false)
		{
			return;
		}

		IsBusy = true;
		IsLoadingMore = true;

		try
		{
			var page = await _s3Service.GetObjectsAsync(
				BucketName,
				CurrentPath,
				_activeSearchPrefix,
				GetLoadMoreObjectCount(),
				_nextContinuationToken);

			if (_activeSearchPrefix is null)
			{
				AppendItems(page.Items);
			}
			else
			{
				AppendMissingItems(page.Items);
			}

			UpdatePagingState(page);

			int GetLoadMoreObjectCount()
			{
				var configuredValue = Math.Max(0, _settingsService.LoadMoreObjectCount);
				return configuredValue == 0 ? GetInitialObjectLoadCount() : configuredValue;
			}
		}
		catch (Exception ex)
		{
			var message = ex is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.ErrorCode == "PermanentRedirect"
				? _localizationService.GetString("BucketRegionMismatch", "This bucket is in a different region than the one you connected with. Please reconnect with the correct region.")
				: _localizationService.FormatString("ObjectLoadMoreFailed", "Failed to load more objects: {0}", ex.Message);

			_statusMessageService.ShowError(message);
		}
		finally
		{
			UpdateLoadedItemsStatus();
			IsLoadingMore = false;
			IsBusy = false;
			LoadMoreCommand.NotifyCanExecuteChanged();
		}
	}

	[RelayCommand]
	private async Task SearchAsync()
	{
		if (string.IsNullOrWhiteSpace(SearchText))
		{
			// Clearing the box returns to the unfiltered folder listing without an explicit action.
			if (_activeSearchPrefix is not null)
			{
				await LoadAsync(searchPrefix: null);
			}

			return;
		}

		if (string.Equals(_activeSearchPrefix, SearchText, StringComparison.Ordinal))
		{
			return;
		}

		await LoadSearchResultsAsync(SearchText);
	}

	private async Task LoadSearchResultsAsync(string searchPrefix)
	{
		if (IsBusy)
		{
			return;
		}

		IsBusy = true;
		IsInitialLoadInProgress = true;
		ClearPagingState();

		try
		{
			var page = await _s3Service.GetObjectsAsync(BucketName, CurrentPath, prefixFilter: searchPrefix, pageSize: GetInitialObjectLoadCount());

			_activeSearchPrefix = searchPrefix;
			AppendMissingItems(page.Items);
			UpdatePagingState(page);
		}
		catch (Exception ex)
		{
			ClearPagingState();

			var message = ex is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.ErrorCode == "PermanentRedirect"
				? _localizationService.GetString("BucketRegionMismatch", "This bucket is in a different region than the one you connected with. Please reconnect with the correct region.")
				: _localizationService.FormatString("ObjectLoadFailed", "Failed to load objects: {0}", ex.Message);

			_statusMessageService.ShowError(message);
		}
		finally
		{
			UpdateLoadedItemsStatus();
			IsInitialLoadInProgress = false;
			IsBusy = false;
		}
	}

	partial void OnSearchTextChanged(string value)
	{
		// Filter the already-loaded rows instantly (contains match), then debounce the S3 prefix search.
		ApplyLocalFilter();
		ScheduleSearch(value);
	}

	private void ScheduleSearch(string value)
	{
		_searchDebounceCts?.Cancel();
		_searchDebounceCts = new CancellationTokenSource();

		// Clearing reloads immediately; typing waits out the debounce window.
		var delay = string.IsNullOrEmpty(value) ? TimeSpan.Zero : _searchDebounceDelay;
		_ = RunDebouncedSearchAsync(delay, _searchDebounceCts.Token);
	}

	private async Task RunDebouncedSearchAsync(TimeSpan delay, CancellationToken cancellationToken)
	{
		try
		{
			if (delay > TimeSpan.Zero)
			{
				await Task.Delay(delay, cancellationToken);
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}

		if (cancellationToken.IsCancellationRequested is false)
		{
			await SearchAsync();
		}
	}

	private void ExitSearch()
	{
		_searchDebounceCts?.Cancel();
		_activeSearchPrefix = null;
		SearchText = string.Empty;
	}

	private void ApplyLocalFilter()
	{
		Items.Clear();

		foreach (var item in _loadedItems)
		{
			if (MatchesLocalFilter(item))
			{
				Items.Add(item);
			}
		}

		UpdateLoadedItemsStatus();
	}

	private bool MatchesLocalFilter(ObjectBrowserItemViewModel item) =>
		string.IsNullOrWhiteSpace(SearchText) || item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase);

	[RelayCommand]
	private async Task OpenItemAsync(ObjectBrowserItemViewModel item)
	{
		if (IsBusy)
		{
			return;
		}

		if (item.IsFolder)
		{
			_pathStack.Push(CurrentPath);
			UpdateCanNavigateBackWithinBucket();
			CurrentPath = item.Key;
			await LoadItemsAsync();
		}
	}

	[RelayCommand]
	private async Task GoBackAsync()
	{
		if (IsBusy)
		{
			return;
		}

		if (_pathStack.Count > 0)
		{
			CurrentPath = _pathStack.Pop();
			UpdateCanNavigateBackWithinBucket();
			await LoadItemsAsync();
		}
		else
		{
			_navigationService.GoBack();
		}
	}

	[RelayCommand]
	private async Task DownloadSelectedAsync(IList<ObjectBrowserItemViewModel> selectedItems)
	{
		if (selectedItems is null || !selectedItems.Any() || _isPreparingDownloads)
		{
			return;
		}

		_isPreparingDownloads = true;

		try
		{
			var selectedItemsSnapshot = selectedItems.ToArray();
			var currentDownloadFolder = _settingsService.DownloadFolder;
			var downloadsFolder = ResolveDownloadFolder(currentDownloadFolder);

			if (_settingsService.AlwaysPromptDownloadLocation)
			{
				var selectedFolder = await _folderPickerService.PickFolderAsync();

				if (string.IsNullOrWhiteSpace(selectedFolder))
				{
					_statusMessageService.ShowInfo(_localizationService.GetString("DownloadCancelled", "Download cancelled."));
					return;
				}

				downloadsFolder = selectedFolder;
			}

			Directory.CreateDirectory(downloadsFolder);

			var downloadItems = selectedItemsSnapshot
				.Select(item => CreateDownloadItem(item, downloadsFolder))
				.ToArray();

			await _downloadManager.EnqueueBatchAsync(downloadItems);

			ShowDownloadsEnqueuedMessage(downloadItems.Length, downloadsFolder);

			static string ResolveDownloadFolder(string? downloadFolder)
			{
				return string.IsNullOrWhiteSpace(downloadFolder)
					? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Pail")
					: downloadFolder;
			}
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError(_localizationService.FormatString("DownloadsEnqueueFailed", "Failed to enqueue downloads: {0}", ex.Message));
		}
		finally
		{
			_isPreparingDownloads = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedObject))]
	private async Task CopyObjectNameAsync(IList<ObjectBrowserItemViewModel> selectedItems)
	{
		if (selectedItems is null || !selectedItems.Any())
		{
			return;
		}

		var names = selectedItems.Select(item => item.Name);

		await _copyActionService.CopyWithFeedbackAsync(
			string.Join(Environment.NewLine, names),
			_localizationService.GetString("ObjectNamesCopied", "Copied object names."),
			_localizationService.GetString("ObjectNamesCopyFailed", "Failed to copy object names."));
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedObject))]
	private async Task CopyObjectFullKeyAsync(IList<ObjectBrowserItemViewModel> selectedItems)
	{
		if (selectedItems is null || !selectedItems.Any())
		{
			return;
		}

		var keys = selectedItems.Select(item => item.Key);

		await _copyActionService.CopyWithFeedbackAsync(
			string.Join(Environment.NewLine, keys),
			_localizationService.GetString("ObjectFullKeysCopied", "Copied full keys."),
			_localizationService.GetString("ObjectFullKeysCopyFailed", "Failed to copy full keys."));
	}

	private bool CanCopySelectedObject() => IsBusy is false;

	private bool CanLoadMoreItems() =>
		IsBusy is false &&
		HasMoreItems &&
		string.IsNullOrWhiteSpace(_nextContinuationToken) is false;

	private DownloadItem CreateDownloadItem(ObjectBrowserItemViewModel item, string downloadsFolder) =>
		new()
		{
			BucketName = BucketName,
			Key = item.Key,
			DestinationPath = Path.Combine(downloadsFolder, item.Name),
			FileName = item.Name,
			TotalBytes = item.IsFolder || item.Size is < 0 ? null : item.Size,
			IsFolder = item.IsFolder,
		};

	private void ShowDownloadsEnqueuedMessage(int count, string destinationFolder)
	{
		var message = count == 1
			? _localizationService.FormatString("DownloadsEnqueuedOne", "Enqueued 1 download to: {0}", destinationFolder)
			: _localizationService.FormatString("DownloadsEnqueuedMany", "Enqueued {0} downloads to: {1}", count, destinationFolder);

		_statusMessageService.ShowInfo(message);
	}

	private void UpdateCanNavigateBackWithinBucket() => CanNavigateBackWithinBucket = _pathStack.Count > 0;

	private void AppendItems(IEnumerable<S3ObjectItem> items)
	{
		foreach (var item in items)
		{
			var row = CreateRow(item);
			_loadedItems.Add(row);

			if (MatchesLocalFilter(row))
			{
				Items.Add(row);
			}
		}
	}

	private void AppendMissingItems(IEnumerable<S3ObjectItem> items)
	{
		foreach (var item in items)
		{
			if (_loadedItems.Any(loadedItem => string.Equals(loadedItem.Key, item.Key, StringComparison.Ordinal)))
			{
				continue;
			}

			var row = CreateRow(item);
			_loadedItems.Add(row);

			if (MatchesLocalFilter(row))
			{
				Items.Add(row);
			}
		}
	}

	private ObjectBrowserItemViewModel CreateRow(S3ObjectItem item) => new(item, _settingsService.ObjectTimestampDisplayMode, _utcTimestampDisplaySuffix);

	private int GetInitialObjectLoadCount() => Math.Max(1, _settingsService.InitialObjectLoadCount);

	private void ResetListingState()
	{
		Items.Clear();
		_loadedItems.Clear();
		ClearPagingState();
		UpdateLoadedItemsStatus();
	}

	private void ClearPagingState()
	{
		_nextContinuationToken = null;
		HasMoreItems = false;
		LoadMoreCommand.NotifyCanExecuteChanged();
	}

	private void UpdatePagingState(S3ObjectPage page)
	{
		_nextContinuationToken = page.NextContinuationToken;
		HasMoreItems = page.HasMoreItems;
		LoadMoreCommand.NotifyCanExecuteChanged();
		UpdateLoadedItemsStatus();
	}

	private void UpdateLoadedItemsStatus() =>
		LoadedItemsStatus = _loadedItems.Count == 1
			? HasMoreItems
				? _localizationService.GetString("ObjectLoadedOneItemMoreAvailableStatus", "Loaded 1 item, more available")
				: _localizationService.GetString("ObjectLoadedOneItemStatus", "Loaded 1 item")
			: HasMoreItems
				? _localizationService.FormatString("ObjectLoadedItemsMoreAvailableStatus", "Loaded {0} items, more available", _loadedItems.Count)
				: _localizationService.FormatString("ObjectLoadedItemsStatus", "Loaded {0} items", _loadedItems.Count);
}
