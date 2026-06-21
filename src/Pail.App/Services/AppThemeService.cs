using Microsoft.UI.Windowing;
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

		ApplyTitleBarTheme(appTheme);
	}

	private static void ApplyTitleBarTheme(AppThemeMode appTheme)
	{
		if (PailApp.MainWindow is null)
		{
			throw new InvalidOperationException("Main window is not ready.");
		}

		if (AppWindowTitleBar.IsCustomizationSupported() is false)
		{
			return;
		}

		var titleBar = PailApp.MainWindow.AppWindow.TitleBar;
		titleBar.PreferredTheme = appTheme switch
		{
			AppThemeMode.System => TitleBarTheme.UseDefaultAppMode,
			AppThemeMode.Light => TitleBarTheme.Light,
			AppThemeMode.Dark => TitleBarTheme.Dark,
			_ => throw new ArgumentOutOfRangeException(nameof(appTheme), appTheme, "Unsupported app theme."),
		};
	}
}
