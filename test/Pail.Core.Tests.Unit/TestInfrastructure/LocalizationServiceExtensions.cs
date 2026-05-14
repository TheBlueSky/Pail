using System.Globalization;
using NSubstitute;
using Pail.Services;

namespace Pail.Core.Tests.Unit.TestInfrastructure;

internal static class LocalizationServiceExtensions
{
	extension(ILocalizationService localizationService)
	{
		public void ReturnsFallbackStrings()
		{
			localizationService
				.GetString(Arg.Any<string>(), Arg.Any<string>())
				.Returns(callInfo => callInfo.ArgAt<string>(1));

			localizationService
				.FormatString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object?[]>())
				.Returns(callInfo => string.Format(CultureInfo.CurrentCulture, callInfo.ArgAt<string>(1), callInfo.ArgAt<object?[]>(2)));
		}
	}
}
