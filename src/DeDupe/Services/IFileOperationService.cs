using DeDupe.Enums;
using DeDupe.Models.Results;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DeDupe.Services
{
    /// <summary>
    /// Handles file system operations (move, copy, delete).
    /// </summary>
    public interface IFileOperationService
    {
        /// <summary>
        /// Moves or copies files to single folder.
        /// </summary>
        Task<FileOperationResult> ExecuteAsync(IReadOnlyList<string> filePaths, string destinationFolder, FileOperationType operationType);

        /// <summary>
        /// Moves or copies files into group-named subfolders in root folder.
        /// </summary>
        Task<FileOperationResult> ExecuteGroupedAsync(IReadOnlyDictionary<string, List<string>> filesByGroupName, string rootFolder, FileOperationType operationType);

        /// <summary>
        /// Deletes files to Recycle Bin or permanently.
        /// </summary>
        Task<FileOperationResult> DeleteAsync(IReadOnlyList<string> filePaths, bool permanentDelete);
    }
}