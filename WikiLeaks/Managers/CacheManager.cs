using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WikiLeaks.Extensions;

namespace WikiLeaks.Managers
{
    public  class CacheManager
    {
        private MainWindow _mainWindow = null;
        string _cacheFolder = EnvironmentEx.AppDataFolder;

        public bool CacheIdsLoaded { get; set; }
        //Made this a int so we can save attachment files as
        //the decimal portion in the order they are loaded.
        //example email id = 25001 has two attachments (hypothetically).
        //25001.1.<fileext> would be first attachment
        //25001.2.<fileext> would e second attachment.
        //May have to figure out where ( in \attachments sub-folder?) to store non text attachments.
        //put reference data in json file for these non text attachments?
        //
        public List<int>  CacheIds { get; set; }

        public CacheManager(MainWindow wnd, string pathToFolder)
        {
            _mainWindow = wnd;
            
            if(string.IsNullOrWhiteSpace(pathToFolder))
                pathToFolder = EnvironmentEx.AppDataFolder + "\\Cache\\";

            CacheIds = new List<int>();

            CacheIdsLoaded = false;
            if (!string.IsNullOrWhiteSpace(pathToFolder) && Directory.Exists(pathToFolder))
                _cacheFolder = pathToFolder;

            //load ids only so we don't bog down ram full of documents if all were doing is a cache hit check
            LoadCache();
        }

        public void LoadCache(bool idsOnly= true)
        {
            CacheIds.Clear();
            
            string[] fileEntries = Directory.GetFiles(_cacheFolder,"*.*", System.IO.SearchOption.AllDirectories);

            foreach (string pathToFile in fileEntries)
            {
               string fileRoot = Path.GetFileNameWithoutExtension(pathToFile);
               int fileId;

               if(int.TryParse(fileRoot,out fileId ))
                    CacheIds.Add(fileId);
            }
            CacheIdsLoaded = true;
        }

        public bool IsCached(int leakId)
        {
            if(!CacheIdsLoaded)
                LoadCache();

            return CacheIds.Where(w => w == leakId).Count() > 0 ? true : false;
        }

        public string GetCachedFile(int leakId)
        {
            string pathToFile = _cacheFolder + leakId.ToString() + ".eml";

            if (!File.Exists(pathToFile))
                return "";

            string doc = File.ReadAllText(pathToFile);
            return doc;
        }


        public bool SaveToCache(int leakId ,string document)
        {
            string pathToFile = _cacheFolder + leakId.ToString() + ".eml";

            try
            {
                File.WriteAllText(pathToFile, document);
                CacheIds.Add(leakId);
            }
            catch(Exception ex)
            {
                Debug.Assert(false, ex.Message);
                _mainWindow.UpdateUi(ex.Message, "messagebox.show");
                return false;
            }
            return true;
        }
    }
}
