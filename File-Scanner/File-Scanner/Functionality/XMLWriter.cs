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
        private ConcurrentQueue<NewFileDataEventArgs> Queue = null;
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
        private string saveFolder = "";
        private int scanNumber = -1;
        private int partNumber = 1;
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
                if (string.IsNullOrEmpty(saveFolder))
                    saveFolder = Path.Combine(SavePath, $"{Settings.OUTPUT_FOLDER_NAME}{ScanNumber.ToString()}");
                return saveFolder;
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
        }
        #endregion

        #region Constructor
        public XMLWriter(ConcurrentQueue<NewFileDataEventArgs> queue)
        {
            Instance = this;
            Queue = queue;
            xmlWriter = new Thread(new ThreadStart(Write));
            AddHandlers();
        }
        #endregion

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
        public void Start()
        {
            // Setup the XML Document
            CreateNewDocument();
            // Start the writer thread
            xmlWriterRunning = true;
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
        private void WriteXML(object sender, NewFileDataEventArgs e)
        {
            // Add the item to the queue
            Queue.Enqueue(e);
        }
        private void Write()
        {
            while (xmlWriterRunning)
                PopAndProcessItem();
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
        private void PopAndProcessItem()
        {
            if (Settings.SETTING_DYNAMIC_SAVE)
            {
                using (var streamWriter = new StreamWriter(SaveFile, false))
                {
                    NewFileDataEventArgs item = null;
                    Queue.TryDequeue(out item);
                    if (item != null)
                    {
                        AddModelToTree(item.Data);
                        xmlDocument.Save(streamWriter);
                    }
                }
            }
            else
            {
                NewFileDataEventArgs item = null;
                Queue.TryDequeue(out item);
                if (item != null)
                {
                    AddModelToTree(item.Data);
                }
            }
        }
        private void AddModelToTree(FileDataModel model)
        {
            if (currentQuantity > Settings.SETTING_SPLIT_QUANTITY)
            {
                SaveDocument();
                CreateNewDocument();
                CreateNewDriveElement();
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
        private void AddItemToTree(string name, string value, XmlElement Parent)
        {
            XmlElement xmlElement = xmlDocument.CreateElement(string.Empty, name, string.Empty);
            Parent.AppendChild(xmlElement);
            XmlText xmlText = xmlDocument.CreateTextNode(value);
            xmlElement.AppendChild(xmlText);
        }
        private string RemoveInvalidChars(string text)
        {
            Regex regex = new Regex("[^a-zA-Z]");
            return regex.Replace(text, "");
        }
        public void NewDrive(DriveInfo driveInfo)
        {
            // Set the current drive information
            currentDrive = driveInfo;
            // Create the xml section for it
            CreateNewDriveElement();
        }
        private void CreateNewDriveElement()
        {
            // Add drive header
            DriveElement = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars($"Drive_{currentDrive.Name}"), string.Empty);
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
        }
        public void IterateScanNumber()
        {
            int SN = ScanNumber;
            SN++;
            using (var writer = new StreamWriter(Path.Combine(SavePath, Settings.STREAM_NUMBER_FILE_NAME)))
            {
                writer.Write(SN.ToString());
            }
        }
    }
}
