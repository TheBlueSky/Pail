namespace Pail.Models;

public sealed class DownloadProgressEventArgs : EventArgs
{
	public DownloadProgressEventArgs(DownloadItem item)
	{
		Item = item;
	}

	public DownloadItem Item { get; }
}
