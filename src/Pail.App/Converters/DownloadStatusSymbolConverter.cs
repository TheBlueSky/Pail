using Microsoft.UI.Xaml.Data;
using Pail.Models;

namespace Pail.App.Converters;

public sealed partial class DownloadStatusSymbolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language) =>
		value switch
		{
			DownloadStatus.Completed => Symbol.Accept,
			DownloadStatus.Failed => Symbol.Cancel,
			DownloadStatus.Cancelled => Symbol.Cancel,
			_ => Symbol.Download,
		};

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotSupportedException();
}
