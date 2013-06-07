using ComicViewer.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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

// The Grouped Items Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234231

namespace ComicViewer
{
    /// <summary>
    /// A page that displays a grouped collection of items.
    /// </summary>
    public sealed partial class GroupedItemsPage : ComicViewer.Common.LayoutAwarePage
    {
        private bool newComicOpened = false;

        public GroupedItemsPage()
        {
            this.InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            IReadOnlyList<StorageFolder> list = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFoldersAsync();
            SampleDataSource.group2.Items.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                IReadOnlyList<StorageFile> list2 = await list[i].GetFilesAsync();
                if (list2.Count > 0)
                {
                    String imagePath = list2[0].Path;
                    SampleDataSource.group2.Items.Add(new SampleDataItem("Group-" + (i + 2) + "-Item-" + (i + 2),
                        list[i].Name,
                        "",
                        imagePath,
                        "",
                        "",
                        SampleDataSource.group2
                    ));
                }
            }
            base.OnNavigatedTo(e);
        }

        /// <summary>
        /// Populates the page with content passed during navigation.  Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="navigationParameter">The parameter value passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested.
        /// </param>
        /// <param name="pageState">A dictionary of state preserved by this page during an earlier
        /// session.  This will be null the first time a page is visited.</param>
        protected override void LoadState(Object navigationParameter, Dictionary<String, Object> pageState)
        {
            // TODO: Create an appropriate data model for your problem domain to replace the sample data
                var sampleDataGroups = SampleDataSource.GetGroups("AllGroups");
                this.DefaultViewModel["Groups"] = sampleDataGroups;
        }

        /// <summary>
        /// Invoked when an item within a group is clicked.
        /// </summary>
        /// <param name="sender">The GridView (or ListView when the application is snapped)
        /// displaying the item clicked.</param>
        /// <param name="e">Event data that describes the item clicked.</param>
        async void ItemView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Navigate to the appropriate destination page, configuring the new page
            // by passing required information as a navigation parameter
            SampleDataItem item = (SampleDataItem)e.ClickedItem;
            var itemId = item.UniqueId;
            if (itemId.Equals("Group-1-Item-1"))
            {
                this.Frame.Navigate(typeof(ComicPage));
                newComicOpened = true;
            }
            else
            {
                this.Frame.Navigate(typeof(ComicPage), item.Title);
            }
        }
    }
}
