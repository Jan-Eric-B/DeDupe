using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DeDupe.Localization
{
    internal sealed partial class LocalizedElementTracker
    {
        private readonly List<TrackedElement> trackedElements = [];

        private readonly Lock sync = new();

        public int Count => trackedElements.Count;

        public record TrackedElement(Type Type, WeakReference<DependencyObject> WeakReference);

        public void Add(DependencyObject dependencyObject)
        {
            lock (sync)
            {
                WeakReference<DependencyObject> reference = new(dependencyObject);
                TrackedElement trackedElement = new(dependencyObject.GetType(), reference);
                trackedElements.Add(trackedElement);
            }
        }

        public IReadOnlyCollection<DependencyObject> GetDependencyObjects()
        {
            lock (sync)
            {
                List<DependencyObject> dependencyObjects = [];

                for (int i = trackedElements.Count - 1; i >= 0; i--)
                {
                    TrackedElement targetElement = trackedElements[i];

                    if (!targetElement.WeakReference.TryGetTarget(out DependencyObject? aliveObject))
                    {
                        trackedElements.RemoveAt(i);
                        continue;
                    }

                    dependencyObjects.Add(aliveObject);
                }

                return dependencyObjects;
            }
        }
    }
}