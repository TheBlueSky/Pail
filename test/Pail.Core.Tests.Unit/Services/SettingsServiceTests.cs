using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class SettingsServiceTests : IDisposable
{
	private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"Pail.Tests.{Guid.NewGuid():N}");
	private readonly List<IDisposable> _disposables = [];

	[Fact]
	internal void Constructor_MissingFile_UsesDefaults()
	{
		// Arrange
		var service = CreateService();

		// Assert
		Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Pail"), service.DownloadFolder);
		Assert.False(service.AlwaysPromptDownloadLocation);
		Assert.Equal(3, service.StatusOverlayDurationSeconds);
		Assert.Equal("eu-west-1", service.DefaultRegion);
		Assert.True(service.UseCredentialChainByDefault);
		Assert.Null(service.LastProfileName);
	}

	[Fact]
	internal async Task Constructor_ExistingFile_BindsConfiguredValues()
	{
		// Arrange
		Directory.CreateDirectory(_tempDirectory);
		await File.WriteAllTextAsync(
			GetSettingsFilePath(),
			"""
			{
				"DownloadFolder": "D:\\S3Downloads",
				"AlwaysPromptDownloadLocation": true,
				"StatusOverlayDurationSeconds": 7,
				"DefaultRegion": "us-east-1",
				"UseCredentialChainByDefault": false,
				"LastProfileName": "prod"
			}
			""");

		var service = CreateService();

		// Assert
		Assert.Equal("D:\\S3Downloads", service.DownloadFolder);
		Assert.True(service.AlwaysPromptDownloadLocation);
		Assert.Equal(7, service.StatusOverlayDurationSeconds);
		Assert.Equal("us-east-1", service.DefaultRegion);
		Assert.False(service.UseCredentialChainByDefault);
		Assert.Equal("prod", service.LastProfileName);
	}

	[Fact]
	internal async Task UpdateAsync_PersistsUpdatedSettings_AndCanBeLoadedAgain()
	{
		// Arrange
		var service = CreateService();

		// Act
		await service.UpdateAsync(settings =>
		{
			settings.DownloadFolder = "E:\\Exports";
			settings.AlwaysPromptDownloadLocation = true;
			settings.StatusOverlayDurationSeconds = 9;
			settings.DefaultRegion = "ap-southeast-2";
			settings.UseCredentialChainByDefault = false;
			settings.LastProfileName = "dev";
		});

		var reloadedService = CreateService();

		// Assert
		Assert.Equal("E:\\Exports", reloadedService.DownloadFolder);
		Assert.True(reloadedService.AlwaysPromptDownloadLocation);
		Assert.Equal(9, reloadedService.StatusOverlayDurationSeconds);
		Assert.Equal("ap-southeast-2", reloadedService.DefaultRegion);
		Assert.False(reloadedService.UseCredentialChainByDefault);
		Assert.Equal("dev", reloadedService.LastProfileName);

		using var document = JsonDocument.Parse(await File.ReadAllTextAsync(GetSettingsFilePath()));
		Assert.Equal("E:\\Exports", document.RootElement.GetProperty(nameof(AppSettings.DownloadFolder)).GetString());
		Assert.Equal("dev", document.RootElement.GetProperty(nameof(AppSettings.LastProfileName)).GetString());
	}

	[Fact]
	internal async Task UpdateAsync_PersistsUpdatedSettings_AndRefreshesFacadeValues()
	{
		// Arrange
		var service = CreateService();

		// Act
		await service.UpdateAsync(settings =>
		{
			settings.DownloadFolder = "F:\\Exports";
			settings.AlwaysPromptDownloadLocation = true;
			settings.StatusOverlayDurationSeconds = 6;
			settings.DefaultRegion = "us-west-1";
			settings.UseCredentialChainByDefault = false;
			settings.LastProfileName = "ops";
		});

		var reloadedService = CreateService();

		// Assert
		Assert.Equal("F:\\Exports", service.DownloadFolder);
		Assert.True(service.AlwaysPromptDownloadLocation);
		Assert.Equal(6, service.StatusOverlayDurationSeconds);
		Assert.Equal("us-west-1", service.DefaultRegion);
		Assert.False(service.UseCredentialChainByDefault);
		Assert.Equal("ops", service.LastProfileName);

		Assert.Equal("F:\\Exports", reloadedService.DownloadFolder);
		Assert.True(reloadedService.AlwaysPromptDownloadLocation);
		Assert.Equal(6, reloadedService.StatusOverlayDurationSeconds);
		Assert.Equal("us-west-1", reloadedService.DefaultRegion);
		Assert.False(reloadedService.UseCredentialChainByDefault);
		Assert.Equal("ops", reloadedService.LastProfileName);
	}

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable.Dispose();
		}

		if (Directory.Exists(_tempDirectory))
		{
			Directory.Delete(_tempDirectory, recursive: true);
		}
	}

	private SettingsService CreateService()
	{
		Directory.CreateDirectory(_tempDirectory);

		var configuration = new ConfigurationBuilder()
			.SetBasePath(_tempDirectory)
			.AddJsonFile(SettingsService.DefaultFileName, optional: true, reloadOnChange: false)
			.Build();

		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(configuration);
		services.AddOptions<AppSettings>().Bind(configuration);

		var serviceProvider = services.BuildServiceProvider();
		var optionsMonitor = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<AppSettings>>();

		_disposables.Add(serviceProvider);

		return new SettingsService(configuration, optionsMonitor, GetSettingsFilePath());
	}

	private string GetSettingsFilePath() => Path.Combine(_tempDirectory, SettingsService.DefaultFileName);
}
