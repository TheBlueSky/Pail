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
	private readonly Stack<string> _pathStack = new();

	private string _bucketName = string.Empty;

	public ObjectBrowserViewModel(IS3Service s3Service, INavigationService navigationService)
	{
		_s3Service = s3Service;
		_navigationService = navigationService;
	}

	[ObservableProperty]
	public partial string CurrentPath { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial string ErrorMessage { get; set; } = string.Empty;

	public ObservableCollection<S3ObjectItem> Items { get; } = [];

	public async Task InitializeAsync(string bucketName)
	{
		_bucketName = bucketName;
		_pathStack.Clear();
		CurrentPath = string.Empty;
		await LoadItemsAsync();
	}

	[RelayCommand]
	public async Task LoadItemsAsync()
	{
		IsBusy = true;
		ErrorMessage = string.Empty;
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
			ErrorMessage = ex is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.ErrorCode == "PermanentRedirect"
				? "This bucket is in a different region than the one you connected with. Please reconnect with the correct region."
				: $"Failed to load objects: {ex.Message}";
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
			// NOTE: In a production WinUI 3 app, use FolderPicker.
			// FolderPicker requires a window handle (HWND) which can be retrieved via WinRT.Interop.WindowNative.GetWindowHandle(window).
			// For this implementation, we use a default Downloads subfolder.
			var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Pail");
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

			ErrorMessage = $"Download complete! Files saved to: {downloadsFolder}";
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Download failed: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}
}
