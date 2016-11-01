using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WikiLeaks.Models
{
    public class SearchStatus
    {
        public int Start { get; set; }

        public int End { get; set; }

        public int Current { get; set; }

        public bool Reset { get; set; }

        public string ControlName { get; set; }

        public string Message { get; set; }
    }
}
