using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Extensions;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public sealed partial class DownloadItemViewModel : ObservableObject
{
	private static readonly string[] ByteSuffixes = ["B", "KB", "MB", "GB", "TB", "PB"];

	private readonly IDownloadManager _manager;
	private readonly ILocalizationService _localizationService;

	public DownloadItemViewModel(DownloadItem item, IDownloadManager manager, ILocalizationService localizationService)
	{
		ArgumentNullException.ThrowIfNull(item);
		ArgumentNullException.ThrowIfNull(manager);
		ArgumentNullException.ThrowIfNull(localizationService);

		Item = item;

		_manager = manager;
		_localizationService = localizationService;

		Refresh();
	}

	public Guid Id => Item.Id;

	public string FileName => Item.FileName;

	public bool IsFolder => Item.IsFolder;

	internal DownloadItem Item { get; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanCancel))]
	[NotifyPropertyChangedFor(nameof(CanRetry))]
	[NotifyPropertyChangedFor(nameof(StatusText))]
	[NotifyPropertyChangedFor(nameof(IsByteProgressIndeterminate))]
	[NotifyPropertyChangedFor(nameof(ProgressBarValue))]
	[NotifyCanExecuteChangedFor(nameof(CancelCommand))]
	[NotifyCanExecuteChangedFor(nameof(RetryCommand))]
	public partial DownloadStatus Status { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsByteProgressIndeterminate))]
	[NotifyPropertyChangedFor(nameof(ProgressBarValue))]
	public partial double? ByteProgress { get; set; }

	[ObservableProperty]
	public partial double? FileProgress { get; set; }

	[ObservableProperty]
	public partial string ProgressText { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string SpeedText { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasTimeRemainingText))]
	public partial string TimeRemainingText { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(HasError))]
	public partial string? ErrorMessage { get; set; }

	public bool IsByteProgressIndeterminate => ByteProgress is null && Status is (DownloadStatus.Queued or DownloadStatus.Downloading);

	public double ProgressBarValue => ByteProgress ?? (Status is DownloadStatus.Completed ? 100 : 0);

	public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

	public bool HasTimeRemainingText => !string.IsNullOrWhiteSpace(TimeRemainingText);

	public string CancelAutomationName => _localizationService.FormatString("DownloadCancelAutomationName", "Cancel download {0}", FileName);

	public string StatusText => Status switch
	{
		DownloadStatus.Queued => _localizationService.GetString("DownloadStatusQueued", "Queued"),
		DownloadStatus.Downloading => _localizationService.GetString("DownloadStatusDownloading", "Downloading"),
		DownloadStatus.Completed => _localizationService.GetString("DownloadStatusCompleted", "Completed"),
		DownloadStatus.Failed => _localizationService.GetString("DownloadStatusFailed", "Failed"),
		DownloadStatus.Cancelled => _localizationService.GetString("DownloadStatusCancelled", "Cancelled"),
		_ => Status.ToString(),
	};

	public bool CanCancel => Status is DownloadStatus.Queued or DownloadStatus.Downloading;

	public bool CanRetry => Status is DownloadStatus.Failed;

	internal void Refresh()
	{
		Status = Item.Status;
		ByteProgress = Item.GetByteProgressPercentage();
		FileProgress = Item.GetFileProgressPercentage();
		ProgressText = GetProgressText();
		SpeedText = _localizationService.FormatString("DownloadSpeed", "{0}/s", FormatBytes(Item.Speed));
		TimeRemainingText = GetTimeRemainingText();
		ErrorMessage = Item.ErrorMessage;
	}

	[RelayCommand(CanExecute = nameof(CanCancel))]
	private Task CancelAsync() => _manager.CancelAsync(Item.Id);

	[RelayCommand(CanExecute = nameof(CanRetry))]
	private Task RetryAsync() => _manager.RetryAsync(Item.Id);

	public string RetryAutomationName => _localizationService.FormatString("DownloadRetryAutomationName", "Retry download {0}", FileName);

	private string GetProgressText() =>
		Item switch
		{
			{ IsFolder: true, TotalFiles: > 0, BytesDownloaded: > 0 } => _localizationService.FormatString(
				"DownloadProgressFilesWithBytes",
				"{0} of {1} files \u00b7 {2} downloaded",
				Item.FilesCompleted,
				Item.TotalFiles,
				FormatBytes(Item.BytesDownloaded)),
			{ IsFolder: true, TotalFiles: > 0, BytesDownloaded: <= 0 } => _localizationService.FormatString(
				"DownloadProgressFiles",
				"{0} of {1} files",
				Item.FilesCompleted,
				Item.TotalFiles),
			{ TotalBytes: long totalBytes } => _localizationService.FormatString(
				"DownloadProgressBytes",
				"{0} of {1}",
				FormatBytes(Item.BytesDownloaded),
				FormatBytes(totalBytes)),
			_ => _localizationService.FormatString("DownloadProgressIndeterminate", "{0} downloaded", FormatBytes(Item.BytesDownloaded)),
		};

	private string GetTimeRemainingText()
	{
		var remaining = Item.GetTimeRemaining();
		return remaining is null
			? string.Empty
			: _localizationService.FormatString("DownloadTimeRemaining", "about {0} remaining", FormatDuration(remaining.Value));
	}

	private static string FormatBytes(double bytes)
	{
		if (double.IsNaN(bytes) || double.IsInfinity(bytes) || bytes < 0)
		{
			bytes = 0;
		}

		return FormatBytes((decimal)bytes);
	}

	private static string FormatBytes(long bytes) => FormatBytes((decimal)Math.Max(0, bytes));

	private static string FormatBytes(decimal bytes)
	{
		var index = 0;
		var number = Math.Max(0, bytes);

		while (number >= 1024 && index < ByteSuffixes.Length - 1)
		{
			number /= 1024;
			index++;
		}

		return string.Format(CultureInfo.CurrentCulture, "{0:n1} {1}", number, ByteSuffixes[index]);
	}

	private static string FormatDuration(TimeSpan duration)
	{
		var totalSeconds = Math.Max(0, (int)Math.Ceiling(duration.TotalSeconds));

		if (totalSeconds < 60)
		{
			return string.Format(CultureInfo.CurrentCulture, "{0}s", totalSeconds);
		}

		var totalMinutes = totalSeconds / 60;
		var seconds = totalSeconds % 60;

		if (totalMinutes < 60)
		{
			return seconds == 0
				? string.Format(CultureInfo.CurrentCulture, "{0}m", totalMinutes)
				: string.Format(CultureInfo.CurrentCulture, "{0}m {1}s", totalMinutes, seconds);
		}

		var hours = totalMinutes / 60;
		var minutes = totalMinutes % 60;
		return minutes == 0
			? string.Format(CultureInfo.CurrentCulture, "{0}h", hours)
			: string.Format(CultureInfo.CurrentCulture, "{0}h {1}m", hours, minutes);
	}
}
