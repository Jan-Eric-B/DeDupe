using CommunityToolkit.WinUI.Controls;
using DeDupe.Constants;
using DeDupe.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace DeDupe.Views.Settings
{
    public sealed partial class ModelSettingsPage : Page
    {
        public ModelSettingsViewModel ViewModel { get; }

        public ModelSettingsPage()
        {
            InitializeComponent();
            ViewModel = App.Current.GetService<ModelSettingsViewModel>();
            DataContext = ViewModel;
        }

        private void ModelSourceSegmented_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is Segmented segmented && segmented.SelectedItem is SegmentedItem item)
            {
                bool isBundled = item.Tag?.ToString() == "Bundled";
                ViewModel.UseBundledModel = isBundled;
            }
        }

        private void ModelFileDropGrid_DragOver(object sender, DragEventArgs e)
        {
            if (ViewModel.UseBundledModel)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Use this model";
                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void ModelFileDropGrid_Drop(object sender, DragEventArgs e)
        {
            if (ViewModel.UseBundledModel)
            {
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                try
                {
                    IReadOnlyList<IStorageItem>? items = await e.DataView.GetStorageItemsAsync();

                    if (items.Count > 0)
                    {
                        IStorageItem? item = items[0];

                        if (item is StorageFile file)
                        {
                            string extension = file.FileType.ToLowerInvariant();

                            if (SupportedFileExtensions.IsSupportedModelFile(extension))
                            {
                                ViewModel.CustomModelFilePath = file.Path;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error handling file drop: {ex.Message}");
                }
            }
        }

        private void ModelFileTextBlock_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            ViewModel.OpenModelLocationCommand.Execute(null);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            ViewModel.OnNavigatedTo();

            // Sync UI with ViewModel state
            SyncUIWithViewModel();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.OnNavigatedFrom();
        }

        private void SyncUIWithViewModel()
        {
            ModelSourceSegmented.SelectedIndex = ViewModel.UseBundledModel ? 0 : 1;
        }
    }
}