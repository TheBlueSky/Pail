using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Windows.ApplicationModel.Resources;
using Pail.Services;

namespace Pail.App.Services;

public sealed class ResourceLocalizationService : ILocalizationService
{
	private const int ErrorNotFoundHResult = unchecked((int)0x80070490);
	private const string ResourcesSubtreeName = "Resources";

	private static readonly string[] PriFileCandidates = ["Pail.pri", "resources.pri"];

	private readonly Lazy<IReadOnlyList<ResourceMap>> _resourceMaps = new(CreateResourceMaps);

	public string GetString(string resourceName, string fallback)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

		foreach (var resourceMap in _resourceMaps.Value)
		{
			if (TryGetString(resourceMap, resourceName, out var value))
			{
				return value;
			}
		}

		return fallback;
	}

	public string FormatString(string resourceName, string fallbackFormat, params object?[] args) =>
		string.Format(CultureInfo.CurrentCulture, GetString(resourceName, fallbackFormat), args);

	private static List<ResourceMap> CreateResourceMaps()
	{
		var resourceMaps = new List<ResourceMap>();

		AddResourceMap(resourceMaps, () => new ResourceManager());

		foreach (var priFileCandidate in PriFileCandidates)
		{
			var priFilePath = Path.Combine(AppContext.BaseDirectory, priFileCandidate);

			if (File.Exists(priFilePath))
			{
				AddResourceMap(resourceMaps, () => new ResourceManager(priFilePath));
			}
		}

		return resourceMaps;

		static void AddResourceMap(List<ResourceMap> resourceMaps, Func<ResourceManager> createResourceManager)
		{
			try
			{
				resourceMaps.Add(createResourceManager().MainResourceMap.GetSubtree(ResourcesSubtreeName));
			}
			catch (Exception ex) when (IsExpectedResourceException(ex))
			{
			}
		}
	}

	private static bool TryGetString(ResourceMap resourceMap, string resourceName, [NotNullWhen(returnValue: true)] out string? value)
	{
		try
		{
			value = resourceMap.GetValue(resourceName).ValueAsString;
			return true;
		}
		catch (Exception ex) when (IsExpectedResourceException(ex))
		{
			value = null;
			return false;
		}
	}

	private static bool IsExpectedResourceException(Exception exception) =>
		exception.HResult == ErrorNotFoundHResult ||
		exception is FileNotFoundException ||
		exception is DirectoryNotFoundException ||
		exception is ArgumentException ||
		exception is COMException { HResult: ErrorNotFoundHResult };
}
