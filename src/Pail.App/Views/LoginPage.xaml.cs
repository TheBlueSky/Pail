using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class LoginPage : Page
{
	public LoginPage()
	{
		InitializeComponent();

		// In a real DI setup, this would be injected or resolved
		// For now, we'll assume it's set after construction if needed,
		// or resolved via a static locator for simplicity in this WinUI environment.
		ViewModel = PailApp.Services.GetRequiredService<LoginViewModel>();
	}

	public LoginViewModel ViewModel { get; }

	protected override async void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);

		await ViewModel.LoadCredentialProfilesAsync();
	}
}
