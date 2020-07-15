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
        // Singleton Instance
        public static XMLWriter Instance;

        // Queue
        private ConcurrentQueue<NewFileDataEventArgs> Queue = null;

        // Threading
        private Thread xmlWriter;

        // Writer State
        private bool xmlWriterRunning = false;

        // Writer Settings
        private string savePath = "";
        private int scanNumber = -1;
        private XmlWriterSettings xmlWriterSettings = null;
        private XmlSerializerNamespaces emptyNamespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
        private XmlDocument xmlDocument = new XmlDocument();
        private XmlElement currentXMLElement = null;
        private XmlSerializer serializer = new XmlSerializer(typeof(FileDataModel));

        // Properties
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
        public int ScanNumber
        {
            get
            {
                if (scanNumber == -1 && File.Exists(Path.Combine(SavePath, ScannerViewModel.STREAM_NUMBER_FILE_NAME)))
                {
                    string output = "";
                    using (var reader = new StreamReader(Path.Combine(SavePath, ScannerViewModel.STREAM_NUMBER_FILE_NAME)))
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
        public string SaveFile { get => Path.Combine(SavePath, $"{ScannerViewModel.OUTPUT_FILE_NAME}{ScanNumber.ToString()}.xml"); }

        // Events
        private void AddHandlers()
        {
            Scanner.Instance.FileDataUpdated += WriteXML;
            Scanner.Instance.ScannerStarted += StartXMLWriter;
            Scanner.Instance.ScannerStopped += StopXMLWriter;
        }

        // Constructor
        public XMLWriter(ConcurrentQueue<NewFileDataEventArgs> queue)
        {
            Instance = this;
            Queue = queue;
            xmlWriter = new Thread(new ThreadStart(Write));
            AddHandlers();
        }
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
            if (!ScannerViewModel.SETTING_DYNAMIC_SAVE)
            {
                using (var streamWriter = new StreamWriter(SaveFile, false))
                {
                    xmlDocument.Save(streamWriter);
                }
            }
            Stop();
        }
        public void Start()
        {
            // Setup the XML Document
            XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
            XmlElement xmlRoot = xmlDocument.DocumentElement;
            xmlDocument.InsertBefore(xmlDeclaration, xmlRoot);
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
        private void PopAndProcessItem()
        {
            if (ScannerViewModel.SETTING_DYNAMIC_SAVE)
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
            XmlElement xmlElement = xmlDocument.CreateElement(string.Empty, model.GetType().FullName, string.Empty);
            currentXMLElement.AppendChild(xmlElement);
            AddItemToTree(nameof(model.Path), model.Path, xmlElement);
            AddItemToTree(nameof(model.Size), model.Size.ToString(), xmlElement);
            AddItemToTree(nameof(model.CreationDate), model.CreationDate.ToString(), xmlElement);
            AddItemToTree(nameof(model.ModifiedDate), model.ModifiedDate.ToString(), xmlElement);
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
        public void NewDrive(string Drive, long TotalSize, long TotalFreeSpace)
        {
            // Add drive header
            XmlElement xmlDrive = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars($"Drive_{Drive}"), string.Empty);
            xmlDocument.AppendChild(xmlDrive);
            // Add drive stats
            XmlElement xmlElementDriveHeader = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars("Drive_Statistics"), string.Empty);
            xmlDrive.AppendChild(xmlElementDriveHeader);
            // Add drive size
            XmlElement xmlElementDriveSize = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars("Drive_Size"), string.Empty);
            xmlElementDriveHeader.AppendChild(xmlElementDriveSize);
            XmlText xmlTextDriveSize = xmlDocument.CreateTextNode(TotalSize.ToString());
            xmlElementDriveSize.AppendChild(xmlTextDriveSize);
            // Add drive free space
            XmlElement xmlElementDriveFreeSpace = xmlDocument.CreateElement(string.Empty, RemoveInvalidChars("Drive_Free_Space"), string.Empty);
            xmlElementDriveHeader.AppendChild(xmlElementDriveFreeSpace);
            XmlText xmlTextDriveFreeSpace = xmlDocument.CreateTextNode(TotalFreeSpace.ToString());
            xmlElementDriveFreeSpace.AppendChild(xmlTextDriveFreeSpace);
            // Set the current element to the current drive
            currentXMLElement = xmlDrive;
        }
        public void IterateScanNumber()
        {
            int SN = ScanNumber;
            SN++;
            using (var writer = new StreamWriter(Path.Combine(SavePath, ScannerViewModel.STREAM_NUMBER_FILE_NAME)))
            {
                writer.Write(SN.ToString());
            }
        }
    }
}
