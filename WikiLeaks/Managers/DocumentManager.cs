using HtmlAgilityPack;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WikiLeaks.Extensions;
using WikiLeaks.Models;

//this is .eml. Attachments are embedded
//https://wikileaks.org/podesta-emails//get/1
//html version (no attachments)
//https://wikileaks.org/podesta-emails/emailid/

namespace WikiLeaks.Managers
{
    public class DocumentManager
    {
        private AppManager _application = null;
        private FilterManager _filterManager = null;
        private CacheManager _cacheManager = null;
        private MainWindow _mainWindow = null;

        Process _searchFolderProc = null;

        public void KillSearchFolderProcess()
        {
            if (_searchFolderProc == null)
                return;
            try
            {
                _searchFolderProc.Kill();
                _searchFolderProc.Close();
            }
            catch (Exception ex) { }
        }

        public bool CancelSearch{ get; set; }
        private Dictionary<int, SearchResult> _searchResults = new Dictionary<int, SearchResult>();

        public void ClearSearchResults()
        {
            _searchResults.Clear();
        }

        //Just being lazy, don't feel like loading all the settings..
        //
        public DocumentManager(MainWindow wnd, AppManager appManager, FilterManager filterManager)
        {
            CancelSearch = false;

            if (wnd == null || appManager == null || filterManager == null)
            {
                Debug.Assert(false, "OBJECT IS NULL");
            }

            _mainWindow = wnd;
            _application = appManager;
            _filterManager = filterManager;

            _cacheManager = new CacheManager(_mainWindow, _application.Settings.CacheFolder);
         
        }

        public void SearchDocuments(int startId, int endId,  string filterName)
        {
            if (startId > endId || startId < 0)
            {
                _mainWindow.UpdateControl("INVALID IDs", "messagebox.show");
                return;
            }

            if (string.IsNullOrWhiteSpace(filterName))
            {
                _mainWindow.UpdateControl("INVALID FILTER", "messagebox.show");
                return;
            }

            Filter searchFilter =   _filterManager.Filters.Where(wf => wf.Name?.ToUpper() == filterName.ToUpper()).FirstOrDefault();

            if(searchFilter == null)
            {
                _mainWindow.UpdateControl("FILTER WASN'T FOUND", "messagebox.show");
                return;
            }
            //Iterate through the cache ID's because they contain references to attachments
            //
            List<int> cacheIds = _cacheManager.CacheIds.Where(w => w == startId && w <= endId).ToList();

            for (int i = startId; i <= endId; i++)
            {
               if(!cacheIds.Contains(i))
                     cacheIds.Add(i);
            }
            SearchStatus emailStatus = new SearchStatus();
            emailStatus.ControlName = "prgEmailProgress";
            emailStatus.Start = startId;
            emailStatus.End = endId;
            emailStatus.Reset = true;
            emailStatus.Current = startId;
            _mainWindow.UpdateControl(emailStatus, "progressbar.update");
            emailStatus.Reset = false;

            foreach (int cacheId in cacheIds)
            {
                emailStatus.Current = cacheId;
                _mainWindow.UpdateControl(emailStatus, "progressbar.update");

                if (CancelSearch)
                {
                    CancelSearch = false;
                    return;
                }

                SearchResult searchResult = new SearchResult();
                searchResult.FilterName = filterName;
                searchResult.ResultCount = 0;
                searchResult.LeakId = cacheId;

                if (_searchResults.ContainsKey(cacheId))
                {   //Is it loaded? We've already processed this doc for another search term, re-use it.
                    searchResult = _searchResults[cacheId];

                    if (searchResult.FilterName != filterName)
                        searchResult.ResultCount = 0;

                    searchResult.FilterName = filterName;
                    //searchResult.SearchTermHitCount.Clear();
                    //searchResult.LeakHitCount.Clear();
                }
                else if (_cacheManager.IsCached(cacheId))
                {   //Is it local?
                    searchResult.Document = _cacheManager.GetCachedFile(cacheId);
                    searchResult.FilterName = filterName;
                    searchResult.ResultCount = 0;
                    searchResult.LeakId = cacheId;
                    _searchResults.Add(cacheId, searchResult);
                }
                else
                {   //Go get it.
                    searchResult.Document = DownloadDocument(cacheId);
                    searchResult.FilterName = filterName;
                    searchResult.ResultCount = 0;
                    searchResult.LeakId = cacheId;
                    _searchResults.Add(cacheId, searchResult);
                }

                string[] searchTerms = searchFilter.SearchTokens.Split(',');
                string highlightColor = searchFilter.HighlightColor.GetHtmlHexColor();


                SearchStatus termStatus = new SearchStatus();
                termStatus.ControlName = "prgSearchTerms";
                termStatus.Start = 0;
                termStatus.End = searchTerms.Count() -1;
                termStatus.Reset = true;
                termStatus.Current = 0;
                _mainWindow.UpdateControl(termStatus, "progressbar.update");
                termStatus.Reset = false;
                int termIdx = 0;

                foreach (string searchTerm in searchTerms)
                {
                    termStatus.Current = termIdx;
                    _mainWindow.UpdateControl(termStatus, "progressbar.update");
                    termIdx++;

                    if (!searchResult.Document.Contains(searchTerm))
                        continue;
                 
                    //update search term metrics
                    if (!searchResult.SearchTermHitCount.ContainsKey(searchTerm.ToLower()))
                        searchResult.SearchTermHitCount.Add(searchTerm.ToLower(), 1);
                    else
                        searchResult.SearchTermHitCount[searchTerm.ToLower()] += 1;

                    //update document hits metrics
                    if (!searchResult.LeakHitCount.ContainsKey(cacheId))
                        searchResult.LeakHitCount.Add(cacheId, 1);
                    else
                        searchResult.LeakHitCount[cacheId] += 1;

                    searchResult.ResultCount++;
                
                    searchResult.Document = searchResult.Document.HighlightText(searchTerm, highlightColor);

                    _searchResults[cacheId] = searchResult;

                    #region old code to make it html format
                    var text = searchResult.Document; // @"<div id='content'>" + node.InnerHtml.TrimStart('\n', '\t');
                    
                    
                    // string html = $@"<meta http-equiv='Content-Type' content='text/html;charset=UTF-8'/><meta http-equiv='X-UA-Compatible' content='IE=edge'/>{text}".FixHtml();

                    #endregion
                    SaveToResults(cacheId, searchResult.Document);

                    if (CancelSearch)
                    {
                        CancelSearch = false;
                        return;
                    }
                }
               // termStatus.Current = 0;
               // _mainWindow.UpdateControl(termStatus, "progressbar.update");


                ////this will update the treeview, by default it shows the filters in the
                ////root, so if the filter found something in an email then add/update
                ////the email id under the filter node.
                if (searchResult.ResultCount > 0 && _mainWindow != null)
                {
                    _mainWindow.UpdateControl(searchResult, "treeview.results");
                }

                // emailStatus.Current = startId;
                //  _mainWindow.UpdateControl(emailStatus, "progressbar.update");
            }
            // emailStatus.Current = startId;
            //  _mainWindow.UpdateControl(emailStatus, "progressbar.update");
        }


