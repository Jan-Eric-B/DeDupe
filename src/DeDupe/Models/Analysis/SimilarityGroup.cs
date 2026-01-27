using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// A group of similar media items (images/frames) identified by clustering.
    /// </summary>
    public partial class SimilarityGroup : ObservableObject
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
        /// Whether at least one item is selected.
        /// </summary>
        public bool IsAnySelected => SelectableItems.Any(x => x.IsSelected);

        /// <summary>
        /// Average similarity score within this group.
        /// </summary>
        public double AverageSimilarity { get; set; } = 0.0;

        /// <summary>
        /// Number of items in the group.
        /// </summary>
        public int Count => SelectableItems.Count;

        /// <summary>
        /// Whether this group contains potential duplicates (more than 1 item).
        /// </summary>
        public bool IsDuplicateGroup => SelectableItems.Count > 1;

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

            SelectableItems = new ObservableCollection<SelectableItem>(Items.Select(CreateSelectableItem));
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
                if (_isSelected != newState)
                {
                    _isSelected = newState;
                    OnPropertyChanged(nameof(IsSelected));
                }

                GroupSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                _isInternalUpdate = false;
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AllSelected));
            OnPropertyChanged(nameof(NoneSelected));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(IsDuplicateGroup));
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
            {
                DeselectAll();
            }
            else
            {
                SelectAll();
            }
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

        #region Item Removal Methods

        /// <summary>
        /// Remove selectable item from group.
        /// </summary>
        public bool RemoveItem(SelectableItem item)
        {
            if (item == null || !SelectableItems.Contains(item))
            {
                return false;
            }

            // Unsubscribe from events before removing
            item.SelectionChanged -= OnIndividualItemSelectionChanged;

            bool removed = SelectableItems.Remove(item);

            if (removed)
            {
                RecalculateGroupSelectionState();
            }

            return removed;
        }

        /// <summary>
        /// Remove multiple items from group.
        /// </summary>
        public int RemoveItems(IEnumerable<SelectableItem> items)
        {
            if (items == null)
            {
                return 0;
            }

            // Create a list to avoid multiple enumeration
            List<SelectableItem> itemList = [.. items];

            if (itemList.Count == 0)
            {
                return 0;
            }

            _isBatchUpdating = true;
            int removedCount = 0;

            try
            {
                foreach (SelectableItem item in itemList)
                {
                    if (SelectableItems.Contains(item))
                    {
                        // Unsubscribe from events before removing
                        item.SelectionChanged -= OnIndividualItemSelectionChanged;
                        SelectableItems.Remove(item);
                        removedCount++;
                    }
                }
            }
            finally
            {
                _isBatchUpdating = false;
            }

            if (removedCount > 0)
            {
                RecalculateGroupSelectionState();
            }

            return removedCount;
        }

        /// <summary>
        /// Remove items by file paths.
        /// </summary>
        public int RemoveItemsByPath(IEnumerable<string> filePaths)
        {
            if (filePaths == null)
            {
                return 0;
            }

            HashSet<string> pathSet = [.. filePaths];

            List<SelectableItem> itemsToRemove = [.. SelectableItems.Where(item => pathSet.Contains(item.FilePath))];

            return RemoveItems(itemsToRemove);
        }

        #endregion Item Removal Methods

        #region Path Helper Methods

        /// <summary>
        /// Get original file paths of all items.
        /// </summary>
        public List<string> GetOriginalFilePaths()
        {
            return [.. SelectableItems.Select(item => item.FilePath)];
        }

        /// <summary>
        /// Get up to 4 image paths for display.
        /// </summary>
        public List<string> GetImageThumbnailPaths()
        {
            return [.. SelectableItems.Take(4).Select(item => item.FilePath)];
        }

        /// <summary>
        /// Get the first item in the group.
        /// </summary>
        public SelectableItem? GetFirstItem()
        {
            return SelectableItems.Count > 0 ? SelectableItems[0] : null;
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
    }
}