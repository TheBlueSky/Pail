using System.Text.Json;
using Pail.Models;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class SettingsServiceTests : IDisposable
{
	private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"Pail.Tests.{Guid.NewGuid():N}");

	[Fact]
	internal async Task LoadAsync_MissingFile_UsesDefaults()
	{
		// Arrange
		var service = CreateService();

		// Act
		await service.LoadAsync();

		// Assert
		Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Pail"), service.Settings.DownloadFolder);
		Assert.False(service.Settings.AlwaysPromptDownloadLocation);
		Assert.Equal(3, service.Settings.StatusOverlayDurationSeconds);
		Assert.Equal("eu-west-1", service.Settings.DefaultRegion);
		Assert.True(service.Settings.UseCredentialChainByDefault);
		Assert.Null(service.Settings.LastProfileName);
	}

	[Fact]
	internal async Task LoadAsync_ExistingFile_BindsConfiguredValues()
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

		// Act
		await service.LoadAsync();

		// Assert
		Assert.Equal("D:\\S3Downloads", service.Settings.DownloadFolder);
		Assert.True(service.Settings.AlwaysPromptDownloadLocation);
		Assert.Equal(7, service.Settings.StatusOverlayDurationSeconds);
		Assert.Equal("us-east-1", service.Settings.DefaultRegion);
		Assert.False(service.Settings.UseCredentialChainByDefault);
		Assert.Equal("prod", service.Settings.LastProfileName);
	}

	[Fact]
	internal async Task SaveAsync_PersistsCurrentSettings_AndCanBeLoadedAgain()
	{
		// Arrange
		var service = CreateService();
		service.Settings.DownloadFolder = "E:\\Exports";
		service.Settings.AlwaysPromptDownloadLocation = true;
		service.Settings.StatusOverlayDurationSeconds = 9;
		service.Settings.DefaultRegion = "ap-southeast-2";
		service.Settings.UseCredentialChainByDefault = false;
		service.Settings.LastProfileName = "dev";

		// Act
		await service.SaveAsync();

		var reloadedService = CreateService();
		await reloadedService.LoadAsync();

		// Assert
		Assert.Equal("E:\\Exports", reloadedService.Settings.DownloadFolder);
		Assert.True(reloadedService.Settings.AlwaysPromptDownloadLocation);
		Assert.Equal(9, reloadedService.Settings.StatusOverlayDurationSeconds);
		Assert.Equal("ap-southeast-2", reloadedService.Settings.DefaultRegion);
		Assert.False(reloadedService.Settings.UseCredentialChainByDefault);
		Assert.Equal("dev", reloadedService.Settings.LastProfileName);

		using var document = JsonDocument.Parse(await File.ReadAllTextAsync(GetSettingsFilePath()));
		Assert.Equal("E:\\Exports", document.RootElement.GetProperty(nameof(AppSettings.DownloadFolder)).GetString());
		Assert.Equal("dev", document.RootElement.GetProperty(nameof(AppSettings.LastProfileName)).GetString());
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDirectory))
		{
			Directory.Delete(_tempDirectory, recursive: true);
		}
	}

	private SettingsService CreateService() => new(GetSettingsFilePath());

	private string GetSettingsFilePath() => Path.Combine(_tempDirectory, SettingsService.DefaultFileName);
}
