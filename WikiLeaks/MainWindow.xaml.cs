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

        private Thread _searchEmailsThread = null;
        private Thread _searchFolderThread = null;

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
            this.txtAttachemtsFolder.Text = _application.Settings.AttachemtsFolder;
            this.txtSearchResultsFolder.Text = _application.Settings.ResultsFolder;
            this.txtFilterFolder.Text = _application.Settings.FilterFolder;
            this.txtAgentExePath.Text = _application.Settings.AgentExePath;
            this.txtAgentSearchFolder.Text = _application.Settings.AgentSearchFolder;
            this.txtAgentSearchFileType.Text = _application.Settings.SearchFileType;
            this.chkUseAgentSettings.IsChecked = _application.Settings.UseAgentSearch;


            _filterManager = new FilterManager(this, _application.Settings.FilterFolder);
            LoadFilters();

            List<Filter> sortedFilters = _filterManager.Filters.OrderBy(ob => ob.Priority).ToList();
            foreach (Filter f in sortedFilters)
            {
                TreeViewItem node = new TreeViewItem();
                node.Header = f.Name;
                node.Tag = f.Name; //note: this could be used to hold the object.
                tvwSearchResults.Items.Add(node);
            }
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
        #region Search Results Events/Code

        private string _viewState = "FilterView"; //default to this.



        private void btnRefreshTreeView_Click(object sender, RoutedEventArgs e)
        {
            switch (_viewState)
            {
                case "CacheView":
                    rdoCacheView_Click(null, null);
                    break;
                case "ResultsView":
                    rdoResultsView_Click(null, null);
                    break;
                case "FilterView":
                    rdoFilterView_Click(null, null);
                    break;

            }
        }

        private void rdoFilterView_Click(object sender, RoutedEventArgs e)
        {
            _viewState = "FilterView";
            //because we have a shitton of files we don't want to reload everytime they click  a button. Make them refresh.
           
            if (_filterManager.Filters == null)
                return;


          
            tvwSearchResults.Visibility = Visibility.Visible;
            tvwCacheFolder.Visibility = Visibility.Hidden;
            tvwResultsFolder.Visibility = Visibility.Hidden;

            if (tvwSearchResults.Items.Count > 0 && sender != null && e != null)
                return;

            tvwSearchResults.Items.Clear();
            //For now make the root nodes based on the filter name.
            //If we loaded by leak id it could take a while.
            //Later we can do an xref to find what document id's match
            //multiple filters. 
            //
            List<Filter> sortedFilters = _filterManager.Filters.OrderBy(ob => ob.Priority).ToList();
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


            tvwResultsFolder.Width = tvwSearchResults.Width;
            tvwResultsFolder.Height = tvwSearchResults.Height;
            tvwResultsFolder.HorizontalAlignment = tvwSearchResults.HorizontalAlignment;
            tvwResultsFolder.VerticalAlignment = tvwSearchResults.VerticalAlignment;
            tvwResultsFolder.Margin = tvwSearchResults.Margin;
            tvwResultsFolder.Visibility = Visibility.Visible;
            tvwSearchResults.Visibility = Visibility.Hidden;
            tvwCacheFolder.Visibility = Visibility.Hidden;

            //because we have a shitton of files we don't want to reload everytime they click  a button. Make them refresh.
            if (tvwResultsFolder.Items.Count > 0 && sender != null && e != null)
                return;

            string directory = _application.Settings.ResultsFolder;
            int batchSize = 500;
            tvwResultsFolder.Items.Clear();
            LoadTreeWithFiles(tvwResultsFolder, directory, "*.html", batchSize);
        }

        private void rdoCacheView_Click(object sender, RoutedEventArgs e)
        {
            _viewState = "CacheView";
            
            tvwCacheFolder.Width = tvwSearchResults.Width;
            tvwCacheFolder.Height = tvwSearchResults.Height;
            tvwCacheFolder.HorizontalAlignment = tvwSearchResults.HorizontalAlignment;
            tvwCacheFolder.VerticalAlignment = tvwSearchResults.VerticalAlignment;
            tvwCacheFolder.Margin = tvwSearchResults.Margin;
            tvwCacheFolder.Visibility = Visibility.Visible;

            tvwSearchResults.Visibility = Visibility.Hidden;
            tvwResultsFolder.Visibility = Visibility.Hidden;

            //because we have a shitton of files we don't want to reload everytime they click  a button. Make them refresh.
            if (tvwCacheFolder.Items.Count > 0 && sender != null && e != null)
                return;


            tvwCacheFolder.Items.Clear();
            string directory = _application.Settings.CacheFolder;
            int batchSize = 500;
            LoadTreeWithFiles(tvwCacheFolder, directory, "*.eml", batchSize);
        }

        private void LoadTreeWithFiles(TreeView treeView, string directory, string fileType, int batchSize)
        {
            this.txtDetails.Text = "Loading...";

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
                        treeView.Items.Add(rootNode);

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

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    this.txtDetails.Text = "";
                }));
            });
            t1.Start();
        }


        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!PreFlightCheckOk())
                return;

            int startId = -1;
            int.TryParse(txtLeakStartId.Text.Trim(), out startId);

            int endId = -1;
            int.TryParse(txtLeakEndId.Text.Trim(), out endId);

            //default to the filter view because it adds to the view as the docs are
            //processed. 
            rdoFilterView.IsChecked = true;

            LoadFilters(); //todo depricate?


            //// SearchDocuments(startId, endId); //sync call for debuging
            _searchEmailsThread = new Thread(() => SearchDocuments(startId, endId));
            _searchEmailsThread.Start();

            if (!_application.Settings.UseAgentSearch)
                return;

            _searchFolderThread = new Thread(() => SearchFolder());
            _searchFolderThread.Start();
        }

        private bool PreFlightCheckOk()
        {
            int startId = -1;
            int.TryParse(txtLeakStartId.Text.Trim(), out startId);

            int endId = -1;
            int.TryParse(txtLeakEndId.Text.Trim(), out endId);

            if (startId <= 0 || endId < startId)
            {
                MessageBox.Show("You must provide valid start and end ids!");
                return false;
            }

            if (_application.Settings.UseAgentSearch)
            {

                if (!string.IsNullOrWhiteSpace(_application.Settings.AgentExePath) && !File.Exists(_application.Settings.AgentExePath))
                {
                    MessageBox.Show("Agent exe file doesn't exist.");
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_application.Settings.AgentSearchFolder) && !Directory.Exists(_application.Settings.AgentSearchFolder))
                {
                    MessageBox.Show("Agent search directory doesn't exist.");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(_application.Settings.SearchFileType))
                {
                    MessageBox.Show("Agent file type is empty.");
                    return true;
                }
            }
            return true;
        }

        private void btnCancelSearch_Click(object sender, RoutedEventArgs e)
        {
            _documentManager.CancelSearch = true;

            if (_searchEmailsThread != null)
            {
                Thread.Sleep(TimeSpan.FromSeconds(3));
                _searchEmailsThread.Abort();
            }

            if (_searchFolderThread != null)
            {
                _documentManager.KillSearchFolderProcess();

                Thread.Sleep(TimeSpan.FromSeconds(3));
                _searchFolderThread.Abort();
            }

        }

        private void SearchDocuments(int startId, int endId)
        {
            if (startId > endId || startId < 0)
            {
                UpdateControl("INVALID IDs", "messagebox.show");
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
            UpdateControl(emailStatus, "progressbar.update");
            emailStatus.Reset = false;

            for (int i = startId; i <= endId; i++)
            {
                _documentManager.SearchDocument(i);
                emailStatus.Current = i;
                UpdateControl(emailStatus, "progressbar.update");

                if (_documentManager.CancelSearch)
                    break;
            }

            _documentManager.CancelSearch = false;

            ToggleRadioButtonsForSearch(true);

            // MessageBox.Show("Document search completed.");
        }


        private void SearchFolder()
        {
            string tempFolder = System.IO.Path.GetTempPath();

            SearchStatus filterStatus = new SearchStatus() { ControlName = "prgFolderFilter", Start = 0, End = _filterManager.Filters.Count() - 1, Reset = true, Current = 0 };
            UpdateControl(filterStatus, "progressbar.update");
            filterStatus.Reset = false;
            int filterIndex = 0;

            foreach (Filter f in _filterManager.Filters)
            {
                filterIndex++;
                filterStatus.Current = filterIndex;
                UpdateControl(filterStatus, "progressbar.update");
                if (File.Exists(tempFolder + "\\" + f.Name + ".srch"))  // Path.Combine(tempFolder, "\\", filterName, ".srch");
                    File.Delete(tempFolder + "\\" + f.Name + ".srch");//clear old search results

                string[] searchTerms = f.SearchTokens.Split(',');
                SearchStatus termStatus = new SearchStatus() { ControlName = "prgFolderTerms", Start = 0, End = searchTerms.Count() - 1, Reset = true, Current = 0 };

                UpdateControl(termStatus, "progressbar.update");
                termStatus.Reset = false;
                int termIndex = 0;
                foreach (string term in searchTerms)
                {
                    _documentManager.SearchFolder(f.Name, term);
                    termIndex++;
                    termStatus.Current = termIndex;
                    UpdateControl(termStatus, "progressbar.update");
                }
            }
        }

        private void ToggleRadioButtonsForSearch(bool enableState)
        {
            System.Windows.Media.Color c = Colors.LightGray;

            if (enableState)
                c = Colors.White;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                rdoResultsView.Focusable = enableState;
                rdoResultsView.IsHitTestVisible = enableState;
                rdoCacheView.Focusable = enableState;
                rdoCacheView.IsHitTestVisible = enableState;

                //todo attachments
                rdoAttachmentsView.Focusable = false;
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
            {
                //its not an email id so it's a root node or a file from the search folders.
                if (!File.Exists(tvi.Tag?.ToString()))
                    return; //its a root node.

                try
                {
                    string argument = "/select, \"" + tvi.Tag?.ToString() + "\"";
                    System.Diagnostics.Process.Start("explorer.exe", argument);
                }
                catch (Exception ex)
                {
                    UpdateControl(ex.Message, "messagebox.show");
                }

                return;
            }

            MimeMessage msg = _documentManager.GetCachedEmail(emailId);
            string pathToFile = "";
            string details = "";
            Dispatcher.BeginInvoke((Action)(() => tabControl.SelectedIndex = 0));

            pathToFile = Path.Combine(_application.Settings.ResultsFolder, nodeName + ".html");
            //show the email
            this.WebBrowser.Navigate(pathToFile);

            if (msg == null)
                return;

            details = "Subject:" + msg.Subject + Environment.NewLine;
            details += "From:" + String.Join(";", msg.From) + Environment.NewLine;
            details += "To:" + msg.To + Environment.NewLine; ;
            details += "Cc:" + msg.Cc + Environment.NewLine; ;
            details += "Date:" + msg.Date + Environment.NewLine;
            //msg.Verify()
            txtDetails.Text = details;
            lstAttachments.ItemsSource = _documentManager.GetAttachments(msg);
        }


        private void tvwResultsFolder_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
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

            pathToFile = Path.Combine(_application.Settings.ResultsFolder, nodeName + ".html");
            //show the email
            this.WebBrowser.Navigate(pathToFile);

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

        private void tvwCacheFolder_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
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
            string details = "";
            Dispatcher.BeginInvoke((Action)(() => tabControl.SelectedIndex = 0));

            if (msg == null)
                return;

            string body = msg.HtmlBody;

            if (string.IsNullOrEmpty(msg.HtmlBody))
                body = msg.TextBody;

            if (string.IsNullOrEmpty(body))
                return;

            this.WebBrowser.NavigateToString(msg.HtmlBody);

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

            if (a == null || a.FileName.ContainsAny(".bat", ".exe.", ".com"))//todo add attachment black list
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
            foreach (Attachment attachment in attachments)
            {
                if (attachment.FileName != a.FileName)
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
            catch (Exception ex)
            {
                UpdateControl(ex.Message, "messagebox.show");
            }
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

            if (!_filterManager.AddFilter(f))
                return;

            LoadFilters();

            TreeViewItem node = new TreeViewItem();
            node.Header = f.Name;
            node.Tag = f.Name; //note: this could be used to hold the object.
            tvwSearchResults.Items.Add(node);

        }

        private void btnSaveFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter selected = (Filter)lstFilters.SelectedItem;

            string originalName = selected.Name;
            //if (selected != null && selected.Name != f.Name)
            //    originalName = selected.Name;

            Filter f = new Filter();
            int priority = 0;
            int.TryParse(txtFilterPriority.Text.Trim(), out priority);
            f.Priority = priority;

            f.Name = txtFilterName.Text.Trim();
            if(f.Name.Contains(" "))
            {
                MessageBox.Show("Spaces not allowed for filter name.");
                txtFilterName.Focus();
                return;
            }

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

        

            if (!_filterManager.SaveFilter(originalName, f))
                return;

            LoadFilters();

            // just refresh this shit, theres a bug in the treeview item if you try to update the tag with a period in the string like myfilter.test 
            //
            tvwSearchResults.Items.Clear();
            _filterManager.Filters.Remove(selected);
            List<Filter> sortedFilters = _filterManager.Filters.OrderBy(ob => ob.Priority).ToList();
            foreach (Filter filter in sortedFilters)
            {
                TreeViewItem node = new TreeViewItem();
                node.Header = filter.Name;
                node.Tag = filter.Name; //note: this could be used to hold the object.
                tvwSearchResults.Items.Add(node);
            }


        }

        private void btnDeleteFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter selecteFilter = (Filter)lstFilters.SelectedItem;
            if (selecteFilter == null)
                return;

            if (!_filterManager.DeleteFilter(selecteFilter.Name))
            {
                MessageBox.Show( "failed to delete " + selecteFilter.Name);
                return;
            }

            LoadFilters();
            tvwSearchResults.Items.Clear();
            _filterManager.Filters.Remove(selecteFilter);
            List<Filter> sortedFilters = _filterManager.Filters.OrderBy(ob => ob.Priority).ToList();
            foreach (Filter f in sortedFilters)
            {
                TreeViewItem node = new TreeViewItem();
                node.Header = f.Name;
                node.Tag = f.Name; //note: this could be used to hold the object.
                tvwSearchResults.Items.Add(node);
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

        #region App Settings Tab Events/Code
        private void btnSaveAppSettings_Click(object sender, RoutedEventArgs e)
        {
            _application.Settings.BaseUrl = this.txtBaseUrlSetting.Text.Trim();
            int i = 0;
            if (int.TryParse(this.txtLeakStartIdSetting.Text, out i))
                _application.Settings.SearchStartId = i;

            if (int.TryParse(this.txtLeakEndIdSetting.Text, out i))
                _application.Settings.SearchEndId = i;

            string tmp = this.txtCachFolder.Text.Trim();
            try
            {
                if (!Directory.Exists(tmp))
                    Directory.CreateDirectory(tmp);
            }
            catch (Exception ex)
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

            if (!chkUseAgentSettings.IsChecked.Value)
            {
                _application.Settings.UseAgentSearch = false;
                _application.SaveSettings("");
                return;
            }

            _application.Settings.UseAgentSearch = true;

            tmp = this.txtAgentExePath.Text.Trim();



            try
            {
                if (!string.IsNullOrWhiteSpace(tmp) && !File.Exists(tmp))
                {
                    MessageBox.Show("Agent exe file doesn't exist.");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Agent exe file error. " + ex.Message);
                return;
            }

            _application.Settings.AgentExePath = tmp;


            tmp = this.txtAgentSearchFolder.Text.Trim();
            if (tmp.Contains(" "))
            {
                MessageBox.Show("Path to Agent search folder can't have spaces.");
                return;
            }
            try
            {
                if (!string.IsNullOrWhiteSpace(tmp) && !Directory.Exists(tmp))
                {
                    MessageBox.Show("Agent search directory doesn't exist.");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Agent search directory error. " + ex.Message);
                return;
            }
            _application.Settings.AgentSearchFolder = tmp;


            tmp = this.txtAgentSearchFileType.Text.Trim();
            try
            {
                if (string.IsNullOrWhiteSpace(tmp))
                {
                    MessageBox.Show("Agent file type is empty.");
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Agent file type error. " + ex.Message);
                return;
            }
            _application.Settings.SearchFileType = tmp;



            _application.SaveSettings("");
        }
        #endregion

        //Used in DocumentManager to update the status of the ui as it processes.
        //

        public void UpdateControl(object sender, string uiElement){

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
                case "treeview.results.search.folder":
                    SearchResult fileInfo = (SearchResult)sender;
                    if (fileInfo.ResultCount == 0)
                        return;
                    
                        Dispatcher.Invoke(DispatcherPriority.Normal, new Action<SearchResult>(UpdateTreeFileResults), fileInfo);
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
                    //        break;//found, already here so bail out.
                    return;
                }
            }
            TreeViewItem node = new TreeViewItem();
            node.Header = tvInfo.LeakId.ToString() + " (" + tvInfo.ResultCount.ToString() + ")";
            node.Tag = tvInfo.LeakId.ToString();
            parentNode.Items.Add(node);
            //update the root node label to show the document count under it.
            parentNode.Header = tvInfo.FilterName + " (" + parentNode.Items?.Count.ToString() + ")"; 
        }

        private void UpdateTreeFileResults(SearchResult fileInfo)
        {
            //These are the properties set in the function
            //sr.FilterName = filterName;
            //sr.LeakId = -1;
            //sr.ResultCount = fileLines.Count();
            //sr.FileName = file;
            //sr.FilePath = path;
            int parentIndex = 0;

            if (fileInfo.FileName == "moloch-chelsea.txt")
                parentIndex = 0;

            //find the root node
            TreeViewItem parentNode = null;
          
            foreach (TreeViewItem rootNode in tvwSearchResults.Items)
            {
                if ((rootNode.Tag.ToString() == fileInfo.FilterName))
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
                // if ((childNode.Tag.ToString() == fileInfo.FilePath)) //this is wrong because same file could be a hit for multipl search terms, so your updating same node with different names
                if(childNode.Header.ToString() == fileInfo.FileName)
                {
                    //childNode.Header = fileInfo.FileName;// + " (" + fileInfo.ResultCount.ToString() + ")";
                    // break;//found, todo update the count
                    return;
                }
            }

            //child node wasn't found so add it.
            TreeViewItem node = new TreeViewItem();
            node.Header = fileInfo.FileName;// + " (" + fileInfo.ResultCount.ToString() + ")";
            node.Tag = fileInfo.FilePath;
            parentNode.Items.Add(node);
            //update the root node label to show the document count under it.
            parentNode.Header = fileInfo.FilterName + " (" + parentNode.Items?.Count.ToString() + ")";
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
                case "prgFolderTerms":
                    updateMe = prgFolderTerms;
                    break;
                case "prgFolderFilter":
                    updateMe = prgFolderFilter;
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
        
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Uri.AbsoluteUri);
        }

     
    }
}