        public void SearchDocument(int docId)
        {
            SearchStatus filterStatus = new SearchStatus() { ControlName = "prgFilterProgress", Start = 0,End = _filterManager.Filters.Count() - 1,Reset = true,Current = 0 };
            
            _mainWindow.UpdateControl(filterStatus, "progressbar.update");
            filterStatus.Reset = false;
            int filterIndex = 0;

            //get the document
            SearchResult searchResult = GetSearchResult(docId);

            //counts how many of the search terms were found (doesn't count quantity).
            //FilterName
            //      1 (3) where 1 = docid, 3 = filterHitCount
            //      This would let the user know in this document 3 of their search terms were in the document.
            //      Letting the user know the document is probably a primary subject of the filter. 
            int filterHitCount = 0;

            foreach(Filter filter in _filterManager.Filters)
            {
                if (CancelSearch){  CancelSearch = false; return; }

                searchResult.FilterName = filter.Name;

                string[] searchTerms = filter.SearchTokens.Split(',');
                string highlightColor = filter.HighlightColor.GetHtmlHexColor();
              
                SearchStatus termStatus = new SearchStatus() { ControlName = "prgSearchTerms", Start = 0, End = searchTerms.Count() - 1,Reset = true, Current = 0 };

                _mainWindow.UpdateControl(termStatus, "progressbar.update");
                termStatus.Reset = false;
                int termIdx = 0;

                foreach (string searchTerm in searchTerms)
                {
                    termStatus.Current = termIdx;
                    _mainWindow.UpdateControl(termStatus, "progressbar.update");
                    termIdx++;

                    if (!searchResult.Document.Contains(searchTerm) || string.IsNullOrWhiteSpace(searchTerm))
                        continue;

                    // if (!searchResult.SearchTermHitCount.ContainsKey(docId.ToString() + searchTerm))
                    //    searchResult.SearchTermHitCount.Add(docId.ToString() + filter.Name, searchTermHitCount)

                    filterHitCount++; 
                    searchResult.Document = searchResult.Document.HighlightText(searchTerm, highlightColor);
                }
                searchResult.ResultCount = filterHitCount;
                _mainWindow.UpdateControl(searchResult, "treeview.results");

                // searchResult.LeakHitCount

               filterIndex++;
                filterStatus.Current = filterIndex;
                _mainWindow.UpdateControl(filterStatus, "progressbar.update");
                filterHitCount = 0;//reset this or it will update the wrong nodes in the treeview.
            }
            
            SaveToResults(docId, searchResult.Document);
        }

