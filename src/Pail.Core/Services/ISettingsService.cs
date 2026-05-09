using Pail.Models;

namespace Pail.Services;

public interface ISettingsService
{
	public AppThemeMode AppTheme { get; }

	public string DownloadFolder { get; }

	public bool AlwaysPromptDownloadLocation { get; }

	public int InitialObjectLoadCount { get; }

	public int LoadMoreObjectCount { get; }

	public int StatusOverlayDurationSeconds { get; }

	public string DefaultRegion { get; }

	public bool UseCredentialChainByDefault { get; }

	public string? LastProfileName { get; }

	public Task UpdateAsync(Action<AppSettings> applyChanges, CancellationToken cancellationToken = default);
}
