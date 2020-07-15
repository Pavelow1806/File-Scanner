using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace File_Scanner.Models
{
    [Serializable]
    public class FileDataModel
    {
        public string Path { get; set; } = "";
        public float Size { get; set; } = 0.0f;
        public DateTime CreationDate { get; set; } = default;
        public DateTime ModifiedDate { get; set; } = default;
    }
}
