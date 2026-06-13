using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.ViewModels;
using Windows.System;

namespace Pail.App.Views;

public sealed partial class BucketListPage : Page
{
	public BucketListPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<BucketListViewModel>();
	}

	public BucketListViewModel ViewModel { get; }

	protected override async void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);

		await ViewModel.LoadBucketsAsync();

		SearchTextBox.Focus(FocusState.Programmatic);
	}

	private void OnBucketDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
	{
		if (e.OriginalSource is FrameworkElement element && element.DataContext is S3BucketItem bucket)
		{
			ViewModel.SelectBucketCommand.Execute(bucket);
		}
	}

	private void OnBucketListPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key == VirtualKey.Enter && ViewModel.SelectedBucket is S3BucketItem bucket)
		{
			ViewModel.SelectBucketCommand.Execute(bucket);
			e.Handled = true;
		}
	}

	private async void OnBucketCopyNameContextClick(object sender, RoutedEventArgs e)
	{
		if (sender is not MenuFlyoutItem { DataContext: S3BucketItem bucket })
		{
			return;
		}

		ViewModel.SelectedBucket = bucket;
		await ViewModel.CopyBucketNameCommand.ExecuteAsync(null);
	}

	private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
	{
		if (sender is TextBox searchTextBox)
		{
			ViewModel.SearchText = searchTextBox.Text ?? string.Empty;
		}
	}

	private void OnPageKeyboardAcceleratorInvoked(KeyboardAccelerator accelerator, KeyboardAcceleratorInvokedEventArgs args)
	{
		if (accelerator == SearchKeyboardAcceleratorF || accelerator == SearchKeyboardAcceleratorF3)
		{
			SearchTextBox.Focus(FocusState.Programmatic);
			args.Handled = true;
		}
	}
}
