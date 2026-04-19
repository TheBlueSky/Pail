using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.ViewModels;
using WinUI.TableView;

namespace Pail.App.Views;

public sealed partial class ObjectBrowserPage : Page
{
	public ObjectBrowserPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<ObjectBrowserViewModel>();
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

	private void OnGridCellDoubleTapped(object sender, TableViewCellDoubleTappedEventArgs e)
	{
		if (e.Item is S3ObjectItem item)
		{
			ViewModel.OpenItemCommand.Execute(item);
		}
	}

	private void OnDownloadClick(object sender, RoutedEventArgs e)
	{
		var selected = ObjectGrid.SelectedItems.Cast<S3ObjectItem>().ToList();
		ViewModel.DownloadSelectedCommand.Execute(selected);
	}

	private void OnGridRowContextFlyoutOpening(object sender, TableViewRowContextFlyoutEventArgs e) =>
		ViewModel.SelectedItem = e.Item as S3ObjectItem;

	private async void OnCopyObjectNameContextClick(object sender, RoutedEventArgs e) =>
		await ViewModel.CopyObjectNameCommand.ExecuteAsync(null);

	private async void OnCopyObjectFullKeyContextClick(object sender, RoutedEventArgs e) =>
		await ViewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);
}
