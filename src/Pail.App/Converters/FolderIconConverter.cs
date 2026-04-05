using Microsoft.UI.Xaml.Data;

namespace Pail.App.Converters;

public partial class FolderIconConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, string language) =>
		(bool)value ? Symbol.Folder : Symbol.Document;

	public object ConvertBack(object value, Type targetType, object parameter, string language) =>
		throw new NotImplementedException();
}
