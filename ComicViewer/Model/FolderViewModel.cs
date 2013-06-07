using ComicViewer.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.Storage.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;


namespace ComicViewer.Model
{
    internal class FolderViewModel : BindableBase
    {
        private ObservableCollection<PictureModel> _pictures = new ObservableCollection<PictureModel>();
        public ObservableCollection<PictureModel> Pictures
        {
            get { return _pictures; }
        }

        private async Task SetPictures(IEnumerable<StorageFile> pictures)
        {
            _pictures.Clear();
            foreach (var picture in pictures.Select(f => new PictureModel(f)))
            {
                await picture.Initialize();
                _pictures.Add(picture);
            }
        }

        public bool ContainsPictures
        {
            get { return Pictures != null && Pictures.Any(); }
        }

        public async Task Initialize(StorageFolder destFolder)
        {
            await SetPictures(await destFolder.GetFilesAsync());
        }
    }
}