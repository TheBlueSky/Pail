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
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopyObjectNameCommand))]
	[NotifyCanExecuteChangedFor(nameof(CopyObjectFullKeyCommand))]
	public partial S3ObjectItem? SelectedItem { get; set; }

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
		IsBusy = true;
		Items.Clear();

		try
		{
			var items = await _s3Service.GetObjectsAsync(_bucketName, CurrentPath);

			foreach (var item in items)
			{
				Items.Add(item);
			}
		}
		catch (Exception ex)
		{
			var message = ex is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.ErrorCode == "PermanentRedirect"
				? "This bucket is in a different region than the one you connected with. Please reconnect with the correct region."
				: $"Failed to load objects: {ex.Message}";

			_statusMessageService.ShowError(message);
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task OpenItemAsync(S3ObjectItem item)
	{
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
				var selectedFolder = await _folderPickerService.PickFolderAsync(downloadsFolder);

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

	private static string ResolveDownloadFolder(string? downloadFolder)
	{
		if (string.IsNullOrWhiteSpace(downloadFolder))
		{
			return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Pail");
		}

		return downloadFolder;
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
}
