using File_Scanner.Functionality;
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
        #region Fields
        // Main item queue
        private ConcurrentQueue<EventArgs> ItemQueue = new ConcurrentQueue<EventArgs>();
        // Scanner
        private Scanner Scanner;
        // XML Writer
        private XMLWriter XMLWriter;
        #endregion

        #region UI Updates
        private Thread UIUpdateThread;
        private ConcurrentQueue<string> UIUpdates = new ConcurrentQueue<string>();
        private bool running = false;
        #endregion

        #region Properties
        public int DirectoryCount
        {
            get
            {
                lock (Scanner.directoryCountLock) { return Scanner.directoryCount; }
            }
        }
        public int FileCount
        {
            get
            {
                lock (Scanner.fileCountLock) { return Scanner.fileCount; }
            }
        }
        public string CurrentDirectory
        {
            get
            {
                lock (Scanner.currentDirectoryLock) { return Scanner.currentDirectory; }
            }
        }
        public string CurrentFile
        {
            get
            {
                lock (Scanner.currentFileLock) { return Scanner.currentFile; }
            }
        }
        public bool Ready
        {
            get => !Scanner.Running;
        }
        public bool Running
        {
            get => Scanner.Running;
        }
        public double ScannedPercentage { get => (double.IsNaN(Scanner.Completed) ? 0.0f : Scanner.Completed); }
        public double UnscannedPercentage { get => 1.0f - ScannedPercentage; }
        #endregion

        #region Constructor
        public ScannerViewModel()
        {
            Scanner = new Scanner();
            XMLWriter = new XMLWriter();
            AddHandlers();
            UIUpdateThread = new Thread(new ThreadStart(UIUpdate));
            running = true;
            UIUpdateThread.Start();
        }
        ~ScannerViewModel()
        {
            UIUpdateThread.Join();
        }
        #endregion

        #region UI Update Loop
        private void UIUpdate()
        {
            // Check if the thread has been stopped externally
            if (!running) return;

            // Notify the UI of changes
            while (UIUpdates.Count > 0)
            {
                string item = "";
                UIUpdates.TryDequeue(out item);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(item));
            }

            // Sleep for designated amount of time
            Thread.Sleep(Settings.SETTING_MILLISECONDS_BETWEEN_UI_UPDATES);

            // Call itself again
            UIUpdate();
        }
        #endregion

        #region Events
        private void AddHandlers()
        {
            Scanner.PropertyChanged += OnPropertyChanged;
        }
        #endregion

        #region Commands
        private ICommand goCommand;
        public ICommand GoCommand
        {
            get
            {
                if (goCommand == null)
                    goCommand = new RelayCommand(new Action(Scanner.StartScan));

                return goCommand;
            }
        }

        private ICommand stopCommand;
        public ICommand StopCommand
        {
            get
            {
                if (stopCommand == null)
                    stopCommand = new RelayCommand(new Action(Scanner.StopScan));

                return stopCommand;
            }
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!UIUpdates.Contains(e.PropertyName))
                UIUpdates.Enqueue(e.PropertyName);
        }
        #endregion
    }
}
