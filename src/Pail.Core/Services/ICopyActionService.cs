namespace Pail.Services;

public interface ICopyActionService
{
	public Task<bool> CopyWithFeedbackAsync(string value, string successMessage, string failureMessage);
}
