using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using Pail.App.Services;

namespace Pail.App.Views;

public sealed partial class MainPage : Page
{
	private INavigationHostService? _navigationService;
	private bool _isUpdatingSelection;

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
			NavigateToTopLevelPage("BucketListPage");
			return;
		}

		SyncSelectedItem(ContentFrame.CurrentSourcePageType);
		UpdateBackButtonState();
	}

	private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
	{
		if (_isUpdatingSelection)
		{
			return;
		}

		if (args.SelectedItem is NavigationViewItem { Tag: string pageKey })
		{
			NavigateToTopLevelPage(pageKey);
		}
	}

	private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
	{
		_navigationService?.GoBack();
		UpdateBackButtonState();
	}

	private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
	{
		SyncSelectedItem(e.SourcePageType);
		UpdateBackButtonState();
	}

	private void NavigateToTopLevelPage(string pageKey)
	{
		_navigationService ??= PailApp.Services.GetRequiredService<INavigationHostService>();

		var currentPageType = ContentFrame.CurrentSourcePageType;

		if ((pageKey == "BucketListPage" && currentPageType == typeof(BucketListPage)) ||
			(pageKey == "SettingsPage" && currentPageType == typeof(SettingsPage)))
		{
			SyncSelectedItem(currentPageType);
			UpdateBackButtonState();
			return;
		}

		_navigationService.NavigateTo(pageKey);
		ContentFrame.BackStack.Clear();
		UpdateBackButtonState();
	}

	private void SyncSelectedItem(Type? pageType)
	{
		_isUpdatingSelection = true;
		NavView.SelectedItem = pageType == typeof(SettingsPage) ? SettingsNavItem : BucketsNavItem;
		_isUpdatingSelection = false;
	}

	private void UpdateBackButtonState() => NavView.IsBackEnabled = ContentFrame.CanGoBack;
}
