using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Navigation;
using Pail.Models;
using Pail.Services;
using Pail.ViewModels;

namespace Pail.App.Views;

public sealed partial class BucketListPage : Page
{
	private readonly IStatusMessageService _statusMessageService;
	private readonly DispatcherQueueTimer _statusTimer;

	public BucketListPage()
	{
		InitializeComponent();

		ViewModel = PailApp.Services.GetRequiredService<BucketListViewModel>();

		_statusMessageService = PailApp.Services.GetRequiredService<IStatusMessageService>();
		_statusTimer = DispatcherQueue.CreateTimer();
		_statusTimer.Interval = TimeSpan.FromSeconds(3);
		_statusTimer.Tick += OnStatusTimerTick;

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	public BucketListViewModel ViewModel { get; }

	protected override async void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);

		await ViewModel.LoadBucketsAsync();
	}

	private void OnBucketClick(object sender, ItemClickEventArgs e)
	{
		if (e.ClickedItem is S3BucketItem bucket)
		{
			ViewModel.SelectBucketCommand.Execute(bucket);
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
