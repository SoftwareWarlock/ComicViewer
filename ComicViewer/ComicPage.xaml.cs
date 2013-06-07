using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml;
using Windows.UI.Core;
using ComicViewer.Common;
using ComicViewer.Model;
using FileShare;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Storage;
using Windows.Foundation;
using System.Windows;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using WinRTXamlToolkit.Extensions;
using Windows.Graphics.Display;
using Windows.UI.ViewManagement;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace ComicViewer
{
    public sealed partial class ComicPage : LayoutAwarePage
    {
        private FolderViewModel viewModel;
        private Boolean pageChange = false;

        public ComicPage()
        {
            this.InitializeComponent();
            viewModel = new FolderViewModel();
            this.ManipulationDelta += ZoomManipulationDelta;
            flipViewer.SelectionChanged += FlipViewSelectionChanged;
            Back.Click += Back_Click;
            Go.Click += Go_Click;
            Window.Current.SizeChanged += WindowSizeChanged;
            DataContext = viewModel;
        }     

        private void WindowSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            // Obtain view state by explicitly querying for it
            ApplicationViewState myViewState = ApplicationView.Value;
            int count = flipViewer.Items.Count;
            for(int i = 0; i < count; i++)
            {
                var flipViewItem = flipViewer.ItemContainerGenerator.ContainerFromIndex((i));
                var scrollViewItem = FindFirstElementInVisualTree<ScrollViewer>(flipViewItem);
                if (scrollViewItem is ScrollViewer)
                {
                    ScrollViewer scroll = (ScrollViewer)scrollViewItem;
                    scroll.Height = e.Size.Height;
                    scroll.Width = e.Size.Width;
                    scroll.ZoomToFactor(0.75f);
                }
            }
        }
        private void ProggressBarVisible(bool visible)
        {
            ProgressRingLoad.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            ProgressRingLoad.IsActive = visible;
        }

        protected override async void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            if (pageState != null && pageState.ContainsKey("DataContext"))
            {
                viewModel = (FolderViewModel)pageState["DataContext"];
            }
            else if (navigationParameter is FileActivatedEventArgs)
            {
                ProggressBarVisible(true);
                FileActivatedEventArgs args = (FileActivatedEventArgs)navigationParameter;

                StorageFile sourceFile = null;
                StorageFolder destFolder = null;
                try
                {
                    sourceFile = (StorageFile)args.Files.ToList().ElementAt(0);
                    destFolder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync(sourceFile.Name, CreationCollisionOption.ReplaceExisting);

                    if (sourceFile.FileType.Equals("cbz") || sourceFile.FileType.Equals(".cbz"))
                        await FolderZip.UnZipFile(sourceFile, destFolder);
                    else if (sourceFile.FileType.Equals("cbr") || sourceFile.FileType.Equals(".cbr"))
                        await FolderZip.UnRarFile(sourceFile, destFolder);
                    await viewModel.Initialize(destFolder);
                    ProggressBarVisible(false);
                }
                catch (UnauthorizedAccessException e)
                {
                    ShowWarningAndClose("Cannont access files outside of Libraries", "You can copy it to documents if you want to open it");
                }
            }
            else if (navigationParameter is String)
            {
                ProggressBarVisible(true);
                String args = (String)navigationParameter;
                StorageFolder destFolder = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync(args);
                await viewModel.Initialize(destFolder);
                ProggressBarVisible(false);
            }
            else
            {
                Windows.Storage.Pickers.FileOpenPicker filePicker = new Windows.Storage.Pickers.FileOpenPicker();
                filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary; 
                filePicker.FileTypeFilter.Add("*");
                filePicker.CommitButtonText = "Open";

                StorageFile sourceFile = null;
                StorageFolder destFolder = null;
                try
                {
                    sourceFile = await filePicker.PickSingleFileAsync();
                    if (sourceFile == null)
                    {
                        this.Frame.GoBack();
                    }
                    else
                    {
                        ProggressBarVisible(true);
                        destFolder = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFolderAsync(sourceFile.Name, CreationCollisionOption.ReplaceExisting);

                        if (sourceFile.FileType.Equals("cbz") || sourceFile.FileType.Equals(".cbz"))
                            await FolderZip.UnZipFile(sourceFile, destFolder);
                        else if (sourceFile.FileType.Equals("cbr") || sourceFile.FileType.Equals(".cbr"))
                            await FolderZip.UnRarFile(sourceFile, destFolder);
                        await viewModel.Initialize(destFolder);
                        ProggressBarVisible(false);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    ShowWarningAndClose("Cannont access files outside of Libraries", "You can copy it to pictures if you want to open it");
                }
                catch (Exception e)
                {
                    ShowWarningAndClose("An error has occured that has not been handled", "Error info: " + e.Message);
                }
            }
            /*
            if (viewModel!=null && !viewModel.ContainsPictures)
            {
                try
                {
                    await ShowWarningAndClose("Could not find any images in file.", "CBZ/CBR has no pictures");
                }
                catch (Exception e)
                {

                }
            }
            */
        }

        private async Task ShowWarningAndClose(String error1, String error2)
        {
            var messageDialog = new MessageDialog(error2, error1);
            messageDialog.Commands.Add(new UICommand("Close", new UICommandInvokedHandler(this.CommandInvokedHandler)));

            await messageDialog.ShowAsync();
        }

        private async Task ShowWarning(String error1, String error2)
        {
            var messageDialog = new MessageDialog(error2, error1);
            messageDialog.Commands.Add(new UICommand("Back", null));

            await messageDialog.ShowAsync();
        }

        private void CommandInvokedHandler(IUICommand command)
        {
            this.Frame.GoBack();
        }

        protected override void SaveState(Dictionary<String, Object> pageState)
        {
            pageState["DataContext"] = viewModel;
        }

        private void ZoomManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (sender is ScrollViewer)
                ((ScrollViewer)flipViewer.ItemContainerGenerator.ContainerFromItem(sender)).ZoomToFactor(e.Delta.Scale);
        }
        private T FindFirstElementInVisualTree<T>(DependencyObject parentElement) where T : DependencyObject
        {
            if (parentElement != null)
            {
                var count = VisualTreeHelper.GetChildrenCount(parentElement);
                if (count == 0)
                    return null;

                for (int i = 0; i < count; i++)
                {
                    var child = VisualTreeHelper.GetChild(parentElement, i);

                    if (child != null && child is T)
                        return (T)child;
                    else
                    {
                        var result = FindFirstElementInVisualTree<T>(child);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }
            return null;
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
            {
                this.Frame.GoBack();
            }
            else
            {
                this.Frame.Navigate(typeof(ComicPage));
            }
        }
        private void Go_Click(object sender, RoutedEventArgs e)
        {
            int pageNumber = flipViewer.SelectedIndex;
            try
            {
                pageNumber = int.Parse(textBox.Text) - 1;
            }
            catch (System.FormatException)
            {
                ShowWarning("Please enter a valid number", "Number entered was an invalid format");
            }
            int count = flipViewer.Items.Count;
            if (pageNumber <= count)
            {
                pageChange = true;
                flipViewer.SelectedIndex = pageNumber;
            }
            else
            {
                ShowWarning("Page number is out of range", "Number of pages is " + flipViewer.Items.Count);
            }
        }
        private void FlipViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is FlipView && !pageChange)
            {
                FlipView item = (FlipView)sender;
                var flipViewItem = ((FlipView)sender).ItemContainerGenerator.ContainerFromIndex(((FlipView)sender).SelectedIndex);
                var scrollViewItem = FindFirstElementInVisualTree<ScrollViewer>(flipViewItem);
                if (scrollViewItem is ScrollViewer)
                {
                    ScrollViewer scroll = (ScrollViewer)scrollViewItem;
                    scroll.ScrollToHorizontalOffset(0);
                    scroll.ScrollToVerticalOffset(0);
                    scroll.ZoomToFactor(1.0f);
                    /*
                     * 
                     * FIX THIS LATER
                     * 
                     * FOR REAL, FIX IT
                     * 
                     * CAMERON, YOU HAVEN'T FIXED IT YET. GET YOUR SHIT TOGETHER.
                     * 
                    Storyboard storyboard = new Storyboard();

                    Duration duration = new Duration(TimeSpan.FromSeconds(2));
                    DoubleAnimation verticalOffsetAnimation = new DoubleAnimation { To = 0, Duration = duration };


                    Storyboard.SetTarget(verticalOffsetAnimation, scroll);
                    Storyboard.SetTargetName(verticalOffsetAnimation, scroll.Name);
                    String str = ScrollViewer.VerticalOffsetProperty.ToString();
                    //PropertyPath property = new PropertyPath(ScrollViewer.VerticalOffsetProperty);
                    //Storyboard.SetTargetProperty(verticalOffsetAnimation, property);

                    storyboard.Children.Add(verticalOffsetAnimation);

                    storyboard.Begin();
                     * */
                }
            }
            pageChange = false;
            pageNumberLabel.Text = "(" + (flipViewer.SelectedIndex + 1) + "/" + flipViewer.Items.Count + ")";
        }
    }
}
