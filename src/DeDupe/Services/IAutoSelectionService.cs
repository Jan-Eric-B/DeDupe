using DeDupe.Enums;
using DeDupe.Models.Analysis;
using System.Collections.Generic;

namespace DeDupe.Services
{
    /// <summary>
    /// Applies auto-selection strategies to similarity groups, except best item per group based on the chosen strategy.
    /// </summary>
    public interface IAutoSelectionService
    {
        /// <summary>
        /// Selects all items in the group except the one best matching the given strategy.
        /// </summary>
        void ApplyStrategy(SimilarityGroup group, SelectionStrategy strategy);

        void ApplyStrategyToAll(IEnumerable<SimilarityGroup> groups, SelectionStrategy strategy);
    }
}