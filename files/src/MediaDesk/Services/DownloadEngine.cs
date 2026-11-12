using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using MediaDesk.Models;

namespace MediaDesk.Services;

public readonly record struct DownloadProgress(long Downloaded, long? Total, double BytesPerSecond);

public sealed class DownloadEngine : IDisposable
{
    private readonly HttpClient _http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.None,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10
    }) { Timeout = Timeout.InfiniteTimeSpan };

    public static bool IsSupportedUrl(string text) => Uri.TryCreate(text, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !string.IsNullOrWhiteSpace(uri.Host);

    public static string MakeFileName(Uri uri)
    {
        string raw = Uri.UnescapeDataString(uri.AbsolutePath.TrimEnd('/').Split('/').LastOrDefault() ?? "");
        raw = raw.Length == 0 ? "download.bin" : raw;
        foreach (char bad in Path.GetInvalidFileNameChars()) raw = raw.Replace(bad, '_');
        if (raw.Length > 150) raw = raw[..150];
        return raw is "." or ".." or "" ? "download.bin" : raw;
    }

    public async Task<string> DownloadAsync(DownloadEntry item, IProgress<DownloadProgress> progress, CancellationToken cancel)
    {
        if (!IsSupportedUrl(item.Url)) throw new InvalidOperationException("Only HTTP(S) links are supported.");
        string destination = Path.GetFullPath(item.TargetPath);
        string folder = Path.GetDirectoryName(destination) ?? throw new IOException("Missing download directory.");
        Directory.CreateDirectory(folder);
        string partial = destination + ".part";
        long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is "text/html" or "application/xhtml+xml")
            throw new InvalidOperationException(
                "The URL returned a web page, not a downloadable file. Paste a direct file URL.");

        bool resumeAccepted = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent
            && response.Content.Headers.ContentRange?.From == offset;
        if (!resumeAccepted) offset = 0;
        long? total = response.Content.Headers.ContentRange?.Length;
        if (!total.HasValue && response.Content.Headers.ContentLength is long length) total = offset + length;

        await using var input = await response.Content.ReadAsStreamAsync(cancel);
        await using var output = new FileStream(partial, offset > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.Read, 1024 * 128, useAsync: true);
        var bytes = new byte[1024 * 128];
        long downloaded = offset;
        long sampleBytes = downloaded;
        var timer = Stopwatch.StartNew();
        progress.Report(new DownloadProgress(downloaded, total, 0));
        while (true)
        {
            int count = await input.ReadAsync(bytes, cancel);
            if (count == 0) break;
            await output.WriteAsync(bytes.AsMemory(0, count), cancel);
            downloaded += count;
            if (timer.ElapsedMilliseconds >= 350)
            {
                double rate = (downloaded - sampleBytes) / timer.Elapsed.TotalSeconds;
                progress.Report(new DownloadProgress(downloaded, total, rate));
                sampleBytes = downloaded;
                timer.Restart();
            }
        }
        await output.FlushAsync(cancel);
        if (total.HasValue && downloaded != total.Value)
            throw new IOException($"Download incomplete ({downloaded} of {total.Value} bytes).");
        output.Close();
        // Never silently replace an existing user file.
        if (File.Exists(destination)) throw new IOException("Destination file already exists. Choose a new filename.");
        File.Move(partial, destination);
        progress.Report(new DownloadProgress(downloaded, total ?? downloaded, 0));
        return destination;
    }

    public void Dispose() => _http.Dispose();
}
