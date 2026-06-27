using System.Globalization;
using Pail.Models;

namespace Pail.ViewModels;

public sealed class ObjectBrowserItemViewModel
{
	private readonly S3ObjectItem _item;

	public ObjectBrowserItemViewModel(S3ObjectItem item, DateTimeDisplayMode timestampDisplayMode, string utcDisplaySuffix)
	{
		ArgumentNullException.ThrowIfNull(utcDisplaySuffix);

		_item = item;

		LastModifiedDisplay = FormatLastModified(item.LastModified, timestampDisplayMode, utcDisplaySuffix);
	}

	public string Key => _item.Key;

	public string Name => _item.Name;

	public long? Size => _item.Size;

	public DateTimeOffset? LastModified => _item.LastModified;

	public bool IsFolder => _item.IsFolder;

	public string SizeDisplay => _item.SizeDisplay;

	public string LastModifiedDisplay { get; }

	private static string FormatLastModified(DateTimeOffset? lastModified, DateTimeDisplayMode timestampDisplayMode, string utcDisplaySuffix) => lastModified is null
		? string.Empty
		: timestampDisplayMode switch
		{
			DateTimeDisplayMode.Utc => string.Format(CultureInfo.CurrentCulture, "{0:yyyy-MM-dd HH:mm:ss} {1}", lastModified.Value.ToUniversalTime(), utcDisplaySuffix),
			_ => lastModified.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.CurrentCulture),
		};
}
