using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace File_Scanner.OperationEventArgs
{
    public class NewFileXMLDataEventArgs : EventArgs
    {
        public string XML { get; set; } = "";
        public NewFileXMLDataEventArgs(string xml)
        {
            XML = xml;
        }
    }
}
