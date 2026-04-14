using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pail.Models;
using Pail.Services;

namespace Pail.ViewModels;

public partial class LoginViewModel : ObservableObject
{
	public const string AutomaticProfileOption = "Automatic (recommended)";

	private readonly IAwsProfileService _awsProfileService;
	private readonly IS3Service _s3Service;
	private readonly INavigationService _navigationService;
	private readonly ISettingsService _settingsService;
	private readonly IStatusMessageService _statusMessageService;

	public LoginViewModel(
		IAwsProfileService awsProfileService,
		IS3Service s3Service,
		INavigationService navigationService,
		ISettingsService settingsService,
		IStatusMessageService statusMessageService)
	{
		_awsProfileService = awsProfileService;
		_s3Service = s3Service;
		_navigationService = navigationService;
		_settingsService = settingsService;
		_statusMessageService = statusMessageService;

		Region = string.IsNullOrWhiteSpace(_settingsService.DefaultRegion) ? Region : _settingsService.DefaultRegion;
		UseDefaultChain = _settingsService.UseCredentialChainByDefault;
		SelectedProfileName = string.IsNullOrWhiteSpace(_settingsService.LastProfileName) ? AutomaticProfileOption : _settingsService.LastProfileName;
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
	public partial bool UseDefaultChain { get; set; }

	[ObservableProperty]
	public partial string SelectedProfileName { get; set; } = AutomaticProfileOption;

	[ObservableProperty]
	public partial bool IsBusy { get; set; }

	public ObservableCollection<string> AvailableProfiles { get; } = [AutomaticProfileOption];

	public IReadOnlyList<string> AvailableRegions { get; } = AwsRegions.All;

	public async Task LoadCredentialProfilesAsync()
	{
		try
		{
			var profileNames = await _awsProfileService.GetProfileNamesAsync();

			AvailableProfiles.Clear();
			AvailableProfiles.Add(AutomaticProfileOption);

			foreach (var profileName in profileNames)
			{
				AvailableProfiles.Add(profileName);
			}

			if (!AvailableProfiles.Any(profileName => IAwsProfileService.ProfileNameComparer.Equals(profileName, SelectedProfileName)))
			{
				SelectedProfileName = AutomaticProfileOption;
			}
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError($"Failed to load AWS profiles: {ex.Message}");
		}
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

			_navigationService.NavigateTo("MainPage");
		}
		catch (Exception ex)
		{
			_statusMessageService.ShowError($"Login failed: {ex.Message}");
		}
		finally
		{
			IsBusy = false;
		}
	}

	private string? GetSelectedProfileName() =>
		IAwsProfileService.ProfileNameComparer.Equals(SelectedProfileName, AutomaticProfileOption) ? null : SelectedProfileName;
}
