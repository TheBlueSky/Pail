using NSubstitute;
using Pail.Services;

namespace Pail.Core.Tests.Unit.Services;

public sealed class CopyActionServiceTests
{
	private readonly IClipboardService _clipboardService = Substitute.For<IClipboardService>();
	private readonly IStatusMessageService _statusMessageService = Substitute.For<IStatusMessageService>();

	[Fact]
	internal async Task CopyWithFeedbackAsync_WhenCopySucceeds_ShowsSuccessMessage()
	{
		// Arrange
		var service = new CopyActionService(_clipboardService, _statusMessageService);
		_clipboardService.CopyTextAsync("bucket-a").Returns(true);

		// Act
		var result = await service.CopyWithFeedbackAsync("bucket-a", "Copied bucket-a", "Copy failed");

		// Assert
		Assert.True(result);
		_statusMessageService.Received(1).ShowInfo("Copied bucket-a");
		_statusMessageService.DidNotReceive().ShowError(Arg.Any<string>());
	}

	[Fact]
	internal async Task CopyWithFeedbackAsync_WhenCopyFails_ShowsErrorMessage()
	{
		// Arrange
		var service = new CopyActionService(_clipboardService, _statusMessageService);
		_clipboardService.CopyTextAsync("bucket-a").Returns(false);

		// Act
		var result = await service.CopyWithFeedbackAsync("bucket-a", "Copied bucket-a", "Copy failed");

		// Assert
		Assert.False(result);
		_statusMessageService.Received(1).ShowError("Copy failed");
		_statusMessageService.DidNotReceive().ShowInfo(Arg.Any<string>());
	}
}
