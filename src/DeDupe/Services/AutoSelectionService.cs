using DeDupe.Enums;
using DeDupe.Models.Analysis;
using System.Collections.Generic;
using System.Linq;

namespace DeDupe.Services
{
    /// <inheritdoc/>
    public class AutoSelectionService : IAutoSelectionService
    {
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
        }

        public void ApplyStrategyToAll(IEnumerable<SimilarityGroup> groups, SelectionStrategy strategy)
        {
            if (groups == null)
            {
                return;
            }

            foreach (SimilarityGroup group in groups)
            {
                ApplyStrategy(group, strategy);
            }
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
    }
}