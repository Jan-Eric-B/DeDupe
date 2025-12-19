using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.Models.Ui
{
    /// <summary>
    /// Represents a step in the wizard stepper control.
    /// </summary>
    public partial class StepItem : ObservableObject
    {
        /// <summary>
        /// The step number (1-based).
        /// </summary>
        [ObservableProperty]
        public partial int StepNumber { get; set; }

        /// <summary>
        /// Display title for the step.
        /// </summary>
        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        /// <summary>
        /// Description or subtitle for the step.
        /// </summary>
        [ObservableProperty]
        public partial string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether this step has been completed.
        /// </summary>
        [ObservableProperty]
        public partial bool IsCompleted { get; set; }

        /// <summary>
        /// Whether this is the currently active step.
        /// </summary>
        [ObservableProperty]
        public partial bool IsCurrent { get; set; }

        /// <summary>
        /// Whether this step can be navigated to.
        /// </summary>
        [ObservableProperty]
        public partial bool IsEnabled { get; set; } = true;
    }
}