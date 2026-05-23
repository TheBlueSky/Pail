using Microsoft.UI.Xaml.Input;
using Pail.ViewModels;
using Windows.System;

namespace Pail.App.Controls;

public sealed partial class DownloadQueueFlyout : UserControl
{
	public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
		nameof(ViewModel),
		typeof(DownloadManagerViewModel),
		typeof(DownloadQueueFlyout),
		new PropertyMetadata(null, OnViewModelChanged));

	public DownloadQueueFlyout()
	{
		InitializeComponent();
	}

	public event EventHandler? CloseRequested;

	public DownloadManagerViewModel ViewModel
	{
		get => (DownloadManagerViewModel)GetValue(ViewModelProperty);
		set => SetValue(ViewModelProperty, value);
	}

	private static void OnViewModelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
	{
		if (dependencyObject is DownloadQueueFlyout flyout)
		{
			flyout.DataContext = args.NewValue;
		}
	}

	private async void OnDownloadsListKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key is not VirtualKey.Delete || DownloadsList.SelectedItem is not DownloadItemViewModel selectedItem)
		{
			return;
		}

		if (!selectedItem.CancelCommand.CanExecute(null))
		{
			return;
		}

		e.Handled = true;

		await selectedItem.CancelCommand.ExecuteAsync(null);
	}

	private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
	{
		if (e.Key is VirtualKey.Escape)
		{
			e.Handled = true;
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}
	}
}
