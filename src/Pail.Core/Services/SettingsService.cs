using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Pail.Models;

namespace Pail.Services;

public sealed class SettingsService : ISettingsService
{
	public const string DefaultFileName = "appsettings.user.json";

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true,
	};

	private readonly string _settingsFilePath;

	public SettingsService(string? settingsFilePath = null)
	{
		_settingsFilePath = string.IsNullOrWhiteSpace(settingsFilePath)
			? Path.Combine(AppContext.BaseDirectory, DefaultFileName)
			: settingsFilePath;
	}

	public AppSettings Settings { get; private set; } = new();

	public async Task LoadAsync(CancellationToken cancellationToken = default)
	{
		if (File.Exists(_settingsFilePath) is false)
		{
			Settings = new AppSettings();
			return;
		}

		var configuration = new ConfigurationBuilder()
			.SetBasePath(Path.GetDirectoryName(_settingsFilePath) ?? AppContext.BaseDirectory)
			.AddJsonFile(Path.GetFileName(_settingsFilePath), optional: true, reloadOnChange: false)
			.Build();

		Settings = configuration.Get<AppSettings>() ?? new AppSettings();

		await Task.CompletedTask;
	}

	public async Task SaveAsync(CancellationToken cancellationToken = default)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath) ?? AppContext.BaseDirectory);

		await using var stream = File.Create(_settingsFilePath);
		await JsonSerializer.SerializeAsync(
			stream,
			Settings,
			SerializerOptions,
			cancellationToken);
	}
}
