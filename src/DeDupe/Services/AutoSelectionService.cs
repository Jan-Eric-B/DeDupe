using DeDupe.Enums;
using DeDupe.Models.Analysis;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public partial class AutoSelectionService(ILogger<AutoSelectionService> logger) : IAutoSelectionService
    {
        private readonly ILogger<AutoSelectionService> _logger = logger;

        /// <inheritdoc/>
        public void ApplyStrategy(SimilarityGroup group, SelectionStrategy strategy)
        {
            if (group == null || group.SelectableItems.Count == 0)
            {
                return;
            }

            switch (strategy)
            {
                case SelectionStrategy.KeepHighestResolution:
                    ApplyKeepHighestResolution(group);
                    break;

                case SelectionStrategy.KeepLowestResolution:
                    ApplyKeepLowestResolution(group);
                    break;

                case SelectionStrategy.KeepNewest:
                    ApplyKeepNewest(group);
                    break;

                case SelectionStrategy.KeepOldest:
                    ApplyKeepOldest(group);
                    break;

                case SelectionStrategy.KeepLargestFileSize:
                    ApplyKeepLargestFileSize(group);
                    break;

                case SelectionStrategy.KeepSmallestFileSize:
                    ApplyKeepSmallestFileSize(group);
                    break;

                case SelectionStrategy.KeepAll:
                    group.DeselectAll();
                    break;

                case SelectionStrategy.KeepNone:
                    group.SelectAll();
                    break;
            }

            LogStrategyAppliedToGroup(strategy.ToString(), group.SelectableItems.Count);
        }

        public void ApplyStrategyToAll(IEnumerable<SimilarityGroup> groups, SelectionStrategy strategy)
        {
            if (groups == null)
            {
                return;
            }

            int groupCount = 0;
            foreach (SimilarityGroup group in groups)
            {
                ApplyStrategy(group, strategy);
                groupCount++;
            }

            LogStrategyAppliedToAllGroups(strategy.ToString(), groupCount);
        }

        private static void ApplyKeepHighestResolution(SimilarityGroup group)
        {
            SelectableItem? bestItem = group.SelectableItems
                .OrderByDescending(item => item.Metadata.PixelCount)
                .ThenByDescending(item => item.Metadata.FileSize)
                .FirstOrDefault();

            SelectAllExcept(group, bestItem);
        }

        private static void ApplyKeepLowestResolution(SimilarityGroup group)
        {
            SelectableItem? bestItem = group.SelectableItems
                .OrderBy(item => item.Metadata.PixelCount)
                .ThenBy(item => item.Metadata.FileSize)
                .FirstOrDefault();

            SelectAllExcept(group, bestItem);
        }

        private static void ApplyKeepNewest(SimilarityGroup group)
        {
            SelectableItem? bestItem = group.SelectableItems
                .OrderByDescending(item => item.Metadata.CreatedDate)
                .ThenByDescending(item => item.Metadata.LastModifiedDate)
                .FirstOrDefault();

            SelectAllExcept(group, bestItem);
        }

        private static void ApplyKeepOldest(SimilarityGroup group)
        {
            SelectableItem? bestItem = group.SelectableItems
                .OrderBy(item => item.Metadata.CreatedDate)
                .ThenByDescending(item => item.Metadata.LastModifiedDate)
                .FirstOrDefault();

            SelectAllExcept(group, bestItem);
        }

        private static void ApplyKeepLargestFileSize(SimilarityGroup group)
        {
            SelectableItem? bestItem = group.SelectableItems
                .OrderByDescending(item => item.Metadata.FileSize)
                .ThenByDescending(item => item.Metadata.PixelCount)
                .FirstOrDefault();

            SelectAllExcept(group, bestItem);
        }

        private static void ApplyKeepSmallestFileSize(SimilarityGroup group)
        {
            SelectableItem? bestItem = group.SelectableItems
                .OrderBy(item => item.Metadata.FileSize)
                .ThenByDescending(item => item.Metadata.PixelCount)
                .FirstOrDefault();

            SelectAllExcept(group, bestItem);
        }

        private static void SelectAllExcept(SimilarityGroup group, SelectableItem? itemToKeep)
        {
            foreach (SelectableItem item in group.SelectableItems)
            {
                item.IsSelected = item != itemToKeep;
            }
        }

        #region Logging

        [LoggerMessage(Level = LogLevel.Debug, Message = "Selection strategy {StrategyName} applied to group with {ItemCount} item(s)")]
        private partial void LogStrategyAppliedToGroup(string strategyName, int itemCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Selection strategy {StrategyName} applied to {GroupCount} group(s)")]
        private partial void LogStrategyAppliedToAllGroups(string strategyName, int groupCount);

        #endregion Logging
    }
}