using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// A group of similar media items (images/frames) identified by clustering.
    /// </summary>
    public partial class SimilarityGroup : INotifyPropertyChanged
    {
        #region Fields

        private string _name;
        private bool? _isSelected = false;
        private bool _isBatchUpdating;
        private bool _isInternalUpdate;

        #endregion Fields

        #region Events

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? GroupSelectionChanged;

        #endregion Events

        #region Properties

        /// <summary>
        /// Unique identifier for this group.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Editable display name for the group.
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Analysis items in this group (immutable after construction).
        /// </summary>
        public IReadOnlyList<AnalysisItem> Items { get; }

        /// <summary>
        /// Selectable wrappers for UI binding.
        /// </summary>
        public ObservableCollection<SelectableItem> SelectableItems { get; }

        /// <summary>
        /// Group selection state: true = all selected, false = none, null = partial.
        /// </summary>
        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                // Internal update from RecalculateGroupSelectionState
                if (_isInternalUpdate)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    GroupSelectionChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }

                // UI update - toggle selection
                if (AllSelected)
                    DeselectAll();
                else
                    SelectAll();
            }
        }

        /// <summary>
        /// Number of currently selected items.
        /// </summary>
        public int SelectedCount => SelectableItems.Count(x => x.IsSelected);

        /// <summary>
        /// Whether all items are selected.
        /// </summary>
        public bool AllSelected => SelectableItems.Count > 0 && SelectableItems.All(x => x.IsSelected);

        /// <summary>
        /// Whether no items are selected.
        /// </summary>
        public bool NoneSelected => SelectableItems.All(x => !x.IsSelected);

        /// <summary>
        /// Average similarity score within this group.
        /// </summary>
        public double AverageSimilarity { get; set; } = 0.0;

        /// <summary>
        /// Number of items in the group.
        /// </summary>
        public int Count => Items.Count;

        /// <summary>
        /// Whether this group contains potential duplicates (more than 1 item).
        /// </summary>
        public bool IsDuplicateGroup => Items.Count > 1;

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Create a group with multiple items.
        /// </summary>
        public SimilarityGroup(int id, IEnumerable<AnalysisItem> items, string? name = null)
        {
            ArgumentNullException.ThrowIfNull(items);

            Id = id;
            Items = items.ToList().AsReadOnly();
            _name = name ?? $"Group {id + 1}";

            SelectableItems = new ObservableCollection<SelectableItem>(
                Items.Select(CreateSelectableItem));
        }

        /// <summary>
        /// Create a group with a single item.
        /// </summary>
        public SimilarityGroup(int id, AnalysisItem item, string? name = null)
            : this(id, item != null ? [item] : throw new ArgumentNullException(nameof(item)), name)
        {
        }

        #endregion Constructors

        #region Private Methods

        private SelectableItem CreateSelectableItem(AnalysisItem item)
        {
            SelectableItem selectableItem = new(item);
            selectableItem.SelectionChanged += OnIndividualItemSelectionChanged;
            return selectableItem;
        }

        private void OnIndividualItemSelectionChanged(object? sender, EventArgs e)
        {
            if (_isBatchUpdating)
                return;

            RecalculateGroupSelectionState();
        }

        private void RecalculateGroupSelectionState()
        {
            bool? newState = NoneSelected ? false : (AllSelected ? true : null);

            _isInternalUpdate = true;
            try
            {
                IsSelected = newState;
            }
            finally
            {
                _isInternalUpdate = false;
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AllSelected));
            OnPropertyChanged(nameof(NoneSelected));
        }

        private void SetAllItemsSelection(bool selected)
        {
            _isBatchUpdating = true;

            try
            {
                foreach (SelectableItem item in SelectableItems)
                {
                    item.IsSelected = selected;
                }
            }
            finally
            {
                _isBatchUpdating = false;
            }

            RecalculateGroupSelectionState();
        }

        #endregion Private Methods

        #region Public Selection Methods

        /// <summary>
        /// Select all items in the group.
        /// </summary>
        public void SelectAll() => SetAllItemsSelection(true);

        /// <summary>
        /// Deselect all items in the group.
        /// </summary>
        public void DeselectAll() => SetAllItemsSelection(false);

        /// <summary>
        /// Toggle selection of all items.
        /// </summary>
        public void ToggleSelection()
        {
            if (AllSelected)
                DeselectAll();
            else
                SelectAll();
        }

        /// <summary>
        /// Get all currently selected items.
        /// </summary>
        public IEnumerable<SelectableItem> GetSelectedItems()
        {
            return SelectableItems.Where(x => x.IsSelected);
        }

        /// <summary>
        /// Get file paths of all selected items.
        /// </summary>
        public List<string> GetSelectedFilePaths()
        {
            return [.. SelectableItems.Where(x => x.IsSelected).Select(x => x.FilePath)];
        }

        #endregion Public Selection Methods

        #region Path Helper Methods

        /// <summary>
        /// Get original file paths of all items.
        /// </summary>
        public List<string> GetOriginalFilePaths()
        {
            return [.. Items.Select(item => item.Source.Metadata.FilePath)];
        }

        /// <summary>
        /// Get up to 4 image paths for preview thumbnails.
        /// </summary>
        public List<string> GetPreviewImagePaths()
        {
            return [.. Items.Take(4).Select(item => item.Source.Metadata.FilePath)];
        }

        /// <summary>
        /// Get the first item in the group.
        /// </summary>
        public AnalysisItem? GetFirstItem()
        {
            return Items.Count > 0 ? Items[0] : null;
        }

        #endregion Path Helper Methods

        #region Cleanup

        /// <summary>
        /// Unsubscribe from all selection events.
        /// </summary>
        public void Cleanup()
        {
            foreach (SelectableItem item in SelectableItems)
            {
                item.SelectionChanged -= OnIndividualItemSelectionChanged;
            }
        }

        #endregion Cleanup

        #region Object Overrides

        public override string ToString()
        {
            return $"SimilarityGroup {Id} ({Name}): {Count} item{(Count == 1 ? "" : "s")} (Avg: {AverageSimilarity:P1})";
        }

        #endregion Object Overrides

        #region INotifyPropertyChanged

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged
    }
}