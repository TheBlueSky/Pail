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
	private readonly Stack<string> _pathStack = new();
	private string? _nextContinuationToken;

	private bool _canNavigateBackWithinBucket;
	private bool _isPreparingDownloads;
	private string _bucketName = string.Empty;

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

		UpdateLoadedItemsStatus();
	}

	[ObservableProperty]
	public partial string CurrentPath { get; set; } = string.Empty;

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

	public ObservableCollection<S3ObjectItem> Items { get; } = [];

	public bool CanNavigateBackWithinBucket
	{
		get => _canNavigateBackWithinBucket;
		private set => SetProperty(ref _canNavigateBackWithinBucket, value);
	}

	public async Task InitializeAsync(string bucketName)
	{
		_bucketName = bucketName;
		_pathStack.Clear();
		UpdateCanNavigateBackWithinBucket();
		CurrentPath = string.Empty;
		await LoadItemsAsync();
	}

	[RelayCommand]
	public async Task LoadItemsAsync()
	{
		if (IsBusy)
		{
			return;
		}

		IsBusy = true;
		IsInitialLoadInProgress = true;
		ResetListingState();

		try
		{
			var page = await _s3Service.GetObjectsAsync(_bucketName, CurrentPath, pageSize: GetInitialObjectLoadCount());

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
				_bucketName,
				CurrentPath,
				pageSize: GetLoadMoreObjectCount(),
				continuationToken: _nextContinuationToken);

			AppendItems(page.Items);
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
	private async Task OpenItemAsync(S3ObjectItem item)
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
	private async Task DownloadSelectedAsync(IList<S3ObjectItem> selectedItems)
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

				if (!string.Equals(currentDownloadFolder, selectedFolder, StringComparison.Ordinal))
				{
					await _settingsService.UpdateAsync(settings => settings.DownloadFolder = selectedFolder);
				}
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
	private async Task CopyObjectNameAsync(IList<S3ObjectItem> selectedItems)
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
	private async Task CopyObjectFullKeyAsync(IList<S3ObjectItem> selectedItems)
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

	private DownloadItem CreateDownloadItem(S3ObjectItem item, string downloadsFolder) =>
		new()
		{
			BucketName = _bucketName,
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
			Items.Add(item);
		}
	}

	private int GetInitialObjectLoadCount() => Math.Max(1, _settingsService.InitialObjectLoadCount);

	private void ResetListingState()
	{
		Items.Clear();
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
		LoadedItemsStatus = Items.Count == 1
			? HasMoreItems
				? _localizationService.GetString("ObjectLoadedOneItemMoreAvailableStatus", "Loaded 1 item, more available")
				: _localizationService.GetString("ObjectLoadedOneItemStatus", "Loaded 1 item")
			: HasMoreItems
				? _localizationService.FormatString("ObjectLoadedItemsMoreAvailableStatus", "Loaded {0} items, more available", Items.Count)
				: _localizationService.FormatString("ObjectLoadedItemsStatus", "Loaded {0} items", Items.Count);

}
