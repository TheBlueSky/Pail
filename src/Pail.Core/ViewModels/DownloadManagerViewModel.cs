using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public sealed partial class DownloadManagerViewModel : ObservableObject, IDisposable
{
	private static readonly string[] ByteSuffixes = ["B", "KB", "MB", "GB", "TB", "PB"];

	private readonly IDownloadManager _downloadManager;
	private readonly IDispatcherService _dispatcherService;
	private readonly IFileManagerService _fileManagerService;
	private readonly ILocalizationService _localizationService;
	private readonly IStatusMessageService _statusMessageService;
	private readonly Dictionary<Guid, DownloadItemViewModel> _downloads = [];
	private readonly Dictionary<Guid, int> _sortOrder = [];

	private bool _disposed;
	private int _nextSortOrder;

	public DownloadManagerViewModel(
		IDownloadManager downloadManager,
		IDispatcherService dispatcherService,
		IFileManagerService fileManagerService,
		ILocalizationService localizationService,
		IStatusMessageService statusMessageService)
	{
		_downloadManager = downloadManager;
		_dispatcherService = dispatcherService;
		_fileManagerService = fileManagerService;
		_localizationService = localizationService;
		_statusMessageService = statusMessageService;

		foreach (var item in _downloadManager.GetActiveDownloads())
		{
			AddItem(item);
		}

		RecomputeAggregates();

		_downloadManager.ProgressChanged += OnProgressChanged;
		_downloadManager.DownloadRemoved += OnDownloadRemoved;
	}

	public ObservableCollection<DownloadItemViewModel> Items { get; } = [];

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasActiveDownloads))]
	[NotifyPropertyChangedFor(nameof(CanCancelAll))]
	[NotifyCanExecuteChangedFor(nameof(CancelAllCommand))]
	public partial int ActiveCount { get; set; }

	[ObservableProperty]
	public partial string ActiveCountText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial int CompletedCount { get; set; }

	[ObservableProperty]
	public partial int FailedCount { get; set; }

	[ObservableProperty]
	public partial int CancelledCount { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasFinishedDownloads))]
	[NotifyPropertyChangedFor(nameof(CanClearFinished))]
	[NotifyCanExecuteChangedFor(nameof(ClearCompletedCommand))]
	public partial int FinishedCount { get; set; }

	[ObservableProperty]
	public partial double OverallProgress { get; set; }

	[ObservableProperty]
	public partial double OverallSpeed { get; set; }

	[ObservableProperty]
	public partial bool HasOverallByteProgress { get; set; }

	[ObservableProperty]
	public partial bool IsOverallProgressIndeterminate { get; set; }

	[ObservableProperty]
	public partial string OverallProgressText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string OverallSpeedText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool HasItems { get; set; }

	public bool HasActiveDownloads => ActiveCount > 0;

	public bool HasFinishedDownloads => FinishedCount > 0;

	public bool CanCancelAll => HasActiveDownloads;

	public bool CanClearFinished => HasFinishedDownloads;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_downloadManager.ProgressChanged -= OnProgressChanged;
		_downloadManager.DownloadRemoved -= OnDownloadRemoved;
		_disposed = true;
	}

	[RelayCommand]
	private Task CancelAsync(Guid id) => _downloadManager.CancelAsync(id);

	[RelayCommand(CanExecute = nameof(CanCancelAll))]
	private Task CancelAllAsync() => _downloadManager.CancelAllAsync();

	[RelayCommand(CanExecute = nameof(CanClearFinished))]
	private void ClearCompleted() => _downloadManager.ClearCompleted();

	private void OnProgressChanged(object? sender, DownloadProgressEventArgs e) =>
		_dispatcherService.Run(
			() =>
			{
				if (_disposed)
				{
					return;
				}

				if (_downloads.TryGetValue(e.Item.Id, out var downloadItemViewModel))
				{
					downloadItemViewModel.Refresh();
				}
				else
				{
					AddItem(e.Item);
				}

				RecomputeAggregates();
			});

	private void OnDownloadRemoved(object? sender, DownloadItemRemovedEventArgs e) =>
		_dispatcherService.Run(
			() =>
			{
				if (_disposed)
				{
					return;
				}

				if (_downloads.Remove(e.Id, out var downloadItemViewModel))
				{
					_sortOrder.Remove(e.Id);
					Items.Remove(downloadItemViewModel);
				}

				RecomputeAggregates();
			});

	private void AddItem(DownloadItem item)
	{
		var downloadItemViewModel = new DownloadItemViewModel(item, _downloadManager, _fileManagerService, _localizationService, _statusMessageService);
		_downloads[item.Id] = downloadItemViewModel;
		_sortOrder[item.Id] = _nextSortOrder++;
		Items.Add(downloadItemViewModel);
	}

	private void RecomputeAggregates()
	{
		ActiveCount = _downloads.Values.Count(item => item.Status is DownloadStatus.Queued or DownloadStatus.Downloading);
		ActiveCountText = _localizationService.FormatString("DownloadQueueActiveCount", "{0} active", ActiveCount);
		CompletedCount = _downloads.Values.Count(item => item.Status is DownloadStatus.Completed);
		FailedCount = _downloads.Values.Count(item => item.Status is DownloadStatus.Failed);
		CancelledCount = _downloads.Values.Count(item => item.Status is DownloadStatus.Cancelled);
		FinishedCount = CompletedCount + FailedCount + CancelledCount;
		OverallSpeed = _downloads.Values
			.Where(item => item.Status is DownloadStatus.Downloading)
			.Sum(item => item.Item.Speed);
		OverallSpeedText = _localizationService.FormatString("DownloadSpeed", "{0}/s", FormatBytes(OverallSpeed));
		HasItems = Items.Count > 0;

		var knownItems = _downloads.Values
			.Select(item => item.Item)
			.Where(item => item.TotalBytes is > 0)
			.ToArray();

		var totalBytes = knownItems.Sum(item => item.TotalBytes!.Value);
		HasOverallByteProgress = totalBytes > 0;
		IsOverallProgressIndeterminate = HasActiveDownloads && !HasOverallByteProgress;
		OverallProgress = totalBytes > 0 ? Math.Clamp((double)knownItems.Sum(item => item.BytesDownloaded) / totalBytes * 100, 0, 100) : 0;
		OverallProgressText = HasOverallByteProgress
			? _localizationService.FormatString("DownloadQueueOverallProgress", "{0:n0}%", OverallProgress)
			: _localizationService.GetString("DownloadQueueOverallProgressUnknown", "Progress unavailable");

		SortItems();
	}

	private void SortItems()
	{
		var sortedItems = Items
			.OrderBy(GetStatusSortRank)
			.ThenBy(item => _sortOrder[item.Id])
			.ToArray();

		for (var targetIndex = 0; targetIndex < sortedItems.Length; targetIndex++)
		{
			var item = sortedItems[targetIndex];
			var currentIndex = Items.IndexOf(item);

			if (currentIndex != targetIndex)
			{
				Items.Move(currentIndex, targetIndex);
			}
		}
	}

	private static int GetStatusSortRank(DownloadItemViewModel item) => item.Status switch
	{
		DownloadStatus.Queued or DownloadStatus.Downloading => 0,
		DownloadStatus.Failed or DownloadStatus.Cancelled => 1,
		DownloadStatus.Completed => 2,
		_ => 3,
	};

	private static string FormatBytes(double bytes)
	{
		if (double.IsNaN(bytes) || double.IsInfinity(bytes) || bytes < 0)
		{
			bytes = 0;
		}

		var suffixIndex = 0;
		var number = Math.Max(0, bytes);

		while (number >= 1024 && suffixIndex < ByteSuffixes.Length - 1)
		{
			number /= 1024;
			suffixIndex++;
		}

		return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:n1} {1}", number, ByteSuffixes[suffixIndex]);
	}
}
