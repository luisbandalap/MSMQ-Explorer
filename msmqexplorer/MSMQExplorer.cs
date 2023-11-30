#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Messaging;
using System.Windows.Forms;
using Be.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using MSMQExplorer.Properties;
using ScintillaNET;
using ScintillaNET_FindReplaceDialog;
using Message = System.Messaging.Message;
using System.Timers;
using System.Reflection;

#endregion

namespace MSMQExplorer
{
    public partial class FormMsmqExplorer : Form
    {
        private readonly FindReplace _findReplaceSend;
        private readonly FindReplace _findReplaceRec;

        // Hold OSVersion to display appropriate diaogs

        // Member variables
        private readonly bool _isMsmqInstalled;
        private readonly MsmqMessaging _msmqMessaging;
        private string _inXsdPath;
        private bool _isRecPrivateQueue;
        private bool _isSendPrivateQueue;
        private string _messageLabel;
        private OperatingSystem _operatingSystem;
        private RibbonOrbStyle _orbStyle;
        private int _orbStyleIndex;
        private string _orbStyleName;
        private RibbonTheme _orbTheme;
        private int _orbThemeIndex;
        private string _orbThemeName;
        private string _outXsdPath;
        private string _priority;
        private string _recFormat;
        private string _recHostName;
        private List<byte> _recMessageByteList;
        private DynamicByteProvider _recMessageByteProvider;
        private string _selectedMessageId;
        private string _selectedRecQueueName;
        private string _selectedSendQueueName;
        private string _selectedTab;
        private string _sendFormat;
        private string _sendHostName;
        private List<byte> _sendMessageByteList;
        private DynamicByteProvider _sendMessageByteProvider;
        private int _timeToReachQueue;
        private string _timeToReachQueuePeriod;
        private int _timeToReceive;
        private string _timeToRecievePeriod;
        private bool _useDeadLetterQueue;
        private bool _useEncryption;
        private bool _useJournal;
        private bool _useRecTransaction;
        private bool _useSendTransaction;
        private bool _useTracing;
        private FormSettings _formSettings;
        private bool _showUpdatedTimestamp;
        private List<MSMQLog> _msmqLog;

        // Timers - Count Refresh
        private System.Timers.Timer _countTimer;
        private bool _autoUpdateCount;
        private int _autoUpdateCountInterval;

        // Timers - Queue Refresh
        private System.Timers.Timer _queueTimer;
        private bool _autoUpdateQueue;
        private int _autoUpdateQueueInterval;

        /// <summary>
        ///     Build the form
        /// </summary>
        public FormMsmqExplorer()
        {
            try
            {
                InitializeComponent();
                LoadSettings();
                InitMainWindow();
                InitLog();
                // Configure Scintilla controls
                InitScintilla(scintillaSendXML);
                InitScintilla(scintillaRecXML);

                // Initialise ObjectListView
                fastObjectListViewMessages.GetColumn(0).ImageGetter = rowObject => 5;
                fastObjectListViewMessages.GetColumn(2).AspectGetter = delegate(object message)
                {
                    try
                    {
                        Message model = message as Message;
                        return model == null ? string.Empty : model.SourceMachine;
                    }
                    catch (Exception)
                    {
                        return "<Not Available>";
                    }
                };

                // Initialise ByteProvider and HexBox controls
                hexBoxSendHex.ByteProvider = _sendMessageByteProvider;
                hexBoxRecHex.ByteProvider = _recMessageByteProvider;

                // Initialise MSMQ Messaging class
                _msmqMessaging = new MsmqMessaging();
                _isMsmqInstalled = _msmqMessaging.IsMsmqInstalled();

                // Initialise the FindReplace dialog
                _findReplaceSend = new FindReplace(scintillaSendXML);
                _findReplaceRec = new FindReplace(scintillaRecXML);
                // Initialise the settings dialog
                _formSettings = new FormSettings();
                _formSettings.Hide();

                // Create the Timer
                _countTimer = new System.Timers.Timer {Enabled = false};
                _countTimer.Elapsed  += new ElapsedEventHandler(OnCountRefreshEvent);

                // Create the Timer
                _queueTimer = new System.Timers.Timer { Enabled = false };
                _queueTimer.Elapsed += new ElapsedEventHandler(OnQueueRefreshEvent);

                // Run the timers
                InitTimers();
            }

            catch (Exception e)
            {
                ShowError(Strings.Error, Strings.UiError + Environment.NewLine + Environment.NewLine + e.Message,
                    e.StackTrace);
            }
        }

        private void InitLog()
        {
            _msmqLog = new List<MSMQLog>();
            fastObjectListViewLog.SetObjects(_msmqLog);
            AddLogEntry("Info", "Log initialised");
        }

        private void AddLogEntry(string entryType, string logEntry)
        {
            MSMQLog newEntry = new MSMQLog(DateTime.Now, entryType, logEntry);
            _msmqLog.Add(newEntry);
            fastObjectListViewLog.SetObjects(_msmqLog);
        }

        private void InitMainWindow()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = string.Format(Strings.FormMsmqExplorer_InitMainWindow_MSMQ_Explorer_____0___1___2__Revision__3_,
                version.Major, version.Minor, version.Build, version.Revision);
        }

        // Handles the queue count refresh event
        private void OnCountRefreshEvent(object source, ElapsedEventArgs e)
        {
            if (_autoUpdateCount)
                UpdateAllMessageCounts();
        }

        // Handles the queue refresh event
        private void OnQueueRefreshEvent(object source, ElapsedEventArgs e)
        {
            if (_autoUpdateQueue)
            {
                // UpdateAllMessageCounts();
            }

        }

        private void InitScintilla(Scintilla scintilla)
        {
            // Set the XML Lexer
            scintilla.Lexer = Lexer.Xml;

            // Show line numbers
            scintilla.Margins[0].Width = 34;

            // Enable XML syntax
            scintilla.StyleResetDefault();
            scintilla.Styles[Style.Default].Font = "Courier";
            scintilla.Styles[Style.Default].Size = 10;
            scintilla.StyleClearAll();
            scintilla.Styles[Style.Xml.Attribute].ForeColor = Color.Red;
            scintilla.Styles[Style.Xml.Entity].ForeColor = Color.Red;
            scintilla.Styles[Style.Xml.Comment].ForeColor = Color.Green;
            scintilla.Styles[Style.Xml.Tag].ForeColor = Color.Blue;
            scintilla.Styles[Style.Xml.TagEnd].ForeColor = Color.Blue;
            scintilla.Styles[Style.Xml.DoubleString].ForeColor = Color.DeepPink;
            scintilla.Styles[Style.Xml.SingleString].ForeColor = Color.DeepPink;

            // Enable folding
            scintilla.SetProperty("fold", "1");
            scintilla.SetProperty("fold.compact", "1");
            scintilla.SetProperty("fold.html", "1");

            scintilla.Margins[2].Type = MarginType.Symbol;
            scintilla.Margins[2].Mask = Marker.MaskFolders;
            scintilla.Margins[2].Sensitive = true;
            scintilla.Margins[2].Width = 20;

            // Reset folder markers
            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                scintilla.Markers[i].SetForeColor(SystemColors.ControlLightLight);
                scintilla.Markers[i].SetBackColor(SystemColors.ControlDark);
            }

