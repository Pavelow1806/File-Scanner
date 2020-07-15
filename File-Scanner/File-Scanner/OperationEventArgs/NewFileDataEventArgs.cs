using File_Scanner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace File_Scanner.OperationEventArgs
{
    public class NewFileDataEventArgs : EventArgs
    {
        public FileDataModel Data = null;
        public NewFileDataEventArgs(FileDataModel data) 
        {
            Data = data;
        }
    }
}
