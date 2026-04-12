namespace Pail.Services;

public interface IClipboardService
{
	public Task<bool> CopyTextAsync(string text);
}
