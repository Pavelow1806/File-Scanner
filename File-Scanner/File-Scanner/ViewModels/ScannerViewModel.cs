using File_Scanner.Models;
using File_Scanner.OperationEventArgs;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using System.Xml.Serialization;

namespace File_Scanner.ViewModels
{
    public class ScannerViewModel : INotifyPropertyChanged
    {
        #region Constants
        private const int THREAD_PAUSE_CHECK_INTERVAL = 100;
        private const string STREAM_NUMBER_FILE_NAME = "StreamNumber.txt";
        private const string OUTPUT_FILE_NAME = "ScanResults_";
        #endregion

        #region Fields
        // Threading
        private Thread scanner;
        private Thread xmlWriter;
        private ConcurrentBag<FileDataModel> files = new ConcurrentBag<FileDataModel>();
        private ConcurrentQueue<NewFileXMLDataEventArgs> XmlQueue = new ConcurrentQueue<NewFileXMLDataEventArgs>();
        private bool scannerRunning = false;
        private bool scannerPaused = false;
        // View Model field
        private int directoryCount = 0;
        private int fileCount = 0;
        private string currentDirectory = "";
        private string currentFile = "";
        // Locks
        private object directoryCountLock = new object();
        private object fileCountLock = new object();
        private object currentDirectoryLock = new object();
        private object currentFileLock = new object();
        // XML Writing
        private string savePath = "";
        private int scanNumber = -1;
        private XmlWriterSettings xmlWriterSettings = null;
        private XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
        private XmlDocument xmlDocument = new XmlDocument();
        private XmlElement currentXMLElement = null;
        #endregion

        #region Properties
        public int DirectoryCount
        {
            get
            {
                lock (directoryCountLock) { return directoryCount; }
            }
            set
            {
                lock (directoryCountLock)
                {
                    if (directoryCount != value)
                    {
                        directoryCount = value;
                        OnPropertyChanged(nameof(DirectoryCount));
                    }
                }
            }
        }
        public int FileCount
        {
            get
            {
                lock (fileCountLock) { return fileCount; }
            }
            set
            {
                lock (fileCountLock)
                {
                    if (fileCount != value)
                    {
                        fileCount = value;
                        OnPropertyChanged(nameof(FileCount));
                    }
                }
            }
        }
        public string CurrentDirectory
        {
            get
            {
                lock (currentDirectoryLock) { return currentDirectory; }
            }
            set
            {
                lock (currentDirectoryLock)
                {
                    if (currentDirectory != value)
                    {
                        currentDirectory = value;
                        OnPropertyChanged(nameof(CurrentDirectory));
                    }
                }
            }
        }
        public string CurrentFile
        {
            get
            {
                lock (currentFileLock) { return currentFile; }
            }
            set
            {
                lock (currentFileLock)
                {
                    if (currentFile != value)
                    {
                        currentFile = value;
                        OnPropertyChanged(nameof(CurrentFile));
                    }
                }
            }
        }
        public string SavePath
        {
            get
            {
                if (string.IsNullOrEmpty(savePath))
                    savePath = Environment.CurrentDirectory;
                return savePath;
            }
        }
        public int ScanNumber
        {
            get
            {
                if (scanNumber == -1 && File.Exists(Path.Combine(SavePath, STREAM_NUMBER_FILE_NAME)))
                {
                    string output = "";
                    using (var reader = new StreamReader(Path.Combine(SavePath, STREAM_NUMBER_FILE_NAME)))
                    {
                        output = reader.ReadLine();
                    }
                    int.TryParse(output, out scanNumber);
                    return scanNumber;
                }
                else
                {
                    return scanNumber;
                }
            }
        }
        public string SaveFile { get => Path.Combine(SavePath, $"{OUTPUT_FILE_NAME}{ScanNumber.ToString()}.txt"); }
        public XmlWriterSettings XmlWriterSettings
        {
            get
            {
                if (xmlWriterSettings == null)
                {
                    xmlWriterSettings = new XmlWriterSettings();
                    xmlWriterSettings.Indent = true;
                    xmlWriterSettings.OmitXmlDeclaration = true;
                }
                return xmlWriterSettings;
            }
        }
        #endregion

        #region Events
        private event EventHandler<NewFileDataEventArgs> FileDataUpdated;

        private void AddHandlers()
        {
            FileDataUpdated += WriteXML;
        }
        #endregion

        #region Constructors
        public ScannerViewModel()
        {
            AddHandlers();
            scanner = new Thread(new ThreadStart(Scan));
            xmlWriter = new Thread(new ThreadStart(Write));
        }
        #endregion

        #region Commands
        private ICommand goCommand;
        public ICommand GoCommand
        {
            get
            {
                if (goCommand == null)
                    goCommand = new RelayCommand(new Action(StartScan));

                return goCommand;
            }
        }

        private ICommand stopCommand;
        public ICommand StopCommand
        {
            get
            {
                if (stopCommand == null)
                    stopCommand = new RelayCommand(new Action(StopScan));

                return stopCommand;
            }
        }
        #endregion

