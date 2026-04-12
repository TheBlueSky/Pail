using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class ObjectBrowserPage : Page
{
	private readonly IStatusMessageService _statusMessageService;
	private readonly DispatcherQueueTimer _statusTimer;

	public ObjectBrowserPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<ObjectBrowserViewModel>();

		_statusMessageService = PailApp.Services.GetRequiredService<IStatusMessageService>();
		_statusTimer = DispatcherQueue.CreateTimer();
		_statusTimer.Interval = TimeSpan.FromSeconds(3);
		_statusTimer.Tick += OnStatusTimerTick;

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
		_statusMessageService.MessageRaised += OnStatusMessageRaised;

	private void OnUnloaded(object sender, RoutedEventArgs e) =>
		_statusMessageService.MessageRaised -= OnStatusMessageRaised;

	private void OnStatusTimerTick(DispatcherQueueTimer sender, object args)
	{
		sender.Stop();
		StatusInfoBar.IsOpen = false;
	}

	private void OnStatusMessageRaised(object? sender, StatusMessage message) =>
		DispatcherQueue.TryEnqueue(() =>
		{
			StatusInfoBar.Severity = message.Level == StatusMessageLevel.Error ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
			StatusInfoBar.Message = message.Message;
			StatusInfoBar.IsOpen = true;

			_statusTimer.Stop();
			_statusTimer.Start();
		});
}
