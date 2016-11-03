using MimeKit;
using mshtml;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using WikiLeaks.Abstract;
using WikiLeaks.Extensions;
using WikiLeaks.Managers;
using WikiLeaks.Models;

namespace WikiLeaks
{

    [Export(typeof(MainWindow))]
    public partial class MainWindow : IPartImportsSatisfiedNotification {

        private AppManager _application = null;
        private FilterManager _filterManager = null;
        private DocumentManager _documentManager = null;

        private Thread _searchThread = null;

        public MainWindow() {
            InitializeComponent();

            _application = new AppManager(this, "");

            if (_application.Settings == null)
                return;

            this.txtLeakStartIdSetting.Text = _application.Settings.SearchStartId.ToString();
            this.txtLeakEndIdSetting.Text = _application.Settings.SearchEndId.ToString();
            this.txtLeakEndId.Text = this.txtLeakEndIdSetting.Text;
            this.txtLeakStartId.Text = this.txtLeakStartIdSetting.Text;
            this.txtBaseUrlSetting.Text = _application.Settings.BaseUrl;
            this.txtCachFolder.Text = _application.Settings.CacheFolder;
            this.txtSearchResultsFolder.Text = _application.Settings.ResultsFolder;
            this.txtFilterFolder.Text = _application.Settings.FilterFolder;

            _filterManager = new FilterManager(this, _application.Settings.FilterFolder);
            LoadFilters();
            _documentManager = new DocumentManager(this, _application, _filterManager);

            rdoFilterView.IsChecked = true;

            //todo attachments
            rdoAttachmentsView.Focusable = false;
            rdoAttachmentsView.IsHitTestVisible = false;
            rdoAttachmentsView.Background = new SolidColorBrush(Colors.LightGray);
            
        }

        [Import]
        public IMainWindowViewModel ViewModel
        {
            get { return DataContext as IMainWindowViewModel; }
            set { DataContext = value; }
        }

        [Import]
        public ICssStyle CssStyle { get; set; }

        public void OnImportsSatisfied() {
            ViewModel.Initialize();
        }

        void Attachment_Click(object sender, RoutedEventArgs e) {
            var button = sender as Button;
            var attachment = button?.DataContext as Attachment;

            if (attachment == null)
                return;

            var tempFileName = Path.Combine(Path.GetTempPath(), attachment.FileName);
            File.WriteAllBytes(tempFileName, attachment.Data);

            System.Diagnostics.Process.Start(tempFileName);
        }

        void WebBrowser_OnLoadCompleted(object sender, NavigationEventArgs e){
            var webBrowser = sender as WebBrowser;

            var document = webBrowser?.Document as HTMLDocument;

            if (document == null)
                return;

            CssStyle.Update(document);
        }

        #region App Settings Tab Events/Code
        private void btnSaveAppSettings_Click(object sender, RoutedEventArgs e)
        {
            _application.Settings.BaseUrl = this.txtBaseUrlSetting.Text.Trim();
            int i = 0;
            if (int.TryParse(this.txtLeakStartIdSetting.Text, out i))
                _application.Settings.SearchStartId = i;

            if (int.TryParse(this.txtLeakEndIdSetting.Text, out i))
                _application.Settings.SearchEndId = i;

            string tmp  = this.txtCachFolder.Text.Trim(); 
            try
            {
                if (!Directory.Exists(tmp))
                    Directory.CreateDirectory(tmp);
            }catch(Exception ex)
            {
                MessageBox.Show("Could not create cache folder. " + ex.Message);
                return;
            }
            _application.Settings.CacheFolder = tmp;

          
            tmp = this.txtSearchResultsFolder.Text.Trim();
            try
            {
                if (!Directory.Exists(tmp))
                    Directory.CreateDirectory(tmp);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create results folder. " + ex.Message);
                return;
            }
            _application.Settings.ResultsFolder = tmp;

            tmp = this.txtFilterFolder.Text.Trim();
            try
            {
                if (!Directory.Exists(tmp))
                    Directory.CreateDirectory(tmp);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create filter folder. " + ex.Message);
                return;
            }

            tmp = this.txtAttachemtsFolder.Text.Trim();
            try
            {
                if (!Directory.Exists(tmp))
                    Directory.CreateDirectory(tmp);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create attachments folder. " + ex.Message);
                return;
            }

            _application.Settings.AttachemtsFolder = tmp;
            _application.SaveSettings("");
        }
        #endregion

