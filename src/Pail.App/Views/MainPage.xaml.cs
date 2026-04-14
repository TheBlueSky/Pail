using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Pail.App.Services;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class MainPage : Page
{
	private INavigationHostService? _navigationService;
	private ObjectBrowserViewModel? _observedObjectBrowserViewModel;
	private bool _isUpdatingSelection;

	public MainPage()
	{
		InitializeComponent();
		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
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

		TrackObjectBrowserViewModel();
		SyncSelectedItem(ContentFrame.CurrentSourcePageType);
		UpdateBackButtonState();
	}

	private void OnUnloaded(object sender, RoutedEventArgs e) => TrackObjectBrowserViewModel(null);

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

	private async void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) => await TryGoBackAsync();

	private async void OnBackKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
	{
		args.Handled = await TryGoBackAsync();
	}

	private async void OnPointerPressed(object sender, PointerRoutedEventArgs e)
	{
		if (e.GetCurrentPoint(this).Properties.IsXButton1Pressed)
		{
			e.Handled = await TryGoBackAsync();
		}
	}

	private void OnContentFrameNavigated(object sender, NavigationEventArgs e)
	{
		TrackObjectBrowserViewModel();
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

	private async Task<bool> TryGoBackAsync()
	{
		_navigationService ??= PailApp.Services.GetRequiredService<INavigationHostService>();

		if (_observedObjectBrowserViewModel?.CanNavigateBackWithinBucket is true)
		{
			await _observedObjectBrowserViewModel.GoBackCommand.ExecuteAsync(null);
			UpdateBackButtonState();
			return true;
		}

		if (_navigationService.CanGoBack)
		{
			_navigationService.GoBack();
			UpdateBackButtonState();
			return true;
		}

		UpdateBackButtonState();
		return false;
	}

	private void TrackObjectBrowserViewModel(ObjectBrowserViewModel? viewModel = null)
	{
		var nextViewModel = viewModel ?? (ContentFrame.Content as ObjectBrowserPage)?.ViewModel;

		if (ReferenceEquals(_observedObjectBrowserViewModel, nextViewModel))
		{
			return;
		}

		if (_observedObjectBrowserViewModel is not null)
		{
			_observedObjectBrowserViewModel.PropertyChanged -= OnObjectBrowserViewModelPropertyChanged;
		}

		_observedObjectBrowserViewModel = nextViewModel;

		if (_observedObjectBrowserViewModel is not null)
		{
			_observedObjectBrowserViewModel.PropertyChanged += OnObjectBrowserViewModelPropertyChanged;
		}
	}

	private void OnObjectBrowserViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(ObjectBrowserViewModel.CanNavigateBackWithinBucket))
		{
			UpdateBackButtonState();
		}
	}

	private void UpdateBackButtonState() =>
		NavView.IsBackEnabled = (_navigationService?.CanGoBack ?? false) || (_observedObjectBrowserViewModel?.CanNavigateBackWithinBucket ?? false);
}
