namespace Pail.Models;

public sealed record DownloadProgress(
	long BytesDownloaded,
	long? TotalBytes,
	string FileName,
	double Speed,
	TimeSpan ElapsedTime,
	TimeSpan? RemainingTime,
	int FilesCompleted,
	int TotalFiles);
