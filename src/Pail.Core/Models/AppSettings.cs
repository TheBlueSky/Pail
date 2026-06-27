namespace Pail.Models;

public sealed class AppSettings
{
	public AppThemeMode AppTheme { get; set; } = AppThemeMode.System;

	public string DownloadFolder { get; set; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
		"Downloads",
		"Pail");

	public bool AlwaysPromptDownloadLocation { get; set; }

	public int InitialObjectLoadCount { get; set; } = 1000;

	public int LoadMoreObjectCount { get; set; }

	public DateTimeDisplayMode ObjectTimestampDisplayMode { get; set; } = DateTimeDisplayMode.Utc;

	public int StatusOverlayDurationSeconds { get; set; } = 3;

	public string DefaultRegion { get; set; } = "eu-west-1";

	public bool UseCredentialChainByDefault { get; set; } = true;

	public string? LastProfileName { get; set; }

	public int MaxParallelDownloads { get; set; } = 3;

	public bool AutoClearCompletedDownloads { get; set; } = true;

	public int AutoClearCompletedDownloadsDelaySeconds { get; set; } = 5;
}
