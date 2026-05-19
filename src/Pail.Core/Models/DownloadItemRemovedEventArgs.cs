namespace Pail.Models;

public sealed class DownloadItemRemovedEventArgs : EventArgs
{
	public DownloadItemRemovedEventArgs(Guid id, DownloadItem item)
	{
		Id = id;
		Item = item;
	}

	public Guid Id { get; }

	public DownloadItem Item { get; }
}
