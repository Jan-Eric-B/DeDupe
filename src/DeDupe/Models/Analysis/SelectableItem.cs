using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Models.Media;
using DeDupe.Models.Results;
using System;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Wrapper for AnalysisItem which adds selection state for UI binding.
    /// </summary>
    public partial class SelectableItem(AnalysisItem item) : ObservableObject
    {
        private bool _isSelected;

        public event EventHandler? SelectionChanged;

        public AnalysisItem Item { get; } = item ?? throw new ArgumentNullException(nameof(item));

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public string FilePath => Item.Source.Metadata.FilePath;

        public MediaMetadata Metadata => Item.Source.Metadata;
    }
}