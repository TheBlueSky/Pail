using Pail.Models;

namespace Pail.Services;

public interface IDownloadManager
{
	public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

	public event EventHandler<DownloadItemRemovedEventArgs>? DownloadRemoved;

	public Task EnqueueAsync(DownloadItem item, CancellationToken cancellationToken = default);

	public Task EnqueueBatchAsync(IEnumerable<DownloadItem> items, CancellationToken cancellationToken = default);

	public Task CancelAsync(Guid downloadId);

	public Task CancelAllAsync();

	public Task RetryAsync(Guid downloadId, CancellationToken cancellationToken = default);

	public IReadOnlyCollection<DownloadItem> GetActiveDownloads();

	public void ClearCompleted();
}
