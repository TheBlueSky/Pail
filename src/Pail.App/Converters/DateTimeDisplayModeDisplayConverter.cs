using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Data;
using Pail.Models;
using Pail.Services;

namespace Pail.App.Converters;

public sealed partial class DateTimeDisplayModeDisplayConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language) => value switch
	{
		DateTimeDisplayMode.Utc => PailApp.Services.GetRequiredService<ILocalizationService>().GetString("DateTimeDisplayModeUtc", "UTC"),
		DateTimeDisplayMode.Local => PailApp.Services.GetRequiredService<ILocalizationService>().GetString("DateTimeDisplayModeLocal", "Local time"),
		_ => value?.ToString() ?? string.Empty,
	};

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException();
}
