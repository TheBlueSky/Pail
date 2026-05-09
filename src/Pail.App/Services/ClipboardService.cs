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

	public async Task<string?> ReadTextAsync()
	{
		try
		{
			var dataPackageView = Clipboard.GetContent();

			if (dataPackageView is null || !dataPackageView.Contains(StandardDataFormats.Text))
			{
				return null;
			}

			var text = await dataPackageView.GetTextAsync();

			return string.IsNullOrWhiteSpace(text) ? null : text;
		}
		catch
		{
			return null;
		}
	}
}
