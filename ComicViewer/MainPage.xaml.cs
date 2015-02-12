using ComicViewer.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ComicViewer.Common;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using FileShare;
using Windows.UI.Popups;
using SharpCompress.Common;
using Windows.ApplicationModel.DataTransfer;
using WinRTXamlToolkit.Extensions;
using Windows.System;

// The Grouped Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234231

namespace ComicViewer
{
    /// <summary>
    /// A page that displays a grouped collection of items.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public ObservableCollection<ComicTile> ComicTiles { get; set; }
        public ObservableCollection<CollectionView> CollectionViews { get; set; }
        public ObservableCollection<CollectionTile> CollectionTiles { get; set; }
        private ObservableDictionary defaultViewModel = new ObservableDictionary();
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }

        public MainPage()
        {
            this.InitializeComponent();
            ComicTiles = new ObservableCollection<ComicTile>();
            CollectionTiles = new ObservableCollection<CollectionTile>();
            CollectionViews = new ObservableCollection<CollectionView>();
        }

        private void LoadingGridVisible(bool visible)
        {
            LoadingGrid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }


        private async Task CreateComicTiles()
        {
            StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFolder recentlyOpened = (StorageFolder)await localFolder.TryGetItemAsync("Recently Opened");
            if (recentlyOpened == null)
            {
                await localFolder.CreateFolderAsync("Recently Opened");
                recentlyOpened = await localFolder.GetFolderAsync("Recently Opened");
            }
            IReadOnlyList<StorageFolder> list = await recentlyOpened.GetFoldersAsync();
            for (int i = 0; i < list.Count; i++)
            {
                IReadOnlyList<StorageFile> list2 = await list[i].GetFilesAsync();
                if (list2.Count > 0)
                {
                    String imagePath = list2[0].Path;
                    ComicTiles.Add(new ComicTile(list[i].Name, imagePath, list[i]));
                }
            }
        }

        private async Task CreateCollectionTiles()
        {
            CollectionTiles.Clear();
            StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFolder collectionsFolder = (StorageFolder)await localFolder.TryGetItemAsync("Collections");
            if (collectionsFolder == null)
            {
                collectionsFolder = await localFolder.CreateFolderAsync("Collections");
            }

            IReadOnlyList<StorageFolder> collections = await collectionsFolder.GetFoldersAsync();
            foreach(StorageFolder collection in collections)
            {
                List<ComicTile> tiles = new List<ComicTile>();
                IReadOnlyList<StorageFolder> comics = await collection.GetFoldersAsync();
                foreach(StorageFolder comic in comics)
                {
                    IReadOnlyList<StorageFile> files = await comic.GetFilesAsync();
                    if (files.Count != 0)
                    {
                        foreach(StorageFile file in files)
                        {
                            if (file.FileType == ".jpg" || file.FileType == ".JPG" || file.FileType == ".png" || file.FileType == ".PNG")
                            {
                                tiles.Add(new ComicTile(comic.Name, file.Path, comic));
                                break;
                            }
                        }
                    }

                }
                CollectionTiles.Add(new CollectionTile(collection.Name, tiles));
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            LoadingRing.Visibility = Visibility.Visible;
            await CreateComicTiles();
            await CreateCollectionTiles();
            foreach(CollectionTile tile in CollectionTiles)
            {
                CollectionViews.Add(new CollectionView(tile));
            }
            defaultViewModel["ComicTiles"] = ComicTiles;
            defaultViewModel["CollectionViews"] = CollectionViews;
            LoadingRing.Visibility = Visibility.Collapsed;
        }

        public void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ComicTile item = (ComicTile)e.ClickedItem;
            GridView gridView = sender as GridView;
            MarkedUp.AnalyticClient.SessionEvent("Comic opened", new Dictionary<String, String>() { { "FeatureType", gridView.Name == "collectionsGridView" ? "collections" : "previouslyOpened" } });
            this.Frame.Navigate(typeof(ComicPage), item.Folder);
        }

       private async Task ShowWarning(String error1, String error2)
       {
           var messageDialog = new MessageDialog(error2, error1);
           messageDialog.Commands.Add(new UICommand("Okay", null));

           await messageDialog.ShowAsync();
       }

       private async void OpenComicCollection(StorageFolder chosenFolder, StorageFolder collections)
       {
           LoadingGridVisible(true);
           List<StorageFile> files = await RecursivelySearchForFiles(chosenFolder);
           StorageFolder collectionFolder = (StorageFolder)await collections.TryGetItemAsync(chosenFolder.Name);
           if (collectionFolder == null)
           {
               collectionFolder = await collections.CreateFolderAsync(chosenFolder.Name);
           }
           else
           {
               ShowWarning("Collection already exist!", "Adding new comics");
           }

           foreach (StorageFile sourceFile in files)
           {
               StorageFolder destFolder = (StorageFolder)await collectionFolder.TryGetItemAsync(sourceFile.Name);
               if (destFolder == null)
               {
                   destFolder = await collectionFolder.CreateFolderAsync(sourceFile.Name);
                   try
                   {
                       DefaultViewModel["LoadingFile"] = sourceFile.Name;
                       if (sourceFile.FileType.Equals("cbz") || sourceFile.FileType.Equals(".cbz"))
                           await FolderZip.UnZipFile(sourceFile, destFolder);
                       else if (sourceFile.FileType.Equals("cbr") || sourceFile.FileType.Equals(".cbr"))
                           await FolderZip.UnRarFile(sourceFile, destFolder);
                   }
                   catch (InvalidFormatException exception)
                   {
                       ShowWarning("Error opening file:" + sourceFile.Name, "Please try again");
                   }
               }
               LoadingBar.Value += (1.0 / files.Count()) * 100;
           }

           await CreateCollectionTiles();
           CollectionViews.Clear();
           foreach (CollectionTile tile in CollectionTiles)
           {
               CollectionViews.Add(new CollectionView(tile));
           }
           defaultViewModel["ComicTiles"] = ComicTiles;
           defaultViewModel["CollectionViews"] = CollectionViews;
           LoadingGridVisible(false);
       }
       private async Task<List<StorageFile>> RecursivelySearchForFiles(StorageFolder destFolder)
       {
           List<StorageFile> filesList = new List<StorageFile>();
           IReadOnlyList<StorageFile> files = await destFolder.GetFilesAsync();
           IReadOnlyList<StorageFolder> folders = await destFolder.GetFoldersAsync();
           foreach (StorageFile file in files)
           {
               if (file.FileType == ".cbz" || file.FileType == ".cbr" || file.FileType == "cbz" || file.FileType == "cbr")
                    filesList.Add(file);
           }
           foreach(StorageFolder folder in folders)
           {
               List<StorageFile> tempFiles = await RecursivelySearchForFiles(folder);
               foreach (StorageFile file in tempFiles)
                   if (file.FileType == ".cbz" || file.FileType == ".cbr" || file.FileType == "cbz" || file.FileType == "cbr")
                        filesList.Add(file);
           }
           return filesList;
       }

       private void ComicsLocationButton_Click(object sender, RoutedEventArgs e)
       {
           StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
           DataPackage package = new DataPackage();
           package.RequestedOperation = DataPackageOperation.Copy;
           package.SetText(localFolder.Path.ToString());
           Clipboard.SetContent(package);
       }

       private void OpenComicsButton_Click(object sender, RoutedEventArgs e)
       {
           MarkedUp.AnalyticClient.SessionEvent("Opened a new comic");
           this.Frame.Navigate(typeof(ComicPage));
       }

       private async void OpenCollectionButton_Click(object sender, RoutedEventArgs e)
       {
           MarkedUp.AnalyticClient.SessionEvent("Opened a collection");
           StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
           StorageFolder collections = await localFolder.GetFolderAsync("Collections");

           Windows.Storage.Pickers.FolderPicker folderPicker = new Windows.Storage.Pickers.FolderPicker();
           folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
           folderPicker.FileTypeFilter.Add("*");
           folderPicker.CommitButtonText = "Open";

           StorageFolder chosenFolder = await folderPicker.PickSingleFolderAsync();
           if (chosenFolder != null)
           {
               OpenComicCollection(chosenFolder, collections);
           }
       }

       private void collectionsGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
       {
           GridView collectionGridView = VisualTreeHelperExtensions.GetFirstDescendantOfType<GridView>(CollectionsHubSection);
           GridView recentlyOpenedGridView = VisualTreeHelperExtensions.GetFirstDescendantOfType<GridView>(RecentlyOpenedHubSection);
           BottomAppBar.IsOpen = collectionGridView.SelectedItems.Count > 0 || recentlyOpenedGridView.SelectedItems.Count > 0;
       }

       private async void DeleteAppBarButton_Click(object sender, RoutedEventArgs e)
       {
           LoadingRing.Visibility = Visibility.Visible;
           GridView collectionGridView = VisualTreeHelperExtensions.GetFirstDescendantOfType<GridView>(CollectionsHubSection);

           foreach (CollectionView collectionView in collectionGridView.SelectedItems)
           {
               CollectionTile collectionTile = collectionView.Tile;
               CollectionTiles.Remove(collectionTile);
               CollectionViews.Remove(collectionView);
               StorageFolder localFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
               StorageFolder collectionsFolder = (StorageFolder)await localFolder.TryGetItemAsync("Collections");
               if(null != collectionsFolder)
               {
                   StorageFolder collectionFolder = await collectionsFolder.TryGetItemAsync(collectionTile.Title) as StorageFolder;
                   await collectionFolder.DeleteAsync();
               }
           }

           GridView recentlyOpenedGridView = VisualTreeHelperExtensions.GetFirstDescendantOfType<GridView>(RecentlyOpenedHubSection);

           foreach (ComicTile comicTile in recentlyOpenedGridView.SelectedItems)
           {
               ComicTiles.Remove(comicTile);
               await comicTile.Folder.DeleteAsync();
           }
           LoadingRing.Visibility = Visibility.Collapsed;
       }

       private void ClearAppBarButton_Click(object sender, RoutedEventArgs e)
       {
           GridView gridView = VisualTreeHelperExtensions.GetFirstDescendantOfType<GridView>(CollectionsHubSection);
           gridView.SelectedItem = null;

           GridView recentlyOpenedGridView = VisualTreeHelperExtensions.GetFirstDescendantOfType<GridView>(RecentlyOpenedHubSection);
           recentlyOpenedGridView.SelectedItem = null;
       }

       protected override void OnTapped(TappedRoutedEventArgs e)
       {
           base.OnTapped(e);
           BottomAppBar.IsOpen = false;
       }

       protected override void OnKeyUp(KeyRoutedEventArgs e)
       {
           base.OnKeyUp(e);
           if (e.Key == VirtualKey.Escape)
               BottomAppBar.IsOpen = false;
       }
    }
}
