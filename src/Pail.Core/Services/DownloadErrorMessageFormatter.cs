using System.Net;
using System.Net.Sockets;
using Amazon.Runtime;
using Amazon.S3;

namespace Pail.Services;

internal static class DownloadErrorMessageFormatter
{
	public static DownloadFailureMessage Format(Exception exception, ILocalizationService localizationService)
	{
		ArgumentNullException.ThrowIfNull(exception);
		ArgumentNullException.ThrowIfNull(localizationService);

		var summary = exception switch
		{
			AmazonS3Exception s3Exception when IsPermissionError(s3Exception) => localizationService.GetString(
				"DownloadErrorPermission",
				"Pail could not read this object from S3. Check permissions and try again."),
			AmazonS3Exception s3Exception when IsNotFoundError(s3Exception) => localizationService.GetString(
				"DownloadErrorNotFound",
				"The S3 object was not found. It may have been deleted or moved."),
			AmazonS3Exception s3Exception when IsRegionMismatch(s3Exception) => localizationService.GetString(
				"DownloadErrorRegionMismatch",
				"This bucket is in a different region. Reconnect using the bucket's region, then try again."),
			AmazonS3Exception s3Exception when IsThrottlingError(s3Exception) => localizationService.GetString(
				"DownloadErrorThrottled",
				"Amazon S3 is throttling download requests. Try again in a moment."),
			AmazonServiceException serviceException when IsNetworkOrTimeoutError(serviceException) => localizationService.GetString(
				"DownloadErrorNetwork",
				"The network connection was interrupted. Check your connection and try again."),
			AmazonServiceException serviceException => localizationService.FormatString(
				"DownloadErrorS3",
				"Amazon S3 download failed: {0}",
				GetTechnicalDetails(serviceException)),
			UnauthorizedAccessException => localizationService.GetString(
				"DownloadErrorStoragePermission",
				"Pail could not write to the download location. Check folder permissions and try again."),
			DirectoryNotFoundException => localizationService.GetString(
				"DownloadErrorDownloadFolderMissing",
				"The download folder was not found. Choose a valid folder and try again."),
			HttpRequestException or SocketException or TimeoutException => localizationService.GetString(
				"DownloadErrorNetwork",
				"The network connection was interrupted. Check your connection and try again."),
			OperationCanceledException => localizationService.GetString(
				"DownloadErrorNetwork",
				"The network connection was interrupted. Check your connection and try again."),
			IOException => localizationService.GetString(
				"DownloadErrorStorageOrNetwork",
				"Pail could not finish the download. Check your connection, free disk space, and folder permissions, then try again."),
			_ => localizationService.FormatString(
				"DownloadErrorUnknown",
				"Download failed: {0}",
				GetTechnicalDetails(exception)),
		};

		return new DownloadFailureMessage(summary, GetTechnicalDetails(exception));
	}

	private static bool IsPermissionError(AmazonServiceException exception) =>
		exception.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized ||
		IsErrorCode(exception, "AccessDenied", "InvalidAccessKeyId", "SignatureDoesNotMatch", "ExpiredToken");

	private static bool IsNotFoundError(AmazonServiceException exception) =>
		exception.StatusCode is HttpStatusCode.NotFound ||
		IsErrorCode(exception, "NoSuchBucket", "NoSuchKey", "NotFound", "404");

	private static bool IsRegionMismatch(AmazonServiceException exception) =>
		exception.StatusCode is HttpStatusCode.MovedPermanently ||
		IsErrorCode(exception, "PermanentRedirect", "AuthorizationHeaderMalformed", "IllegalLocationConstraintException");

	private static bool IsThrottlingError(AmazonServiceException exception) =>
		exception.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable ||
		IsErrorCode(exception, "SlowDown", "Throttling", "ThrottlingException", "RequestLimitExceeded");

	private static bool IsNetworkOrTimeoutError(AmazonServiceException exception) =>
		exception.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout ||
		exception.InnerException is HttpRequestException or SocketException or TimeoutException ||
		IsErrorCode(exception, "RequestTimeout", "RequestTimeoutException", "TimeoutError", "NetworkingError");

	private static bool IsErrorCode(AmazonServiceException exception, params string[] values) =>
		values.Any(value => string.Equals(exception.ErrorCode, value, StringComparison.OrdinalIgnoreCase));

	private static string GetTechnicalDetails(Exception exception) =>
		string.IsNullOrWhiteSpace(exception.Message) ? exception.GetType().Name : exception.Message;
}

internal sealed record DownloadFailureMessage(string Summary, string Details);
