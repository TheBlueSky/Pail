using System.Collections.Concurrent;
using System.Diagnostics;
using Pail.Models;

namespace Pail.Services;

public sealed class DownloadManager : IDownloadManager, IDisposable
{
	private static readonly TimeSpan SpeedSampleWindow = TimeSpan.FromSeconds(5);

	private readonly IS3Service _s3Service;
	private readonly ISettingsService _settingsService;
	private readonly IDispatcherService _dispatcherService;
	private readonly ILocalizationService _localizationService;
	private readonly SemaphoreSlim _semaphore;
	private readonly ConcurrentDictionary<Guid, DownloadRegistration> _registrations = new();

	public DownloadManager(IS3Service s3Service, ISettingsService settingsService, IDispatcherService dispatcherService, ILocalizationService localizationService)
	{
		ArgumentNullException.ThrowIfNull(s3Service);
		ArgumentNullException.ThrowIfNull(settingsService);
		ArgumentNullException.ThrowIfNull(dispatcherService);
		ArgumentNullException.ThrowIfNull(localizationService);

		_s3Service = s3Service;
		_settingsService = settingsService;
		_dispatcherService = dispatcherService;
		_localizationService = localizationService;

		// SemaphoreSlim cannot be resized; the parallel limit is snapshotted at construction.
		// Changes to MaxParallelDownloads take effect on next app start.
		var maxParallel = Math.Max(1, settingsService.MaxParallelDownloads);
		_semaphore = new SemaphoreSlim(maxParallel, maxParallel);
	}

	public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

	public event EventHandler<DownloadItemRemovedEventArgs>? DownloadRemoved;

	public IReadOnlyCollection<DownloadItem> GetActiveDownloads() =>
		[.. _registrations.Values.Select(r => r.Item)];

	public Task EnqueueAsync(DownloadItem item, CancellationToken cancellationToken = default)
	{
		var registration = RegisterDownload(item, cancellationToken);
		_ = RunDownloadAsync(registration);

		return Task.CompletedTask;
	}

	public Task EnqueueBatchAsync(IEnumerable<DownloadItem> items, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(items);

		foreach (var item in items)
		{
			var registration = RegisterDownload(item, cancellationToken);
			_ = RunDownloadAsync(registration);
		}

		return Task.CompletedTask;
	}

	public Task CancelAsync(Guid downloadId)
	{
		if (_registrations.TryGetValue(downloadId, out var registration))
		{
			TryCancel(registration);
		}

		return Task.CompletedTask;
	}

	public Task CancelAllAsync()
	{
		foreach (var registration in _registrations.Values)
		{
			TryCancel(registration);
		}

		return Task.CompletedTask;
	}

	public Task RetryAsync(Guid downloadId, CancellationToken cancellationToken = default)
	{
		if (!_registrations.TryGetValue(downloadId, out var currentRegistration))
		{
			return Task.CompletedTask;
		}

		if (currentRegistration.Item.Status is not DownloadStatus.Failed)
		{
			return Task.CompletedTask;
		}

		var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var retryRegistration = new DownloadRegistration(currentRegistration.Item, linkedCancellationTokenSource);

		if (!_registrations.TryUpdate(downloadId, retryRegistration, currentRegistration))
		{
			linkedCancellationTokenSource.Dispose();
			return Task.CompletedTask;
		}

		currentRegistration.CancellationTokenSource.Dispose();
		_dispatcherService.Run(() => retryRegistration.Item.TransitionTo(DownloadStatus.Queued));
		RaiseProgressChanged(retryRegistration.Item);

		_ = RunDownloadAsync(retryRegistration);

		return Task.CompletedTask;
	}

	public void ClearCompleted()
	{
		foreach (var entry in _registrations)
		{
			var status = entry.Value.Item.Status;

			if (status is DownloadStatus.Completed or DownloadStatus.Cancelled or DownloadStatus.Failed)
			{
				if (_registrations.TryRemove(entry.Key, out var removed))
				{
					removed.CancellationTokenSource.Dispose();
					RaiseDownloadRemoved(entry.Key, removed.Item);
				}
			}
		}
	}

	public void Dispose()
	{
		foreach (var registration in _registrations.Values)
		{
			TryCancel(registration);
			registration.CancellationTokenSource.Dispose();
		}

		_registrations.Clear();
		_semaphore.Dispose();
	}

	private DownloadRegistration RegisterDownload(DownloadItem item, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(item);

		var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		var registration = new DownloadRegistration(item, linkedCancellationTokenSource);

		if (!_registrations.TryAdd(item.Id, registration))
		{
			linkedCancellationTokenSource.Dispose();
			throw new InvalidOperationException($"A download with id {item.Id} is already registered.");
		}

		RaiseProgressChanged(item);
		return registration;
	}