        #region Filter Tab Events/Code
        public void AutoSizeColumns()
        {
            GridView gv = lstFilters.View as GridView;
            if (gv != null)
            {
                foreach (var c in gv.Columns)
                {
                    // Code below was found in GridViewColumnHeader.OnGripperDoubleClicked() event handler (using Reflector)
                    // i.e. it is the same code that is executed when the gripper is double clicked
                    if (double.IsNaN(c.Width))
                    {
                        c.Width = c.ActualWidth;
                    }
                    c.Width = double.NaN;
                }
            }
        }


        private void btnAddFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter f = new Filter();

            f.Name = txtFilterName.Text.Trim();
            int tmp = -1;
            if (int.TryParse(txtFilterPriority.Text.Trim(), out tmp))
                f.Priority = tmp;

            f.SearchTokens = txtFilterSearchTokens.Text.Trim();
            f.HighlightColor = txtFilterHighlightColor.Text.Trim();
            try
            {
               System.Drawing.Color myColor = ColorTranslator.FromHtml(f.HighlightColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("You must supply a valie html color. " + ex.Message);
                return;
            }

            if (_filterManager.AddFilter(f))
            {
                LoadFilters();
            }
        }

        private void btnSaveFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter f = new Filter();
            int priority = 0;
            int.TryParse(txtFilterPriority.Text.Trim(), out priority);
            f.Priority = priority;

            f.Name = txtFilterName.Text.Trim();

            if (string.IsNullOrWhiteSpace(f.Name))
            {
                MessageBox.Show("You must supply a name.");
                txtFilterName.Focus();
                return;
            }
            f.SearchTokens = txtFilterSearchTokens.Text.Trim();
            f.HighlightColor = txtFilterHighlightColor.Text.Trim();

            try
            {
                System.Drawing.Color myColor = ColorTranslator.FromHtml(f.HighlightColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("You must supply a valid html color. " + ex.Message);
                return;
            }

            Filter selected = (Filter)lstFilters.SelectedItem;

            string originalName = f.Name;
            if (selected != null && selected.Name != f.Name)
                originalName = selected.Name;

            if (_filterManager.SaveFilter(originalName, f))
                LoadFilters();
        }

        private void btnDeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter f = (Filter)lstFilters.SelectedItem;
            if (f == null)
            {
                return;
            }

            if (_filterManager.DeleteFilter(f.Name))
            {
                LoadFilters();
            }
        }

        private void lstFilters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            txtFilterName.Text = "";
            txtFilterPriority.Text = "";
            txtFilterSearchTokens.Text = "";
            txtFilterHighlightColor.Text = "";

            Filter f = (Filter)lstFilters.SelectedItem;
            if (f == null)
            {
                return;
            }

