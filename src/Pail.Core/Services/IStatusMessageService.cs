namespace Pail.Services;

public interface IStatusMessageService
{
	public event EventHandler<StatusMessage>? MessageRaised;

	public void ShowInfo(string message);

	public void ShowError(string message);
}

public sealed record StatusMessage(string Message, StatusMessageLevel Level);

public enum StatusMessageLevel
{
	Info,
	Error,
}
