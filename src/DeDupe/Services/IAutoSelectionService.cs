using DeDupe.Enums;
using DeDupe.Models.Analysis;
using System.Collections.Generic;

namespace DeDupe.Services
{
    /// <summary>
    /// Service for applying auto selection to similarity groups.
    /// </summary>
    public interface IAutoSelectionService
    {
        void ApplyStrategy(SimilarityGroup group, SelectionStrategy strategy);

        void ApplyStrategyToAll(IEnumerable<SimilarityGroup> groups, SelectionStrategy strategy);
    }
}