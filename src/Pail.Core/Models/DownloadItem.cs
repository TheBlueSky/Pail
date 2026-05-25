namespace Pail.Models;

public sealed class DownloadItem
{
	public Guid Id { get; init; } = Guid.NewGuid();

	public required string BucketName { get; init; }

	public required string Key { get; init; }

	public required string DestinationPath { get; init; }

	public required string FileName { get; init; }

	public long? TotalBytes { get; set; }

	public long BytesDownloaded { get; set; }

	public double Speed { get; set; }

	public DownloadStatus Status { get; private set; } = DownloadStatus.Queued;

	public DateTimeOffset? StartTime { get; private set; }

	public DateTimeOffset? EndTime { get; private set; }

	public string? ErrorMessage { get; private set; }

	public string? ErrorDetails { get; private set; }

	public bool IsFolder { get; init; }

	public int FilesCompleted { get; set; }

	public int TotalFiles { get; set; }

	public void TransitionTo(DownloadStatus newStatus, string? errorMessage = null, string? errorDetails = null)
	{
		if (newStatus is DownloadStatus.Queued && (errorMessage is not null || errorDetails is not null))
		{
			throw new ArgumentException("Error information cannot be supplied when transitioning to Queued; the retry resets attempt state including ErrorMessage and ErrorDetails.", nameof(errorMessage));
		}

		if (Status is DownloadStatus.Completed or DownloadStatus.Cancelled or DownloadStatus.Failed)
		{
			if (newStatus is DownloadStatus.Queued)
			{
				ResetAttemptState();
				Status = DownloadStatus.Queued;
				return;
			}
			else
			{
				throw new InvalidOperationException($"Cannot transition from terminal state {Status} to {newStatus}.");
			}
		}

		// Queued must transition to Downloading, Cancelled, or Failed
		if (Status is DownloadStatus.Queued && newStatus is DownloadStatus.Completed)
		{
			throw new InvalidOperationException($"Cannot transition directly from {Status} to {newStatus}.");
		}

		Status = newStatus;

		if (newStatus is DownloadStatus.Downloading && StartTime is null)
		{
			StartTime = DateTimeOffset.UtcNow;
		}
		else if (newStatus is DownloadStatus.Completed or DownloadStatus.Failed or DownloadStatus.Cancelled)
		{
			EndTime ??= DateTimeOffset.UtcNow;
		}

		if (errorMessage is not null)
		{
			ErrorMessage = errorMessage;
		}

		if (errorDetails is not null)
		{
			ErrorDetails = errorDetails;
		}
	}

	private void ResetAttemptState()
	{
		BytesDownloaded = 0;
		Speed = 0;
		StartTime = null;
		EndTime = null;
		ErrorMessage = null;
		ErrorDetails = null;
		FilesCompleted = 0;
	}
}
