using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileSyncInspector
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public string Status { get; set; } // např. "missing", "added", "modified"
        public List<FileSystemItem> Children { get; set; } = new List<FileSystemItem>();




    }
}
