using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class ObjectBrowserViewModel : ObservableObject
{
	private readonly IS3Service _s3Service;
	private readonly INavigationService _navigationService;
	private readonly ICopyActionService _copyActionService;
	private readonly IFolderPickerService _folderPickerService;
	private readonly ISettingsService _settingsService;
	private readonly IStatusMessageService _statusMessageService;
	private readonly Stack<string> _pathStack = new();
	private string? _nextContinuationToken;

	private bool _canNavigateBackWithinBucket;
	private string _bucketName = string.Empty;

	public ObjectBrowserViewModel(
		IS3Service s3Service,
		INavigationService navigationService,
		ICopyActionService copyActionService,
		IFolderPickerService folderPickerService,
		ISettingsService settingsService,
		IStatusMessageService statusMessageService)
	{
		_s3Service = s3Service;
		_navigationService = navigationService;
		_copyActionService = copyActionService;
		_folderPickerService = folderPickerService;
		_settingsService = settingsService;
		_statusMessageService = statusMessageService;
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
	[NotifyCanExecuteChangedFor(nameof(CopyObjectNameCommand))]
	[NotifyCanExecuteChangedFor(nameof(CopyObjectFullKeyCommand))]
	public partial S3ObjectItem? SelectedItem { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(LoadMoreCommand))]
	public partial bool HasMoreItems { get; set; }

	[ObservableProperty]
	public partial string LoadedItemsStatus { get; set; } = "Loaded 0 items";

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
			var page = await _s3Service.GetObjectsAsync(_bucketName, CurrentPath, GetInitialObjectLoadCount());

			AppendItems(page.Items);
			UpdatePagingState(page);
		}
		catch (Exception ex)
		{
			ClearPagingState();

			var message = ex is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.ErrorCode == "PermanentRedirect"
				? "This bucket is in a different region than the one you connected with. Please reconnect with the correct region."
				: $"Failed to load objects: {ex.Message}";

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
				GetLoadMoreObjectCount(),
				_nextContinuationToken);

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
				? "This bucket is in a different region than the one you connected with. Please reconnect with the correct region."
				: $"Failed to load more objects: {ex.Message}";

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

	private bool CanLoadMoreItems() =>
		IsBusy is false &&
		HasMoreItems &&
		string.IsNullOrWhiteSpace(_nextContinuationToken) is false;

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
		if (selectedItems is null || !selectedItems.Any())
		{
			return;
		}

		IsBusy = true;

		try
		{
			var currentDownloadFolder = _settingsService.DownloadFolder;
			var downloadsFolder = ResolveDownloadFolder(currentDownloadFolder);

			if (_settingsService.AlwaysPromptDownloadLocation)
			{
				var selectedFolder = await _folderPickerService.PickFolderAsync();

				if (string.IsNullOrWhiteSpace(selectedFolder))
				{
					_statusMessageService.ShowInfo("Download cancelled.");
					return;
				}

				downloadsFolder = selectedFolder;

				if (!string.Equals(currentDownloadFolder, selectedFolder, StringComparison.Ordinal))
				{
					await _settingsService.UpdateAsync(settings => settings.DownloadFolder = selectedFolder);
				}
			}

			Directory.CreateDirectory(downloadsFolder);

			foreach (var item in selectedItems)
			{
				if (item.IsFolder)
				{
					await _s3Service.DownloadFolderAsync(_bucketName, item.Key, Path.Combine(downloadsFolder, item.Name));
				}
				else
				{
					await _s3Service.DownloadObjectAsync(_bucketName, item.Key, Path.Combine(downloadsFolder, item.Name));
				}
			}

			_statusMessageService.ShowInfo($"Download complete! Files saved to: {downloadsFolder}");

			static string ResolveDownloadFolder(string? downloadFolder)
			{
				return string.IsNullOrWhiteSpace(downloadFolder)
					? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Pail")
					: downloadFolder;
			}
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError($"Download failed: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedObject))]
	private async Task CopyObjectNameAsync()
	{
		if (SelectedItem is null)
		{
			return;
		}

		await _copyActionService.CopyWithFeedbackAsync(
			SelectedItem.Name,
			$"Copied object name: {SelectedItem.Name}",
			"Failed to copy object name.");
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedObject))]
	private async Task CopyObjectFullKeyAsync()
	{
		if (SelectedItem is null)
		{
			return;
		}

		await _copyActionService.CopyWithFeedbackAsync(
			SelectedItem.Key,
			$"Copied full key: {SelectedItem.Key}",
			"Failed to copy full key.");
	}

	private bool CanCopySelectedObject() => IsBusy is false && SelectedItem is not null;

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
		SelectedItem = null;
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

	private void UpdateLoadedItemsStatus()
	{
		var label = Items.Count == 1 ? "item" : "items";
		LoadedItemsStatus = HasMoreItems
			? $"Loaded {Items.Count} {label}, more available"
			: $"Loaded {Items.Count} {label}";
	}
}
