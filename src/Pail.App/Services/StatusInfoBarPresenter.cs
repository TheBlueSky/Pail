using Microsoft.UI.Dispatching;
using Pail.Services;

namespace Pail.App.Services;

public sealed partial class StatusInfoBarPresenter : IDisposable
{
	private readonly DispatcherQueue _dispatcherQueue;
	private readonly InfoBar _infoBar;
	private readonly IStatusMessageService _statusMessageService;
	private readonly ISettingsService _settingsService;
	private readonly DispatcherQueueTimer _statusTimer;

	private bool _isAttached;

	public StatusInfoBarPresenter(
		DispatcherQueue dispatcherQueue,
		InfoBar infoBar,
		IStatusMessageService statusMessageService,
		ISettingsService settingsService)
	{
		_dispatcherQueue = dispatcherQueue;
		_infoBar = infoBar;
		_statusMessageService = statusMessageService;
		_settingsService = settingsService;

		_statusTimer = dispatcherQueue.CreateTimer();
		_statusTimer.Interval = ResolveDisplayDuration();
		_statusTimer.Tick += OnStatusTimerTick;
	}

	public void Attach()
	{
		if (_isAttached)
		{
			return;
		}

		_statusMessageService.MessageRaised += OnStatusMessageRaised;
		_isAttached = true;
	}

	public void Detach()
	{
		if (_isAttached is false)
		{
			return;
		}

		_statusMessageService.MessageRaised -= OnStatusMessageRaised;
		_statusTimer.Stop();
		_infoBar.IsOpen = false;
		_isAttached = false;
	}

	public void Dispose()
	{
		Detach();
		_statusTimer.Tick -= OnStatusTimerTick;
	}

	private void OnStatusTimerTick(DispatcherQueueTimer sender, object args)
	{
		sender.Stop();
		_infoBar.IsOpen = false;
	}

	private void OnStatusMessageRaised(object? sender, StatusMessage message) =>
		_dispatcherQueue.TryEnqueue(() =>
		{
			_infoBar.Severity = MapSeverity(message.Level);
			_infoBar.Message = message.Message;
			_infoBar.IsOpen = true;

			_statusTimer.Interval = ResolveDisplayDuration();
			_statusTimer.Stop();
			_statusTimer.Start();
		});

	private TimeSpan ResolveDisplayDuration() =>
		TimeSpan.FromSeconds(Math.Max(1, _settingsService.StatusOverlayDurationSeconds));

	private static InfoBarSeverity MapSeverity(StatusMessageLevel level) =>
		level == StatusMessageLevel.Error ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
}
