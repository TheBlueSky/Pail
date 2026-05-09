using Microsoft.UI;
using Microsoft.Windows.Storage.Pickers;
using Pail.Services;
using WinRT.Interop;

namespace Pail.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
	public async Task<string?> PickFolderAsync()
	{
		var window = PailApp.MainWindow;

		if (window is null)
		{
			return null;
		}

		var windowId = Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(window));
		var picker = new FolderPicker(windowId) { SuggestedStartLocation = PickerLocationId.Downloads };
		var selectedFolder = await picker.PickSingleFolderAsync();

		return selectedFolder?.Path;
	}
}
