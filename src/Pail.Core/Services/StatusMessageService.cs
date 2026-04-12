namespace Pail.Services;

public sealed class StatusMessageService : IStatusMessageService
{
	public event EventHandler<StatusMessage>? MessageRaised;

	public void ShowInfo(string message) =>
		MessageRaised?.Invoke(this, new StatusMessage(message, StatusMessageLevel.Info));

	public void ShowError(string message) =>
		MessageRaised?.Invoke(this, new StatusMessage(message, StatusMessageLevel.Error));
}
