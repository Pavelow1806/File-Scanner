using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Scanner.Functionality
{
    public class Settings
    {
        #region Constants
        public const int THREAD_PAUSE_CHECK_INTERVAL = 100;
        public const string STREAM_NUMBER_FILE_NAME = "StreamNumber.txt";
        public const string OUTPUT_FOLDER_NAME = "ScanResults_";
        public const string OUTPUT_FILE_NAME = "SR_";
        public const bool SETTING_DYNAMIC_SAVE = false;
        public const int SETTING_SPLIT_QUANTITY = 10000;
        #endregion
    }
}
