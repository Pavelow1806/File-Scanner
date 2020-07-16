using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace File_Scanner.Functionality
{
    public static class Settings
    {
        #region Constants
        public static int THREAD_PAUSE_CHECK_INTERVAL = 100;
        public static string STREAM_NUMBER_FILE_NAME = "StreamNumber.txt";
        public static string OUTPUT_FOLDER_NAME = "ScanResults_";
        public static string OUTPUT_FILE_NAME = "SR_";
        public static bool SETTING_DYNAMIC_SAVE = false;
        public static int SETTING_SPLIT_QUANTITY = 10000;
        public static int SETTING_MILLISECONDS_BETWEEN_UI_UPDATES = 10;
        #endregion
    }
}
