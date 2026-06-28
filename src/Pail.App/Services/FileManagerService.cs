using Pail.Services;
using Windows.Storage;
using Windows.System;

namespace Pail.App.Services;

public sealed class FileManagerService : IFileManagerService
{
	public async Task<bool> ShowInFileManagerAsync(string path, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return false;
		}

		try
		{
			cancellationToken.ThrowIfCancellationRequested();

			var fullPath = Path.GetFullPath(path);

			if (File.Exists(fullPath))
			{
				return await ShowFileAsync(fullPath, cancellationToken);
			}

			if (Directory.Exists(fullPath))
			{
				return await ShowFolderAsync(fullPath, cancellationToken);
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			return false;
		}

		return false;
	}

	private static async Task<bool> ShowFileAsync(string path, CancellationToken cancellationToken)
	{
		var containingFolderPath = Path.GetDirectoryName(path);

		if (string.IsNullOrWhiteSpace(containingFolderPath) || !Directory.Exists(containingFolderPath))
		{
			return false;
		}

		cancellationToken.ThrowIfCancellationRequested();

		var containingFolder = await StorageFolder.GetFolderFromPathAsync(containingFolderPath);
		var file = await StorageFile.GetFileFromPathAsync(path);
		var options = new FolderLauncherOptions();
		options.ItemsToSelect.Add(file);

		cancellationToken.ThrowIfCancellationRequested();

		return await Launcher.LaunchFolderAsync(containingFolder, options);
	}

	private static async Task<bool> ShowFolderAsync(string path, CancellationToken cancellationToken)
	{
		var parentFolderPath = Directory.GetParent(path)?.FullName;

		if (!string.IsNullOrWhiteSpace(parentFolderPath) && Directory.Exists(parentFolderPath))
		{
			cancellationToken.ThrowIfCancellationRequested();

			var parentFolder = await StorageFolder.GetFolderFromPathAsync(parentFolderPath);
			var folder = await StorageFolder.GetFolderFromPathAsync(path);
			var options = new FolderLauncherOptions();
			options.ItemsToSelect.Add(folder);

			cancellationToken.ThrowIfCancellationRequested();

			if (await Launcher.LaunchFolderAsync(parentFolder, options))
			{
				return true;
			}
		}

		cancellationToken.ThrowIfCancellationRequested();

		var targetFolder = await StorageFolder.GetFolderFromPathAsync(path);
		return await Launcher.LaunchFolderAsync(targetFolder);
	}
}
