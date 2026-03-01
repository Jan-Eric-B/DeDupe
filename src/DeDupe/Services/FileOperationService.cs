using DeDupe.Enums;
using DeDupe.Models.Results;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public sealed partial class FileOperationService(ILogger<FileOperationService> logger) : IFileOperationService
    {
        private readonly ILogger<FileOperationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <inheritdoc />
        public async Task<FileOperationResult> ExecuteAsync(IReadOnlyList<string> filePaths, string destinationFolder, FileOperationType operationType)
        {
            if (filePaths.Count == 0 || string.IsNullOrEmpty(destinationFolder))
            {
                return FileOperationResult.Empty;
            }

            LogFileOperationStarting(operationType, filePaths.Count);

            try
            {
                FileOperationResult result = await Task.Run(() =>
                {
                    EnsureDirectoryExists(destinationFolder);

                    int successCount = 0;
                    int failCount = 0;
                    List<string> successfulPaths = [];
                    List<string> failedPaths = [];

                    foreach (string sourcePath in filePaths)
                    {
                        (bool success, string? error) = ProcessSingleFile(sourcePath, destinationFolder, operationType);

                        if (success)
                        {
                            successCount++;
                            successfulPaths.Add(sourcePath);
                        }
                        else
                        {
                            failCount++;
                            failedPaths.Add(sourcePath);
                            LogFileOperationSkipped(operationType, sourcePath, error);
                        }
                    }

                    return new FileOperationResult(successCount, failCount, successfulPaths, failedPaths);
                });

                LogFileOperationCompleted(operationType, result.SuccessCount, result.FailedCount);
                return result;
            }
            catch (Exception ex)
            {
                LogFileOperationAborted(ex, operationType);
                return new FileOperationResult(0, filePaths.Count, [], [.. filePaths]);
            }
        }

        /// <inheritdoc />
        public async Task<FileOperationResult> ExecuteGroupedAsync(IReadOnlyDictionary<string, List<string>> filesByGroupName, string rootFolder, FileOperationType operationType)
        {
            if (filesByGroupName.Count == 0 || string.IsNullOrEmpty(rootFolder))
            {
                return FileOperationResult.Empty;
            }

            int totalFiles = filesByGroupName.Values.Sum(v => v.Count);
            LogFileOperationStarting(operationType, totalFiles);

            try
            {
                FileOperationResult result = await Task.Run(() =>
                {
                    int totalSuccess = 0;
                    int totalFailed = 0;
                    List<string> successfulPaths = [];
                    List<string> failedPaths = [];

                    foreach (KeyValuePair<string, List<string>> groupEntry in filesByGroupName)
                    {
                        string? groupName = FolderNameValidationService.Sanitize(groupEntry.Key);

                        if (string.IsNullOrEmpty(groupName))
                        {
                            LogInvalidGroupName(groupEntry.Key);
                            totalFailed += groupEntry.Value.Count;
                            failedPaths.AddRange(groupEntry.Value);
                            continue;
                        }

                        string groupFolder = Path.Combine(rootFolder, groupName);

                        try
                        {
                            EnsureDirectoryExists(groupFolder);
                        }
                        catch (Exception ex)
                        {
                            LogGroupFolderCreationFailed(ex, groupFolder);
                            totalFailed += groupEntry.Value.Count;
                            failedPaths.AddRange(groupEntry.Value);
                            continue;
                        }

                        foreach (string sourcePath in groupEntry.Value)
                        {
                            (bool success, string? error) = ProcessSingleFile(sourcePath, groupFolder, operationType);

                            if (success)
                            {
                                totalSuccess++;
                                successfulPaths.Add(sourcePath);
                            }
                            else
                            {
                                totalFailed++;
                                failedPaths.Add(sourcePath);
                                LogFileOperationSkipped(operationType, sourcePath, error);
                            }
                        }
                    }

                    return new FileOperationResult(totalSuccess, totalFailed, successfulPaths, failedPaths);
                });

                LogFileOperationCompleted(operationType, result.SuccessCount, result.FailedCount);
                return result;
            }
            catch (Exception ex)
            {
                LogFileOperationAborted(ex, operationType);
                return new FileOperationResult(0, totalFiles, [], [.. filesByGroupName.Values.SelectMany(v => v)]);
            }
        }

        private static (bool Success, string? Error) ProcessSingleFile(string sourcePath, string destinationFolder, FileOperationType operationType)
        {
            try
            {
                if (!File.Exists(sourcePath))
                {
                    return (false, "Source file not found");
                }

                string fileName = Path.GetFileName(sourcePath);
                string destinationPath = GetUniqueFilePath(Path.Combine(destinationFolder, fileName));

                if (operationType == FileOperationType.Move)
                {
                    File.Move(sourcePath, destinationPath);
                }
                else
                {
                    File.Copy(sourcePath, destinationPath, overwrite: false);
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExt} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        /// <inheritdoc />
        public async Task<FileOperationResult> DeleteAsync(IReadOnlyList<string> filePaths, bool permanentDelete)
        {
            if (filePaths.Count == 0)
            {
                return FileOperationResult.Empty;
            }

            LogDeletionStarting(permanentDelete, filePaths.Count);

            try
            {
                FileOperationResult result = await Task.Run(() =>
                {
                    int success = 0;
                    int fail = 0;
                    List<string> deleted = [];
                    List<string> failed = [];

                    foreach (string filePath in filePaths)
                    {
                        try
                        {
                            if (!File.Exists(filePath))
                            {
                                LogFileNotFoundForDeletion(filePath);
                                fail++;
                                failed.Add(filePath);
                                continue;
                            }

                            if (permanentDelete)
                            {
                                File.Delete(filePath);
                            }
                            else
                            {
                                FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                            }

                            success++;
                            deleted.Add(filePath);
                        }
                        catch (Exception ex)
                        {
                            LogFileDeletionFailed(ex, filePath);
                            fail++;
                            failed.Add(filePath);
                        }
                    }

                    return new FileOperationResult(success, fail, deleted, failed);
                });

                LogDeletionCompleted(permanentDelete, result.SuccessCount, result.FailedCount);
                return result;
            }
            catch (Exception ex)
            {
                LogDeletionAborted(ex);
                return new FileOperationResult(0, filePaths.Count, [], [.. filePaths]);
            }
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Information, Message = "{Operation} starting for {FileCount} files")]
        private partial void LogFileOperationStarting(FileOperationType operation, int fileCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "{Operation} completed: {SuccessCount} succeeded, {FailedCount} failed")]
        private partial void LogFileOperationCompleted(FileOperationType operation, int successCount, int failedCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "{Operation} operation aborted")]
        private partial void LogFileOperationAborted(Exception ex, FileOperationType operation);

        [LoggerMessage(Level = LogLevel.Warning, Message = "File {Operation} skipped for {FilePath}: {Reason}")]
        private partial void LogFileOperationSkipped(FileOperationType operation, string filePath, string? reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Group name '{OriginalName}' produced invalid folder name, group skipped")]
        private partial void LogInvalidGroupName(string originalName);

        [LoggerMessage(Level = LogLevel.Error, Message = "Group folder creation failed for {FolderPath}")]
        private partial void LogGroupFolderCreationFailed(Exception ex, string folderPath);

        [LoggerMessage(Level = LogLevel.Information, Message = "Deletion starting (Permanent: {Permanent}) for {FileCount} files")]
        private partial void LogDeletionStarting(bool permanent, int fileCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Deletion completed (Permanent: {Permanent}): {SuccessCount} succeeded, {FailedCount} failed")]
        private partial void LogDeletionCompleted(bool permanent, int successCount, int failedCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "Deletion operation aborted")]
        private partial void LogDeletionAborted(Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "File not found for deletion: {FilePath}")]
        private partial void LogFileNotFoundForDeletion(string filePath);

        [LoggerMessage(Level = LogLevel.Error, Message = "File deletion failed for {FilePath}")]
        private partial void LogFileDeletionFailed(Exception ex, string filePath);

        #endregion Logging
    }
}