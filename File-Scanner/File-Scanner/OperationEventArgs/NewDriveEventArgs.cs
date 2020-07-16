using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Scanner.OperationEventArgs
{
    public class NewDriveEventArgs : EventArgs
    {
        public DriveInfo Data = null;
        public NewDriveEventArgs(DriveInfo data)
        {
            Data = data;
        }
    }
}
