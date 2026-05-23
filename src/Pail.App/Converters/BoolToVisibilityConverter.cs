using Microsoft.UI.Xaml.Data;

namespace Pail.App.Converters;

public sealed partial class BoolToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language)
	{
		var isVisible = value switch
		{
			bool boolValue => boolValue,
			int intValue => intValue > 0,
			null => false,
			_ => true,
		};

		if (parameter is string text && string.Equals(text, "Invert", StringComparison.OrdinalIgnoreCase))
		{
			isVisible = !isVisible;
		}

		return isVisible ? Visibility.Visible : Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, string language)
	{
		var isVisible = value is Visibility.Visible;

		if (parameter is string text && string.Equals(text, "Invert", StringComparison.OrdinalIgnoreCase))
		{
			isVisible = !isVisible;
		}

		return isVisible;
	}
}
