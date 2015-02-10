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
        public ObservableCollection<ComicTile> ButtonTiles { get; set; }
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
            ButtonTiles = new ObservableCollection<ComicTile>();
            CollectionViews = new ObservableCollection<CollectionView>();
            ButtonTiles.Add(new ComicTile("Open a new comic", "", null));
            ButtonTiles.Add(new ComicTile("Open a new collection", "", null));
        }

        private void ProggressBarVisible(bool visible)
        {
            LoadingBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        private void LoadingTextVisible(bool visible)
        {
            LoadingText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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
            ProggressBarVisible(true);
            await CreateComicTiles();
            await CreateCollectionTiles();
            foreach(CollectionTile tile in CollectionTiles)
            {
                CollectionViews.Add(new CollectionView(tile));
            }
            defaultViewModel["ButtonTiles"] = ButtonTiles;
            defaultViewModel["ComicTiles"] = ComicTiles;
            defaultViewModel["CollectionViews"] = CollectionViews;
            ProggressBarVisible(false);
        }

        public void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ComicTile item = (ComicTile)e.ClickedItem;
            GridView gridView = sender as GridView;
            MarkedUp.AnalyticClient.SessionEvent("Comic opened", new Dictionary<String, String>() { { "FeatureType", gridView.Name == "collectionsGridView" ? "collections" : "previouslyOpened" } });
            this.Frame.Navigate(typeof(ComicPage), item.Folder);
        }

       private async void buttonGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ComicTile clicked = (ComicTile)e.ClickedItem;
            if (clicked.Title == "Open a new comic")
            {
                MarkedUp.AnalyticClient.SessionEvent("Opened a new comic");
                this.Frame.Navigate(typeof(ComicPage));
            }
            else if (clicked.Title == "Open a new collection")
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
                    ProggressBarVisible(true);
                    LoadingTextVisible(true);
                    List<StorageFile> files = await RecursivelySearchForFiles(chosenFolder);
                    StorageFolder collectionFolder = (StorageFolder)await collections.TryGetItemAsync(chosenFolder.Name);
                    if (collectionFolder == null)
                    {
                        collectionFolder = await collections.CreateFolderAsync(chosenFolder.Name);
                    }
                    else
                    {
                        ShowWarning("Collection already exist!", "Adding new, nonexisting comics");
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
                                ShowWarning("Error opening file:" + sourceFile.Name, "Please restart the app and try again");
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
                    ProggressBarVisible(false);
                    LoadingTextVisible(false);
                }
            }
        }
       private async Task ShowWarning(String error1, String error2)
       {
           var messageDialog = new MessageDialog(error2, error1);
           messageDialog.Commands.Add(new UICommand("Okay", null));

           await messageDialog.ShowAsync();
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
    }
}
