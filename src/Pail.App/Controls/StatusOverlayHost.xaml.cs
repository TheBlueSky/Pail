using Microsoft.Extensions.DependencyInjection;
using Pail.App.Services;
using Pail.Services;

namespace Pail.App.Controls;

public sealed partial class StatusOverlayHost : UserControl
{
	private readonly StatusInfoBarPresenter _statusPresenter;

	public StatusOverlayHost()
	{
		InitializeComponent();

		var settings = PailApp.Services.GetRequiredService<ISettingsService>().Settings;
		var displayDuration = TimeSpan.FromSeconds(Math.Max(1, settings.StatusOverlayDurationSeconds));

		_statusPresenter = new StatusInfoBarPresenter(
			DispatcherQueue,
			StatusInfoBar,
			PailApp.Services.GetRequiredService<IStatusMessageService>(),
			displayDuration);

		Loaded += OnLoaded;
		Unloaded += OnUnloaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e) =>
		_statusPresenter.Attach();

	private void OnUnloaded(object sender, RoutedEventArgs e) =>
		_statusPresenter.Detach();
}
