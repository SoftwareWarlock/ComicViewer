using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicViewer.Model
{
    public class ComicTile
    {
        public String Image { get; set; }
        public String Title { get; set; }
        public StorageFolder Folder { get; set; }

        public ComicTile(String title, String imagePath, StorageFolder folder)
        {
            Title = title;
            Image = imagePath;
            Folder = folder;
        }
    }
}
