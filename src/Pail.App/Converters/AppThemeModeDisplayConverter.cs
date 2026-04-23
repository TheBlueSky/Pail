using Microsoft.UI.Xaml.Data;
using Pail.Models;

namespace Pail.App.Converters;

public sealed partial class AppThemeModeDisplayConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language) => value switch
	{
		AppThemeMode.Light => "Light",
		AppThemeMode.Dark => "Dark",
		AppThemeMode.System => "Use system setting",
		_ => value?.ToString() ?? string.Empty,
	};

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException();
}
