using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
	private readonly ISettingsService _settingsService;
	private readonly IFolderPickerService _folderPickerService;
	private readonly IStatusMessageService _statusMessageService;

	public SettingsViewModel(
		ISettingsService settingsService,
		IFolderPickerService folderPickerService,
		IStatusMessageService statusMessageService)
	{
		_settingsService = settingsService;
		_folderPickerService = folderPickerService;
		_statusMessageService = statusMessageService;

		DownloadFolder = _settingsService.DownloadFolder;
		AlwaysPromptDownloadLocation = _settingsService.AlwaysPromptDownloadLocation;
		StatusOverlayDurationSeconds = _settingsService.StatusOverlayDurationSeconds;
		DefaultRegion = _settingsService.DefaultRegion;
		UseCredentialChainByDefault = _settingsService.UseCredentialChainByDefault;
		LastProfileName = _settingsService.LastProfileName ?? string.Empty;
	}

	[RelayCommand]
	private async Task BrowseDownloadFolderAsync()
	{
		try
		{
			var selectedPath = await _folderPickerService.PickFolderAsync(DownloadFolder);

			if (string.IsNullOrWhiteSpace(selectedPath) is false)
			{
				DownloadFolder = selectedPath;
			}
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError($"Failed to select folder: {ex.Message}");
		}
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
			await _settingsService.UpdateAsync(
				settings =>
				{
					settings.DownloadFolder = string.IsNullOrWhiteSpace(DownloadFolder) ? settings.DownloadFolder : DownloadFolder.Trim();
					settings.AlwaysPromptDownloadLocation = AlwaysPromptDownloadLocation;
					settings.StatusOverlayDurationSeconds = Math.Max(1, StatusOverlayDurationSeconds);
					settings.DefaultRegion = string.IsNullOrWhiteSpace(DefaultRegion) ? settings.DefaultRegion : DefaultRegion;
					settings.UseCredentialChainByDefault = UseCredentialChainByDefault;
					settings.LastProfileName = string.IsNullOrWhiteSpace(LastProfileName) ? null : LastProfileName.Trim();
				});

			DownloadFolder = _settingsService.DownloadFolder;
			StatusOverlayDurationSeconds = _settingsService.StatusOverlayDurationSeconds;
			DefaultRegion = _settingsService.DefaultRegion;
			LastProfileName = _settingsService.LastProfileName ?? string.Empty;

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
