using Pail.Models;

namespace Pail.Extensions;

internal static class DownloadItemExtensions
{
	extension(DownloadItem item)
	{
		public double? GetByteProgressPercentage() =>
			item.TotalBytes is null ?
				null :
				item.TotalBytes.Value == 0 ?
					item.Status is DownloadStatus.Completed ? 100 : 0 :
					Math.Clamp((double)item.BytesDownloaded / item.TotalBytes.Value * 100, 0, 100);

		public double? GetFileProgressPercentage() =>
			item.TotalFiles <= 0 ? null : Math.Clamp((double)item.FilesCompleted / item.TotalFiles * 100, 0, 100);

		public TimeSpan? GetTimeRemaining()
		{
			if (item.Speed <= 0 || item.TotalBytes is null)
			{
				return null;
			}

			var remainingBytes = item.TotalBytes.Value - item.BytesDownloaded;

			if (remainingBytes <= 0)
			{
				return TimeSpan.Zero;
			}

			var seconds = remainingBytes / item.Speed;
			return double.IsNaN(seconds) || double.IsInfinity(seconds) ? null : TimeSpan.FromSeconds(seconds);
		}
	}
}
