using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
	private readonly ISettingsService _settingsService;
	private readonly IStatusMessageService _statusMessageService;

	public SettingsViewModel(ISettingsService settingsService, IStatusMessageService statusMessageService)
	{
		_settingsService = settingsService;
		_statusMessageService = statusMessageService;

		var settings = _settingsService.Settings;

		DownloadFolder = settings.DownloadFolder;
		AlwaysPromptDownloadLocation = settings.AlwaysPromptDownloadLocation;
		StatusOverlayDurationSeconds = settings.StatusOverlayDurationSeconds;
		DefaultRegion = settings.DefaultRegion;
		UseCredentialChainByDefault = settings.UseCredentialChainByDefault;
		LastProfileName = settings.LastProfileName ?? string.Empty;
	}

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial string DownloadFolder { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool AlwaysPromptDownloadLocation { get; set; }

	[ObservableProperty]
	public partial int StatusOverlayDurationSeconds { get; set; }

	[ObservableProperty]
	public partial string DefaultRegion { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool UseCredentialChainByDefault { get; set; }

	[ObservableProperty]
	public partial string LastProfileName { get; set; } = string.Empty;

	public IReadOnlyList<string> AvailableRegions { get; } = AwsRegions.All;

	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveAsync()
	{
		IsBusy = true;

		try
		{
			var settings = _settingsService.Settings;
			settings.DownloadFolder = string.IsNullOrWhiteSpace(DownloadFolder) ? settings.DownloadFolder : DownloadFolder.Trim();
			settings.AlwaysPromptDownloadLocation = AlwaysPromptDownloadLocation;
			settings.StatusOverlayDurationSeconds = Math.Max(1, StatusOverlayDurationSeconds);
			settings.DefaultRegion = string.IsNullOrWhiteSpace(DefaultRegion) ? settings.DefaultRegion : DefaultRegion;
			settings.UseCredentialChainByDefault = UseCredentialChainByDefault;
			settings.LastProfileName = string.IsNullOrWhiteSpace(LastProfileName) ? null : LastProfileName.Trim();

			DownloadFolder = settings.DownloadFolder;
			StatusOverlayDurationSeconds = settings.StatusOverlayDurationSeconds;
			DefaultRegion = settings.DefaultRegion;
			LastProfileName = settings.LastProfileName ?? string.Empty;

			await _settingsService.SaveAsync();
			_statusMessageService.ShowInfo("Settings saved.");
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError($"Failed to save settings: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSave() => IsBusy is false;
}