            // Style the folder markers
            scintilla.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            scintilla.Markers[Marker.Folder].SetBackColor(SystemColors.ControlText);
            scintilla.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
            scintilla.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            scintilla.Markers[Marker.FolderEnd].SetBackColor(SystemColors.ControlText);
            scintilla.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            scintilla.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            scintilla.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            scintilla.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            scintilla.AutomaticFold = AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change;
        }

        /// <summary>
        ///     Sets the OS version so that we can display appropriate dialogs
        /// </summary>
        private void GetOsVersion()
        {
            try
            {
                // Get Operating system information
                _operatingSystem = Environment.OSVersion;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        /// <summary>
        ///     Set default drop down values
        /// </summary>
        private void SetDefaults()
        {
            _inXsdPath = "";
            _outXsdPath = "";
            _sendFormat = "XML";
            _recFormat = "XML";
            _useDeadLetterQueue = true;
            _timeToReachQueue = 0;
            _timeToReceive = 0;
            _useRecTransaction = true;
            _useSendTransaction = true;
            comboBoxTimeToReceive.SelectedIndex = 0;
            comboBoxTimeReachQueue.SelectedIndex = 0;
            comboBoxSendFormat.SelectedIndex = 0;
            comboBoxRecFormat.SelectedIndex = 0;
            _timeToReachQueuePeriod = "Seconds";
            _timeToRecievePeriod = "Seconds";
            _messageLabel = "";
            _msmqMessaging.RecHostName = _recHostName;
            _msmqMessaging.SendHostName = _sendHostName;
            _selectedTab = "Send";
            _useJournal = false;
            _useEncryption = false;
            _useTracing = false;
            _priority = "Normal";
            comboBoxPriority.SelectedIndex = 5;
            ToggleRibbon();
        }

        private void RefreshQueueLists()
        {
            RefreshRecQueueList();
            RefreshSendQueueList();
        }

        private void RefreshRecQueueList()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                _msmqMessaging.RefreshRecQueueList();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RefreshSendQueueList()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                _msmqMessaging.RefreshSendQueueList();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        ///     Refreshes / populates the Message List
        /// </summary>
        private void RefreshMessageList()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                _msmqMessaging.RefreshRecMessageList(_selectedRecQueueName);
                PopulateMessageList();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void SendMessage()
        {
            const string queuePath = "";
            try
            {
                if (string.IsNullOrEmpty(_selectedSendQueueName))
                {
                    ShowError("Error", Strings.FormMsmqExplorer_SendMessage_Please_select_a_queue_, "");
                }
                else if (_sendMessageByteList == null)
                {
                    ShowError("Error", Strings.FormMsmqExplorer_SendMessage_Please_enter_some_message_text_to_send_, "");
                }
                else
                {
                    // Send the message
                    _msmqMessaging.SendMessage(_selectedSendQueueName, _sendMessageByteList, _messageLabel, _sendFormat,
                        _useSendTransaction, _isSendPrivateQueue, _timeToReachQueue, _timeToReachQueuePeriod,
                        _timeToReceive, _timeToRecievePeriod, _useDeadLetterQueue,
                        _useEncryption, _useTracing, _useJournal, _priority);

                    // Add status
                    DateTime now = DateTime.Now;
                    string messageText = Strings.FormMsmqExplorer_SendMessage_Message_sent_on_queue__ + _selectedSendQueueName + Strings.FormMsmqExplorer_SendMessage__to_host__ +
                                         _sendHostName;
                    textBoxStatus.Text = messageText;
                    AddLogEntry("Send", messageText);

                }
                UpdateMessageCountSend(_selectedSendQueueName);
            }
            catch (Exception ex)
            {
                ShowError(Strings.FormMsmqExplorer_SendMessage_Send_Error,
                    Strings.FormMsmqExplorer_SendMessage_An_error_has_occurred_sending_a_message_to_the_queue__If_the_XML_box_is_checked__is_your_XML_correctly_formed_ +
                    Environment.NewLine + queuePath, ex.ToString());
            }
        }

        /// <summary>
        /// </summary>
        private void ReceiveMessage()
        {
            try
            {
                // Receive message
                bool isJournal = _selectedRecQueueName.IndexOf("Journal", StringComparison.Ordinal) > 0;
                _recMessageByteList = _msmqMessaging.ReceiveMessage(_selectedRecQueueName, isJournal, _recFormat,
                    _useRecTransaction, _isRecPrivateQueue);
                UpdateReceiveTextControls();

                // Refresh the counts and the tree
                UpdateMessageCountRec(_selectedRecQueueName);
                RefreshMessageList();

                // Add status
                DateTime now = DateTime.Now;
                string messageText = Strings.FormMsmqExplorer_ReceiveMessage_Message_received_on_queue__ + _selectedRecQueueName + Strings.FormMsmqExplorer_ReceiveMessage__from_host__ +
                                     _recHostName;
                textBoxStatus.Text = messageText;
                AddLogEntry("Receive", messageText);
            }
            catch (Exception ex)
            {
                ShowError("Receive Error",
                    Strings.FormMsmqExplorer_ReceiveMessage_An_error_has_occurred_while_receiving_a_message_from_the_queue_ + Environment.NewLine + ex.Message,
                    ex.ToString());
            }
        }

        /// <summary>
        ///     Display error dialog via the TaskDialog control
        /// </summary>
        /// <param name="instrText"></param>
        /// <param name="errText"></param>
        /// <param name="exceptionText"></param>
        private void ShowError(string instrText, string errText, string exceptionText)
        {
            AddLogEntry("Error", errText + " " + exceptionText);
            if (_operatingSystem == null || _operatingSystem.Version.Major < 6)
            {
                MessageBox.Show(errText + Environment.NewLine + Environment.NewLine + exceptionText, Strings.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            else
            {
                TaskDialog dialog = new TaskDialog
                {
                    Caption = "Error!",
                    InstructionText = instrText,
                    Icon = TaskDialogStandardIcon.Error,
                    Cancelable = false,
                    Text = errText,
                    DetailsCollapsedLabel = "Show Error Details",
                    DetailsExpandedLabel = "Hide Error Details",
                    DetailsExpandedText = exceptionText
                };
                SystemSounds.Exclamation.Play();
                dialog.Show();
            }
        }

        /// <summary>
        ///     Handle 'Copy' edit function
        /// </summary>
        private void ClipBoardCopy()
        {
            Control control = ActiveControl;

            // Work out if within Split Container or within child Split Container
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }

            if (control.GetType() == typeof (TextBox))
            {
                TextBox textBox = (TextBox) control;
                textBox.Copy();
            }
            else if (control.GetType() == typeof (Scintilla))
            {
                Scintilla scintilla = (Scintilla) control;
                // scintilla.Clipboard.Copy();
            }
            else if (control.GetType() == typeof (ComboBox))
            {
                ComboBox comboBox = (ComboBox) control;
                Clipboard.SetText(comboBox.Text);
            }
        }

        /// <summary>
        ///     Handle 'Cut' edit function
        /// </summary>
        private void ClipBoardCut()
        {
            Control control = ActiveControl;

            // Work out if within Split Container or within child Split Container
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }

            if (control.GetType() == typeof (TextBox))
            {
                TextBox textBox = (TextBox) control;
                textBox.Cut();
            }
            else if (control.GetType() == typeof (Scintilla))
            {
                Scintilla scintilla = (Scintilla) control;
                // scintilla.Clipboard.Cut();
            }
            if (control.GetType() != typeof (ComboBox)) return;
            ComboBox comboBox = (ComboBox) control;
            Clipboard.SetText(comboBox.Text);
            comboBox.Text = string.Empty;
        }

        /// <summary>
        ///     Handle the 'Paste' edit function
        /// </summary>
        private void ClipBoardPaste()
        {
            Control control = ActiveControl;

            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() == typeof (TextBox))
            {
                TextBox textBox = (TextBox) control;
                textBox.Paste();
            }
            else if (control.GetType() == typeof (Scintilla))
            {
                Scintilla scintilla = (Scintilla) control;
                // scintilla.Clipboard.Paste();
            }
            else if (control.GetType() == typeof (ComboBox))
            {
                ComboBox comboBox = (ComboBox) control;
                comboBox.Text = Clipboard.GetText();
            }
        }

        /// <summary>
        ///     Handle the 'Undo' edit function
        /// </summary>
        private void ClipBoardUndo()
        {
            Control control = ActiveControl;

            // Work out if within Split Container or within child Split Container
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }

            if (control.GetType() == typeof (TextBox))
            {
                TextBox textBox = (TextBox) control;
                textBox.Undo();
            }
            else if (control.GetType() == typeof (Scintilla))
            {
                Scintilla scintilla = (Scintilla) control;
                // scintilla.UndoRedo.Undo();
            }
        }

        /// <summary>
        ///     Handle the 'Redo' edit menu item
        /// </summary>
        private void ClipBoardRedo()
        {
            Control control = ActiveControl;

            // Work out if within Split Container or within child Split Container
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() == typeof (SplitContainer))
            {
                SplitContainer splintContainer = (SplitContainer) control;
                control = splintContainer.ActiveControl;
            }
            if (control.GetType() != typeof (Scintilla)) return;
            Scintilla scintilla = (Scintilla) control;
            // scintilla.UndoRedo.Redo();
        }

        private static void ShowAboutBox()
        {
            AboutBox about = new AboutBox();
            about.ShowDialog();
        }

        /// <summary>
        ///     Handle Drag and Drop on the outbound XML control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxOutXML_DragEnter(object sender, DragEventArgs e)
        {
            {
                // If file is dragged, show cursor "Drop allowed"
                e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            }
        }

        /// <summary>
        ///     Handle Drop event on the outbound XML control
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxOutXML_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                Array a = (Array) e.Data.GetData(DataFormats.FileDrop);
                if (a == null) return;
                string fileName = a.GetValue(0).ToString();
                Activate();
                scintillaSendXML.Text = File.ReadAllText(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.DragDropError + ex.Message);
            }
        }

        /// <summary>
        ///     Handle outbound XML validation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonValidateOut_Click(object sender, EventArgs e)
        {
            HandleValidationButton(scintillaSendXML.Text, _outXsdPath);
        }

        /// <summary>
        ///     Handle inbound XML validation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonValidateIn_Click(object sender, EventArgs e)
        {
            HandleValidationButton(scintillaRecXML.Text, _inXsdPath);
        }

        /// <summary>
        ///     Handle the Validate event
        /// </summary>
        /// <param name="xml">Input XML</param>
        /// <param name="xsdFileName">Input XSD</param>
        private static void HandleValidationButton(string xml, string xsdFileName)
        {
            // Validate the XML against the XSD
            if (xsdFileName == string.Empty) return;
            XmlValidation validator = new XmlValidation();
            bool isValid = ValidateXml(validator, xml, xsdFileName);
            ValidateResults results = new ValidateResults(isValid, validator.Errors, validator.Warnings)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            results.ShowDialog();
        }

        /// <summary>
        ///     Wrapper to call the XML validator class
        /// </summary>
        /// <param name="validator"></param>
        /// <param name="xml"></param>
        /// <param name="xsdFileName"></param>
        /// <returns></returns>
        private static bool ValidateXml(XmlValidation validator, string xml, string xsdFileName)
        {
            validator.AddSchema(@xsdFileName);

            // Write current XML to a temp file
            string tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, xml);

            bool isValid = validator.IsValid(@tempFile);
            return isValid;
        }

        /// <summary>
        ///     Handle getting XSD file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonXSDOut_Click(object sender, EventArgs e)
        {
            _outXsdPath = GetXsdFile();
            textBoxXSDOut.Text = Path.GetFileName(_outXsdPath);
        }

        /// <summary>
        ///     Handle getting XSD file
        /// </summary>
        /// <returns></returns>
        private string GetXsdFile()
        {
            // Get XSD schema
            openFileDialog1.Title = Strings.SelectXsd;
            openFileDialog1.Filter = Strings.XsdFilter;
            openFileDialog1.FileName = "";
            openFileDialog1.ShowDialog();
            string fileName = openFileDialog1.FileName;

            return fileName;
        }

        /// <summary>
        ///     Handle getting XSD file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonXSDIn_Click(object sender, EventArgs e)
        {
            _inXsdPath = GetXsdFile();
            textBoxXSDIn.Text = Path.GetFileName(_inXsdPath);
        }

        /// <summary>
        ///     Handle selection of a Queue from the tree view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeViewQueues_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if ((e.Action != TreeViewAction.ByMouse) && (e.Action != TreeViewAction.ByKeyboard)) return;
            if (e.Node.Tag == null) return;
            try
            {
                if (e.Node.Parent.Text == "Public")
                {
                    checkBoxRecTrans.Enabled = true;
                    _isRecPrivateQueue = false;
                }
                else
                {
                    checkBoxRecTrans.Enabled = false;
                    _isRecPrivateQueue = true;
                }

                // Populate the Messages based on the Queue selected in Treeview
                if (e.Node.Tag == null) return;
                // Clear the existing Messages from ListView 
                _selectedRecQueueName = e.Node.Tag.ToString();
                textBoxSelectedRecQueue.Text = _recHostName + "\\" + _selectedRecQueueName;
                RefreshMessageList();
                textBoxQueue.Text = _selectedRecQueueName;
            }
            catch (Exception ex)
            {
                ShowError("Error Querying Queue", "An error has occurred while attempting to query the queue:" +
                                                  Environment.NewLine + ex.Message, ex.ToString());
            }
        }

        /// <summary>
        ///     Populate the Queue Trees
        /// </summary>
        private void PopulateQueueTrees()
        {
            PopulateRecQueueTree();
            PopulateSendQueueTree();
            UpdateAllMessageCounts();
        }

        private void AddQueueNodeWithChildren(BufferedTreeView treeView, TreeNode parentNode, MessageQueue queueItem)
        {
            string queueName = queueItem.QueueName;
            TreeNode mainNode = parentNode.Nodes.Add(Strings.FormMsmqExplorer_AddQueueNodeWithChildren_Processing___);
            mainNode.Tag = queueName;
            mainNode.Name = queueName;
            mainNode.ImageIndex = 1;
            mainNode.SelectedImageIndex = 1;

            // Get Journal Queue
            string journalQueueName = queueName + ";Journal";

            // Add Journal Queue
            TreeNode subNode = mainNode.Nodes.Add(Strings.FormMsmqExplorer_AddQueueNodeWithChildren_Processing___);
            subNode.Tag = journalQueueName;
            subNode.Name = journalQueueName;
            subNode.ImageIndex = 2;
            subNode.SelectedImageIndex = 2;
   
            try
            {
                // Get Poison Queue
                string poisonQueueName = queueName + ";Poison";

                // Add Poison Message Queue
                subNode = mainNode.Nodes.Add(Strings.FormMsmqExplorer_AddQueueNodeWithChildren_Processing___);
                subNode.Tag = poisonQueueName;
                subNode.Name = poisonQueueName;
                subNode.ImageIndex = 3;
                subNode.SelectedImageIndex = 3;

                // Get Retry Queue
                string retryQueueName = queueName + ";Retry";

                // Add Retry Message Queue
                subNode = mainNode.Nodes.Add(Strings.FormMsmqExplorer_AddQueueNodeWithChildren_Processing___);
                subNode.Tag = retryQueueName;
                subNode.Name = retryQueueName;
                subNode.ImageIndex = 4;
                subNode.SelectedImageIndex = 4;
            }
            catch (Exception)
            {
            }
        }

        private void AddQueueNode(BufferedTreeView treeView, TreeNode parentNode, MessageQueue queueItem)
        {
            string queueName = queueItem.QueueName;
            TreeNode tempNode = parentNode.Nodes.Add(Strings.FormMsmqExplorer_AddQueueNodeWithChildren_Processing___);
            tempNode.Tag = queueName;
            tempNode.Name = queueName;
            tempNode.ImageIndex = 1;
            tempNode.SelectedImageIndex = 1;


        }

        private void AddSystemQueueNode(BufferedTreeView treeView, TreeNode parentNode, SystemMessageQueue queueItem)
        {
            string queueName = queueItem.QueueName;
            TreeNode tempNode = parentNode.Nodes.Add(Strings.FormMsmqExplorer_AddQueueNodeWithChildren_Processing___);
            tempNode.Tag = queueName;
            tempNode.Name = queueName;
            tempNode.ImageIndex = 1;
            tempNode.SelectedImageIndex = 1;
        }

        /// <summary>
        ///     Populate a Queue tree based on queue type (Private or Public)
        /// </summary>
        private void PopulateRecQueueTree()
        {
            bufferedTreeViewRecQueues.BeginUpdate();

            // Clear the nodes
            bufferedTreeViewRecQueues.Nodes.Clear();

            // Add the 'Private' root node
            TreeNode privateNode = bufferedTreeViewRecQueues.Nodes.Add("Private");
            privateNode.ImageIndex = 0;
            // Display the paths of the queues in the list.
            foreach (MessageQueue queueItem in _msmqMessaging.RecPrivateQueueList)
            {
                AddQueueNodeWithChildren(bufferedTreeViewRecQueues, privateNode, queueItem);
            }
            privateNode.Expand();
            // Populate Public queues
            // If on a WORKGROUP, this will fail so capture the exception and carry on
            try
            {
                // Add the 'Public' root node
                TreeNode publicNode = bufferedTreeViewRecQueues.Nodes.Add("Public");
                publicNode.ImageIndex = 0;
                // Display the paths of the queues in the list.
                foreach (MessageQueue queueItem in _msmqMessaging.RecPublicQueueList ?? new MessageQueue[] { })
                {
                    AddQueueNodeWithChildren(bufferedTreeViewRecQueues, publicNode, queueItem);
                }
                publicNode.Expand();
            }
            catch (Exception)
            {
            }

            try
            {
                // Add the 'System' root node
                TreeNode systemNode = bufferedTreeViewRecQueues.Nodes.Add("System");
                systemNode.ImageIndex = 0;
                // Display the paths of the queues in the list.
                foreach (SystemMessageQueue queueItem in _msmqMessaging.RecSystemQueueList)
                {
                    AddSystemQueueNode(bufferedTreeViewRecQueues, systemNode, queueItem);
                }
                systemNode.Expand();
            }
            catch (Exception)
            {
            }
            bufferedTreeViewRecQueues.Sort();
            bufferedTreeViewRecQueues.EndUpdate();
        }

        /// <summary>
        ///     Populate a Queue tree based on queue type (Private or Public)
        /// </summary>
        private void PopulateSendQueueTree()
        {
            bufferedTreeViewSendQueues.BeginUpdate();

            // Clear the nodes
            bufferedTreeViewSendQueues.Nodes.Clear();

            // Add the 'Private' root node
            TreeNode privateNode = bufferedTreeViewSendQueues.Nodes.Add("Private");
            privateNode.ImageIndex = 0;

            // Display the paths of the queues in the list.
            foreach (MessageQueue queueItem in _msmqMessaging.SendPrivateQueueList)
            {
                AddQueueNode(bufferedTreeViewSendQueues, privateNode, queueItem);
            }
            // Populate Public queues
            // If on a WORKGROUP, this will fail so capture the exception and carry on
            try
            {
                // Add the 'Public' root node
                TreeNode publicNode = bufferedTreeViewSendQueues.Nodes.Add("Public");
                publicNode.ImageIndex = 0;
                // Display the paths of the queues in the list.
                foreach (MessageQueue queueItem in _msmqMessaging.SendPublicQueueList ?? new MessageQueue[] { })
                {
                    AddQueueNode(bufferedTreeViewSendQueues, publicNode, queueItem);
                }
            }
            catch (Exception)
            {
            }
            bufferedTreeViewSendQueues.ExpandAll();
            bufferedTreeViewSendQueues.Sort();
            bufferedTreeViewSendQueues.EndUpdate();
        }

        /// <summary>
        ///     Populates the list of messages on a specific queue
        /// </summary>
        private void PopulateMessageList()
        {
            // Populate the FastObjectListView
            fastObjectListViewMessages.SetObjects(_msmqMessaging.RecMessageList);
        }

        /// <summary>
        /// </summary>
        private void PreviewMessage()
        {
            try
            {
                _recMessageByteList = _msmqMessaging.PreviewMessage(_recFormat, _selectedMessageId);
                UpdateReceiveTextControls();
                AddLogEntry("Preview", Strings.FormMsmqExplorer_PreviewMessage_Previewed_message_from_queue__ + _selectedRecQueueName + Strings.FormMsmqExplorer_PurgeQueue__on_host__ + _recHostName);
            }
            catch (Exception ex)
            {
                ShowError("Error",
                    Strings.FormMsmqExplorer_PreviewMessage_An_error_occurred_while_attempting_to_preview_the_selected_message_ + Environment.NewLine +
                    ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        /// </summary>
        private void UpdateReceiveTextControls()
        {
            string messageText = StringTools.GetStringFromByteList(_recMessageByteList);
            scintillaRecXML.Text = (_recFormat == "XML") ? StringTools.PrettyXml(messageText) : messageText;
            textBoxRecText.Text = messageText;
            _recMessageByteProvider = new DynamicByteProvider(StringTools.GetByteListFromString(messageText));
            hexBoxRecHex.ByteProvider = _recMessageByteProvider;
        }

        /// <summary>
        /// </summary>
        private void DeleteMessage()
        {
            try
            {
                // Confirm with user before progressing
                DialogResult confirmResult = MessageBox.Show(Strings.ConfirmDeleteMessage, Strings.ConfirmDelete,
                    MessageBoxButtons.YesNo);
                if (confirmResult != DialogResult.Yes) return;
                _msmqMessaging.DeleteMessage(_selectedRecQueueName, _selectedMessageId);
                UpdateMessageCountRec(_selectedRecQueueName);
                RefreshMessageList();
                AddLogEntry("Deleted", Strings.FormMsmqExplorer_DeleteMessage_Message_deleted_from_queue__ + _selectedRecQueueName + Strings.FormMsmqExplorer_PurgeQueue__on_host__ + _recHostName);
            }
            catch
                (Exception ex)
            {
                ShowError("Error",
                    Strings.FormMsmqExplorer_DeleteMessage_An_error_occurred_while_attempting_to_delete_the_selected_message_ + Environment.NewLine +
                    ex.Message, ex.StackTrace);
            }
        }

        /// <summary>
        ///     Add a grab handle to the split container
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void splitContainerReceive_Paint(object sender, PaintEventArgs e)
        {
            PaintDots(sender, e);
        }

        /// <summary>
        ///     Add a grab handle to the split container
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void splitContainerDetail_Paint(object sender, PaintEventArgs e)
        {
            PaintDots(sender, e);
        }

        /// <summary>
        ///     Paint the dots on separator
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void PaintDots(object sender, PaintEventArgs e)
        {
            SplitContainer control = sender as SplitContainer;
            if (control == null) return;

            // Paint the three dots'
            Point[] points = new Point[3];
            int w = control.Width;
            int h = control.Height;
            int d = control.SplitterDistance;
            int sW = control.SplitterWidth;

            // Calculate the position of the points
            if (control.Orientation == Orientation.Horizontal)
            {
                points[0] = new Point((w/2), d + (sW/2));
                points[1] = new Point(points[0].X - 10, points[0].Y);
                points[2] = new Point(points[0].X + 10, points[0].Y);
            }
            else
            {
                points[0] = new Point(d + (sW/2), (h/2));
                points[1] = new Point(points[0].X, points[0].Y - 10);
                points[2] = new Point(points[0].X, points[0].Y + 10);
            }

            foreach (Point p in points)
            {
                p.Offset(-2, -2);
                e.Graphics.FillEllipse(SystemBrushes.ControlDark,
                    new Rectangle(p, new Size(3, 3)));

                p.Offset(1, 1);
                e.Graphics.FillEllipse(SystemBrushes.ControlLight,
                    new Rectangle(p, new Size(3, 3)));
            }
        }

        /// <summary>
        ///     Handle Load event routines
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMSMQTestHarness_Shown(object sender, EventArgs e)
        {
            Application.DoEvents();
            if (!_isMsmqInstalled)
            {
                MqNotInstalled();
            }
            DoubleBuffered = true;
            GetOsVersion();
            _recHostName = textBoxRecHost.Text;
            _sendHostName = textBoxSendHost.Text;

            SetDefaults();

            // Populate queues
            RefreshQueueLists();
            PopulateQueueTrees();
        }

        /// <summary>
        ///     Hanbdle clicking of the 'Refresh' button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRefreshMessages_Click(object sender, EventArgs e)
        {
            RefreshMessageList();
        }

        /// <summary>
        ///     Handle change to the 'Host' text box in the Send panel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxSendHost_TextChanged(object sender, EventArgs e)
        {
            // _sendHostName = textBoxSendHost.Text;
        }

        /// <summary>
        ///     Saves settings into project Settings object
        /// </summary>
        private void SaveSettings()
        {
            // Save Window settings
            Settings.Default.WindowHeight = Size.Height;
            Settings.Default.WindowWidth = Size.Width;
            Settings.Default.WindowX = Location.X;
            Settings.Default.WindowY = Location.Y;

            // Save Window Split Settings
            Settings.Default.MainWindowSplit = splitContainerReceive.SplitterDistance;
            Settings.Default.SubWindowSplit = splitContainerDetail.SplitterDistance;

            // Save Column settings for Message List View
            StringCollection settings = new StringCollection();

            /* for (int colNum = 0; colNum < listViewMessages.Columns.Count; colNum++)
            {
                settings.Add(listViewMessages.Columns[colNum].Width.ToString(CultureInfo.InvariantCulture));
                settings.Add(listViewMessages.Columns[colNum].DisplayIndex.ToString(CultureInfo.InvariantCulture));
            } */

            Settings.Default.ExplorerViewSettings = settings;

            // Save 'Send' options
            Settings.Default.SendHostName = _sendHostName;
            // Settings.Default.SendXML = _isOutXml;

            // Save 'Receive' options
            Settings.Default.RecHostNAme = _recHostName;
            // Settings.Default.RecXML = _isInXml;

            // Save the Office Style
            Settings.Default.OfficeStyle = _orbStyle;
            Settings.Default.OfficeStyleIndex = _orbStyleIndex;

            // Save the Ribbon Theme
            Settings.Default.ThemeColour = _orbTheme;
            Settings.Default.ThemeIndex = _orbThemeIndex;

            // Save 'Settings'
            Settings.Default.AutoUpdateCount = _autoUpdateCount;
            Settings.Default.AutoUpdateCountInterval = _autoUpdateCountInterval;
            Settings.Default.AutoUpdateQueue = _autoUpdateQueue;
            Settings.Default.AutoUpdateQueueInterval = _autoUpdateQueueInterval;
            Settings.Default.ShowUpdateTimestamp = _showUpdatedTimestamp;

            // Save the settings
            Settings.Default.Save();
        }

        /// <summary>
        ///     Loads settings
        /// </summary>
        private void LoadSettings()
        {
            // Load Window settings
            // Height = Settings.Default.WindowHeight;
            // Width = Settings.Default.WindowWidth;
            // Location = new Point(Settings.Default.WindowX, Settings.Default.WindowY);

            // Load Split settings
            // splitContainerReceive.SplitterDistance = Settings.Default.MainWindowSplit;
            // splitContainerDetail.SplitterDistance = Settings.Default.SubWindowSplit;

            // Load the Column settings for the Message View
            /* StringCollection settings = Settings.Default.ExplorerViewSettings;
            if (settings != null && settings.Count != 0)
            {
                for (int colNum = 0; colNum < settings.Count/2; colNum++)
                {
                    int width = Convert.ToInt32(settings[colNum*2]);
                    int displayIndex = Convert.ToInt32(settings[colNum*2 + 1]);
                    listViewMessages.Columns[colNum].Width = width;
                    listViewMessages.Columns[colNum].DisplayIndex = displayIndex;
                }
            }*/

            // Load 'Send' options
            // textBoxSendHost.Text = Settings.Default.SendHostName;
            // textBoxXSDOut.Text = Settings.Default.SendValidateXSD;
            // checkBoxXMLOut.Checked = Settings.Default.SendXML;

            // Load 'Receive' options
            // textBoxRecHost.Text = Settings.Default.RecHostNAme;
            // textBoxXSDIn.Text = Settings.Default.RecValidateXSD;
            // checkBoxXMLIn.Checked = Settings.Default.RecXML;

            // Upgrade settings if new version
            if (Settings.Default.CallUpgrade)
            {
                Settings.Default.Upgrade();
                Settings.Default.CallUpgrade = false;
            }

            // Load Orb Style
            _orbStyle = Settings.Default.OfficeStyle;
            _orbStyleIndex = Settings.Default.OfficeStyleIndex;

            ribbonMain.OrbStyle = _orbStyle;
            ribbonButtonStyle.DropDownItems[_orbStyleIndex].Checked = true;

            _orbTheme = Settings.Default.ThemeColour;
            _orbThemeIndex = Settings.Default.ThemeIndex;

            Theme.ThemeColor = _orbTheme;
            ribbonMain.Refresh();
            ribbonButtonTheme.DropDownItems[_orbThemeIndex].Checked = true;

            // Load 'Settings'
            _autoUpdateCount = Settings.Default.AutoUpdateCount;
            _autoUpdateCountInterval = Settings.Default.AutoUpdateCountInterval;
            _autoUpdateQueue = Settings.Default.AutoUpdateQueue;
            _autoUpdateQueueInterval = Settings.Default.AutoUpdateQueueInterval;
            _showUpdatedTimestamp = Settings.Default.ShowUpdateTimestamp;
        }

        /// <summary>
        ///     Handle closing of the application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMSMQExplorer_FormClosed(object sender, FormClosedEventArgs e)
        {
            // Save current settings
            SaveSettings();
        }

        /// <summary>
        ///     Refreshes the Receive controls
        /// </summary>
        private void RefreshRecControls()
        {
            string currentHost = _msmqMessaging.RecHostName;

            try
            {
                // Set cursor as hourglass
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();

                // Get the proposed host name
                string newHostName = textBoxRecHost.Text;

                _msmqMessaging.RecHostName = newHostName;
                RefreshRecQueueList();
                PopulateRecQueueTree();
                _recHostName = newHostName;
            }
            catch (Exception ex)
            {
                ShowError(Strings.FormMsmqExplorer_RefreshSendControls_Connect_Error,
                    Strings.FormMsmqExplorer_RefreshRecControls_An_error_has_occurred_while_refreshing_the_queues_from_the_specified_host__ + Environment.NewLine +
                    ex.Message, ex.ToString());
                _msmqMessaging.RecHostName = currentHost;
            }
            finally
            {
                // Set cursor as default arrow
                Cursor = Cursors.Default;
            }
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            // Pause the timer
            _countTimer.Stop();
            _queueTimer.Stop();

            RefreshRecControls();
            UpdateAllMessageCounts();

            // Restart the timer
            _countTimer.Start();
            _queueTimer.Start();
        }

        private void RefreshSendControls()
        {
            string currentHost = _msmqMessaging.SendHostName;

            try
            {
                // Set cursor as hourglass
                Cursor = Cursors.WaitCursor;
                Application.DoEvents();

                // Get the proposed host name
                string newHostName = textBoxSendHost.Text;

                _msmqMessaging.SendHostName = newHostName;
                RefreshSendQueueList();
                PopulateSendQueueTree();
                _sendHostName = newHostName;
            }
            catch (Exception ex)
            {
                ShowError(Strings.FormMsmqExplorer_RefreshSendControls_Connect_Error,
                    Strings.FormMsmqExplorer_RefreshRecControls_An_error_has_occurred_while_refreshing_the_queues_from_the_specified_host__ + Environment.NewLine +
                    ex.Message, ex.ToString());
                _msmqMessaging.SendHostName = currentHost;
            }
            finally
            {
                // Set cursor as default arrow
                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        ///     Handling clicking of the 'Connect' button in the Send panel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSendConnect_Click(object sender, EventArgs e)
        {
            // Pause the timer
            _countTimer.Stop();
            _queueTimer.Stop();

            RefreshSendControls();
            UpdateAllMessageCounts();

            // Restart the timer
            _countTimer.Start();
            _queueTimer.Start();
        }

        private void CopyMessage()
        {
            _sendMessageByteList = _msmqMessaging.PreviewMessage(_recFormat, _selectedMessageId);

            // Refresh the Send tabs
            RefreshSendMessageControls(false);

            // Open the Send tab
            tabControlMain.SelectedIndex = 0;
        }

        private void OpenXmlFile()
        {
            // Open a File Open dialog and load in the contents
            openFileDialog1.Title = Strings.SelectXml;
            openFileDialog1.Filter = Strings.XmlFilter;
            openFileDialog1.FileName = "";
            openFileDialog1.ShowDialog();
            string fileName = openFileDialog1.FileName;
            if (fileName == "" || !File.Exists(fileName)) return;
            // scintillaSendXML.Text = File.ReadAllText(fileName);
            _sendMessageByteList = StringTools.GetByteListFromString(File.ReadAllText(fileName));
            RefreshSendMessageControls(false);
        }

        private void RefreshSendMessageControls(bool isBinary)
        {
            _sendMessageByteProvider = new DynamicByteProvider(_sendMessageByteList);
            hexBoxSendHex.ByteProvider = _sendMessageByteProvider;
            hexBoxSendHex.Refresh();
            if (isBinary) return;
            scintillaSendXML.Text = StringTools.GetStringFromByteList(_sendMessageByteList);
            textBoxSendText.Text = StringTools.GetStringFromByteList(_sendMessageByteList);
        }

        /// <summary>
        ///     Dialog to show when MSMQ is not detected
        /// </summary>
        private static void MqNotInstalled()
        {
            MessageBox.Show(Strings.MsmqNotInstalledMessage, Strings.MsmqNotInstalled);
            Application.Exit();
        }

        /// <summary>
        ///     Updates the message counts for a given queue and host in the treeViewQueues control
        /// </summary>
        /// <param name="queueName"></param>
        private void UpdateMessageCountSend(string queueName)
        {
            // Get the new count from the queue
            UpdateMessageCountControl(queueName, bufferedTreeViewSendQueues, _sendHostName);
            if (_sendHostName == _recHostName)
            {
                UpdateMessageCountControl(queueName, bufferedTreeViewRecQueues, _recHostName);
            }
        }

        private void UpdateMessageCountRec(string queueName)
        {
            // Get the new count from the queue
            UpdateMessageCountControl(queueName, bufferedTreeViewRecQueues, _recHostName);
            if (_sendHostName == _recHostName)
            {
                UpdateMessageCountControl(queueName, bufferedTreeViewSendQueues, _sendHostName);
            }
        }

        private void UpdateMessageCount(string queueName, string hostName)
        {
            // Get the new count from the queue
            UpdateMessageCountControl(queueName, bufferedTreeViewRecQueues, hostName);
        }

        /// <summary>
        ///     Update Message Count in given control
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="treeView"></param>
        private void UpdateMessageCountControl(string queueName, BufferedTreeView treeView, string hostName)
        {
            // Update the tree view
            TreeNode[] nodes = treeView.Nodes.Find(queueName, true);
            if (!nodes.Any()) return;
            TreeNode node = nodes[0];
            int messageCount = _msmqMessaging.GetMessageCount(queueName, hostName);
            UpdateTreeNodeCount(node, messageCount);
            // Update child counts
            foreach (TreeNode childNode in node.Nodes)
            {
                UpdateMessageCount(childNode.Tag.ToString(), hostName);
            }
        }

        private void UpdateTreeNodeCount(TreeNode node, int count)
        {
            string queueName = node.Tag.ToString();
            UpdateNodeText(node, MsmqMessaging.GetNiceQueueName(queueName) + " (" + count + ")");
            // Highlight inaccessible queues
            if (count == -1)
            {
                node.ForeColor = Color.Red;
            }
        }

        /// <summary>
        ///     Updates all message counts for a given host in the treeViewQueues control
        /// </summary>
        private void UpdateAllMessageCounts()
        {
            UpdateAllMessagesInTreeViewNodes(bufferedTreeViewRecQueues.Nodes, _recHostName);
            UpdateAllMessagesInTreeViewNodes(bufferedTreeViewSendQueues.Nodes, _sendHostName);
        }

        delegate void UpdateNodeTextCallback(TreeNode node, string text);

        private void UpdateNodeText(TreeNode node, string text)
        {
            if (_showUpdatedTimestamp)
                node.Text = text + " (updated: " + DateTime.Now.ToString() + ")";
            else
            {
                node.Text = text;
            }
        }

        /// <summary>
        ///     Recursive function to update queue counts in all nodes and child node in the given TreeNodeCollection
        /// </summary>
        /// <param name="nodes"></param>
        private void UpdateAllMessagesInTreeViewNodes(IEnumerable nodes, string hostName)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag != null)
                {
                    string queueName = node.Tag.ToString();
                    int messageCount = _msmqMessaging.GetMessageCount(queueName, hostName);
                    string newText = MsmqMessaging.GetNiceQueueName(queueName) + " (" + messageCount + ")";
                    if (this.InvokeRequired)
                    {
                        UpdateNodeTextCallback setNodeCallBack = new UpdateNodeTextCallback(UpdateNodeText);
                        this.Invoke(setNodeCallBack, new object[] { node, newText });
                    }
                    else
                    {
                        UpdateNodeText(node, newText);
                    }
                }
                UpdateAllMessagesInTreeViewNodes(node.Nodes, hostName);
            }
        }

        /// <summary>
        ///     Display the Scintilla ShowFind dialog
        /// </summary>
        private void ShowFind()
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                _findReplaceSend.ShowFind();
            }
            else
            {
                _findReplaceRec.ShowFind();
            }
        }

        /// <summary>
        ///     Display the Scintilla ShowFind / ShowReplace dialog
        /// </summary>
        private void ShowReplace()
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                _findReplaceSend.ShowReplace();
            }
            else
            {
                _findReplaceRec.ShowReplace();
            }
        }

        /// <summary>
        ///     Handles clicking the 'Refresh' button in the Receive panel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxRecHost_TextChanged(object sender, EventArgs e)
        {
            _recHostName = textBoxRecHost.Text;
        }

        /// <summary>
        /// </summary>
        private void PurgeRecQueue()
        {
            PurgeQueue(_recHostName, _selectedRecQueueName);
        }

        /// <summary>
        /// </summary>
        private void PurgeSendQueue()
        {
            PurgeQueue(_sendHostName, _selectedSendQueueName);
        }

        /// <summary>
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="queueName"></param>
        private void PurgeQueue(string hostName, string queueName)
        {
            // Confirm with user before progressing
            DialogResult confirmResult =
                MessageBox.Show(Strings.ConfirmPurgeMessage + Environment.NewLine + Strings.Host + hostName +
                                Environment.NewLine + Strings.Queue + queueName, Strings.ConfirmPurge,
                    MessageBoxButtons.YesNo);
            if (confirmResult != DialogResult.Yes) return;
            _msmqMessaging.PurgeQueue(hostName, queueName);
            UpdateMessageCountRec(queueName);
            if (queueName == _selectedRecQueueName)
            {
                RefreshMessageList();
            }
            AddLogEntry(Strings.FormMsmqExplorer_PurgeQueue_Purge, Strings.FormMsmqExplorer_PurgeQueue_Purged_queue_ + queueName + Strings.FormMsmqExplorer_PurgeQueue__on_host__ + hostName);
        }

        /// <summary>
        ///     Load default settings, including window size and position
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FormMsmqExplorer_Load(object sender, EventArgs e)
        {
            // Use non-PNG icon for Windows Server 2003 / XP
            // Icon = _operatingSystem.Version.Major >= 6 ? Resource.App_Icon_48 : Resource.App_Icon_32_XP;
            Icon = Resource.MSMQ_mail_internet;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void treeViewQueues_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            TreeNode node = bufferedTreeViewRecQueues.GetNodeAt(e.X, e.Y);
            if (node == null) return;
            bufferedTreeViewRecQueues.SelectedNode = node;
            object tag = bufferedTreeViewRecQueues.SelectedNode.Tag;

            if (tag == null) return;
            _selectedRecQueueName = bufferedTreeViewRecQueues.SelectedNode.Tag.ToString();
            textBoxSelectedRecQueue.Text = _selectedRecQueueName;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemMsgDelete_Click(object sender, EventArgs e)
        {
            DeleteMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemMsgPreview_Click(object sender, EventArgs e)
        {
            PreviewMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="hostName"></param>
        /// <returns></returns>
        private MessageQueue CreateQueue(string hostName)
        {
            NewQueueForm newQueueForm = new NewQueueForm(hostName) {StartPosition = FormStartPosition.CenterParent};
            DialogResult result = newQueueForm.ShowDialog();

            if (result != DialogResult.OK) return null;
            try
            {
                string queueType = newQueueForm.QueueType;
                string queueName = newQueueForm.QueueName;
                bool isTransactional = newQueueForm.IsTransaction;

                MessageQueue queue = _msmqMessaging.CreateQueue(hostName, queueType, queueName, isTransactional);
                return queue;
            }
            catch (Exception ex)
            {
                ShowError(Strings.FormMsmqExplorer_CreateQueue_Failed, Strings.FormMsmqExplorer_CreateQueue_Failed_to_create_the_new_queue_ + Environment.NewLine + ex.Message,
                    ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// </summary>
        private void DeleteRecQueue()
        {
            if (!DeleteQueue(_recHostName, _selectedRecQueueName)) return;
            DeleteTreeNode(_selectedRecQueueName, bufferedTreeViewRecQueues);
            DeleteTreeNode(_selectedRecQueueName, bufferedTreeViewSendQueues);
            if (_selectedSendQueueName == _selectedRecQueueName)
                _selectedSendQueueName = "";
            _selectedRecQueueName = "";
            RefreshRecQueueList();
        }

        /// <summary>
        /// </summary>
        /// <param name="hostName"></param>
        /// <param name="queueName"></param>
        /// <returns></returns>
        private bool DeleteQueue(string hostName, string queueName)
        {
            if (string.IsNullOrEmpty(queueName) || string.IsNullOrEmpty(hostName))
            {
                return false;
            }
            // Check we're not trying to delete a system queue
            int subQueueDivider = queueName.IndexOf(";", StringComparison.Ordinal);
            if (subQueueDivider > 0)
            {
                ShowError(Strings.FormMsmqExplorer_DeleteQueue_Cannot_delete_a_system_subqueue_, "", "");
                return false;
            }

            // Confirm with user before progressing
            DialogResult confirmResult =
                MessageBox.Show(Strings.ConfirmQueueDeleteMessage + Environment.NewLine + Strings.Host + hostName +
                                Environment.NewLine + Strings.Queue + queueName,
                    Strings.ConfirmQueueDelete,
                    MessageBoxButtons.YesNo);
            if (confirmResult != DialogResult.Yes) return false;
            _msmqMessaging.DeleteQueue(hostName, queueName);
            return true;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bufferedTreeViewSendQueues_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if ((e.Action != TreeViewAction.ByMouse) && (e.Action != TreeViewAction.ByKeyboard)) return;
            // Populate the Messages based on the Queue selected in Treeview
            if (e.Node.Tag == null) return;
            _selectedSendQueueName = e.Node.Tag.ToString();

            textBoxSelectedSendQueue.Text = _sendHostName + "\\" + _selectedSendQueueName;

            if (e.Node.Parent.Text == "Public")
            {
                checkBoxTrans.Enabled = true;
                _isSendPrivateQueue = false;
            }
            else
            {
                checkBoxTrans.Enabled = false;
                _isSendPrivateQueue = true;
            }
        }

        private void DeleteSendQueue()
        {
            if (!DeleteQueue(_sendHostName, _selectedSendQueueName)) return;
            RefreshSendQueueList();
            DeleteTreeNode(_selectedSendQueueName, bufferedTreeViewRecQueues);
            DeleteTreeNode(_selectedSendQueueName, bufferedTreeViewSendQueues);
            if (_selectedRecQueueName == _selectedSendQueueName)
                _selectedRecQueueName = "";
            _selectedSendQueueName = "";
        }

        /// <summary>
        ///     Removes the specified queue from the TreeView
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="treeView"></param>
        private void DeleteTreeNode(string queueName, BufferedTreeView treeView)
        {
            // Look in Private
            TreeNode privateNode = treeView.Nodes[0];
            // Look in Public
            TreeNode node = privateNode.Nodes.Cast<TreeNode>().FirstOrDefault(x => x.Tag.ToString() == queueName);
            if (node != null)
            {
                treeView.Nodes.Remove(node);
            }

            // Look in Public
            TreeNode publicNode = treeView.Nodes[0];
            // Look in Public
            node = publicNode.Nodes.Cast<TreeNode>().FirstOrDefault(x => x.Tag.ToString() == queueName);
            if (node != null)
            {
                treeView.Nodes.Remove(node);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void splitContainerSend_Paint(object sender, PaintEventArgs e)
        {
            PaintDots(sender, e);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bufferedTreeViewSendQueues_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            TreeNode node = bufferedTreeViewSendQueues.GetNodeAt(e.X, e.Y);
            if (node == null) return;
            bufferedTreeViewSendQueues.SelectedNode = node;
            object tag = bufferedTreeViewSendQueues.SelectedNode.Tag;

            if (tag == null) return;
            _selectedSendQueueName = bufferedTreeViewSendQueues.SelectedNode.Tag.ToString();
            textBoxSelectedSendQueue.Text = _selectedSendQueueName;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemQueuePurge_Click(object sender, EventArgs e)
        {
            PurgeQueue(_recHostName, _selectedRecQueueName);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemQueueReceive_Click(object sender, EventArgs e)
        {
            ReceiveMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemPurge_Click(object sender, EventArgs e)
        {
            PurgeQueue(_sendHostName, _selectedSendQueueName);
        }

        /// <summary>
        /// </summary>
        private void SaveXml()
        {
            SaveXmlToFile(tabControlMain.SelectedIndex == 0 ? scintillaSendXML.Text : scintillaRecXML.Text);
        }

        /// <summary>
        /// </summary>
        /// <param name="xml"></param>
        private void SaveXmlToFile(string xml)
        {
            saveFileDialog.Filter = Strings.XmlFilter;
            saveFileDialog.Title = Strings.SaveAsXml;
            saveFileDialog.FileName = "";
            saveFileDialog.ShowDialog();
            string fileName = saveFileDialog.FileName;

            if (!string.IsNullOrEmpty(fileName))
            {
                File.WriteAllText(fileName, xml);
            }
        }

        /// <summary>
        /// </summary>
        private void FormatXml()
        {
            try
            {
                if (tabControlMain.SelectedIndex == 0)
                {
                    scintillaSendXML.Text = StringTools.PrettyXml(scintillaSendXML.Text);
                }
                else
                {
                    scintillaRecXML.Text = StringTools.PrettyXml(scintillaRecXML.Text);
                }
            }
            catch (Exception ex)
            {
                ShowError(Strings.FormMsmqExplorer_FormatXml_Error_formatting_XML,
                    Strings.FormMsmqExplorer_FormatXml_An_error_has_occurred_while_formatting_your_XML__Please_ensure_it_is_valid_, ex.StackTrace);
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxTrans_CheckedChanged(object sender, EventArgs e)
        {
            _useSendTransaction = checkBoxTrans.Checked;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxRecTrans_CheckedChanged(object sender, EventArgs e)
        {
            _useRecTransaction = checkBoxRecTrans.Checked;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemMsgCopy_Click(object sender, EventArgs e)
        {
            CopyMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDownTimeToReceive_ValueChanged(object sender, EventArgs e)
        {
            _timeToReceive = Convert.ToInt32(numericUpDownTimeToReceive.Value);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void numericUpDownTimeToReachQueue_ValueChanged(object sender, EventArgs e)
        {
            _timeToReachQueue = Convert.ToInt32(numericUpDownTimeToReachQueue.Value);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxTimeToReceive_SelectedIndexChanged(object sender, EventArgs e)
        {
            _timeToRecievePeriod = comboBoxTimeToReceive.Text;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxTimeReachQueue_SelectedIndexChanged(object sender, EventArgs e)
        {
            _timeToReachQueuePeriod = comboBoxTimeReachQueue.Text;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxMsgLabel_TextChanged(object sender, EventArgs e)
        {
            _messageLabel = textBoxMsgLabel.Text;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxUserDeadLetter_CheckedChanged(object sender, EventArgs e)
        {
            _useDeadLetterQueue = checkBoxUserDeadLetter.Checked;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControlReceiveType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Update other tabs, if required
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBoxSendText_TextChanged(object sender, EventArgs e)
        {
            _sendMessageByteList = StringTools.GetByteListFromString(textBoxSendText.Text);
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void scintillaSendXML_TextChanged(object sender, EventArgs e)
        {
            _sendMessageByteList = StringTools.GetByteListFromString(scintillaSendXML.Text);
        }

        /// <summary>
        /// </summary>
        private void OpenOtherFile()
        {
            // Open a File Open dialog and load in the contents
            openFileDialog1.Title = Strings.SelectAllFile;
            openFileDialog1.Filter = Strings.AllFilesFilter;
            openFileDialog1.FileName = "";
            openFileDialog1.ShowDialog();
            string fileName = openFileDialog1.FileName;
            if (fileName == "" || !File.Exists(fileName)) return;
            // tabControlSendType.SelectedTab = tabPageHex;
            tabControlSendType.SelectedIndex = 1;
            BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open));
            _sendMessageByteList = reader.ReadAllBytes().ToList();
            reader.Close();
            RefreshSendMessageControls(true);
        }

        /// <summary>
        /// </summary>
        private void SaveOther()
        {
            SaveToOtherFile(tabControlMain.SelectedIndex == 0 ? _sendMessageByteList : _recMessageByteList);
        }

        /// <summary>
        /// </summary>
        /// <param name="messageByteList"></param>
        private void SaveToOtherFile(List<byte> messageByteList)
        {
            saveFileDialog.Filter = Strings.AllFilesFilter;
            saveFileDialog.Title = Strings.SaveAsOther;
            saveFileDialog.FileName = "";
            saveFileDialog.ShowDialog();
            string fileName = saveFileDialog.FileName;
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }
            BinaryWriter writer = new BinaryWriter(File.Open(fileName, FileMode.Create));

            if (string.IsNullOrEmpty(fileName)) return;
            writer.Write(messageByteList.ToArray());
            writer.Flush();
            writer.Close();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fastObjectListViewMessages_DoubleClick(object sender, EventArgs e)
        {
            textBoxMsgId.Text = _selectedMessageId;
            PreviewMessage();
        }

        /// <summary>
        /// </summary>
        private void ShowQueueProperties()
        {
            MessageQueue queue;
            if (_selectedTab == "Send")
            {
                if (string.IsNullOrEmpty(_selectedSendQueueName) || _selectedSendQueueName.Contains(";"))
                {
                    return;
                }
                queue = _msmqMessaging.GetQueueByName(_sendHostName, _selectedSendQueueName);
            }
            else
            {
                if (string.IsNullOrEmpty(_selectedRecQueueName) || _selectedRecQueueName.Contains(";"))
                {
                    return;
                }
                queue = _msmqMessaging.GetQueueByName(_recHostName, _selectedRecQueueName);
            }

            FormQueueProperties queueProperties = new FormQueueProperties(queue)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            queueProperties.ShowDialog();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonQueueNew_Click(object sender, EventArgs e)
        {
            NewQueue();
        }

        private void NewQueue()
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                NewSendQueue();
            }
            else
            {
                NewRecQueue();
            }
        }

        private void NewSendQueue()
        {
            MessageQueue queue = CreateQueue(_sendHostName);
            if (queue == null) return;
            string queueName = queue.QueueName;
            RefreshSendQueueList();
            if (queueName.StartsWith("private$"))
            {
                AddQueueNode(bufferedTreeViewSendQueues, bufferedTreeViewSendQueues.Nodes[0], queue);
                if (_sendHostName == _recHostName)
                {
                    AddQueueNodeWithChildren(bufferedTreeViewRecQueues, bufferedTreeViewRecQueues.Nodes[0], queue);
                    UpdateMessageCountRec(queueName);

                }
            }
            else
            {
                AddQueueNode(bufferedTreeViewSendQueues, bufferedTreeViewSendQueues.Nodes[1], queue);
                if (_sendHostName == _recHostName)
                {
                    AddQueueNodeWithChildren(bufferedTreeViewRecQueues, bufferedTreeViewRecQueues.Nodes[1], queue);
                    UpdateMessageCountRec(queueName);
                }
            }
            UpdateMessageCountSend(queueName);
        }

        private void NewRecQueue()
        {
            MessageQueue queue = CreateQueue(_recHostName);
            if (queue == null) return;
            string queueName = queue.QueueName;
            RefreshRecQueueList();
            if (queueName.StartsWith("private$"))
            {
                AddQueueNodeWithChildren(bufferedTreeViewRecQueues, bufferedTreeViewRecQueues.Nodes[0], queue);
                if (_sendHostName == _recHostName)
                {
                    AddQueueNode(bufferedTreeViewSendQueues, bufferedTreeViewSendQueues.Nodes[0], queue);
                    UpdateMessageCountSend(queueName);
                }
            }
            else
            {
                AddQueueNodeWithChildren(bufferedTreeViewRecQueues, bufferedTreeViewRecQueues.Nodes[1], queue);
                if (_sendHostName == _recHostName)
                {
                    AddQueueNode(bufferedTreeViewSendQueues, bufferedTreeViewSendQueues.Nodes[1], queue);
                    UpdateMessageCountSend(queueName);

                }
            }
            UpdateMessageCountRec(queueName);
        }

        /// <summary>
        /// </summary>
        private void DeleteQueue()
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                DeleteSendQueue();
            }
            else
            {
                DeleteRecQueue();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonQueueDelete_Click(object sender, EventArgs e)
        {
            DeleteQueue();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonQueuePurge_Click(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                PurgeSendQueue();
            }
            else
            {
                PurgeRecQueue();
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonMsgDelete_Click(object sender, EventArgs e)
        {
            DeleteMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonMsgPreview_Click(object sender, EventArgs e)
        {
            PreviewMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonMsgReceive_Click(object sender, EventArgs e)
        {
            ReceiveMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonMsgCopy_Click(object sender, EventArgs e)
        {
            CopyMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonAbout_Click(object sender, EventArgs e)
        {
            ShowAboutBox();
        }

        /// <summary>
        /// </summary>
        private void UpdateApplication()
        {
            automaticUpdater.ForceCheckForUpdate();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonUpdate_Click(object sender, EventArgs e)
        {
            UpdateApplication();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonOrbMenuItemOpenXML_Click(object sender, EventArgs e)
        {
            OpenXmlFile();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonOrbMenuItemOpenOther_Click(object sender, EventArgs e)
        {
            OpenOtherFile();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonOrbMenuItemSaveAsXml_Click(object sender, EventArgs e)
        {
            SaveXml();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonOrbMenuItemSaveAsOther_Click(object sender, EventArgs e)
        {
            SaveOther();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonOrbMenuItemExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fastObjectListViewMessages_SelectionChanged(object sender, EventArgs e)
        {
            Message selectedMessage = (Message) fastObjectListViewMessages.SelectedObject;
            if (selectedMessage == null) return;
            _selectedMessageId = selectedMessage.Id;
            textBoxSelectedMsgId.Text = _selectedMessageId;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonStyle_DropDownItemClicked(object sender, RibbonItemEventArgs e)
        {
            _orbStyleName = e.Item.Text;
            switch (_orbStyleName)
            {
                case "Office 2007":
                {
                    _orbStyle = RibbonOrbStyle.Office_2007;
                    _orbStyleIndex = 0;
                    break;
                }
                case "Office 2010":
                {
                    _orbStyle = RibbonOrbStyle.Office_2010;
                    _orbStyleIndex = 1;
                    break;
                }
                default:
                {
                    _orbStyle = RibbonOrbStyle.Office_2013;
                    _orbStyleIndex = 2;
                    break;
                }
            }
            ribbonMain.OrbStyle = _orbStyle;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonCheckForUpdate_Click(object sender, EventArgs e)
        {
            UpdateApplication();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonSend_Click(object sender, EventArgs e)
        {
            SendMessage();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonQueueProperties_Click(object sender, EventArgs e)
        {
            ShowQueueProperties();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                _selectedTab = "Send";
                ToggleRibbon();
            }
            else
            {
                _selectedTab = "Receive";
                ToggleRibbon();
            }
        }

        /// <summary>
        /// </summary>
        private void ToggleRibbon()
        {
            if (_selectedTab == "Send")
            {
                ribbonButtonMsgPreview.Enabled = false;
                ribbonButtonMsgDelete.Enabled = false;
                ribbonButtonMsgProps.Enabled = false;
                ribbonButtonMsgReceive.Enabled = false;
                ribbonButtonMsgCopy.Enabled = false;
                ribbonButtonMsgSend.Enabled = true;
            }
            else
            {
                ribbonButtonMsgPreview.Enabled = true;
                ribbonButtonMsgDelete.Enabled = true;
                ribbonButtonMsgProps.Enabled = true;
                ribbonButtonMsgReceive.Enabled = true;
                ribbonButtonMsgCopy.Enabled = true;
                ribbonButtonMsgSend.Enabled = false;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonPaste_Click(object sender, EventArgs e)
        {
            ClipBoardPaste();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonCut_Click(object sender, EventArgs e)
        {
            ClipBoardCut();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonCopy_Click(object sender, EventArgs e)
        {
            ClipBoardCopy();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonFormat_Click(object sender, EventArgs e)
        {
            FormatXml();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonMsgProps_Click(object sender, EventArgs e)
        {
            ShowMessageProperties();
        }

        /// <summary>
        /// </summary>
        private void ShowMessageProperties()
        {
            if (string.IsNullOrEmpty(_selectedMessageId))
            {
                return;
            }
            Message message = _msmqMessaging.GetMessageById(_selectedMessageId);
            MessageQueue queue = _msmqMessaging.GetQueueByName(_recHostName, _selectedRecQueueName);
            FormMessageProperties messageProperties = new FormMessageProperties(message, queue)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            messageProperties.ShowDialog();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxUseEncryption_CheckedChanged(object sender, EventArgs e)
        {
            _useEncryption = checkBoxUseEncryption.Checked;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxUseTracing_CheckedChanged(object sender, EventArgs e)
        {
            _useTracing = checkBoxUseTracing.Checked;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxUserJournal_CheckedChanged(object sender, EventArgs e)
        {
            _useJournal = checkBoxUserJournal.Checked;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void comboBoxPriority_SelectedIndexChanged(object sender, EventArgs e)
        {
            _priority = comboBoxPriority.Text;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemQueueProps_Click(object sender, EventArgs e)
        {
            ShowQueueProperties();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemMsgProps_Click(object sender, EventArgs e)
        {
            ShowMessageProperties();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripMenuItemSendQueueProps_Click(object sender, EventArgs e)
        {
            ShowQueueProperties();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonFind_Click(object sender, EventArgs e)
        {
            ShowFind();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ribbonButtonReplace_Click(object sender, EventArgs e)
        {
            ShowReplace();
        }

        private void toolStripMenuItemNewQueueSend_Click(object sender, EventArgs e)
        {
            NewQueue();
        }

        private void toolStripMenuItemDeleteQueueSend_Click(object sender, EventArgs e)
        {
            DeleteQueue();
        }

        private void toolStripMenuItemNewQueueRec_Click(object sender, EventArgs e)
        {
            NewQueue();
        }

        private void toolStripMenuItemDeleteQueueRec_Click(object sender, EventArgs e)
        {
            DeleteQueue();
        }

        private void ribbonButtonOpenXML_Click(object sender, EventArgs e)
        {
            OpenXmlFile();
        }

        private void ribbonButtonTheme_DropDownItemClicked(object sender, RibbonItemEventArgs e)
        {
            _orbThemeName = e.Item.Text;
            switch (_orbThemeName)
            {
                case "Normal":
                {
                    _orbTheme = RibbonTheme.Normal;
                    _orbThemeIndex = 0;
                    break;
                }
                case "Blue":
                {
                    _orbTheme = RibbonTheme.Blue;
                    _orbThemeIndex = 1;
                    break;
                }
                case "Black":
                {
                    _orbTheme = RibbonTheme.Black;
                    _orbThemeIndex = 2;
                    break;
                }
                case "Green":
                {
                    _orbTheme = RibbonTheme.Green;
                    _orbThemeIndex = 3;
                    break;
                }
                case "Purple":
                {
                    _orbTheme = RibbonTheme.Purple;
                    _orbThemeIndex = 4;
                    break;
                }
                case "JellyBelly":
                {
                    _orbTheme = RibbonTheme.JellyBelly;
                    _orbThemeIndex = 5;
                    break;
                }
                default:
                {
                    _orbTheme = RibbonTheme.Halloween;
                    _orbThemeIndex = 6;
                    break;
                }
            }
            Theme.ThemeColor = _orbTheme;
            ribbonMain.Refresh();
        }

        private void ribbonButtonHelp_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.lifeaftersiebel.co.uk/#contact-us");
        }

        private void comboBoxRecFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            _recFormat = comboBoxRecFormat.Text;
        }

        private void comboBoxSendFormat_SelectedIndexChanged(object sender, EventArgs e)
        {
            _sendFormat = comboBoxSendFormat.Text;
        }

        private void ribbonButtonSettings_Click(object sender, EventArgs e)
        {
            // Set form from current settings
            _formSettings.AutoUpdateCount = _autoUpdateCount;
            _formSettings.AutoUpdateInterval = _autoUpdateCountInterval;
            _formSettings.AutoUpdateQueue = _autoUpdateQueue;
            _formSettings.AutoUpdateQueueInterval = _autoUpdateQueueInterval;
            _formSettings.ShowUpdatedTimestamp = _showUpdatedTimestamp;
            _formSettings.UpdateControls();
            // Show form
            _formSettings.ShowDialog();
            // Update settings based on form
            _autoUpdateCount = _formSettings.AutoUpdateCount;
            _autoUpdateCountInterval = _formSettings.AutoUpdateInterval;
            _autoUpdateQueue = _formSettings.AutoUpdateQueue;
            _autoUpdateQueueInterval = _formSettings.AutoUpdateQueueInterval;
            _showUpdatedTimestamp = _formSettings.ShowUpdatedTimestamp;

            // Re-configure the timers
            InitTimers();

            // Save settings
            SaveSettings();
        }

        private void InitTimers()
        {
            if (_autoUpdateCount)
            {
                // Configure the timers
                _countTimer.Enabled = true;
                _countTimer.Interval = _autoUpdateCountInterval * 1000;
            }
            else
            {
                _countTimer.Enabled = false;
            }

            if (_autoUpdateQueue)
            {
                // Configure the timers
                _queueTimer.Enabled = true;
                _queueTimer.Interval = _autoUpdateQueueInterval * 1000;
            }
            else
            {
                _queueTimer.Enabled = false;
            }
        }

        private void ribbonButtonIncSearch_Click(object sender, EventArgs e)
        {
            ShowIncFind();
        }
        private void ShowIncFind()
        {
            if (tabControlMain.SelectedIndex == 0)
            {
                _findReplaceSend.ShowIncrementalSearch();
            }
            else
            {
                _findReplaceRec.ShowIncrementalSearch();
            }
        }
    }
}