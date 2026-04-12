using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.ViewModels;

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

	private void OnGridRightTapped(object sender, RightTappedRoutedEventArgs e)
	{
		if (e.OriginalSource is FrameworkElement { DataContext: S3ObjectItem item })
		{
			ViewModel.SelectedItem = item;
		}
	}

	private async void OnCopyObjectNameContextClick(object sender, RoutedEventArgs e) =>
		await ViewModel.CopyObjectNameCommand.ExecuteAsync(null);

	private async void OnCopyObjectFullKeyContextClick(object sender, RoutedEventArgs e) =>
		await ViewModel.CopyObjectFullKeyCommand.ExecuteAsync(null);
}
