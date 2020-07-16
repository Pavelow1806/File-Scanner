using File_Scanner.Models;
using File_Scanner.OperationEventArgs;
using File_Scanner.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace File_Scanner.Functionality
{
    public class XMLWriter
    {
        #region Singleton Instance
        public static XMLWriter Instance;
        #endregion

        #region Queue
        private ConcurrentQueue<EventArgs> Queue = null;
        #endregion

        #region Threading
        private Thread xmlWriter;
        #endregion

        #region Writer State
        private bool xmlWriterRunning = false;
        #endregion

        #region Fields
        private XmlElement DriveElement;
        private int currentQuantity = 0;
        #endregion

        #region Writer Settings
        private string savePath = "";
        private int scanNumber = -1;
        private int partNumber = 1;
        private bool newDrive = false;
        private DriveInfo currentDrive;
        private XmlWriterSettings xmlWriterSettings = null;
        private XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
        private XmlDocument xmlDocument = new XmlDocument();
        private XmlElement currentXMLElement = null;
        private XmlSerializer serializer = new XmlSerializer(typeof(FileDataModel));
        #endregion

        #region Properties
        public bool Running 
        { 
            get => xmlWriterRunning;
            set => xmlWriterRunning = value;
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
        public string SaveFolder
        {
            get
            {
                return Path.Combine(SavePath, $"{Settings.OUTPUT_FOLDER_NAME}{ScanNumber.ToString()}");
            }
        }
        public int ScanNumber
        {
            get
            {
                if (scanNumber == -1 && File.Exists(Path.Combine(SavePath, Settings.STREAM_NUMBER_FILE_NAME)))
                {
                    string output = "";
                    using (var reader = new StreamReader(Path.Combine(SavePath, Settings.STREAM_NUMBER_FILE_NAME)))
                    {
                        output = reader.ReadLine();
                    }
                    int.TryParse(output, out scanNumber);
                    return scanNumber;
                }
                else if (scanNumber == -1 && !File.Exists(Path.Combine(SavePath, Settings.STREAM_NUMBER_FILE_NAME)))
                {
                    scanNumber = 0;
                    using (var writer = new StreamWriter(Path.Combine(SavePath,Settings.STREAM_NUMBER_FILE_NAME)))
                    {
                        writer.Write(scanNumber.ToString());
                    }
                    return scanNumber;
                }
                else
                {
                    return scanNumber;
                }
            }
        }
        public string SaveFile { get => Path.Combine(SaveFolder, $"{Settings.OUTPUT_FILE_NAME}{ScanNumber.ToString()}_PARTNUM.xml"); }
        #endregion

        #region Events
        private void AddHandlers()
        {
            Scanner.Instance.FileDataUpdated += WriteXML;
            Scanner.Instance.ScannerStarted += StartXMLWriter;
            Scanner.Instance.ScannerStopped += StopXMLWriter;
            Scanner.Instance.NewDrive += NewDrive;
        }
        #endregion

        #region Constructor
        public XMLWriter(ConcurrentQueue<EventArgs> queue)
        {
            Instance = this;
            Queue = queue;
            xmlWriter = new Thread(new ThreadStart(Write));
            AddHandlers();
        }
        #endregion

        #region Control Methods
        public void Start()
        {
            // Starting a new scan so iterate scan number
            IterateScanNumber();
            // Setup the XML Document
            CreateNewDocument();
            // Start the writer thread
            xmlWriterRunning = true;
            xmlWriter = new Thread(new ThreadStart(Write));
            xmlWriter.Start();
        }
        public void Stop()
        {
            // Stop the thread
            xmlWriterRunning = false;
            if (xmlWriter.IsAlive)
                xmlWriter.Join();
            // Reset the XML objects
            xmlDocument = new XmlDocument();
            currentXMLElement = null;
        }
        #endregion

        #region Event Handling Functions
        private void StartXMLWriter(object sender, EventArgs e)
        {
            Stop();
            Start();
        }
        private void StopXMLWriter(object sender, EventArgs e)
        {
            xmlWriterRunning = false;
            // Loop through the remaining items in the queue and write them to the xml stream
            while (!Queue.IsEmpty)
            {
                PopAndProcessItem();
            }
            // If the setting for dynamic saving isn't checked then save the xml file
            if (!Settings.SETTING_DYNAMIC_SAVE)
            {
                SaveDocument();
            }
            Stop();
        }
        private void WriteXML(object sender, NewFileDataEventArgs e)
        {
            // Add the item to the queue
            Queue.Enqueue(e);
        }
        public void NewDrive(object sender, NewDriveEventArgs e)
        {
            // Set the current drive information
            currentDrive = e.Data;
            // Queue the item
            Queue.Enqueue(e);
        }
        #endregion

        #region Main XML Writing Loop
        private void Write()
        {
            // Start writing the output on the correct thread
            while (xmlWriterRunning)
                PopAndProcessItem();
        }
        private void PopAndProcessItem()
        {
            if (Settings.SETTING_DYNAMIC_SAVE)
            {
                using (var streamWriter = new StreamWriter(SaveFile, false))
                {
                    EventArgs item = null;
                    Queue.TryDequeue(out item);
                    // Dequeue the new item
                    // Check if it's a new file item, if so add it to the tree
                    NewFileDataEventArgs FileDataItem = item as NewFileDataEventArgs;
                    if (FileDataItem != null)
                    {
                        AddModelToTree(FileDataItem.Data);
                    }
                    // Check if it's a new drive item, if so create a new drive and continue
                    NewDriveEventArgs DriveItem = item as NewDriveEventArgs;
                    if (DriveItem != null)
                    {
                        CreateNewDriveElement(DriveItem.Data);
                    }
                    xmlDocument.Save(streamWriter);
                }
            }
            else
            {
                EventArgs item = null;
                Queue.TryDequeue(out item);
                // Dequeue the new item
                // Check if it's a new file item, if so add it to the tree
                NewFileDataEventArgs FileDataItem = item as NewFileDataEventArgs;
                if (FileDataItem != null)
                {
                    AddModelToTree(FileDataItem.Data);
                }
                // Check if it's a new drive item, if so create a new drive and continue
                NewDriveEventArgs DriveItem = item as NewDriveEventArgs;
                if (DriveItem != null)
                {
                    CreateNewDriveElement(DriveItem.Data);
                }
            }
        }
        #endregion

        #region Queue Processing Functions
        private void AddModelToTree(FileDataModel model)
        {
            try
            {
                if (currentQuantity > Settings.SETTING_SPLIT_QUANTITY)
                {
                    SaveDocument();
                    CreateNewDocument();
                    CreateNewDriveElement(currentDrive);
                    currentQuantity = 0;
                    partNumber++;
                }
                XmlElement xmlElement = xmlDocument.CreateElement(string.Empty, model.GetType().FullName, string.Empty);
                currentXMLElement.AppendChild(xmlElement);
                AddItemToTree(nameof(model.Path), model.Path, xmlElement);
                AddItemToTree(nameof(model.Size), model.Size.ToString(), xmlElement);
                AddItemToTree(nameof(model.CreationDate), model.CreationDate.ToString(), xmlElement);
                AddItemToTree(nameof(model.ModifiedDate), model.ModifiedDate.ToString(), xmlElement);
                currentQuantity++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private void AddItemToTree(string name, string value, XmlElement Parent)
        {
            XmlElement xmlElement = xmlDocument.CreateElement(string.Empty, name, string.Empty);
            Parent.AppendChild(xmlElement);
            XmlText xmlText = xmlDocument.CreateTextNode(value);
            xmlElement.AppendChild(xmlText);
        }
        private void CreateNewDriveElement(DriveInfo drive)
        {
            // Add drive header
            DriveElement = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars($"Drive_{drive.Name}"), string.Empty);
            xmlDocument.AppendChild(DriveElement);
            // Add drive stats
            XmlElement xmlElementDriveHeader = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars("Drive_Statistics"), string.Empty);
            DriveElement.AppendChild(xmlElementDriveHeader);
            // Add drive size
            XmlElement xmlElementDriveSize = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars("Drive_Size"), string.Empty);
            xmlElementDriveHeader.AppendChild(xmlElementDriveSize);
            XmlText xmlTextDriveSize = xmlDocument.CreateTextNode(currentDrive.TotalSize.ToString());
            xmlElementDriveSize.AppendChild(xmlTextDriveSize);
            // Add drive free space
            XmlElement xmlElementDriveFreeSpace = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars("Drive_Free_Space"), string.Empty);
            xmlElementDriveHeader.AppendChild(xmlElementDriveFreeSpace);
            XmlText xmlTextDriveFreeSpace = xmlDocument.CreateTextNode(currentDrive.TotalFreeSpace.ToString());
            xmlElementDriveFreeSpace.AppendChild(xmlTextDriveFreeSpace);
            // Set the current element to the current drive
            currentXMLElement = DriveElement;
            currentDrive = drive;
        }
        private void CreateNewDocument()
        {
            xmlDocument = new XmlDocument();
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement xmlRoot = xmlDocument.DocumentElement;
            xmlDocument.InsertBefore(xmlDeclaration, xmlRoot);
        }
        private void SaveDocument()
        {
            // Create the output directory if it doesn't exist
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);
            // Save the current document
            using (var streamWriter = new StreamWriter(SaveFile.Replace("PARTNUM", partNumber.ToString()), false))
            {
                xmlDocument.Save(streamWriter);
            }
        }
        private string RemoveInvalidChars(string text)
        {
            Regex regex = new Regex("[^a-zA-Z]");
            return regex.Replace(text, "");
        }
        #endregion

        public void IterateScanNumber()
        {
            int SN = ScanNumber;
            SN++;
            scanNumber = SN;
            partNumber = 1;
            using (var writer = new StreamWriter(Path.Combine(SavePath, Settings.STREAM_NUMBER_FILE_NAME)))
            {
                writer.Write(SN.ToString());
            }
        }
    }
}
