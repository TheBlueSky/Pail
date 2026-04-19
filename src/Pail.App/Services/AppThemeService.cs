using Pail.Models;
using Pail.Services;

namespace Pail.App.Services;

public sealed class AppThemeService : IAppThemeService
{
	public void ApplyTheme(AppThemeMode appTheme)
	{
		if (PailApp.MainWindow?.Content is not FrameworkElement rootElement)
		{
			throw new InvalidOperationException("Main window content is not ready.");
		}

		rootElement.RequestedTheme = appTheme switch
		{
			AppThemeMode.System => ElementTheme.Default,
			AppThemeMode.Light => ElementTheme.Light,
			AppThemeMode.Dark => ElementTheme.Dark,
			_ => throw new ArgumentOutOfRangeException(nameof(appTheme), appTheme, "Unsupported app theme."),
		};
	}
}
