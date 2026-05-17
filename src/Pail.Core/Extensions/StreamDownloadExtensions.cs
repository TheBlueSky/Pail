using System.Diagnostics;
using Pail.Models;

namespace Pail.Extensions;

internal static class StreamDownloadExtensions
{
	private const int DefaultBufferSize = 81_920; // 80 KB

	private static readonly TimeSpan ProgressThrottleInterval = TimeSpan.FromMilliseconds(100);

	extension(Stream source)
	{
		public async Task CopyToWithProgressAsync(
			Stream destination,
			string fileName,
			long? totalBytes,
			IProgress<DownloadProgress>? progress,
			CancellationToken cancellationToken)
		{
			var buffer = new byte[DefaultBufferSize];
			var bytesRead = 0L;
			var stopwatch = Stopwatch.StartNew();
			var lastReportTime = TimeSpan.Zero;
			int read;

			while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
			{
				await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
				bytesRead += read;

				if (progress is null)
				{
					continue;
				}

				var elapsed = stopwatch.Elapsed;
				var isFinalRead = totalBytes is not null && bytesRead == totalBytes.Value;

				if (elapsed - lastReportTime < ProgressThrottleInterval && !isFinalRead)
				{
					continue;
				}

				lastReportTime = elapsed;

				var speed = elapsed.TotalSeconds > 0 ? bytesRead / elapsed.TotalSeconds : 0;
				var remainingTime = speed > 0 && totalBytes > 0
					? TimeSpan.FromSeconds(Math.Max(0, totalBytes.Value - bytesRead) / speed)
					: null as TimeSpan?;

				progress.Report(
					new DownloadProgress(
						BytesDownloaded: bytesRead,
						TotalBytes: totalBytes,
						FileName: fileName,
						Speed: speed,
						ElapsedTime: elapsed,
						RemainingTime: remainingTime,
						FilesCompleted: 0,
						TotalFiles: 1));
			}
		}
	}
}