        /// <summary>
        /// This is a used during the document searches. Its primary function is to 
        /// get the document. Since a document can contain multiple search terms we
        /// try and get it from the dictionary first, then the cache, and last download it
        /// if needed.
        /// </summary>
        /// <param name="docId"></param>
        /// <param name="fromSearchResults">by default tries to get the document from the SearchResults list.</param>
        private SearchResult GetSearchResult(int docId)
        {
            SearchResult searchResult = new SearchResult();
            searchResult.FilterName = "";
            searchResult.ResultCount = 0;
            searchResult.LeakId = docId;

            if (_searchResults.ContainsKey(docId))
            {   //Is it loaded? We've already docId this doc for another search term, re-use it.
                searchResult = _searchResults[docId];
            }
            else if (_cacheManager.IsCached(docId))
            {   //Is it local?
                searchResult.Document = _cacheManager.GetCachedFile(docId);
                searchResult.ResultCount = 0;
                searchResult.LeakId = docId;
                _searchResults.Add(docId, searchResult);
            }
            else
            {   //Go get it.
                searchResult.Document = DownloadDocument(docId);
                searchResult.ResultCount = 0;
                searchResult.LeakId = docId;
                _searchResults.Add(docId, searchResult);
            }

            return searchResult;
        }

        protected bool SaveToResults(int leakId, string document)
        {
            //saves as html so we can display highlighting.
            //
            string pathToFile = _application.Settings.ResultsFolder + leakId + ".html";

            try
            {
                File.WriteAllText(pathToFile, document);
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.Message);
                _mainWindow.UpdateControl(ex.Message, "messagebox.show");
                return false;
            }
            return true;

        }

        //html agility pack doesn't have an async function.
        //  public async Task<string> DownloadDocument(int leakId, bool ignoreCache = false)
        public string DownloadDocument(int leakId,  bool ignoreCache = false)
        {
            if (_cacheManager.IsCached(leakId) && ignoreCache)
            {
               return _cacheManager.GetCachedFile(leakId);
            }
            string content = "";
            string url = _application.Settings.BaseUrl + leakId.ToString();
            try
            {
                WebClient client = new WebClient();
                Stream stream = client.OpenRead(url);
                StreamReader reader = new StreamReader(stream);
                 content = reader.ReadToEnd();
            }catch(Exception ex)
            {
                //todo log this. It's usually a timeout error.
            }
            if (string.IsNullOrWhiteSpace(content))
                return content;

            _cacheManager.SaveToCache(leakId, content);

            //Since this is going in the wild I'm sure they don't want every neckbeard on earth slamming their servers.
            // 10 second delay between requests.
            //LOOK FOR ALREADY DOWNLOADED CACHE FILES! DON'T BE A DICK! 
            Thread.Sleep(TimeSpan.FromSeconds(10));

            return content;
        }

        public string GetResultFile(int leakId) {

            if (_searchResults.ContainsKey(leakId))
                return _searchResults[leakId].Document;

            string pathToFile = _application.Settings.ResultsFolder;

            if ((leakId % 1) != 0)
            {
                // pathToFile += "Attachments\\";
                return "TODO IMPLEMENT ATTACHEMENTS";
            }

            pathToFile += leakId.ToString() + ".html";

            if (!File.Exists(pathToFile))
                return "File not found.";

            return File.ReadAllText(pathToFile);
           
        }

        public MimeMessage GetCachedEmail(int leakId)
        {
            MimeMessage msg = null;
            string pathToFile = Path.Combine( _application.Settings.CacheFolder , leakId + ".eml");

            if (!File.Exists(pathToFile))
            {
                _mainWindow.UpdateControl("Invalid file: " + pathToFile, "messagebox.show");
                return msg;
            }
            
            try
            {
                 msg = MimeMessage.Load(pathToFile); //.Load(stream);
           

            }
            catch(Exception ex)
            {
                _mainWindow.UpdateControl(ex.Message, "messagebox.show");
            }
            return msg;
        }

