using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class BucketListViewModel : ObservableObject
{
	private readonly IS3Service _s3Service;
	private readonly INavigationService _navigationService;
	private readonly ICopyActionService _copyActionService;

	public BucketListViewModel(
		IS3Service s3Service,
		INavigationService navigationService,
		ICopyActionService copyActionService)
	{
		_s3Service = s3Service;
		_navigationService = navigationService;
		_copyActionService = copyActionService;
	}

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopyBucketNameCommand))]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial string ErrorMessage { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopyBucketNameCommand))]
	public partial S3BucketItem? SelectedBucket { get; set; }

	public ObservableCollection<S3BucketItem> Buckets { get; } = [];

	[RelayCommand]
	public async Task LoadBucketsAsync()
	{
		IsBusy = true;
		ErrorMessage = string.Empty;
		Buckets.Clear();

		try
		{
			var buckets = await _s3Service.GetBucketsAsync();

			foreach (var bucket in buckets)
			{
				Buckets.Add(bucket);
			}
		}
		catch (Exception ex)
		{
			ErrorMessage = $"Failed to load buckets: {ex.Message}";
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private void SelectBucket(S3BucketItem bucket)
	{
		if (bucket is not null)
		{
			SelectedBucket = bucket;

			_navigationService.NavigateTo("ObjectBrowserPage", bucket.Name);
		}
	}

	[RelayCommand(CanExecute = nameof(CanCopySelectedBucket))]
	private async Task CopyBucketNameAsync()
	{
		if (SelectedBucket is null)
		{
			return;
		}

		await _copyActionService.CopyWithFeedbackAsync(
			SelectedBucket.Name,
			$"Copied bucket name: {SelectedBucket.Name}",
			"Failed to copy bucket name.");
	}

	private bool CanCopySelectedBucket() => IsBusy is false && SelectedBucket is not null;
}
