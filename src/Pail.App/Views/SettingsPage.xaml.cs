using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class SettingsPage : Page
{
	public SettingsPage()
	{
		InitializeComponent();
		ViewModel = PailApp.Services.GetRequiredService<SettingsViewModel>();
	}

	public SettingsViewModel ViewModel { get; }

	public string AppVersion { get; } = GetAppVersion();

	private static string GetAppVersion()
	{
		var version = Assembly.GetExecutingAssembly().GetName().Version;
		return version is null ? "Unknown version" : version.ToString(4);
	}
}
