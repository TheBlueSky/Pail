using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class LoginViewModel : ObservableObject
{
	private const string AutomaticProfileOption = "Automatic (recommended)";

	private readonly IAwsProfileService _awsProfileService;
	private readonly IS3Service _s3Service;
	private readonly IAwsIdentityService _awsIdentityService;
	private readonly IAwsSessionInfoService _awsSessionInfoService;
	private readonly INavigationService _navigationService;
	private readonly ISettingsService _settingsService;
	private readonly IClipboardService _clipboardService;
	private readonly IAwsConsoleCredentialsParser _awsConsoleCredentialsParser;
	private readonly IStatusMessageService _statusMessageService;
	private readonly ILocalizationService _localizationService;
	private readonly string _automaticProfileOption;

	public LoginViewModel(
		IAwsProfileService awsProfileService,
		IS3Service s3Service,
		IAwsIdentityService awsIdentityService,
		IAwsSessionInfoService awsSessionInfoService,
		INavigationService navigationService,
		ISettingsService settingsService,
		IClipboardService clipboardService,
		IAwsConsoleCredentialsParser awsConsoleCredentialsParser,
		IStatusMessageService statusMessageService,
		ILocalizationService localizationService)
	{
		_awsProfileService = awsProfileService;
		_s3Service = s3Service;
		_awsIdentityService = awsIdentityService;
		_awsSessionInfoService = awsSessionInfoService;
		_navigationService = navigationService;
		_settingsService = settingsService;
		_clipboardService = clipboardService;
		_awsConsoleCredentialsParser = awsConsoleCredentialsParser;
		_statusMessageService = statusMessageService;
		_localizationService = localizationService;

		_automaticProfileOption = GetAutomaticProfileOption();

		Region = string.IsNullOrWhiteSpace(_settingsService.DefaultRegion) ? Region : _settingsService.DefaultRegion;
		UseDefaultChain = _settingsService.UseCredentialChainByDefault;
		SelectedProfileName = string.IsNullOrWhiteSpace(_settingsService.LastProfileName) ? _automaticProfileOption : _settingsService.LastProfileName;
	}

	[ObservableProperty]
	public partial string AccessKey { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string SecretKey { get; set; } = string.Empty;

	[ObservableProperty]
	public partial string Region { get; set; } = "eu-west-1";

	[ObservableProperty]
	public partial string SessionToken { get; set; } = string.Empty;

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanRefreshProfiles))]
	public partial bool UseDefaultChain { get; set; }

	[ObservableProperty]
	public partial string SelectedProfileName { get; set; } = string.Empty;

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanRefreshProfiles))]
	public partial bool IsLoadingCredentialProfiles { get; set; }

	public ObservableCollection<string> AvailableProfiles { get; } = [];

	public IReadOnlyList<string> AvailableRegions { get; } = AwsRegions.All;

	public bool CanRefreshProfiles => UseDefaultChain && IsLoadingCredentialProfiles is false;

	[RelayCommand]
	public async Task LoadCredentialProfilesAsync()
	{
		if (IsLoadingCredentialProfiles)
		{
			return;
		}

		IsLoadingCredentialProfiles = true;

		try
		{
			AvailableProfiles.Clear();
			AvailableProfiles.Add(_automaticProfileOption);

			var profileNames = await _awsProfileService.GetProfileNamesAsync();

			foreach (var profileName in profileNames)
			{
				AvailableProfiles.Add(profileName);
			}

			if (!AvailableProfiles.Any(profileName => IAwsProfileService.ProfileNameComparer.Equals(profileName, SelectedProfileName)))
			{
				SelectedProfileName = _automaticProfileOption;
			}
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError(_localizationService.FormatString("AwsProfilesLoadFailed", "Failed to load AWS profiles: {0}", ex.Message));
		}
		finally
		{
			IsLoadingCredentialProfiles = false;
		}
	}

	[RelayCommand]
	private async Task PasteCredentialsAsync()
	{
		var clipboardText = await _clipboardService.ReadTextAsync();

		if (string.IsNullOrWhiteSpace(clipboardText))
		{
			_statusMessageService.ShowError(_localizationService.GetString("ClipboardMissingAwsCredentials", "Clipboard does not contain AWS Console credentials."));
			return;
		}

		var parsedCredentials = _awsConsoleCredentialsParser.Parse(clipboardText);

		if (parsedCredentials is null)
		{
			_statusMessageService.ShowError(_localizationService.GetString("ClipboardInvalidAwsCredentials", "Clipboard text is not in the expected AWS Console credential format."));
			return;
		}

		AccessKey = parsedCredentials.AccessKey;
		SecretKey = parsedCredentials.SecretKey;
		SessionToken = parsedCredentials.SessionToken;
		UseDefaultChain = false;
	}

	[RelayCommand]
	private async Task LoginAsync()
	{
		IsBusy = true;

		try
		{
			IAwsCredentials credentials = UseDefaultChain ?
				new AwsDefaultChainCredentials(GetSelectedProfileName(), Region) :
				new AwsSessionCredentials(AccessKey, SecretKey, SessionToken, Region);

			await _s3Service.InitializeAsync(credentials);

			// Attempt a simple call to verify credentials
			await _s3Service.GetBucketsAsync();

			var identityTask = _awsIdentityService.TryGetCallerIdentityAsync(credentials);
			var accountAliasTask = _awsIdentityService.TryGetAccountAliasAsync(credentials);

			await Task.WhenAll(identityTask, accountAliasTask);

			var identity = await identityTask;
			var accountAlias = await accountAliasTask;

			_awsSessionInfoService.SetCurrent(
				new AwsSessionInfo(
					credentials.Region,
					credentials is AwsDefaultChainCredentials defaultChainCredentials ? defaultChainCredentials.ProfileName : null,
					identity?.AccountId,
					identity?.CallerArn,
					accountAlias));

			_navigationService.NavigateTo("MainPage", clearBackStack: true);
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError(_localizationService.FormatString("LoginFailed", "Login failed: {0}", ex.Message));
		}
		finally
		{
			IsBusy = false;
		}

		string? GetSelectedProfileName()
		{
			return IAwsProfileService.ProfileNameComparer.Equals(SelectedProfileName, _automaticProfileOption) ? null : SelectedProfileName;
		}
	}

	private string GetAutomaticProfileOption() =>
		_localizationService.GetString("AutomaticProfileOption", AutomaticProfileOption);
}
