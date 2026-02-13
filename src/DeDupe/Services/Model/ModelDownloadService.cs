using DeDupe.Constants;
using DeDupe.Models.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DeDupe.Services.Model
{
    /// <inheritdoc/>
    public record ModelDownloadProgress(double Percentage, long BytesDownloaded, long TotalBytes, string StatusText);

    public partial class ModelDownloadService : IModelDownloadService
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<ModelDownloadService> _logger;

        private readonly int _maxRetries = _retryDelays.Length;

        private static readonly TimeSpan[] _retryDelays =
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
                { "User-Agent", AppInformation.UserAgent }
            }
        };

        private readonly string _cacheDirectory;

        public ModelDownloadService(ISettingsService settingsService, ILogger<ModelDownloadService> logger)
        {
            _logger = logger;
            _settingsService = settingsService;
            _cacheDirectory = _settingsService.ModelFolderPath;
        }

        /// <inheritdoc/>
        public string? GetLocalModelPath(BundledModelInfo model)
        {
            string cachedPath = Path.Combine(_cacheDirectory, model.FileName);
            return File.Exists(cachedPath) ? cachedPath : null;
        }

        /// <inheritdoc/>
        public bool IsModelAvailable(BundledModelInfo model)
        {
            return GetLocalModelPath(model) != null;
        }

        /// <inheritdoc/>
        public async Task DownloadModelAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (model.DownloadUrl is null)
            {
                throw new InvalidOperationException($"Model '{model.DisplayName}' has no download URL.");
            }

            Directory.CreateDirectory(_cacheDirectory);

            string targetPath = Path.Combine(_cacheDirectory, model.FileName);
            string tempPath = targetPath + ".download";

            LogModelDownloadStarting(model.DisplayName, model.ExpectedFileSize / 1024.0 / 1024.0);

            try
            {
                // Stream response instead of buffering in memory
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

                // Remove stale target before moving.
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                // Verify integrity
                if (model.ExpectedSha256 is not null)
                {
                    LogHashVerificationStarting(model.DisplayName);

                    string actualHash = await ComputeSha256Async(tempPath, cancellationToken);
                    if (!string.Equals(actualHash, model.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(tempPath);
                        LogHashVerificationFailed(model.DisplayName, model.ExpectedSha256, actualHash);
                        throw new InvalidOperationException($"Hash mismatch for '{model.DisplayName}'. Expected: {model.ExpectedSha256}, Got: {actualHash}");
                    }

                    LogHashVerificationPassed(model.DisplayName);
                }

                File.Move(tempPath, targetPath);

                progress?.Report(new ModelDownloadProgress(1.0, bytesRead, totalBytes, $"{model.DisplayName} downloaded"));

                LogModelDownloadCompleted(model.DisplayName, bytesRead / 1024.0 / 1024.0);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogModelDownloadFailed(model.DisplayName, ex);

                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        LogTempFileCleanupFailed(tempPath, cleanupEx);
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Computes SHA-256 incrementally.
        /// </summary>
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

        /// <inheritdoc/>
        public async Task<string> EnsureModelAvailableAsync(BundledModelInfo model, IProgress<ModelDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            string? existingPath = GetLocalModelPath(model);
            if (existingPath != null)
            {
                LogModelCacheHit(model.DisplayName, existingPath);
                return existingPath;
            }

            if (model.DownloadUrl is null)
            {
                throw new InvalidOperationException($"Model '{model.DisplayName}' has no download URL and is not cached locally.");
            }

            LogModelCacheMiss(model.DisplayName);

            Exception? lastException = null;

            for (int attempt = 0; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        TimeSpan delay = _retryDelays[Math.Min(attempt - 1, _retryDelays.Length - 1)];

                        LogModelDownloadRetrying(model.DisplayName, attempt, _maxRetries, delay.TotalSeconds);

                        progress?.Report(new ModelDownloadProgress(0, 0, model.ExpectedFileSize, $"Retry {attempt}/{_maxRetries} — waiting {delay.TotalSeconds:F0}s..."));

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
                    LogModelDownloadAttemptFailed(model.DisplayName, attempt + 1, _maxRetries + 1, ex.Message);
                }
            }

            throw new InvalidOperationException($"Failed to download '{model.DisplayName}' after {_maxRetries + 1} attempts.", lastException);
        }

        /// <inheritdoc/>
        public bool DeleteCachedModel(BundledModelInfo model)
        {
            string cachedPath = Path.Combine(_cacheDirectory, model.FileName);
            if (File.Exists(cachedPath))
            {
                File.Delete(cachedPath);
                LogCachedModelDeleted(model.DisplayName, cachedPath);
                return true;
            }
            return false;
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "Model download starting for '{ModelName}' ({ExpectedSizeMb:F1} MB)")]
        private partial void LogModelDownloadStarting(string modelName, double expectedSizeMb);

        [LoggerMessage(Level = LogLevel.Information, Message = "Model download completed for '{ModelName}' ({DownloadedSizeMb:F1} MB)")]
        private partial void LogModelDownloadCompleted(string modelName, double downloadedSizeMb);

        [LoggerMessage(Level = LogLevel.Error, Message = "Model download failed for '{ModelName}'")]
        private partial void LogModelDownloadFailed(string modelName, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Model download retrying for '{ModelName}', attempt {Attempt}/{MaxRetries} after {DelaySeconds:F0}s")]
        private partial void LogModelDownloadRetrying(string modelName, int attempt, int maxRetries, double delaySeconds);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Model download attempt {Attempt}/{TotalAttempts} failed for '{ModelName}': {ErrorMessage}")]
        private partial void LogModelDownloadAttemptFailed(string modelName, int attempt, int totalAttempts, string errorMessage);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Model cache hit for '{ModelName}' at {CachedPath}")]
        private partial void LogModelCacheHit(string modelName, string cachedPath);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Model cache miss for '{ModelName}', download required")]
        private partial void LogModelCacheMiss(string modelName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Hash verification starting for '{ModelName}'")]
        private partial void LogHashVerificationStarting(string modelName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Hash verification passed for '{ModelName}'")]
        private partial void LogHashVerificationPassed(string modelName);

        [LoggerMessage(Level = LogLevel.Error, Message = "Hash verification failed for '{ModelName}', expected '{ExpectedHash}' but got '{ActualHash}'")]
        private partial void LogHashVerificationFailed(string modelName, string expectedHash, string actualHash);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Temp file cleanup failed for '{TempPath}'")]
        private partial void LogTempFileCleanupFailed(string tempPath, Exception ex);

        [LoggerMessage(Level = LogLevel.Information, Message = "Cached model deleted for '{ModelName}' at {CachedPath}")]
        private partial void LogCachedModelDeleted(string modelName, string cachedPath);

        #endregion Logging
    }
}