using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DeDupe.Collections
{
    /// <summary>
    /// An ObservableCollection that supports bulk operations with a single Reset notification,
    /// preventing UI thread flooding when updating large collections bound to ItemsControls.
    /// </summary>
    public class BulkObservableCollection<T> : ObservableCollection<T>
    {
        private bool _suppressNotifications;

        /// <summary>
        /// Replaces all items in the collection with the provided items,
        /// firing a single CollectionChanged Reset notification.
        /// </summary>
        public void ReplaceAll(IEnumerable<T> items)
        {
            _suppressNotifications = true;

            try
            {
                Items.Clear();

                foreach (T item in items)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotifications = false;
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotifications)
            {
                base.OnCollectionChanged(e);
            }
        }
    }
}
