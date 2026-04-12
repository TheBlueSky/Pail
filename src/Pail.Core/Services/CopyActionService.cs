namespace Pail.Services;

public sealed class CopyActionService : ICopyActionService
{
	private readonly IClipboardService _clipboardService;
	private readonly IStatusMessageService _statusMessageService;

	public CopyActionService(IClipboardService clipboardService, IStatusMessageService statusMessageService)
	{
		_clipboardService = clipboardService;
		_statusMessageService = statusMessageService;
	}

	public async Task<bool> CopyWithFeedbackAsync(string value, string successMessage, string failureMessage)
	{
		var copied = await _clipboardService.CopyTextAsync(value);

		if (copied)
		{
			_statusMessageService.ShowInfo(successMessage);
			return true;
		}

		_statusMessageService.ShowError(failureMessage);
		return false;
	}
}
