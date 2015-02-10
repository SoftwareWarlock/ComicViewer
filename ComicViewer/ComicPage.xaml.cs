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
using SharpCompress.Common;


namespace ComicViewer
{
    public sealed partial class ComicPage : LayoutAwarePage
    {
        private FolderViewModel viewModel;
        private Size dimensions;
        private int pagesCount;
        private float zoomFactor = 1.0f;

        public ComicPage()
        {
            this.InitializeComponent();
            viewModel = new FolderViewModel();
            flipViewer.SelectionChanged += FlipViewSelectionChanged;
            Back.Click += Back_Click;
            Go.Click += Go_Click;
            Window.Current.SizeChanged += WindowSizeChanged;
            DataContext = viewModel;
            dimensions = new Size(Window.Current.Bounds.Width, Window.Current.Bounds.Height);
        }     

        private void WindowSizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            dimensions = e.Size;
            MarkedUp.AnalyticClient.SessionEvent("Window size changed");
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

                MarkedUp.AnalyticClient.SessionEvent("Comic opened by file");
                StorageFile sourceFile = null;
                StorageFolder destFolder = null;
                try
                {
                    sourceFile = (StorageFile)args.Files.ToList().ElementAt(0);
                    StorageFolder recentlyOpened = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("Recently Opened");
                    destFolder = await recentlyOpened.CreateFolderAsync(sourceFile.Name, CreationCollisionOption.ReplaceExisting);

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
            else if (navigationParameter is StorageFolder)
            {
                ProggressBarVisible(true);
                StorageFolder destFolder = (StorageFolder)navigationParameter;
                StorageFolder recentlyOpened = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("Recently Opened");
                StorageFolder testFolder = (StorageFolder)await recentlyOpened.TryGetItemAsync(destFolder.Name);
                if (testFolder == null)
                {
                    testFolder = await recentlyOpened.CreateFolderAsync(destFolder.Name);
                    IReadOnlyList<StorageFile> files = await destFolder.GetFilesAsync();
                    foreach(StorageFile file in files)
                    {
                        await file.CopyAsync(testFolder);
                    }
                }
                await viewModel.Initialize(destFolder);
                ProggressBarVisible(false);
            }
            else
            {
                Windows.Storage.Pickers.FileOpenPicker filePicker = new Windows.Storage.Pickers.FileOpenPicker();
                filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary; 
                filePicker.FileTypeFilter.Add(".cbz");
                filePicker.FileTypeFilter.Add(".cbr");
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
                        StorageFolder recentlyOpened = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("Recently Opened");
                        destFolder = await recentlyOpened.CreateFolderAsync(sourceFile.Name, CreationCollisionOption.ReplaceExisting);

                        try
                        {
                            if (sourceFile.FileType.Equals("cbz") || sourceFile.FileType.Equals(".cbz"))
                                await FolderZip.UnZipFile(sourceFile, destFolder);
                            else if (sourceFile.FileType.Equals("cbr") || sourceFile.FileType.Equals(".cbr"))
                                await FolderZip.UnRarFile(sourceFile, destFolder);
                        }
                        catch (InvalidFormatException exception)
                        {
                            ShowWarningAndClose("Error opening file:" + sourceFile.Name, "Please restart the app and try again");
                        }
                        await viewModel.Initialize(destFolder);
                        ProggressBarVisible(false);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    //ShowWarningAndClose("Cannont access files outside of Libraries", "You can copy it to pictures if you want to open it");
                }
                catch (Exception e)
                {
                    ShowWarningAndClose("An error has occured that has not been handled", "Error info: " + e.Message);
                }
            }


            pagesCount = viewModel.Pictures.Count;
            pageNumberLabel.Text = "(1/" + pagesCount + ")";
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
                ShowWarning("Please enter a valid number", "Text entered not a number!");
            }
            int count = flipViewer.Items.Count;
            if (pageNumber <= count)
            {
                flipViewer.SelectedIndex = pageNumber;
            }
            else
            {
                ShowWarning("Page number is out of range", "Number of pages is " + flipViewer.Items.Count);
            }
        }
        private void resetScroll(ScrollViewer scroll)
        {
            var pictureModel = FindFirstElementInVisualTree<Image>(scroll);
            if (pictureModel is Image)
            {
                scroll.ChangeView(0, 0, zoomFactor, true);
            }
        }
        private void FlipViewSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is FlipView && flipViewer.SelectedIndex!=0)
            {
                var flipViewItem = flipViewer.ContainerFromIndex(flipViewer.SelectedIndex);
                var scrollViewItem = FindFirstElementInVisualTree<ScrollViewer>(flipViewItem);

                if (scrollViewItem is ScrollViewer)
                {
                    scrollViewItem.Width = dimensions.Width;
                    scrollViewItem.Height = dimensions.Height;
                    scrollViewItem.ChangeView(0, 0, zoomFactor, true);
                }
            }
            pageNumberLabel.Text = "(" + (flipViewer.SelectedIndex + 1) + "/" + pagesCount + ")";
        }

        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            zoomFactor = ((ScrollViewer)sender).ZoomFactor;
        }
    }
}
