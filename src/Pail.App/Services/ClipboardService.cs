using Pail.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Pail.App.Services;

public sealed class ClipboardService : IClipboardService
{
	public Task<bool> CopyTextAsync(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return Task.FromResult(false);
		}

		try
		{
			var package = new DataPackage();
			package.SetText(text);

			Clipboard.SetContent(package);

			return Task.FromResult(true);
		}
		catch
		{
			return Task.FromResult(false);
		}
	}
}