	private async Task RunDownloadAsync(DownloadRegistration registration)
	{
		var item = registration.Item;
		var token = registration.CancellationTokenSource.Token;

		try
		{
			await _semaphore.WaitAsync(token);
		}
		catch (OperationCanceledException ex) when (IsDownloadCancellation(ex, token))
		{
			_dispatcherService.Run(() => item.TransitionTo(DownloadStatus.Cancelled));
			RaiseProgressChanged(item);

			return;
		}

		try
		{
			_dispatcherService.Run(() => item.TransitionTo(DownloadStatus.Downloading));
			RaiseProgressChanged(item);

			var progress = new SyncProgress<DownloadProgress>(p => OnProgressReported(registration, p));

			if (item.IsFolder)
			{
				await _s3Service.DownloadFolderAsync(item.BucketName, item.Key, item.DestinationPath, progress, token);
			}
			else
			{
				await _s3Service.DownloadObjectAsync(item.BucketName, item.Key, item.DestinationPath, progress, token);
			}

			_dispatcherService.Run(() => item.TransitionTo(DownloadStatus.Completed));
			RaiseProgressChanged(item);
		}
		catch (OperationCanceledException)
		{
			_dispatcherService.Run(() => item.TransitionTo(DownloadStatus.Cancelled));
			RaiseProgressChanged(item);
		}
		catch (Exception ex)
		{
			var failureMessage = DownloadErrorMessageFormatter.Format(ex, _localizationService);
			_dispatcherService.Run(() => item.TransitionTo(DownloadStatus.Failed, failureMessage.Summary, failureMessage.Details));
			RaiseProgressChanged(item);
		}
		finally
		{
			_semaphore.Release();
			ScheduleAutoClearIfApplicable(registration);
		}

		static bool IsDownloadCancellation(OperationCanceledException exception, CancellationToken cancellationToken)
		{
			return cancellationToken.IsCancellationRequested &&
				exception.CancellationToken.CanBeCanceled &&
				exception.CancellationToken.Equals(cancellationToken);
		}
	}

	private void ScheduleAutoClearIfApplicable(DownloadRegistration registration)
	{
		if (!_settingsService.AutoClearCompletedDownloads)
		{
			return;
		}

		if (registration.Item.Status is not DownloadStatus.Completed and not DownloadStatus.Cancelled)
		{
			return;
		}

		var delaySeconds = Math.Max(0, _settingsService.AutoClearCompletedDownloadsDelaySeconds);

		_ = Task
			.Delay(TimeSpan.FromSeconds(delaySeconds))
			.ContinueWith(
				_ =>
				{
					if (_registrations.TryRemove(registration.Item.Id, out var removed))
					{
						removed.CancellationTokenSource.Dispose();
						RaiseDownloadRemoved(registration.Item.Id, removed.Item);
					}
				},
				TaskScheduler.Default);
	}

	private void OnProgressReported(DownloadRegistration registration, DownloadProgress progress)
	{
		var rollingSpeed = registration.RecordSampleAndComputeRollingSpeed(progress.BytesDownloaded, SpeedSampleWindow);

		_dispatcherService.Run(
			() =>
			{
				var item = registration.Item;
				item.BytesDownloaded = progress.BytesDownloaded;
				item.TotalBytes = progress.TotalBytes ?? item.TotalBytes;
				item.FilesCompleted = progress.FilesCompleted;

				if (progress.TotalFiles > 0)
				{
					item.TotalFiles = progress.TotalFiles;
				}

				item.Speed = rollingSpeed;
			});

		RaiseProgressChanged(registration.Item);
	}

	private void RaiseProgressChanged(DownloadItem item) =>
		ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(item));

	private void RaiseDownloadRemoved(Guid id, DownloadItem item) =>
		DownloadRemoved?.Invoke(this, new DownloadItemRemovedEventArgs(id, item));

	private static void TryCancel(DownloadRegistration registration)
	{
		try
		{
			registration.CancellationTokenSource.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// Already disposed during teardown; nothing to cancel.
		}
	}

	private sealed class DownloadRegistration
	{
		private readonly List<(TimeSpan Elapsed, long Bytes)> _samples = [];
		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

		public DownloadRegistration(DownloadItem item, CancellationTokenSource cancellationTokenSource)
		{
			Item = item;
			CancellationTokenSource = cancellationTokenSource;
		}

		public DownloadItem Item { get; }

		public CancellationTokenSource CancellationTokenSource { get; }

		public double RecordSampleAndComputeRollingSpeed(long bytesDownloaded, TimeSpan window)
		{
			lock (_samples)
			{
				var elapsed = _stopwatch.Elapsed;
				_samples.Add((elapsed, bytesDownloaded));

				var threshold = elapsed - window;

				while (_samples.Count > 1 && _samples[0].Elapsed < threshold)
				{
					_samples.RemoveAt(0);
				}

				if (_samples.Count < 2)
				{
					return elapsed.TotalSeconds > 0 ? bytesDownloaded / elapsed.TotalSeconds : 0;
				}

				var (Elapsed, Bytes) = _samples[0];
				var latest = _samples[^1];
				var seconds = (latest.Elapsed - Elapsed).TotalSeconds;
				return seconds > 0 ? (latest.Bytes - Bytes) / seconds : 0;
			}
		}
	}
}
