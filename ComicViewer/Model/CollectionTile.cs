using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicViewer.Model
{
    public class CollectionTile
    {
        public ObservableCollection<ComicTile> Tiles { get; set; }
        public String Title { get; set; }

        public CollectionTile(String title, List<ComicTile> tiles)
        {
            Title = title;
            Tiles = new ObservableCollection<ComicTile>(tiles);
        }
    }
}
