namespace Pail.Models;

public sealed record S3BucketItem(string Name, DateTime? CreationDate);

public sealed class S3ObjectItem
{
	private const string NoSizeDisplay = "-";

	private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB", "PB"];

	public required string Key { get; init; }

	public required string Name { get; init; }

	public long? Size { get; init; }

	public DateTime? LastModified { get; init; }

	public bool IsFolder { get; init; }

	public string SizeDisplay => IsFolder || Size is null ? NoSizeDisplay : FormatSize(Size.Value);

	private static string FormatSize(long bytes)
	{
		if (bytes < 0)
		{
			return NoSizeDisplay;
		}

		var index = 0;
		decimal number = bytes;

		while (number >= 1024 && index < Suffixes.Length - 1)
		{
			number /= 1024;
			index++;
		}

		return $"{number:n1} {Suffixes[index]}";
	}
}