        #region Scanning Functionality
        private void StartScan()
        {
            // Reset all metrics
            DirectoryCount = 0;
            FileCount = 0;
            CurrentDirectory = "";
            CurrentFile = "";
            // Setup the XML Document
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement xmlRoot = xmlDocument.DocumentElement;
            xmlDocument.InsertBefore(xmlDeclaration, xmlRoot);
            // Start the scan thread
            scannerRunning = true;
            scanner = new Thread(new ThreadStart(Scan));
            scanner.Start();
            xmlWriter.Start();
        }
        private void StopScan()
        {
            // Pause execution on the scanning thread
            scannerPaused = true;
            // Check whether the user would definitely like to stop
            var result = MessageBox.Show("Are you sure you would like to stop scanning?", "Stop scanning?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                scannerRunning = false;
                scannerPaused = false;
                DirectoryCount = 0;
                FileCount = 0;
                CurrentDirectory = "";
                CurrentFile = "";
            }
            else
                scannerPaused = false;
        }
        private void Scan()
        {
            // Start the scanner
            scannerRunning = true;
            // Iterate the scan number
            IterateScanNumber();
            // Get all drives on this machine
            string[] drives = System.Environment.GetLogicalDrives();
            // Cycle through each and go through each folder tree
            foreach (var drive in drives)
            {
                // Indent the drive on the XML output
                XmlElement xmlDrive = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars($"Drive_{drive}"), string.Empty);
                xmlDocument.AppendChild(xmlDrive);
                // Set the current XML element to be the current drive
                currentXMLElement = xmlDrive;

                // Stop scanning if required
                if (!scannerRunning)
                    break;

                // Get data from the current drive
                DriveInfo driveInfo = new DriveInfo(drive);
                if (!driveInfo.IsReady)
                {
                    Console.WriteLine($"The drive {driveInfo.Name} couldn't be read.");
                    continue;
                }
                DirectoryInfo rootDirectory = driveInfo.RootDirectory;
                IterateThroughDirectory(rootDirectory);
            }
            // Once we've finished scanning, uncheck the bool
            scannerRunning = false;
        }
        private void IterateThroughDirectory(DirectoryInfo directory)
        {
            // Change the current directory and display the path
            DirectoryCount++;
            CurrentDirectory = directory.FullName;
            FileInfo[] fileArray = null;
            try
            {
                // Attempt to get all of the files within the diretory we're currently looking at
                fileArray = directory.GetFiles("*.*");
            }
            catch (Exception ex)
            {
                // Output if we weren't able to
                Console.WriteLine(ex.Message);
            }

            if (fileArray != null)
            {
                // Cycle through each file in the array gathered from the current directory
                foreach (var file in fileArray)
                {
                    // Before we scan each file, ensure that we're ready to scan and don't need to stop the thread or pause it
                    if (!scannerRunning)
                        break;

                    while (scannerPaused)
                    {
                        // Pause the thread if the scanner isn't running
                        Thread.Sleep(THREAD_PAUSE_CHECK_INTERVAL);
                    }

                    // Add the file specification to the bag
                    FileDataModel currentFile = new FileDataModel()
                    {
                        Path = file.FullName,
                        Size = file.Length,
                        CreationDate = file.CreationTime,
                        ModifiedDate = file.LastWriteTime
                    };
                    files.Add(currentFile);

                    // Add the item to the queue
                    FileDataUpdated?.BeginInvoke(this, new NewFileDataEventArgs(currentFile), null, null);

                    // Display the current file
                    FileCount++;
                    CurrentFile = Path.GetFileName(file.FullName);
                }

                // Get all of the directories within this directory
                DirectoryInfo[] directories = directory.GetDirectories();
                // Iterate through each
                foreach (var subDirectory in directories)
                {
                    // Stop scanning if the thread was stopped
                    if (!scannerRunning)
                        break;

                    // Otherwise iterate through the directory
                    IterateThroughDirectory(subDirectory);
                }
            }
        }
        #endregion

        #region XML Writing Functionality
        public void IterateScanNumber()
        {
            int SN = ScanNumber;
            SN++;
            using (var writer = new StreamWriter(Path.Combine(SavePath, STREAM_NUMBER_FILE_NAME)))
            {
                writer.Write(SN.ToString());
            }
        }
        private void WriteXML(object sender, NewFileDataEventArgs e)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(FileDataModel));
            var xml = "";
            using (var stringWriter = new StringWriter())
            {
                using (var xmlWriter = XmlWriter.Create(stringWriter, XmlWriterSettings))
                {
                    serializer.Serialize(xmlWriter, e.Data, emptyNamespaces);
                    xml = stringWriter.ToString();
                }
            }
            XmlQueue.Enqueue(new NewFileXMLDataEventArgs(xml));
        }
        private void Write()
        {
            while (scannerRunning)
            {
                // Pause writing while the scanner is paused
                while (scannerPaused)
                {

                }
                // Stop the thread if the scanner has stopped
                if (!scannerRunning)
                    break;
                // Otherwise continue writing
                using (var streamWriter = new StreamWriter(SaveFile, false))
                {
                    NewFileXMLDataEventArgs item = null;
                    XmlQueue.TryDequeue(out item);
                    if (item != null)
                    {
                        XmlText content = xmlDocument.CreateTextNode(item.XML);
                        currentXMLElement.AppendChild(content);
                        xmlDocument.Save(streamWriter);
                    }
                }
            }
        }
        private string RemoveInvalidChars(string text)
        {
            Regex regex = new Regex("[^a-zA-Z]");
            return regex.Replace(text, "");
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string PropertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        #endregion
    }
}
