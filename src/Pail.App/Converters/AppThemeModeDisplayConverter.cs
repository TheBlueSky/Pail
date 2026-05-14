using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Data;
using Pail.Services;
using Pail.Models;

namespace Pail.App.Converters;

public sealed partial class AppThemeModeDisplayConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language) => value switch
	{
		AppThemeMode.Light => PailApp.Services.GetRequiredService<ILocalizationService>().GetString("ThemeModeLight", "Light"),
		AppThemeMode.Dark => PailApp.Services.GetRequiredService<ILocalizationService>().GetString("ThemeModeDark", "Dark"),
		AppThemeMode.System => PailApp.Services.GetRequiredService<ILocalizationService>().GetString("ThemeModeSystem", "Use system setting"),
		_ => value?.ToString() ?? string.Empty,
	};

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException();
}
