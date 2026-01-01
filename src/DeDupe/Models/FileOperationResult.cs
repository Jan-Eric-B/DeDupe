using System.Collections.Generic;

namespace DeDupe.Models
{
    /// <summary>
    /// Result of file operation (move or copy).
    /// </summary>
    public class FileOperationResult(int successCount, int failedCount, List<string> successfulPaths, List<string> failedPaths)
    {
        public int SuccessCount { get; } = successCount;
        public int FailedCount { get; } = failedCount;
        public List<string> SuccessfulPaths { get; } = successfulPaths;
        public List<string> FailedPaths { get; } = failedPaths;

        public bool HasFailures => FailedCount > 0;
        public bool AllSucceeded => FailedCount == 0 && SuccessCount > 0;
        public int TotalCount => SuccessCount + FailedCount;
    }
}