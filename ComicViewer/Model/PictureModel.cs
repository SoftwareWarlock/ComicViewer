using ComicViewer.Common;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicViewer.Model
{
    internal class PictureModel : BindableBase
    {
        private StorageFile _file;

        public PictureModel(StorageFile file)
        {
            _file = file;
        }

        public string UniqueId
        {
            get { return _file.Path; }
        }

        private BitmapImage _image;
        public BitmapImage Image
        {
            get { return _image; }
        }

        public async Task Initialize()
        {
            var fileStream = await _file.OpenAsync(Windows.Storage.FileAccessMode.Read);
            BitmapImage bmp = new BitmapImage();
            bmp.SetSource(fileStream);
            _image = bmp;
            OnPropertyChanged("Image");
        }
    }
}