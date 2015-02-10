using ComicViewer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace ComicViewer.Common
{
    public sealed partial class CollectionView : UserControl
    {
        public CollectionTile Tile { get; set; }

        private ObservableDictionary defaultViewModel = new ObservableDictionary();
        public ObservableDictionary DefaultViewModel
        {
            get { return this.defaultViewModel; }
        }


        public CollectionView(CollectionTile tile)
        {
            Tile = tile;
            defaultViewModel["Tile"] = Tile;
            this.InitializeComponent();
        }
        public void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ComicTile item = (ComicTile)e.ClickedItem;
            ((Frame)Window.Current.Content).Navigate(typeof(ComicPage), item.Folder);
        }
    }
}
