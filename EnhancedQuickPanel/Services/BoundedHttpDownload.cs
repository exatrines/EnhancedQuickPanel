using System.Net.Http;
using System.Threading;

namespace EnhancedQuickPanel.Services;

internal enum BoundedDownloadStatus
{
    Success,
    TooLarge,
    Empty,
    Failed,
    Canceled,
}

internal readonly record struct BoundedDownloadResult(
    BoundedDownloadStatus Status,
    byte[] Bytes,
    int HttpStatus);

/// <summary>Downloads at most 5MB from HTTP(S), aborting as soon as the limit is exceeded.</summary>
internal static class BoundedHttpDownload
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly HttpClient Client = new()
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };

    public static async Task<BoundedDownloadResult> GetAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(Timeout);
        var token = linked.Token;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return Fail(BoundedDownloadStatus.Failed, (int)response.StatusCode);

            if (response.Content.Headers.ContentLength is > MaxBytes)
                return Fail(BoundedDownloadStatus.TooLarge, (int)response.StatusCode);

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), token).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (buffer.Length + read > MaxBytes)
                    return Fail(BoundedDownloadStatus.TooLarge, (int)response.StatusCode);

                buffer.Write(chunk, 0, read);
            }

            if (buffer.Length == 0)
                return Fail(BoundedDownloadStatus.Empty, (int)response.StatusCode);

            return new BoundedDownloadResult(BoundedDownloadStatus.Success, buffer.ToArray(), (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Fail(BoundedDownloadStatus.Canceled, 0);
        }
        catch (OperationCanceledException)
        {
            return Fail(BoundedDownloadStatus.Failed, 0);
        }
        catch (Exception)
        {
            return Fail(BoundedDownloadStatus.Failed, 0);
        }
    }

    private static BoundedDownloadResult Fail(BoundedDownloadStatus status, int httpStatus) =>
        new(status, [], httpStatus);
}
