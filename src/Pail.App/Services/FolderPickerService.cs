using Pail.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pail.App.Services;

public sealed class FolderPickerService : IFolderPickerService
{
	public async Task<string?> PickFolderAsync(string? suggestedPath = null, CancellationToken cancellationToken = default)
	{
		var window = PailApp.MainWindow;

		if (window is null)
		{
			return null;
		}

		var picker = new FolderPicker();
		picker.FileTypeFilter.Add("*");

		picker.SuggestedStartLocation = PickerLocationId.Downloads;

		InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

		var selectedFolder = await picker.PickSingleFolderAsync();
		return selectedFolder?.Path;
	}
}
