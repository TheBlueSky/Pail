using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using Pail.App.Services;

namespace Pail.App.Views;

public sealed partial class MainPage : Page
{
	private INavigationHostService? _navigationService;

	public MainPage()
	{
		InitializeComponent();
		Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e)
	{
		_navigationService ??= PailApp.Services.GetRequiredService<INavigationHostService>();
		_navigationService.RegisterContentFrame(ContentFrame);

		if (ContentFrame.Content is null)
		{
			_navigationService.NavigateTo("BucketListPage");
			return;
		}

		UpdateBackButtonState();
	}

	private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
	{
	}

	private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
		_navigationService?.GoBack();
		UpdateBackButtonState();
	}

	private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
	{
	}

	private void UpdateBackButtonState() => NavView.IsBackEnabled = ContentFrame.CanGoBack;
}
