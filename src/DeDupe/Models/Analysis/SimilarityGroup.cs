using CommunityToolkit.Mvvm.ComponentModel;
using DeDupe.Models.Results;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DeDupe.Models.Analysis
{
    /// <summary>
    /// Group of similar media items (images/frames) identified by clustering.
    /// </summary>
    public partial class SimilarityGroup : ObservableObject
    {
        private bool _isBatchUpdating;

        private bool _isInternalUpdate;

        public int Id { get; }

        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>
        /// Analysis items in this group (immutable after construction).
        /// </summary>
        public IReadOnlyList<AnalysisItem> Items { get; }

        [ObservableProperty]
        public partial double AverageSimilarity { get; set; }

        public int Count => SelectableItems.Count;

        /// <summary>
        /// Group contains potential duplicates (more than 1 item).
        /// </summary>
        public bool IsDuplicateGroup => SelectableItems.Count > 1;

        /// <summary>
        /// Create Group with multiple items.
        /// </summary>
        public SimilarityGroup(int id, IEnumerable<AnalysisItem> items, string? name = null)
        {
            ArgumentNullException.ThrowIfNull(items);

            Id = id;
            Items = items.ToList().AsReadOnly();
            Name = name ?? $"Group {id + 1}";

            SelectableItems = new ObservableCollection<SelectableItem>(Items.Select(CreateSelectableItem));
        }

        /// <summary>
        /// Create Group with single item.
        /// </summary>
        public SimilarityGroup(int id, AnalysisItem item, string? name = null) : this(id, item != null ? [item] : throw new ArgumentNullException(nameof(item)), name)
        {
        }

        /// <summary>
        /// Get 4 image paths for display.
        /// </summary>
        public List<string> GetImageThumbnailPaths()
        {
            return [.. SelectableItems.Take(4).Select(item => item.FilePath)];
        }

        #region Selection

        /// <summary>
        /// Selectable wrappers for UI binding.
        /// </summary>
        public ObservableCollection<SelectableItem> SelectableItems { get; }

        private bool? _isSelected = false;

        /// <summary>
        /// Group selection state: true = all selected, false = none, null = partial.
        /// </summary>
        public bool? IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

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
                {
                    DeselectAll();
                }
                else
                    SelectAll();
            }
        }

        public int SelectedCount => SelectableItems.Count(x => x.IsSelected);

        public bool AllSelected => SelectableItems.Count > 0 && SelectableItems.All(x => x.IsSelected);

        public bool NoneSelected => SelectableItems.All(x => !x.IsSelected);

        public bool IsAnySelected => SelectableItems.Any(x => x.IsSelected);

        public event EventHandler? GroupSelectionChanged;

        public void SelectAll() => SetAllItemsSelection(true);

        public void DeselectAll() => SetAllItemsSelection(false);

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

        public IEnumerable<SelectableItem> GetSelectedItems()
        {
            return SelectableItems.Where(x => x.IsSelected);
        }

        public List<string> GetSelectedFilePaths()
        {
            return [.. SelectableItems.Where(x => x.IsSelected).Select(x => x.FilePath)];
        }

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
            OnPropertyChanged(nameof(IsAnySelected));
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

        #endregion Selection

        #region Item Removal

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

        public int RemoveItems(IEnumerable<SelectableItem> items)
        {
            if (items == null)
            {
                return 0;
            }

            // Create list to avoid multiple enumeration
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

        #endregion Item Removal

        #region Cleanup

        public void Cleanup()
        {
            // Unsubscribe from all selection events.
            foreach (SelectableItem item in SelectableItems)
            {
                item.SelectionChanged -= OnIndividualItemSelectionChanged;
            }
        }

        #endregion Cleanup
    }
}