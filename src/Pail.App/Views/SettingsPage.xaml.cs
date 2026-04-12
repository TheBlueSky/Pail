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
}
