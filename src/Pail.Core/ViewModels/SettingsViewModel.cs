using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
	private readonly IAppThemeService _appThemeService;
	private readonly ISettingsService _settingsService;
	private readonly IAwsSessionInfoService _awsSessionInfoService;
	private readonly IFolderPickerService _folderPickerService;
	private readonly IStatusMessageService _statusMessageService;
	private readonly INavigationService _navigationService;
	private readonly ILocalizationService _localizationService;

	public SettingsViewModel(
		IAppThemeService appThemeService,
		ISettingsService settingsService,
		IAwsSessionInfoService awsSessionInfoService,
		IFolderPickerService folderPickerService,
		IStatusMessageService statusMessageService,
		INavigationService navigationService,
		ILocalizationService localizationService)
	{
		_appThemeService = appThemeService;
		_settingsService = settingsService;
		_awsSessionInfoService = awsSessionInfoService;
		_folderPickerService = folderPickerService;
		_statusMessageService = statusMessageService;
		_navigationService = navigationService;
		_localizationService = localizationService;

		AppTheme = _settingsService.AppTheme;
		DownloadFolder = _settingsService.DownloadFolder;
		AlwaysPromptDownloadLocation = _settingsService.AlwaysPromptDownloadLocation;
		InitialObjectLoadCount = _settingsService.InitialObjectLoadCount;
		LoadMoreObjectCount = _settingsService.LoadMoreObjectCount;
		StatusOverlayDurationSeconds = _settingsService.StatusOverlayDurationSeconds;
		DefaultRegion = _settingsService.DefaultRegion;
		UseCredentialChainByDefault = _settingsService.UseCredentialChainByDefault;
		LastProfileName = _settingsService.LastProfileName ?? string.Empty;
		MaxParallelDownloads = _settingsService.MaxParallelDownloads;
		AutoClearCompletedDownloads = _settingsService.AutoClearCompletedDownloads;
		AutoClearCompletedDownloadsDelaySeconds = _settingsService.AutoClearCompletedDownloadsDelaySeconds;
	}

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SaveCommand))]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	public partial AppThemeMode AppTheme { get; set; }

	[ObservableProperty]
	public partial string DownloadFolder { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool AlwaysPromptDownloadLocation { get; set; }

	[ObservableProperty]
	public partial int InitialObjectLoadCount { get; set; }

	[ObservableProperty]
	public partial int LoadMoreObjectCount { get; set; }

	[ObservableProperty]
	public partial int StatusOverlayDurationSeconds { get; set; }

	[ObservableProperty]
	public partial string DefaultRegion { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool UseCredentialChainByDefault { get; set; }

	[ObservableProperty]
	public partial string LastProfileName { get; set; } = string.Empty;

	[ObservableProperty]
	public partial int MaxParallelDownloads { get; set; }

	[ObservableProperty]
	public partial bool AutoClearCompletedDownloads { get; set; }

	[ObservableProperty]
	public partial int AutoClearCompletedDownloadsDelaySeconds { get; set; }

	public IReadOnlyList<AppThemeMode> AvailableThemes { get; } =
	[
		AppThemeMode.Light,
		AppThemeMode.Dark,
		AppThemeMode.System,
	];

	public IReadOnlyList<string> AvailableRegions { get; } = AwsRegions.All;

	[RelayCommand]
	private async Task BrowseDownloadFolderAsync()
	{
		try
		{
			var selectedPath = await _folderPickerService.PickFolderAsync();

			if (string.IsNullOrWhiteSpace(selectedPath) is false)
			{
				DownloadFolder = selectedPath;
			}
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError(_localizationService.FormatString("FolderSelectFailed", "Failed to select folder: {0}", ex.Message));
		}
	}

	[RelayCommand]
	private void Logout()
	{
		_awsSessionInfoService.Clear();
		_navigationService.NavigateTo("LoginPage", clearBackStack: true);
	}

	[RelayCommand(CanExecute = nameof(CanSave))]
	private async Task SaveAsync()
	{
		IsBusy = true;

		try
		{
			await _settingsService.UpdateAsync(
				settings =>
				{
					settings.AppTheme = AppTheme;
					settings.DownloadFolder = string.IsNullOrWhiteSpace(DownloadFolder) ? settings.DownloadFolder : DownloadFolder.Trim();
					settings.AlwaysPromptDownloadLocation = AlwaysPromptDownloadLocation;
					settings.InitialObjectLoadCount = Math.Max(1, InitialObjectLoadCount);
					settings.LoadMoreObjectCount = Math.Max(0, LoadMoreObjectCount);
					settings.StatusOverlayDurationSeconds = Math.Max(1, StatusOverlayDurationSeconds);
					settings.DefaultRegion = string.IsNullOrWhiteSpace(DefaultRegion) ? settings.DefaultRegion : DefaultRegion;
					settings.UseCredentialChainByDefault = UseCredentialChainByDefault;
					settings.LastProfileName = string.IsNullOrWhiteSpace(LastProfileName) ? null : LastProfileName.Trim();
					settings.MaxParallelDownloads = Math.Clamp(MaxParallelDownloads, 1, 10);
					settings.AutoClearCompletedDownloads = AutoClearCompletedDownloads;
					settings.AutoClearCompletedDownloadsDelaySeconds = Math.Clamp(AutoClearCompletedDownloadsDelaySeconds, 0, 60);
				});

			ApplySettingsSnapshot();

			try
			{
				_appThemeService.ApplyTheme(AppTheme);
			}
			catch (Exception ex)
			{
				_statusMessageService.ShowError(_localizationService.FormatString("SettingsSavedThemeApplyFailed", "Settings saved, but failed to apply theme: {0}", ex.Message));
				return;
			}

			_statusMessageService.ShowInfo(_localizationService.GetString("SettingsSaved", "Settings saved."));
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError(_localizationService.FormatString("SettingsSaveFailed", "Failed to save settings: {0}", ex.Message));
		}
		finally
		{
			IsBusy = false;
		}
	}

	private bool CanSave() => IsBusy is false;

	private void ApplySettingsSnapshot()
	{
		AppTheme = _settingsService.AppTheme;
		DownloadFolder = _settingsService.DownloadFolder;
		AlwaysPromptDownloadLocation = _settingsService.AlwaysPromptDownloadLocation;
		InitialObjectLoadCount = _settingsService.InitialObjectLoadCount;
		LoadMoreObjectCount = _settingsService.LoadMoreObjectCount;
		StatusOverlayDurationSeconds = _settingsService.StatusOverlayDurationSeconds;
		DefaultRegion = _settingsService.DefaultRegion;
		UseCredentialChainByDefault = _settingsService.UseCredentialChainByDefault;
		LastProfileName = _settingsService.LastProfileName ?? string.Empty;
		MaxParallelDownloads = _settingsService.MaxParallelDownloads;
		AutoClearCompletedDownloads = _settingsService.AutoClearCompletedDownloads;
		AutoClearCompletedDownloadsDelaySeconds = _settingsService.AutoClearCompletedDownloadsDelaySeconds;
	}
}
