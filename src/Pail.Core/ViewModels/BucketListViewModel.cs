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
	private readonly IStatusMessageService _statusMessageService;
	private readonly List<S3BucketItem> _allBuckets = [];

	public BucketListViewModel(
		IS3Service s3Service,
		INavigationService navigationService,
		ICopyActionService copyActionService,
		IStatusMessageService statusMessageService)
	{
		_s3Service = s3Service;
		_navigationService = navigationService;
		_copyActionService = copyActionService;
		_statusMessageService = statusMessageService;
	}

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopyBucketNameCommand))]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CopyBucketNameCommand))]
	public partial S3BucketItem? SelectedBucket { get; set; }

	[ObservableProperty]
	public partial string SearchText { get; set; } = string.Empty;

	public ObservableCollection<S3BucketItem> Buckets { get; } = [];

	partial void OnSearchTextChanged(string value) => ApplyBucketFilter();

	[RelayCommand]
	public async Task LoadBucketsAsync()
	{
		IsBusy = true;
		_allBuckets.Clear();
		Buckets.Clear();

		try
		{
			var buckets = await _s3Service.GetBucketsAsync();

			_allBuckets.AddRange(buckets);
			ApplyBucketFilter();
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError($"Failed to load buckets: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private void ApplyBucketFilter()
	{
		Buckets.Clear();

		foreach (var bucket in _allBuckets)
		{
			if (string.IsNullOrWhiteSpace(SearchText) || bucket.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
			{
				Buckets.Add(bucket);
			}
		}

		if (SelectedBucket is not null && Buckets.Contains(SelectedBucket) is false)
		{
			SelectedBucket = null;
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
