using File_Scanner.Models;
using File_Scanner.OperationEventArgs;
using File_Scanner.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace File_Scanner.Functionality
{
    public class Scanner : INotifyPropertyChanged
    {
        // Threading
        private Thread scanner;

        // Singleton Instance
        public static Scanner Instance;

        // Fields
        private bool scannerRunning = false;
        private bool scannerPaused = false;
        public int directoryCount = 0;
        public int fileCount = 0;
        public string currentDirectory = "";
        public string currentFile = "";

        // Locks
        public object directoryCountLock = new object();
        public object fileCountLock = new object();
        public object currentDirectoryLock = new object();
        public object currentFileLock = new object();

        // Exposed Properties
        public bool Running 
        { 
            get => scannerRunning;
            set 
            {
                if (scannerRunning != value)
                {
                    scannerRunning = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Ready"));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Running"));
                }
            }
        }
        public bool Paused { get => scannerPaused; }
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
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DirectoryCount)));
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
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileCount)));
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
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDirectory)));
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
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentFile)));
                    }
                }
            }
        }

        // Events
        public event EventHandler<NewFileDataEventArgs> FileDataUpdated;
        public event EventHandler ScannerStarted;
        public event EventHandler ScannerStopped;
        public event PropertyChangedEventHandler PropertyChanged;

        public Scanner()
        {
            Instance = this;
            scanner = new Thread(new ThreadStart(Scan));
        }
        public void StartScan()
        {
            // Reset all metrics
            DirectoryCount = 0;
            FileCount = 0;
            CurrentDirectory = "";
            CurrentFile = "";
            // Start the scan thread
            Running = true;
            scanner = new Thread(new ThreadStart(Scan));
            scanner.Start();
            ScannerStopped?.Invoke(this, null);
        }
        public void StopScan()
        {
            // Pause execution on the scanning thread
            scannerPaused = true;
            // Check whether the user would definitely like to stop
            var result = MessageBox.Show("Are you sure you would like to stop scanning?", "Stop scanning?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                Running = false;
                scannerPaused = false;
                ScannerStopped?.Invoke(this, null);
            }
            else
                scannerPaused = false;
        }
        private void Scan()
        {
            // Start the scanner
            Running = true;
            // Iterate the scan number
            XMLWriter.Instance.IterateScanNumber();
            // Get all drives on this machine
            string[] drives = System.Environment.GetLogicalDrives();
            // Cycle through each and go through each folder tree
            foreach (var drive in drives)
            {
                // Stop scanning if required
                if (!Running)
                    break;

                // Get data from the current drive
                DriveInfo driveInfo = new DriveInfo(drive);
                if (!driveInfo.IsReady)
                {
                    Console.WriteLine($"The drive {driveInfo.Name} couldn't be read.");
                    continue;
                }
                // Indent the drive on the XML output
                XMLWriter.Instance.NewDrive(driveInfo);

                // Set the current XML element to be the current drive

                // Begin iterating through the drive
                DirectoryInfo rootDirectory = driveInfo.RootDirectory;
                IterateThroughDirectory(rootDirectory);
            }
            // Once we've finished scanning, uncheck the bool
            Running = false;
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
                    if (!Running)
                        break;

                    while (scannerPaused)
                    {
                        // Pause the thread if the scanner isn't running
                        Thread.Sleep(Settings.THREAD_PAUSE_CHECK_INTERVAL);
                    }

                    // Add the file specification to the bag
                    FileDataModel currentFile = new FileDataModel()
                    {
                        Path = file.FullName,
                        Size = file.Length,
                        CreationDate = file.CreationTime,
                        ModifiedDate = file.LastWriteTime
                    };

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
                    if (!Running)
                        break;

                    // Otherwise iterate through the directory
                    IterateThroughDirectory(subDirectory);
                }
            }
        }
    }
}
