using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Pail.Models;

namespace Pail.Services;

public sealed class SettingsService : ISettingsService
{
	public const string DefaultFileName = "appsettings.user.json";

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true,
	};

	private readonly IConfigurationRoot? _configurationRoot;
	private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
	private readonly string _settingsFilePath;

	public SettingsService(IConfiguration configuration, IOptionsMonitor<AppSettings> optionsMonitor, string? settingsFilePath = null)
	{
		_configurationRoot = configuration as IConfigurationRoot;
		_optionsMonitor = optionsMonitor;
		_settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
			? Path.Combine(AppContext.BaseDirectory, DefaultFileName)
			: settingsFilePath;
	}

	public string DownloadFolder => _optionsMonitor.CurrentValue.DownloadFolder;

	public bool AlwaysPromptDownloadLocation => _optionsMonitor.CurrentValue.AlwaysPromptDownloadLocation;

	public int StatusOverlayDurationSeconds => _optionsMonitor.CurrentValue.StatusOverlayDurationSeconds;

	public string DefaultRegion => _optionsMonitor.CurrentValue.DefaultRegion;

	public bool UseCredentialChainByDefault => _optionsMonitor.CurrentValue.UseCredentialChainByDefault;

	public string? LastProfileName => _optionsMonitor.CurrentValue.LastProfileName;

	public async Task UpdateAsync(Action<AppSettings> applyChanges, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(applyChanges);

		var updatedSettings = CloneSettings(_optionsMonitor.CurrentValue);
		applyChanges(updatedSettings);

		await WriteSettingsAsync(updatedSettings, cancellationToken);
		ReloadTrackedConfiguration();
	}

	private async Task WriteSettingsAsync(AppSettings settings, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath) ?? AppContext.BaseDirectory);

		await using var stream = File.Create(_settingsFilePath);
		await JsonSerializer.SerializeAsync(
			stream,
			settings,
			SerializerOptions,
			cancellationToken);
	}

	private void ReloadTrackedConfiguration() => _configurationRoot?.Reload();

	private static AppSettings CloneSettings(AppSettings source) => new()
	{
		DownloadFolder = source.DownloadFolder,
		AlwaysPromptDownloadLocation = source.AlwaysPromptDownloadLocation,
		StatusOverlayDurationSeconds = source.StatusOverlayDurationSeconds,
		DefaultRegion = source.DefaultRegion,
		UseCredentialChainByDefault = source.UseCredentialChainByDefault,
		LastProfileName = source.LastProfileName,
	};
}