        public List<Attachment> GetAttachments(MimeMessage message)
        {
            List<Attachment> lst = new List<Attachment>();

            if (message == null)
                return lst;

            foreach (var mimeEntity in message.BodyParts)
            {
                var attachment = Attachment.Load(mimeEntity);

                if (attachment == null)
                    continue;

                lst.Add(attachment);
            
             }
            return lst;
        }

        public bool SaveAttachment( int leakId, Attachment  a)
        {
            string pathToFile = Path.Combine(_application.Settings.AttachemtsFolder, leakId.ToString(), a.FileName);

            try
            {
                string directory = Path.GetDirectoryName(pathToFile);

                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllBytes(pathToFile, a.Data);
            }catch(Exception ex)
            {
                _mainWindow.UpdateControl(ex.Message, "messagebox.show");
                return false;
            }
            return true;
        }


        #region File search helper functions

        public bool SearchFolder(string filterName, string searchTerm)
        {
            string appPath = _application.Settings.AgentExePath;

            if (!File.Exists(appPath))
            {
                _mainWindow.UpdateControl("Agent.exe not found at:" + appPath, "messagebox.show");
                return false;
            }

            string tempFolder = System.IO.Path.GetTempPath();

            string searchPathArgs = " -d ";//directory
            string searchPath = _application.Settings.AgentSearchFolder;

            string fileArgs = " -f ";//filenames to search for.
            string fileTypes =  _application.Settings.SearchFileType;

            string searchArgs = " -c ";
            string searchFor = "\"" + searchTerm+ "\"";//text to search for

            string outputArgs = " -o "; //tell app to write to output file. 
                                        // outputArgs += " -oa "; //Append output
            string outputPath = Path.Combine(tempFolder, filterName.Replace(" ", "") + "." +searchTerm.Replace(" ", "") + filterName.Replace(" ", "")); //".srch");
            string outputOptions = " -ofc";//output csv
           
            //-oc //            Output content lines
            outputOptions += " -ocn"; //           Don't Output content lines

            string searchSubFolders = " -s";//search subfolders

            string procStartArg = searchPathArgs + searchPath + fileArgs + fileTypes + searchArgs + searchFor + outputArgs + outputPath + outputOptions + searchSubFolders;

            try
            {
                
                Watch(tempFolder, filterName, searchTerm);
                _searchFolderProc = System.Diagnostics.Process.Start(appPath, procStartArg);
                _searchFolderProc.WaitForExit();
              //  _searchFolderProc.Kill()
                //_searchFolderProc.Close()
            }
            catch (Exception ex)
            {
                _mainWindow.UpdateControl(ex.Message, "messagebox.show");
                return false;
            }
            return true;
        }
     
        protected void Watch(string pathToFolder, string filterName,string searchTerm)
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = pathToFolder;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = "*." + searchTerm.Replace(" ", "") + filterName.Replace(" ", "");// "*.srch";
            watcher.Changed += new System.IO.FileSystemEventHandler((s, e) => OnChanged(s, e, filterName, searchTerm));
            // watcher.Changed += new FileSystemEventHandler(OnChanged);//this is default (without searchTerm parameter)
            watcher.EnableRaisingEvents = true;
        }

        protected void OnChanged(object source, FileSystemEventArgs e, string filterName, string searchTerm)
        {
            Console.WriteLine(e.Name);
            IEnumerable<string> fileLines = ReadLines(e.FullPath);

            foreach (string line in fileLines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] tokens = line.Split(','); 

                if (tokens.Count() < 2)
                    continue;

                string pathToFile = tokens[0];
                //string path = tokens[0];
                //string file = tokens[1];
                SearchResult sr = new SearchResult();
                sr.FilterName = filterName;
                sr.LeakId = -1;
                sr.ResultCount = fileLines.Count();
                //if (sr.SearchTermHitCount.ContainsKey(searchTerm))
                //    sr.SearchTermHitCount[searchTerm] = fileLines.Count();
                //else
                //    sr.SearchTermHitCount.Add(searchTerm, fileLines.Count());

                sr.FileName = searchTerm + "-" + Path.GetFileName(pathToFile);
                sr.FilePath = pathToFile;
              //  sr.Document = tokens[tokens.Count() - 1];
            
                _mainWindow.UpdateControl(sr, "treeview.results.search.folder");
            }
        }

        public static IEnumerable<string> ReadLines(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 0x1000, FileOptions.SequentialScan))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }


        #endregion
    }
}
