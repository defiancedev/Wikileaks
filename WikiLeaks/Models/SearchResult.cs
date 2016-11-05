using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiLeaks.Models
{
    public class SearchResult
    {
        public string FilterName { get; set; }

        //total count for the filter (sum of all search term hits).
        //counts how many of the search terms were found (doesn't count quantity).
        //FilterName
        //      1 (3) where 1 = docid, 3 = filterHitCount
        //      This would let the user know in this document 3 of their search terms were in the document.
        //      Letting the user know the document is probably a primary subject of the filter. 
        //
        public int ResultCount { get; set; }


        //string is search term.
        //int is cound
        //keeps track if search term was found in doc. how effective the search term is..
           public Dictionary<string, int> SearchTermHitCount = new Dictionary<string, int>();

        //int is leak id
        //int is count
        //this is to keep track of how often a search term was found (not how many were found in doc).
        public Dictionary<int, int> LeakHitCount = new Dictionary<int, int>();

        public int LeakId { get; set; }

        public string Document { get; set; }

        public string FileName { get; set; }

        public string FilePath { get; set; }

    }
}
