using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Pail.Models;

namespace Pail.App.Converters;

public sealed partial class DownloadStatusBrushConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language) =>
		value switch
		{
			DownloadStatus.Completed => new SolidColorBrush(Colors.Green),
			DownloadStatus.Failed => new SolidColorBrush(Colors.Firebrick),
			DownloadStatus.Cancelled => new SolidColorBrush(Colors.DarkOrange),
			DownloadStatus.Downloading => new SolidColorBrush(Colors.DodgerBlue),
			_ => new SolidColorBrush(Colors.Gray),
		};

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException();
}
