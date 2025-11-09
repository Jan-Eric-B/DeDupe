using CommunityToolkit.Mvvm.ComponentModel;

namespace DeDupe.Models
{
    /// <summary>
    /// Step in the wizard stepper
    /// </summary>
    public partial class StepItem : ObservableObject
    {
        private int _stepNumber;
        private string _title = string.Empty;
        private string _description = string.Empty;
        private bool _isCompleted;
        private bool _isCurrent;
        private bool _isEnabled = true;

        /// <summary>
        /// Number
        /// </summary>
        public int StepNumber
        {
            get => _stepNumber;
            set => SetProperty(ref _stepNumber, value);
        }

        /// <summary>
        /// Title
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Description
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Has been completed
        /// </summary>
        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        /// <summary>
        /// Is current step
        /// </summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }

        /// <summary>
        /// Can be clicked / navigated to
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }
}