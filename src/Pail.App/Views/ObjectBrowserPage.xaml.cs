using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Pail.App.Services;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class ObjectBrowserPage : Page
{
	private readonly StatusInfoBarPresenter _statusPresenter;

	public ObjectBrowserPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<ObjectBrowserViewModel>();

		_statusPresenter = new StatusInfoBarPresenter(
			DispatcherQueue,
			StatusInfoBar,
			PailApp.Services.GetRequiredService<IStatusMessageService>());

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	public ObjectBrowserViewModel ViewModel { get; }

	protected override async void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);

		if (e.Parameter is string bucketName)
		{
			await ViewModel.InitializeAsync(bucketName);
		}
	}

	private void OnGridDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (ObjectGrid.SelectedItem is S3ObjectItem item)
		{
			ViewModel.OpenItemCommand.Execute(item);
		}
	}

	private void OnDownloadClick(object sender, RoutedEventArgs e)
	{
		var selected = ObjectGrid.SelectedItems.Cast<S3ObjectItem>().ToList();
		ViewModel.DownloadSelectedCommand.Execute(selected);
	}

	private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e) =>
		ViewModel.SelectedItem = ObjectGrid.SelectedItem as S3ObjectItem;

	private async void OnCopyObjectNameContextClick(object sender, RoutedEventArgs e)
	{
		if (sender is not MenuFlyoutItem { DataContext: S3ObjectItem item })
		{
			return;
		}

		ViewModel.SelectedItem = item;
		await ViewModel.CopyObjectNameCommand.ExecuteAsync(null);
	}

	private async void OnCopyObjectFullKeyContextClick(object sender, RoutedEventArgs e)
	{
		if (sender is not MenuFlyoutItem { DataContext: S3ObjectItem item })
		{
			return;
		}

		ViewModel.SelectedItem = item;
		await ViewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);
	}

	private void OnLoaded(object sender, RoutedEventArgs e) =>
		_statusPresenter.Attach();

	private void OnUnloaded(object sender, RoutedEventArgs e) =>
		_statusPresenter.Detach();
}