            txtFilterName.Text = f.Name;
            txtFilterPriority.Text = f.Priority.ToString();
            txtFilterSearchTokens.Text = f.SearchTokens;
            txtFilterHighlightColor.Text = f.HighlightColor;
        }

        #endregion

        #region Search Results Events/Code

        private string _viewState = "FilterView"; //default to this.

    
        private void rdoFilterView_Click(object sender, RoutedEventArgs e)
        {
            _viewState = "FilterView";

            if (_filterManager.Filters == null)
                return;

            List<Filter> sortedFilters = _filterManager.Filters.OrderBy(ob => ob.Priority).ToList();
            tvwSearchResults.Items.Clear();

            //For now make the root nodes based on the filter name.
            //If we loaded by leak id it could take a while.
            //Later we can do an xref to find what document id's match
            //multiple filters. 
            //
            foreach (Filter f in sortedFilters)
            {
                TreeViewItem node = new TreeViewItem();
                node.Header = f.Name;
                node.Tag = f.Name; //note: this could be used to hold the object.
                tvwSearchResults.Items.Add(node);
            }
        }

        private void rdoResultsView_Click(object sender, RoutedEventArgs e)
        {
            _viewState = "ResultsView";

            tvwSearchResults.Items.Clear();

            string directory = _application.Settings.ResultsFolder;
            int batchSize = 500;
            tvwSearchResults.Items.Clear();
            LoadTreeWithFiles(directory, "*.html", batchSize);
        }

        private void rdoCacheView_Click(object sender, RoutedEventArgs e)
        {
            _viewState = "CacheView";
            string directory = _application.Settings.CacheFolder;
            int batchSize = 500;
            tvwSearchResults.Items.Clear();
            LoadTreeWithFiles(directory, "*.eml", batchSize);
        }

        private void LoadTreeWithFiles(string directory, string fileType, int batchSize)
        {
            System.Threading.Thread t1 = new System.Threading.Thread
            (delegate ()
            {
                string[] fileEntries = Directory.GetFiles(directory, fileType, System.IO.SearchOption.AllDirectories);

                List<int> fileNumbers = new List<int>();

                foreach (string pathToFile in fileEntries)
                {
                    string file = Path.GetFileNameWithoutExtension(pathToFile);
                    try
                    {
                        fileNumbers.Add(int.Parse(file));
                    }
                    catch { }

                }
                fileNumbers.Sort();

                var batches = fileNumbers.Batch(batchSize);

                //root nodes
                foreach (var batch in batches)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        string label = batch.ElementAt(0).ToString() + "-" + batch.ElementAt(batch.Count() - 1).ToString();
                        TreeViewItem rootNode = new TreeViewItem();
                        rootNode.Header = label;
                        rootNode.Tag = rootNode.Header;
                        tvwSearchResults.Items.Add(rootNode);

                        //TODO move this to node expanded event
                        foreach (var fileNum in batch)
                        {
                            TreeViewItem node = new TreeViewItem();
                            node.Header = fileNum.ToString();
                            node.Tag = node.Header;
                            rootNode.Items.Add(node);
                        }
                    }));
                }
            });
            t1.Start();
        }

        private void rdoAttachmentsView_Click(object sender, RoutedEventArgs e)
        {
            _viewState = "AttachmentsView";
            tvwSearchResults.Items.Clear();
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            int startId = -1;
            int.TryParse(txtLeakStartId.Text.Trim(), out startId);

            int endId = -1;
            int.TryParse(txtLeakEndId.Text.Trim(), out endId);

            if (startId <= 0 || endId < startId)
            {
                MessageBox.Show("You must provide valid start and end ids!");
                return;
            }

            //default to the filter view because it adds to the view as the docs are
            //processed. 
            rdoFilterView.IsChecked = true;
           
            LoadFilters();
            // SearchDocuments(startId, endId); //sync call for debuging
            _searchThread = new Thread(() => SearchDocuments(startId, endId));
            _searchThread.Start();
        }

        private void btnCancelSearch_Click(object sender, RoutedEventArgs e)
        {
            _documentManager.CancelSearch = true;

            _searchThread.Join(TimeSpan.FromSeconds(15));
        }

        private void SearchDocuments(int startId, int endId)
        {
            if (startId > endId || startId < 0)
            {
                UpdateUi("INVALID IDs", "messagebox.show");
                return;
            }
            ToggleRadioButtonsForSearch(false);

            _documentManager.ClearSearchResults();

            SearchStatus emailStatus = new SearchStatus();
            emailStatus.ControlName = "prgEmailProgress";
            emailStatus.Start = startId;
            emailStatus.End = endId;
            emailStatus.Reset = true;
            emailStatus.Current = startId;
            UpdateUi(emailStatus, "progressbar.update");
            emailStatus.Reset = false;

            for(int i = startId; i <= endId; i++)
            {
                _documentManager.SearchDocument(i);
                emailStatus.Current = i;
                UpdateUi(emailStatus, "progressbar.update");
            }
     
            ToggleRadioButtonsForSearch(true);

            MessageBox.Show("Document search completed.");
        }

        private void ToggleRadioButtonsForSearch(bool enableState)
        {
            System.Windows.Media.Color c = Colors.LightGray;

            if(enableState)
                c = Colors.White;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                rdoResultsView.Focusable           = enableState;
                rdoResultsView.IsHitTestVisible    = enableState;
                rdoCacheView.Focusable             = enableState;
                rdoCacheView.IsHitTestVisible = enableState;

                //todo attachments
                rdoAttachmentsView.Focusable        = false;
                rdoAttachmentsView.IsHitTestVisible = false;
                rdoAttachmentsView.Background = new SolidColorBrush(Colors.LightGray);
            }));

        }

        private void tvwSearchResults_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeViewItem tvi = null;
            lstAttachments.ItemsSource = null;

            if (e.NewValue is TreeViewItem)
                tvi = (TreeViewItem)e.NewValue;

            if (tvi == null)
                return;

            string nodeName = tvi.Tag?.ToString();

            int emailId;
            if (!int.TryParse(nodeName, out emailId))
                return;

            MimeMessage msg = _documentManager.GetCachedEmail(emailId);
            string pathToFile = "";
            string details = "";
            Dispatcher.BeginInvoke((Action)(() => tabControl.SelectedIndex = 0));

            switch (_viewState)
            {
                case "FilterView":
                case "ResultsView":
                    pathToFile = Path.Combine(  _application.Settings.ResultsFolder, nodeName + ".html" );
                    //show the email
                    this.WebBrowser.Navigate(pathToFile);
                    break;
                case "CacheView":
                    if (msg == null)
                        return;
                    this.WebBrowser.NavigateToString(msg.HtmlBody);
                    break;
            }

            if (msg == null)
                return;

            details = "From:" + String.Join(";", msg.From) + Environment.NewLine;
            details += "To:" + msg.To + Environment.NewLine; ;
            details += "Cc:" + msg.Cc + Environment.NewLine; ;
            details += "Subject:" + msg.Subject + Environment.NewLine; ;
            details += "Date:" + msg.Date + Environment.NewLine;
            //msg.Verify()
            txtDetails.Text = details;
            lstAttachments.ItemsSource = _documentManager.GetAttachments(msg);
        }

        private void lstAttachments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Attachment a = (Attachment)lstAttachments.SelectedItem;

            if (a == null|| a.FileName.ContainsAny(".bat",".exe.", ".com"  ))//todo add attachment black list
            {
                return;
            }
            int leakId = 0;
            string nodeName = "";

            if (tvwSearchResults.SelectedItem is TreeViewItem)
            {
                TreeViewItem tvi = (TreeViewItem)tvwSearchResults.SelectedItem;

                if (tvi == null)
                    return;

                 nodeName = tvi.Tag?.ToString();
            }

            if (!int.TryParse(nodeName, out leakId))
                return;

            MimeMessage msg = _documentManager.GetCachedEmail(leakId);

            if (msg == null)
                return;

            List<Attachment> attachments = _documentManager.GetAttachments(msg);
            foreach(Attachment attachment in attachments)
            {
                if (attachment.FileName != a.FileName )
                    continue;

                if (!_documentManager.SaveAttachment(leakId, attachment))
                    return;

                break;
            }

            string pathToFile = Path.Combine(_application.Settings.AttachemtsFolder, leakId.ToString(), a.FileName);
            try
            {
                System.Diagnostics.Process.Start(pathToFile);
            }
            catch(Exception ex)
            {
                UpdateUi(ex.Message, "messagebox.show");
            }
        }


        #endregion

        //Used in DocumentManager to update the status of the ui as it processes.
        //
        public void UpdateUi(object sender, string uiElement){

            switch (uiElement.ToLower())
            {
                case "treeview.results":
                    SearchResult tvInfo = (SearchResult)sender;
                    if (tvInfo.ResultCount == 0)
                        return;
                    //Because another thread is sending this info to update
                    //the ui we have to let the dispatching/owner thread update the ui
                    Dispatcher.Invoke(DispatcherPriority.Normal, new Action<SearchResult>(UpdateResultTree), tvInfo);
                    break;
                case "progressbar.update":
                    SearchStatus sts = (SearchStatus)sender;
                    if (sts != null)
                    {
                        Dispatcher.Invoke(DispatcherPriority.Normal, new Action<SearchStatus>(UpdateStatus), sts);
                    }
                    break;
                case "messagebox.show":
                    MessageBox.Show(sender?.ToString());
                    break;
                case "radiobutton.toggle":
                    ToggleRadioButtonsForSearch((bool)sender);
                    break;
            }
        }

        private Object treeLock = new Object();

        private void UpdateResultTree(SearchResult tvInfo)
        {
            //find the root node
            TreeViewItem parentNode = null;
            int parentIndex = 0;
            foreach (TreeViewItem rootNode in tvwSearchResults.Items)
            {
                if ((rootNode.Tag.ToString() == tvInfo.FilterName))
                {
                    parentNode = rootNode;
                    break;
                }
                parentIndex++;
            }

            if (parentNode == null)
                return;
            //find the child node under the parent
            foreach (TreeViewItem childNode in parentNode.Items)
            {
                if ((childNode.Tag.ToString() == tvInfo.LeakId.ToString()))
                {
                    break;//found, already here so bail out.
                }
            }
            TreeViewItem node = new TreeViewItem();
            node.Header = tvInfo.LeakId.ToString() + " (" + tvInfo.ResultCount.ToString() + ")";
            node.Tag = tvInfo.LeakId.ToString();
            parentNode.Items.Add(node);
            //update the root node label to show the document count under it.
            parentNode.Header = tvInfo.FilterName + " (" + parentNode.Items?.Count.ToString() + ")"; 
        }

        private void UpdateStatus(SearchStatus sts)
        {
            ProgressBar updateMe = null;
            switch (sts.ControlName)
            {
                case "prgEmailProgress":
                    updateMe = prgEmailProgress;
                    break;
                case "prgFilterProgress":
                    updateMe = prgFilterProgress;
                    break;
                case "prgSearchTerms":
                    updateMe = prgSearchTerms;
                    break;
                default:return;
            }
            if (updateMe == null)
                return;

            if (sts.Reset)
            {
                updateMe.Minimum = sts.Start;
                updateMe.Maximum = sts.End;
            }
            updateMe.Value = sts.Current;
        }

        //Loads the treeview in results area and the listview in filters tab.
        //
        private void LoadFilters()
        {
            if (_filterManager.Filters == null)
                return;

            List<Filter> sortedFilters = _filterManager.Filters.OrderBy(ob => ob.Priority).ToList();

            lstFilters.ItemsSource = null;
            lstFilters.ItemsSource = sortedFilters;
            AutoSizeColumns();

            tvwSearchResults.Items.Clear();

            //For now make the root nodes based on the filter name.
            //If we loaded by leak id it could take a while.
            //Later we can do an xref to find what document id's match
            //multiple filters. 
            //
            foreach (Filter f in sortedFilters)
            {
                TreeViewItem node = new TreeViewItem();
                node.Header = f.Name;
                node.Tag = f.Name; //note: this could be used to hold the object.
                tvwSearchResults.Items.Add(node);
            }
        }

      
    }
}
