using DeDupe.Models.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace DeDupe.Services.Model
{
    /// <summary>
    /// Progress info for model downloads.
    /// </summary>
    public record ModelDownloadProgress(double Percentage, long BytesDownloaded, long TotalBytes, string StatusText);

    public class ModelDownloadService : IModelDownloadService
    {
        private const int MaxRetries = 3;

        private static readonly TimeSpan[] RetryDelays =
        [
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)
        ];

        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(30),
            DefaultRequestHeaders =
            {
                { "User-Agent", "DeDupe/1.0 (https://github.com/Jan-Eric-B/DeDupe)" }
            }
        };

        private readonly string _cacheDirectory;

        public ModelDownloadService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _cacheDirectory = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Models");
        }

        public string? GetLocalModelPath(BundledModelInfo model)
        {
            string cachedPath = Path.Combine(_cacheDirectory, model.FileName);
            return File.Exists(cachedPath) ? cachedPath : null;
        }

        public bool IsModelAvailable(BundledModelInfo model)
        {
            return GetLocalModelPath(model) != null;
        }

        public async Task<string> EnsureModelAvailableAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            string? existingPath = GetLocalModelPath(model);
            if (existingPath != null)
            {
                return existingPath;
            }

            if (model.DownloadUrl is null)
            {
                throw new InvalidOperationException($"Model '{model.DisplayName}' has no download URL and is not cached locally.");
            }

            Exception? lastException = null;

            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        TimeSpan delay = RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];

                        progress?.Report(new ModelDownloadProgress(0, 0, model.ExpectedFileSize, $"Retry {attempt}/{MaxRetries} — waiting {delay.TotalSeconds:F0}s..."));

                        await Task.Delay(delay, cancellationToken);
                    }

                    await DownloadModelAsync(model, progress, cancellationToken);

                    string downloadedPath = GetLocalModelPath(model) ?? throw new InvalidOperationException($"Model '{model.DisplayName}' was not found after download.");

                    return downloadedPath;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.WriteLine($"Download attempt {attempt + 1}/{MaxRetries + 1} failed for '{model.DisplayName}': {ex.Message}");
                }
            }

            throw new InvalidOperationException($"Failed to download '{model.DisplayName}' after {MaxRetries + 1} attempts.", lastException);
        }

        public async Task DownloadModelAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (model.DownloadUrl is null)
            {
                throw new InvalidOperationException($"Model '{model.DisplayName}' has no download URL.");
            }

            Directory.CreateDirectory(_cacheDirectory);

            string targetPath = Path.Combine(_cacheDirectory, model.FileName);
            string tempPath = targetPath + ".download";

            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? model.ExpectedFileSize;

                await using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

                byte[] buffer = new byte[81920];
                long bytesRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                    bytesRead += read;

                    if (totalBytes > 0)
                    {
                        double percentage = (double)bytesRead / totalBytes;
                        string sizeMB = $"{bytesRead / 1024.0 / 1024.0:F1}";
                        string totalMB = $"{totalBytes / 1024.0 / 1024.0:F0}";

                        progress?.Report(new ModelDownloadProgress(percentage, bytesRead, totalBytes, $"Downloading {model.DisplayName}... {sizeMB}/{totalMB} MB"));
                    }
                }

                await fileStream.FlushAsync(cancellationToken);
                fileStream.Close();

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                if (model.ExpectedSha256 is not null)
                {
                    string actualHash = await ComputeSha256Async(tempPath, cancellationToken);
                    if (!string.Equals(actualHash, model.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempPath);
                        throw new InvalidOperationException($"Hash mismatch for '{model.DisplayName}'. Expected: {model.ExpectedSha256}, Got: {actualHash}");
                    }
                }

                File.Move(tempPath, targetPath);

                progress?.Report(new ModelDownloadProgress(1.0, bytesRead, totalBytes, $"{model.DisplayName} downloaded"));

                Debug.WriteLine($"Model downloaded: {model.DisplayName} ({bytesRead / 1024.0 / 1024.0:F1} MB) → {targetPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error downloading model '{model.DisplayName}': {ex}");

                // Clean up partial download
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception exTwo)
                    {
                        Debug.WriteLine($"Error deleting model '{model.DisplayName}': {exTwo}");
                    }
                }
                throw;
            }
        }

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            using SHA256? sha256 = SHA256.Create();
            await using FileStream? stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);

            byte[] buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                sha256.TransformBlock(buffer, 0, read, null, 0);
            }
            sha256.TransformFinalBlock([], 0, 0);

            return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
        }

        public bool DeleteCachedModel(BundledModelInfo model)
        {
            string cachedPath = Path.Combine(_cacheDirectory, model.FileName);
            if (File.Exists(cachedPath))
            {
                File.Delete(cachedPath);
                return true;
            }
            return false;
        }
    }
}