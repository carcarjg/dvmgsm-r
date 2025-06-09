// SPDX-License-Identifier: AGPL-3.0-only
/**
* Digital Voice Modem - Desktop Dispatch Console
* AGPLv3 Open Source. Use is subject to license terms.
* DO NOT ALTER OR REMOVE COPYRIGHT NOTICES OR THIS FILE HEADER.
*
* @package DVM / Desktop Dispatch Console
* @license AGPLv3 License (https://opensource.org/licenses/AGPL-3.0)
*
*   Copyright (C) 2024-2025 Caleb, K4PHP
*   Copyright (C) 2025 J. Dean
*   Copyright (C) 2025 Nyx G
*   Copyright (C) 2025 Bryan Biedenkapp, N2PLL
*   Copyright (C) 2025 Steven Jennison, KD8RHO
*
*/

using System.IO;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using NWaves.Signals;

using MaterialDesignThemes.Wpf;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using dvmconsole.Controls;

using Constants = fnecore.Constants;
using fnecore;
using fnecore.DMR;
using fnecore.P25;
using fnecore.P25.KMM;
using fnecore.P25.LC.TSBK;
using Application = System.Windows.Application;
using Cursors = System.Windows.Input.Cursors;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.Net.Sockets;
using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using System.Security.Policy;
using System.Windows.Shapes;
using System.Reflection;
using Path = System.IO.Path;
using static dvmconsole.Codeplug;

namespace dvmconsole
{
    /// <summary>
    /// Data structure representing the position of a <see cref="ChannelBox"/> widget.
    /// </summary>
    public class ChannelPosition
    {
        /*
         ** Properties
         */

        /// <summary>
        /// X
        /// </summary>
        public double X { get; set; }
        /// <summary>
        /// Y
        /// </summary>
        public double Y { get; set; }
    } // public class ChannelPosition

    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// </summary>
    public partial class MainWindow : Window
    {
        public const double MIN_WIDTH = 850;
        public const double MIN_HEIGHT = 380;

        public const int PCM_SAMPLES_LENGTH = 320; // MBE_SAMPLES_LENGTH * 2

        public const int MAX_SYSTEM_NAME_LEN = 10;
        public const int MAX_CHANNEL_NAME_LEN = 21;

        private const string INVALID_SYSTEM = "INVALID SYSTEM";
        private const string INVALID_CODEPLUG_CHANNEL = "INVALID CODEPLUG CHANNEL";
        private const string ERR_INVALID_FNE_REF = "invalid FNE peer reference, this should not happen";
        private const string ERR_INVALID_CODEPLUG = "Codeplug has/may contain errors";
        private const string ERR_SKIPPING_AUDIO = "Skipping channel for audio";

        private const string PLEASE_CHECK_CODEPLUG = "Please check your codeplug for errors.";
        private const string PLEASE_RESTART_CONSOLE = "Please restart the console.";

        private const string URI_RESOURCE_PATH = "pack://application:,,,/dvmconsole;component";

        private bool isShuttingDown = false;
        private bool globalPttState = false;

        private const int GridSize = 5;

        private UIElement draggedElement;
        private Point startPoint;
        private double offsetX;
        private double offsetY;
        private bool isDragging;

        private bool isbooted;
		private bool isregged;
		private bool dereging;
        private string headcode;
        private bool isreging;
        private bool hc1entered;
		private bool hc2entered;
		private bool hc3entered;
		private bool hc4entered;
		private bool hc5entered;
		private bool hc6entered;
		private BackgroundWorker regbackgroundworker1 = new BackgroundWorker();
		private BackgroundWorker regbackgroundworker2 = new BackgroundWorker();
		private BackgroundWorker chregbackgroundworker1 = new BackgroundWorker();
		private BackgroundWorker volbackgroundworker1 = new BackgroundWorker();
		private BackgroundWorker errmsgbackgroundworker1 = new BackgroundWorker();
		private bool rxcall;
        DispatcherTimer RXbgtimer;
        private int curRXbg;
        private List<string> channelNames = new List<string>();
		private bool selectingchannel;
        private string currentchannel;
        private string currentsystem;
        private string currentdestID;
        private string currentmode;
        private int currentInternalID =0;
        private bool _sysregstate;

		public bool sysregstate 
        {
			get => _sysregstate;
			set
			{
				if (_sysregstate != value) // Check if the value is actually changing
				{
					_sysregstate = value;
					SysRegStateChange(); // Call the function when the variable changes
				}
			}
		} // True is connected. false is disconnected.. duh

		//Please dont kill me for this :3 - Nyx
		private bool pttState;
		private bool pageState;
		private bool holdState;
		private string lastSrcId = "0";
		private double volume = 1.0;
		private bool isSelected;
		public byte[] netLDU1 = new byte[9 * 25];
		public byte[] netLDU2 = new byte[9 * 25];

		public ushort pktSeq = 0;                               // RTP packet sequence

		public int p25N = 0;
		public int p25SeqNo = 0;
		public int p25Errs = 0;

		public byte dmrN = 0;
		public int dmrSeqNo = 0;

		public int ambeCount = 0;
		public byte[] ambeBuffer = new byte[FneSystemBase.DMR_AMBE_LENGTH_BYTES];
		public EmbeddedData embeddedData = new EmbeddedData();
		/// <summary>
		/// 
		/// </summary>
		public uint TxStreamId { get; internal set; }

		/// <summary>
		/// 
		/// </summary>
		public uint PeerId { get; set; }
		/// <summary>
		/// 
		/// </summary>
		public uint RxStreamId { get; set; }
		public string VoiceChannel { get; set; }
		public bool IsReceiving { get; set; } = false;
		/// <summary>
		/// Flag indicating whether or not this channel is receiving encrypted.
		/// </summary>
		public bool IsReceivingEncrypted { get; set; } = false;

		/// <summary>
		/// Flag indicating whether or not the console is transmitting with encryption.
		/// </summary>
		public bool IsTxEncrypted { get; set; } = false;
		/// <summary>
		/// Last Packet Time
		/// </summary>
		public DateTime LastPktTime = DateTime.Now;
		public byte[] mi = new byte[P25Defines.P25_MI_LENGTH];     // Message Indicator
		public byte algId = 0;                                     // Algorithm ID
		public ushort kId = 0;                                     // Key ID

		public List<byte[]> chunkedPCM = new List<byte[]>();

		public bool ExternalVocoderEnabled = false;
		public AmbeVocoder ExtFullRateVocoder = null;
		public AmbeVocoder ExtHalfRateVocoder = null;
		public MBEEncoder Encoder = null;
		public MBEDecoder Decoder = null;

		public MBEToneDetector ToneDetector = new MBEToneDetector();

		public P25Crypto Crypter = new P25Crypto();

		private bool windowLoaded = false;
        private bool noSaveSettingsOnClose = false;
        private SettingsManager settingsManager = new SettingsManager();
        private SelectedChannelsManager selectedChannelsManager;
        private FlashingBackgroundManager flashingManager;

        private Brush btnGlobalPttDefaultBg;

        private ChannelBox playbackChannelBox;

        private CallHistoryWindow callHistoryWindow;

        public static string PLAYBACKTG = "LOCPLAYBACK";
        public static string PLAYBACKSYS = "Local Playback";
        public static string PLAYBACKCHNAME = "PLAYBACK";


        private readonly WaveInEvent waveIn;
        private readonly AudioManager audioManager;

        private static System.Timers.Timer channelHoldTimer;

        private Dictionary<string, SlotStatus> systemStatuses = new Dictionary<string, SlotStatus>();
        private FneSystemManager fneSystemManager = new FneSystemManager();

        private bool selectAll = false;
        private KeyboardManager keyboardManager;

        private CancellationTokenSource maintainenceCancelToken = new CancellationTokenSource();
        private Task maintainenceTask = null;

        /*
        ** Properties
        */

        /// <summary>
        /// Codeplug
        /// </summary>
        public Codeplug Codeplug { get; set; }

        /*
        ** Methods
        */

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.keyboardManager = new KeyboardManager();
            MinWidth = Width = MIN_WIDTH;
            MinHeight = Height = MIN_HEIGHT;

            DisableControls();

            settingsManager.LoadSettings();
            InitializeKeyboardShortcuts();
            callHistoryWindow = new CallHistoryWindow(settingsManager, CallHistoryWindow.MAX_CALL_HISTORY);

            selectedChannelsManager = new SelectedChannelsManager();
            flashingManager = new FlashingBackgroundManager(null, channelsCanvas, null, this);

            channelHoldTimer = new System.Timers.Timer(10000);
            channelHoldTimer.Elapsed += OnHoldTimerElapsed;
            channelHoldTimer.AutoReset = true;
            channelHoldTimer.Enabled = true;

            waveIn = new WaveInEvent { WaveFormat = new WaveFormat(8000, 16, 1) };
            waveIn.DataAvailable += WaveIn_DataAvailable;
            waveIn.RecordingStopped += WaveIn_RecordingStopped;

            waveIn.StartRecording();

            audioManager = new AudioManager(settingsManager);

            btnGlobalPtt.PreviewMouseLeftButtonDown += btnGlobalPtt_MouseLeftButtonDown;
            btnGlobalPtt.PreviewMouseLeftButtonUp += btnGlobalPtt_MouseLeftButtonUp;
            btnGlobalPtt.MouseRightButtonDown += btnGlobalPtt_MouseRightButtonDown;

            selectedChannelsManager.SelectedChannelsChanged += SelectedChannelsChanged;
            selectedChannelsManager.PrimaryChannelChanged += PrimaryChannelChanged;

            LocationChanged += MainWindow_LocationChanged;
            SizeChanged += MainWindow_SizeChanged;
            Loaded += MainWindow_Loaded;

			// initialize external AMBE vocoder
			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);
			string path = Uri.UnescapeDataString(uri.Path);
			if (File.Exists(System.IO.Path.Combine(new string[] { System.IO.Path.GetDirectoryName(path), "AMBE.DLL" })))
			{
				ExternalVocoderEnabled = true;
				ExtFullRateVocoder = new AmbeVocoder();
				ExtHalfRateVocoder = new AmbeVocoder(false);
			}

		}

        /// <summary>
        /// 
        /// </summary>
        private void PrimaryChannelChanged()
        {
            var primaryChannel = selectedChannelsManager.PrimaryChannel;
            foreach (UIElement element in channelsCanvas.Children)
            {
                if (element is ChannelBox box)
                {
                    box.IsPrimary = box == primaryChannel;
                }
            }
        }

        /// <summary>
        /// Helper to enable menu controls for Commands submenu.
        /// </summary>
        private void EnableCommandControls()
        {
            menuPageSubscriber.IsEnabled = true;
            menuRadioCheckSubscriber.IsEnabled = true;
            menuInhibitSubscriber.IsEnabled = true;
            menuUninhibitSubscriber.IsEnabled = true;
            menuQuickCall2.IsEnabled = true;
        }

        /// <summary>
        /// Helper to enable form controls when settings and codeplug are loaded.
        /// </summary>
        private void EnableControls()
        {
            btnGlobalPtt.IsEnabled = true;
            btnAlert1.IsEnabled = true;
            btnAlert2.IsEnabled = true;
            btnAlert3.IsEnabled = true;
            btnPageSub.IsEnabled = true;
            btnSelectAll.IsEnabled = true;
            btnKeyStatus.IsEnabled = true;
            btnCallHistory.IsEnabled = true;
        }

        /// <summary>
        /// Helper to disable menu controls for Commands submenu.
        /// </summary>
        private void DisableCommandControls()
        {
            menuPageSubscriber.IsEnabled = false;
            menuRadioCheckSubscriber.IsEnabled = false;
            menuInhibitSubscriber.IsEnabled = false;
            menuUninhibitSubscriber.IsEnabled = false;
            menuQuickCall2.IsEnabled = false;
        }

        /// <summary>
        /// Helper to disable form controls when settings load fails.
        /// </summary>
        private void DisableControls()
        {
            DisableCommandControls();

            btnGlobalPtt.IsEnabled = false;
            btnAlert1.IsEnabled = false;
            btnAlert2.IsEnabled = false;
            btnAlert3.IsEnabled = false;
            btnPageSub.IsEnabled = false;
            btnSelectAll.IsEnabled = false;
            btnKeyStatus.IsEnabled = false;
            btnCallHistory.IsEnabled = false;
        }

        /// <summary>
        /// Helper to load the codeplug.
        /// </summary>
        /// <param name="filePath"></param>
        private void LoadCodeplug(string filePath)
        {
            DisableControls();

            //channelsCanvas.Children.Clear();
            systemStatuses.Clear();

            fneSystemManager.ClearAll();

            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                string yaml = File.ReadAllText(filePath);
                Codeplug = deserializer.Deserialize<Codeplug>(yaml);

                // perform codeplug validation
                List<string> errors = new List<string>();

                // ensure string lengths are acceptable
                // systems
                Dictionary<string, string> replacedSystemNames = new Dictionary<string, string>();
                foreach (Codeplug.System system in Codeplug.Systems)
                {
                    // ensure system name is less then or equals to the max
                    if (system.Name.Length > MAX_SYSTEM_NAME_LEN)
                    {
                        string original = system.Name;
                        system.Name = system.Name.Substring(0, MAX_SYSTEM_NAME_LEN);
                        replacedSystemNames.Add(original, system.Name);
                        Log.WriteLine($"{original} SYSTEM NAME was greater then {MAX_SYSTEM_NAME_LEN} characters, truncated {system.Name}");
                    }
                }

                // zones
                foreach (Codeplug.Zone zone in Codeplug.Zones)
                {
                    // channels
                    foreach (Codeplug.Channel channel in zone.Channels)
                    {
                        if (Codeplug.Systems.Find((x) => x.Name == channel.System) == null)
                            errors.Add($"{channel.Name} refers to an {INVALID_SYSTEM} {channel.System}.");

                        // because we possibly truncated system names above lets see if we
                        // have to replaced the related system name
                        if (replacedSystemNames.ContainsKey(channel.System))
                            channel.System = replacedSystemNames[channel.System];

                        // ensure channel name is less then or equals to the max
                        if (channel.Name.Length > MAX_CHANNEL_NAME_LEN)
                        {
                            string original = channel.Name;
                            channel.Name = channel.Name.Substring(0, MAX_CHANNEL_NAME_LEN);
                            Log.WriteLine($"{original} CHANNEL NAME was greater then {MAX_CHANNEL_NAME_LEN} characters, truncated {channel.Name}");
                        }

                        // clamp slot value
                        if (channel.Slot <= 0)
                            channel.Slot = 1;
                        if (channel.Slot > 2)
                            channel.Slot = 1;
                    }
                }

                // compile list of errors and throw up a messagebox of doom
                if (errors.Count > 0)
                {
                    string newLine = Environment.NewLine + Environment.NewLine;
                    string messageBoxString = $"Loaded codeplug {filePath} contains errors. {PLEASE_CHECK_CODEPLUG}" + newLine;
                    foreach (string error in errors)
                        messageBoxString += error + newLine;
                    messageBoxString = messageBoxString.TrimEnd(new char[] { '\r', '\n' });

                    MessageBox.Show(messageBoxString, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // generate widgets and enable controls
                
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading codeplug: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Log.StackTrace(ex, false);
                DisableControls();
            }
        }

        /// <summary>
        /// Helper to initialize and generate channel widgets on the canvas.
        /// </summary>
        private void GenerateChannelWidgets()
        {
            //channelsCanvas.Children.Clear();
            systemStatuses.Clear();

            fneSystemManager.ClearAll();

            double offsetX = 0;
            double offsetY = 0;

            Cursor = Cursors.Wait;

            if (Codeplug != null)
            {
                // load and initialize systems
                foreach (var system in Codeplug.Systems)
                {
                    // do we have aliases for this system?
                    if (File.Exists(system.AliasPath))
                        system.RidAlias = AliasTools.LoadAliases(system.AliasPath);

                    fneSystemManager.AddFneSystem(system.Name, system, this);
					PeerSystem peer = fneSystemManager.GetFneSystem(system.Name);

                    // hook FNE events
                    peer.peer.PeerConnected += (sender, response) =>
                    {
                        Log.WriteLine("FNE Peer connected");
                        Dispatcher.Invoke(() =>
                        {
                            //EnableCommandControls();
                            sysregstate = true;

                            currentsystem = system.Name;

							string tmphc = regheadcode0.Text + regheadcode1.Text + regheadcode2.Text + regheadcode3.Text + regheadcode4.Text + regheadcode5.Text;
							updateHeadcode(tmphc);
							regheadcode0.Text = "";
							regheadcode1.Text = "";
							regheadcode2.Text = "";
							regheadcode3.Text = "";
							regheadcode4.Text = "";
							regheadcode5.Text = "";

							hc6entered = false;
							hc5entered = false;
							hc4entered = false;
							hc3entered = false;
							hc2entered = false;
							hc1entered = false;

							MediaPlayer mediaPlayer = new MediaPlayer();
							mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S_info.wav")));
							mediaPlayer.Play();
							UpdateRadioBackground("bg_main_hd_light.png");
							headcodeb0.Visibility = Visibility.Visible;
							headcodeb1.Visibility = Visibility.Visible;
							headcodeb2.Visibility = Visibility.Visible;
							headcodeb3.Visibility = Visibility.Visible;
							headcodeb4.Visibility = Visibility.Visible;
							headcodeb5.Visibility = Visibility.Visible;
							isreging = false;
							isregged = true;
						});
                    };
                    
                    peer.peer.PeerDisconnected += (response) =>
                    {
                        Log.WriteLine("FNE Peer disconnected");
                        Dispatcher.Invoke(() =>
                        {
                            DisableCommandControls();
                            sysregstate = false;
							regheadcode0.Text = "";
							regheadcode1.Text = "";
							regheadcode2.Text = "";
							regheadcode3.Text = "";
							regheadcode4.Text = "";
							regheadcode5.Text = "";

							hc6entered = false;
							hc5entered = false;
							hc4entered = false;
							hc3entered = false;
							hc2entered = false;
							hc1entered = false;

							MediaPlayer mediaPlayer = new MediaPlayer();
							mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
							mediaPlayer.Play();
							UpdateRadioBackground("sChregfail.png");
							isreging = false;
							isregged = false;


							foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
                            {
                                if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                                    continue;

                                if (channel.IsReceiving || channel.IsReceivingEncrypted)
                                {
                                    channel.IsReceiving = false;
                                    channel.PeerId = 0;
                                    channel.RxStreamId = 0;

                                    channel.IsReceivingEncrypted = false;
                                    channel.Background = ChannelBox.BLUE_GRADIENT;
                                    channel.VolumeMeterLevel = 0;
                                }
                            }
                        });
                    };

                    // start peer
                    Task.Run(() =>
                    {
                        try
                        {
                            peer.Start();
                        }
                        catch (Exception ex)
                        {
							sysregstate = false;
							regheadcode0.Text = "";
							regheadcode1.Text = "";
							regheadcode2.Text = "";
							regheadcode3.Text = "";
							regheadcode4.Text = "";
							regheadcode5.Text = "";

							hc6entered = false;
							hc5entered = false;
							hc4entered = false;
							hc3entered = false;
							hc2entered = false;
							hc1entered = false;

							MediaPlayer mediaPlayer = new MediaPlayer();
							mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
							mediaPlayer.Play();
							UpdateRadioBackground("sChregfail.png");
							isreging = false;
							isregged = false;
							MessageBox.Show($"Fatal error while connecting to server. {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            Log.StackTrace(ex, false);
                        }
                    });

                }
            }

            // are we showing channels?
            //Dont know. Are you?
            if (settingsManager.ShowChannels && Codeplug != null)
            {
                // iterate through the coeplug zones and begin building channel widgets
                foreach (var zone in Codeplug.Zones)
                {
                    var channel = zone.Channels[0];
                    ChannelBox channelBox = new ChannelBox(selectedChannelsManager, audioManager, channel.Name, channel.System, channel.Tgid, settingsManager.TogglePTTMode);
                    channelBox.ChannelMode = channel.Mode.ToUpperInvariant();
                    if (channel.GetAlgoId() != P25Defines.P25_ALGO_UNENCRYPT && channel.GetKeyId() > 0)
                        channelBox.IsTxEncrypted = true;

                     systemStatuses.Add(channel.Name, new SlotStatus());

                     if (settingsManager.ChannelPositions.TryGetValue(channel.Name, out var position))
                     {
                        Canvas.SetLeft(channelBox, position.X);
                        Canvas.SetTop(channelBox, position.Y);
                     }
                        else
                        {
                            Canvas.SetLeft(channelBox, offsetX);
                            Canvas.SetTop(channelBox, offsetY);
                        }

                        channelBox.PTTButtonClicked += ChannelBox_PTTButtonClicked;
                        channelBox.PageButtonClicked += ChannelBox_PageButtonClicked;
                        channelBox.HoldChannelButtonClicked += ChannelBox_HoldChannelButtonClicked;
                        channelBox.IsSelected = true;
                        channelBox.IsPrimary = true;
                        channelBox.IsEnabled = true;
                        // widget placement
                        channelBox.MouseRightButtonDown += ChannelBox_MouseRightButtonDown;
                        channelBox.MouseRightButtonUp += ChannelBox_MouseRightButtonUp;
                        channelBox.MouseMove += ChannelBox_MouseMove;
                        selectedChannelsManager.AddSelectedChannel(channelBox);

                        channelsCanvas.Children.Add(channelBox);

                        offsetX += 269;

                        if (offsetX + 264 > channelsCanvas.ActualWidth)
                        {
                            offsetX = 20;
                            offsetY += 116;
                        }
                        channelBox.Visibility = Visibility.Hidden;
                }
            }

            // are we showing user configured alert tones?
            if (settingsManager.ShowAlertTones && Codeplug != null)
            {
                // iterate through the alert tones and begin building alert tone widges
                foreach (var alertPath in settingsManager.AlertToneFilePaths)
                {
                    AlertTone alertTone = new AlertTone(alertPath);

                    alertTone.OnAlertTone += SendAlertTone;

                    // widget placement
                    alertTone.MouseRightButtonDown += AlertTone_MouseRightButtonDown;
                    alertTone.MouseRightButtonUp += AlertTone_MouseRightButtonUp;
                    alertTone.MouseMove += AlertTone_MouseMove;

                    if (settingsManager.AlertTonePositions.TryGetValue(alertPath, out var position))
                    {
                        Canvas.SetLeft(alertTone, position.X);
                        Canvas.SetTop(alertTone, position.Y);
                    }
                    else
                    {
                        Canvas.SetLeft(alertTone, 20);
                        Canvas.SetTop(alertTone, 20);
                    }

                    channelsCanvas.Children.Add(alertTone);
                }
            }

            // initialize the playback channel
            playbackChannelBox = new ChannelBox(selectedChannelsManager, audioManager, PLAYBACKCHNAME, PLAYBACKSYS, PLAYBACKTG);
            playbackChannelBox.ChannelMode = "Local";
            playbackChannelBox.HidePTTButton(); // playback box shouldn't have PTT

            if (settingsManager.ChannelPositions.TryGetValue(PLAYBACKCHNAME, out var pos))
            {
                Canvas.SetLeft(playbackChannelBox, pos.X);
                Canvas.SetTop(playbackChannelBox, pos.Y);
            }
            else
            {
                Canvas.SetLeft(playbackChannelBox, offsetX);
                Canvas.SetTop(playbackChannelBox, offsetY);
            }

            playbackChannelBox.PageButtonClicked += ChannelBox_PageButtonClicked;
            playbackChannelBox.HoldChannelButtonClicked += ChannelBox_HoldChannelButtonClicked;

            // widget placement
            playbackChannelBox.MouseRightButtonDown += ChannelBox_MouseRightButtonDown;
            playbackChannelBox.MouseRightButtonUp += ChannelBox_MouseRightButtonUp;
            playbackChannelBox.MouseMove += ChannelBox_MouseMove;
            playbackChannelBox.Visibility = Visibility.Hidden;

            channelsCanvas.Children.Add(playbackChannelBox);

            Cursor = Cursors.Arrow;
		}

        /// <summary>
        /// 
        /// </summary>
        private void SelectedChannelsChanged()
        {
            foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
            {
                if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                    continue;

                Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                if (system == null)
                {
                    MessageBox.Show($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                if (cpgChannel == null)
                {
                    // bryanb: this should actually never happen...
                    MessageBox.Show($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                if (fne == null)
                {
                    MessageBox.Show($"{channel.ChannelName} has a {ERR_INVALID_FNE_REF}. {PLEASE_RESTART_CONSOLE}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                // is the channel selected?
                if (channel.IsSelected)
                {
                    // if the channel is configured for encryption request the key from the FNE
                    uint newTgid = uint.Parse(cpgChannel.Tgid);
                    if (cpgChannel.GetAlgoId() != 0 && cpgChannel.GetKeyId() != 0)
                    {
                        fne.peer.SendMasterKeyRequest(cpgChannel.GetAlgoId(), cpgChannel.GetKeyId());
                        if (Codeplug.KeyFile != null)
                        {
                            if (!File.Exists(Codeplug.KeyFile))
                            {
                                MessageBox.Show($"Key file {Codeplug.KeyFile} not found. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                                var deserializer = new DeserializerBuilder()
                                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                    .IgnoreUnmatchedProperties()
                                    .Build();
                                var keys = deserializer.Deserialize<KeyContainer>(File.ReadAllText(Codeplug.KeyFile));
                                var KeysetItems = new Dictionary<int, KeysetItem>();

                                foreach (var keyEntry in keys.Keys)
                                {
                                    var keyItem = new KeyItem();
                                    keyItem.KeyId = keyEntry.KeyId;
                                    var keyBytes = keyEntry.KeyBytes;
                                    keyItem.SetKey(keyBytes,(uint)keyBytes.Length);
                                    if (!KeysetItems.ContainsKey(keyEntry.AlgId))
                                    {
                                        var asByte = (byte)keyEntry.AlgId;
                                        KeysetItems.Add(keyEntry.AlgId, new KeysetItem() { AlgId = asByte });
                                    }


                                    KeysetItems[keyEntry.AlgId].AddKey(keyItem);
                                }

                                foreach (var eventData in KeysetItems.Select(keyValuePair => keyValuePair.Value).Select(keysetItem => new KeyResponseEvent(0, new KmmModifyKey
                                         {
                                             AlgId = 0,
                                             KeyId = 0,
                                             MessageId = 0,
                                             MessageLength = 0,
                                             KeysetItem = keysetItem
                                         }, [])))
                                {
                                    KeyResponseReceived(eventData);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper to reset channel states.
        /// </summary>
        /// <param name="e"></param>
        private void ResetChannel()
        {
            // reset values
            p25SeqNo = 0;
            p25N = 0;

            dmrSeqNo = 0;
            dmrN = 0;

            pktSeq = 0;

            TxStreamId = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        private void SendAlertTone(AlertTone e)
        {
            Task.Run(() => SendAlertTone(e.AlertFilePath));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="forHold"></param>
        private void SendAlertTone(string filePath, bool forHold = false)
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    ChannelBox primaryChannel = selectedChannelsManager.PrimaryChannel;
                    List<ChannelBox> channelsToProcess = primaryChannel != null
                        ? new List<ChannelBox> { primaryChannel }
                        : selectedChannelsManager.GetSelectedChannels().ToList();

                    foreach (ChannelBox channel in channelsToProcess)
                    {

                        if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                            return;

                        Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                        if (system == null)
                        {
                            Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {ERR_INVALID_CODEPLUG}. {ERR_SKIPPING_AUDIO}.");
                            channel.IsSelected = false;
                            selectedChannelsManager.RemoveSelectedChannel(channel);
                            return;
                        }

                        Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                        if (cpgChannel == null)
                        {
                            Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {ERR_INVALID_CODEPLUG}. {ERR_SKIPPING_AUDIO}.");
                            channel.IsSelected = false;
                            selectedChannelsManager.RemoveSelectedChannel(channel);
                            return;
                        }

                        PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                        if (fne == null)
                        {
                            Log.WriteLine($"{channel.ChannelName} has a {ERR_INVALID_FNE_REF}. {ERR_INVALID_CODEPLUG}. {ERR_SKIPPING_AUDIO}.");
                            channel.IsSelected = false;
                            selectedChannelsManager.RemoveSelectedChannel(channel);
                            return;
                        }

                        if (channel.PageState || (forHold && channel.HoldState) || primaryChannel != null)
                        {
                            byte[] pcmData;

                            Task.Run(async () =>
                            {
                                using (var waveReader = new WaveFileReader(filePath))
                                {
                                    if (waveReader.WaveFormat.Encoding != WaveFormatEncoding.Pcm ||
                                        waveReader.WaveFormat.SampleRate != 8000 ||
                                        waveReader.WaveFormat.BitsPerSample != 16 ||
                                        waveReader.WaveFormat.Channels != 1)
                                    {
                                        MessageBox.Show("The alert tone must be PCM 16-bit, Mono, 8000Hz format.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }

                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        waveReader.CopyTo(ms);
                                        pcmData = ms.ToArray();
                                    }
                                }

                                int chunkSize = 1600;
                                int totalChunks = (pcmData.Length + chunkSize - 1) / chunkSize;

                                if (pcmData.Length % chunkSize != 0)
                                {
                                    byte[] paddedData = new byte[totalChunks * chunkSize];
                                    Buffer.BlockCopy(pcmData, 0, paddedData, 0, pcmData.Length);
                                    pcmData = paddedData;
                                }

                                Task.Run(() =>
                                {
                                    audioManager.AddTalkgroupStream(cpgChannel.Tgid, pcmData);
                                });

                                DateTime startTime = DateTime.UtcNow;

                                if (channel.TxStreamId != 0)
                                    Log.WriteWarning($"{channel.ChannelName} CHANNEL still had a TxStreamId? This shouldn't happen.");

                                channel.TxStreamId = fne.NewStreamId();
                                Log.WriteLine($"({system.Name}) {channel.ChannelMode.ToUpperInvariant()} Traffic *ALRT TONE      * TGID {channel.DstId} [STREAM ID {channel.TxStreamId}]");
                                channel.VolumeMeterLevel = 0;

                                for (int i = 0; i < totalChunks; i++)
                                {
                                    int offset = i * chunkSize;
                                    byte[] chunk = new byte[chunkSize];
                                    Buffer.BlockCopy(pcmData, offset, chunk, 0, chunkSize);

                                    channel.chunkedPCM = AudioConverter.SplitToChunks(chunk);

                                    foreach (byte[] audioChunk in channel.chunkedPCM)
                                    {
                                        if (audioChunk.Length == PCM_SAMPLES_LENGTH)
                                        {
                                            if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                                                P25EncodeAudioFrame(audioChunk, fne, channel, cpgChannel, system);
                                            else if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.DMR)
                                                DMREncodeAudioFrame(audioChunk, fne, channel, cpgChannel, system);
                                        }
                                    }

                                    DateTime nextPacketTime = startTime.AddMilliseconds((i + 1) * 100);
                                    TimeSpan waitTime = nextPacketTime - DateTime.UtcNow;

                                    if (waitTime.TotalMilliseconds > 0)
                                        await Task.Delay(waitTime);
                                }

                                double totalDurationMs = ((double)pcmData.Length / 16000) + 250;
                                await Task.Delay((int)totalDurationMs + 3000);

                                fne.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), false);

                                ResetChannel();

                                Dispatcher.Invoke(() =>
                                {
                                    if (forHold)
                                        channel.PttButton.Background = ChannelBox.GRAY_GRADIENT;
                                    else
                                        channel.PageState = false;
                                });
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to process alert tone: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Log.StackTrace(ex, false);
                }
            }
            else
                MessageBox.Show("Alert file not set or file not found.", "Alert", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateBackground()
        {
        }

		/// <summary>
		/// 
		/// </summary>
		private void UpdateRadioBackground(string resourcename)
		{

			BitmapImage bg = new BitmapImage();

			bg.BeginInit();
				bg.UriSource = new Uri($"{URI_RESOURCE_PATH}/Assets/"+resourcename);
			bg.EndInit();

			canvasBG.ImageSource = bg;
			canvasBG.Stretch = Stretch.Uniform;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private async void OnHoldTimerElapsed(object sender, ElapsedEventArgs e)
        {
            foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
            {
                if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                    continue;

                Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                PeerSystem handler = fneSystemManager.GetFneSystem(system.Name);

                if (channel.HoldState && !channel.IsReceiving && !channel.PttState && !channel.PageState)
                {
                    handler.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), true);
                    await Task.Delay(1000);

                    SendAlertTone(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/hold.wav"), true);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            isShuttingDown = true;

            // stop maintainence task
            if (maintainenceTask != null)
            {
                maintainenceCancelToken.Cancel();

                try
                {
                    maintainenceTask.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) { /* stub */ }
                finally
                {
                    maintainenceCancelToken.Dispose();
                }
            }

            waveIn.StopRecording();

            fneSystemManager.ClearAll();

            if (!noSaveSettingsOnClose)
            {
                if (WindowState == WindowState.Maximized)
                {
                    settingsManager.Maximized = true;
                    if (settingsManager.SnapCallHistoryToWindow)
                        menuSnapCallHistory.IsChecked = false;
                }

                settingsManager.SaveSettings();
            }

            base.OnClosing(e);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Internal maintainence routine.
        /// </summary>
        private async void Maintainence()
        {
            CancellationToken ct = maintainenceCancelToken.Token;
            while (!isShuttingDown)
            {
                foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
                {
                    if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                        continue;

                    Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                    if (system == null)
                        continue;

                    Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                    if (cpgChannel == null)
                        continue;

                    PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                    if (fne == null)
                        continue;

                    // check if the channel is stuck reporting Rx
                    if (channel.IsReceiving)
                    {
                        DateTime now = DateTime.Now;
                        TimeSpan dt = now - channel.LastPktTime;
                        if (dt.TotalMilliseconds > 2000) // 2 seconds is more then enough time -- the interpacket time for P25 is ~180ms and DMR is ~60ms
                        {
                            Log.WriteLine($"({system.Name}) P25D: Traffic *CALL TIMEOUT   * TGID {channel.DstId} ALGID {channel.algId} KID {channel.kId}");
                            Dispatcher.Invoke(() =>
                            {
                                channel.IsReceiving = false;
                                channel.PeerId = 0;
                                channel.RxStreamId = 0;

                                channel.Background = ChannelBox.BLUE_GRADIENT;
                                channel.VolumeMeterLevel = 0;
                            });
                        }
                    }
                }

                try
                {
                    await Task.Delay(1000, ct);
                }
                catch (TaskCanceledException) { /* stub */ }
            }
        }

        /** NAudio Events */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WaveIn_RecordingStopped(object sender, EventArgs e)
        {
            /* stub */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            bool isAnyTgOn = false;
            if (isShuttingDown)
                return;

            foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
            {
                if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                    continue;

                Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                if (system == null)
                {
                    Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {ERR_INVALID_CODEPLUG}. {ERR_SKIPPING_AUDIO}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                if (cpgChannel == null)
                {
                    Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {ERR_INVALID_CODEPLUG}. {ERR_SKIPPING_AUDIO}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                if (fne == null)
                {
                    Log.WriteLine($"{channel.ChannelName} has a {ERR_INVALID_FNE_REF}. {ERR_INVALID_CODEPLUG}. {ERR_SKIPPING_AUDIO}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                // is the channel selected and in a PTT state?
                if (channel.IsSelected && channel.PttState)
                {
                    isAnyTgOn = true;
                    Task.Run(() =>
                    {
                        channel.chunkedPCM = AudioConverter.SplitToChunks(e.Buffer);
                        foreach (byte[] chunk in channel.chunkedPCM)
                        {
                            if (chunk.Length == PCM_SAMPLES_LENGTH)
                            {
                                if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                                    P25EncodeAudioFrame(chunk, fne, channel, cpgChannel, system);
                                else if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.DMR)
                                    DMREncodeAudioFrame(chunk, fne, channel, cpgChannel, system);
                            }
                            else
                                Log.WriteLine("bad sample length: " + chunk.Length);
                        }
                    });
                }
            }

            if (playbackChannelBox != null && isAnyTgOn && playbackChannelBox.IsSelected)
                audioManager.AddTalkgroupStream(PLAYBACKTG, e.Buffer);
        }

        /** WPF Window Events */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            if (settingsManager.SnapCallHistoryToWindow && callHistoryWindow.Visibility == Visibility.Visible && 
                WindowState != WindowState.Maximized)
            {
                callHistoryWindow.Left = Left + ActualWidth + 5;
                callHistoryWindow.Top = Top;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            const double widthOffset = 16;
            const double heightOffset = 115;

            if (!windowLoaded)
                return;

            if (ActualWidth > channelsCanvas.ActualWidth)
            {
                channelsCanvas.Width = ActualWidth;
                canvasScrollViewer.Width = ActualWidth;
            }
            else
                canvasScrollViewer.Width = Width - widthOffset;

            if (ActualHeight > channelsCanvas.ActualHeight)
            {
                channelsCanvas.Height = ActualHeight;
                canvasScrollViewer.Height = ActualHeight;
            }
            else
                canvasScrollViewer.Height = Height - heightOffset;

            if (WindowState == WindowState.Maximized)
                ResizeCanvasToWindow_Click(sender, e);
            else
                settingsManager.Maximized = false;

            if (settingsManager.SnapCallHistoryToWindow && callHistoryWindow.Visibility == Visibility.Visible && 
                WindowState != WindowState.Maximized)
            {
                callHistoryWindow.Height = ActualHeight;
                callHistoryWindow.Left = Left + ActualWidth + 5;
                callHistoryWindow.Top = Top;
            }

            settingsManager.CanvasWidth = channelsCanvas.ActualWidth;
            settingsManager.CanvasHeight = channelsCanvas.ActualHeight;

            settingsManager.WindowWidth = ActualWidth;
            settingsManager.WindowHeight = ActualHeight;
        }
		private BackgroundWorker bootbackgroundworker1 = new BackgroundWorker();
		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

			RXbgtimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(500)
			};
			RXbgtimer.Tick += OnRXbgtimerTick;

			// set PTT toggle mode (this must be done before channel widgets are defined)
			menuToggleLockWidgets.IsChecked = settingsManager.LockWidgets;
            menuSnapCallHistory.IsChecked = settingsManager.SnapCallHistoryToWindow;
            menuTogglePTTMode.IsChecked = settingsManager.TogglePTTMode;
            menuToggleGlobalPTTMode.IsChecked = settingsManager.GlobalPTTKeysAllChannels;
            menuKeepWindowOnTop.IsChecked = settingsManager.KeepWindowOnTop;

            if (!string.IsNullOrEmpty(settingsManager.LastCodeplugPath) && File.Exists(settingsManager.LastCodeplugPath))
                LoadCodeplug(settingsManager.LastCodeplugPath);
            else
                

            // set background configuration
            menuDarkMode.IsChecked = settingsManager.DarkMode;
            UpdateBackground();

            btnGlobalPttDefaultBg = btnGlobalPtt.Background;

            maintainenceTask = Task.Factory.StartNew(Maintainence, maintainenceCancelToken.Token);

			regbackgroundworker1.DoWork += new DoWorkEventHandler(regbackgroundworker1_DoWork);
			regbackgroundworker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(regbackgroundworker1_RunWorkerCompleted);

			regbackgroundworker2.DoWork += new DoWorkEventHandler(regbackgroundworker2_DoWork);
			regbackgroundworker2.RunWorkerCompleted += new RunWorkerCompletedEventHandler(regbackgroundworker2_RunWorkerCompleted);
			regbackgroundworker2.WorkerSupportsCancellation = true;

			chregbackgroundworker1.DoWork += new DoWorkEventHandler(chregbackgroundworker1_DoWork);
			chregbackgroundworker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(chregbackgroundworker1_RunWorkerCompleted);

			volbackgroundworker1.DoWork += new DoWorkEventHandler(volbackgroundworker1_DoWork);
			volbackgroundworker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(volbackgroundworker1_RunWorkerCompleted);
            volbackgroundworker1.WorkerSupportsCancellation = true;

			//errmsgbackgroundworker1
			errmsgbackgroundworker1.DoWork += new DoWorkEventHandler(errmsgbackgroundworker1_DoWork);
			errmsgbackgroundworker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(errmsgbackgroundworker1_RunWorkerCompleted);

			UpdateRadioBackground("schboot.png");
			bootbackgroundworker1.DoWork += new DoWorkEventHandler(bootbackgroundworker1_DoWork);
			bootbackgroundworker1.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bootbackgroundworker1_RunWorkerCompleted);

			bootbackgroundworker1.RunWorkerAsync();
		}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenCodeplug_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Codeplug Files (*.yml)|*.yml|All Files (*.*)|*.*",
                Title = "Open Codeplug"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadCodeplug(openFileDialog.FileName);

                settingsManager.LastCodeplugPath = openFileDialog.FileName;
                noSaveSettingsOnClose = false;
                settingsManager.SaveSettings();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PageRID_Click(object sender, RoutedEventArgs e)
        {
            DigitalPageWindow pageWindow = new DigitalPageWindow(Codeplug.Systems);
            pageWindow.Owner = this;
            pageWindow.Title = "Page Subscriber";

            if (pageWindow.ShowDialog() == true)
            {
                // throw an error if the user does the dumb...
                if (pageWindow.DstId == string.Empty)
                {
                    MessageBox.Show($"Must supply a destination ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(pageWindow.RadioSystem.Name);
                IOSP_CALL_ALRT callAlert = new IOSP_CALL_ALRT(uint.Parse(pageWindow.DstId), uint.Parse(pageWindow.RadioSystem.Rid));

                RemoteCallData callData = new RemoteCallData
                {
                    SrcId = uint.Parse(pageWindow.RadioSystem.Rid),
                    DstId = uint.Parse(pageWindow.DstId),
                    LCO = P25Defines.TSBK_IOSP_CALL_ALRT
                };

                byte[] tsbk = new byte[P25Defines.P25_TSBK_LENGTH_BYTES];

                callAlert.Encode(ref tsbk);

                fne.SendP25TSBK(callData, tsbk);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RadioCheckRID_Click(object sender, RoutedEventArgs e)
        {
            DigitalPageWindow pageWindow = new DigitalPageWindow(Codeplug.Systems);
            pageWindow.Owner = this;
            pageWindow.Title = "Radio Check Subscriber";

            if (pageWindow.ShowDialog() == true)
            {
                // throw an error if the user does the dumb...
                if (pageWindow.DstId == string.Empty)
                {
                    MessageBox.Show($"Must supply a destination ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(pageWindow.RadioSystem.Name);
                IOSP_EXT_FNCT extFunc = new IOSP_EXT_FNCT((ushort)ExtendedFunction.CHECK, uint.Parse(pageWindow.RadioSystem.Rid), uint.Parse(pageWindow.DstId));

                RemoteCallData callData = new RemoteCallData
                {
                    SrcId = uint.Parse(pageWindow.RadioSystem.Rid),
                    DstId = uint.Parse(pageWindow.DstId),
                    LCO = P25Defines.TSBK_IOSP_EXT_FNCT
                };

                byte[] tsbk = new byte[P25Defines.P25_TSBK_LENGTH_BYTES];

                extFunc.Encode(ref tsbk);

                fne.SendP25TSBK(callData, tsbk);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void InhibitRID_Click(object sender, RoutedEventArgs e)
        {
            DigitalPageWindow pageWindow = new DigitalPageWindow(Codeplug.Systems);
            pageWindow.Owner = this;
            pageWindow.Title = "Inhibit Subscriber";

            if (pageWindow.ShowDialog() == true)
            {
                // throw an error if the user does the dumb...
                if (pageWindow.DstId == string.Empty)
                {
                    MessageBox.Show($"Must supply a destination ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(pageWindow.RadioSystem.Name);
                IOSP_EXT_FNCT extFunc = new IOSP_EXT_FNCT((ushort)ExtendedFunction.INHIBIT, P25Defines.WUID_FNE, uint.Parse(pageWindow.DstId));

                RemoteCallData callData = new RemoteCallData
                {
                    SrcId = uint.Parse(pageWindow.RadioSystem.Rid),
                    DstId = uint.Parse(pageWindow.DstId),
                    LCO = P25Defines.TSBK_IOSP_EXT_FNCT
                };

                byte[] tsbk = new byte[P25Defines.P25_TSBK_LENGTH_BYTES];

                extFunc.Encode(ref tsbk);

                fne.SendP25TSBK(callData, tsbk);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UninhibitRID_Click(object sender, RoutedEventArgs e)
        {
            DigitalPageWindow pageWindow = new DigitalPageWindow(Codeplug.Systems);
            pageWindow.Owner = this;
            pageWindow.Title = "Uninhibit Subscriber";

            if (pageWindow.ShowDialog() == true)
            {
                // throw an error if the user does the dumb...
                if (pageWindow.DstId == string.Empty)
                {
                    MessageBox.Show($"Must supply a destination ID.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(pageWindow.RadioSystem.Name);
                IOSP_EXT_FNCT extFunc = new IOSP_EXT_FNCT((ushort)ExtendedFunction.UNINHIBIT, P25Defines.WUID_FNE, uint.Parse(pageWindow.DstId));

                RemoteCallData callData = new RemoteCallData
                {
                    SrcId = uint.Parse(pageWindow.RadioSystem.Rid),
                    DstId = uint.Parse(pageWindow.DstId),
                    LCO = P25Defines.TSBK_IOSP_EXT_FNCT
                };

                byte[] tsbk = new byte[P25Defines.P25_TSBK_LENGTH_BYTES];

                extFunc.Encode(ref tsbk);

                fne.SendP25TSBK(callData, tsbk);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ManualPage_Click(object sender, RoutedEventArgs e)
        {
            QuickCallPage pageWindow = new QuickCallPage();
            pageWindow.Owner = this;

            if (pageWindow.ShowDialog() == true)
            {
                foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
                {
                    Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                    if (system == null)
                    {
                        Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {ERR_INVALID_CODEPLUG}.");
                        channel.IsSelected = false;
                        selectedChannelsManager.RemoveSelectedChannel(channel);
                        continue;
                    }

                    Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                    if (cpgChannel == null)
                    {
                        Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {ERR_INVALID_CODEPLUG}.");
                        channel.IsSelected = false;
                        selectedChannelsManager.RemoveSelectedChannel(channel);
                        continue;
                    }

                    PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                    if (fne == null)
                    {
                        MessageBox.Show($"{channel.ChannelName} has a {ERR_INVALID_FNE_REF}. {PLEASE_RESTART_CONSOLE}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        channel.IsSelected = false;
                        selectedChannelsManager.RemoveSelectedChannel(channel);
                        continue;
                    }

                    // 
                    if (channel.PageState)
                    {
                        ToneGenerator generator = new ToneGenerator();

                        double toneADuration = 1.0;
                        double toneBDuration = 3.0;

                        byte[] toneA = generator.GenerateTone(Double.Parse(pageWindow.ToneA), toneADuration);
                        byte[] toneB = generator.GenerateTone(Double.Parse(pageWindow.ToneB), toneBDuration);

                        byte[] combinedAudio = new byte[toneA.Length + toneB.Length];
                        Buffer.BlockCopy(toneA, 0, combinedAudio, 0, toneA.Length);
                        Buffer.BlockCopy(toneB, 0, combinedAudio, toneA.Length, toneB.Length);

                        int chunkSize = PCM_SAMPLES_LENGTH;
                        int totalChunks = (combinedAudio.Length + chunkSize - 1) / chunkSize;

                        Task.Run(() =>
                        {
                            //_waveProvider.ClearBuffer();
                            audioManager.AddTalkgroupStream(cpgChannel.Tgid, combinedAudio);
                        });

                        await Task.Run(() =>
                        {
                            for (int i = 0; i < totalChunks; i++)
                            {
                                int offset = i * chunkSize;
                                int size = Math.Min(chunkSize, combinedAudio.Length - offset);

                                byte[] chunk = new byte[chunkSize];
                                Buffer.BlockCopy(combinedAudio, offset, chunk, 0, size);

                                if (chunk.Length == 320)
                                {
                                    if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                                        P25EncodeAudioFrame(chunk, fne, channel, cpgChannel, system);
                                    else if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.DMR)
                                        DMREncodeAudioFrame(chunk, fne, channel, cpgChannel, system);
                                }
                            }
                        });

                        double totalDurationMs = (toneADuration + toneBDuration) * 1000 + 750;
                        await Task.Delay((int)totalDurationMs  + 4000);

                        fne.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), false);

                        Dispatcher.Invoke(() =>
                        {
                            //channel.PageState = false; // TODO: Investigate
                            channel.PageSelectButton.Background = ChannelBox.GRAY_GRADIENT;
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TogglePTTMode_Click(object sender, RoutedEventArgs e)
        {
            settingsManager.TogglePTTMode = menuTogglePTTMode.IsChecked;

            // update elements
            foreach (UIElement child in channelsCanvas.Children)
            {
                if (child is ChannelBox)
                    ((ChannelBox)child).PTTToggleMode = settingsManager.TogglePTTMode;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AudioSettings_Click(object sender, RoutedEventArgs e)
        {
            List<Codeplug.Channel> channels = Codeplug?.Zones.SelectMany(z => z.Channels).ToList() ?? new List<Codeplug.Channel>();

            AudioSettingsWindow audioSettingsWindow = new AudioSettingsWindow(settingsManager, audioManager, channels);
            audioSettingsWindow.ShowDialog();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetSettings_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show("Are you sure to wish to reset console settings?", "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult == MessageBoxResult.Yes)
            {
                MessageBox.Show("Settings will be reset after console restart.", "Reset Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                noSaveSettingsOnClose = true;
                settingsManager.Reset();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectWidgets_Click(object sender, RoutedEventArgs e)
        {
            WidgetSelectionWindow widgetSelectionWindow = new WidgetSelectionWindow();
            widgetSelectionWindow.Owner = this;
            if (widgetSelectionWindow.ShowDialog() == true)
            {
                settingsManager.ShowSystemStatus = widgetSelectionWindow.ShowSystemStatus;
                settingsManager.ShowChannels = widgetSelectionWindow.ShowChannels;
                settingsManager.ShowAlertTones = widgetSelectionWindow.ShowAlertTones;

              
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddAlertTone_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
                Title = "Select Alert Tone"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string alertFilePath = openFileDialog.FileName;
                AlertTone alertTone = new AlertTone(alertFilePath);

                alertTone.OnAlertTone += SendAlertTone;

                // widget placement
                alertTone.MouseRightButtonDown += AlertTone_MouseRightButtonDown;
                alertTone.MouseRightButtonUp += AlertTone_MouseRightButtonUp;
                alertTone.MouseMove += AlertTone_MouseMove;

                if (settingsManager.AlertTonePositions.TryGetValue(alertFilePath, out var position))
                {
                    Canvas.SetLeft(alertTone, position.X);
                    Canvas.SetTop(alertTone, position.Y);
                }
                else
                {
                    Canvas.SetLeft(alertTone, 20);
                    Canvas.SetTop(alertTone, 20);
                }

                channelsCanvas.Children.Add(alertTone);
                settingsManager.UpdateAlertTonePaths(alertFilePath);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OpenUserBackground_Click(object sender, RoutedEventArgs e)
        {
            if (!windowLoaded)
                return;

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JPEG Files (*.jpg)|*.jpg|PNG Files (*.png)|*.png|All Files (*.*)|*.*",
                Title = "Open User Background"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                settingsManager.UserBackgroundImage = openFileDialog.FileName;
                settingsManager.SaveSettings();
                UpdateBackground();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleDarkMode_Click(object sender, RoutedEventArgs e)
        {
            if (!windowLoaded)
                return;

            settingsManager.DarkMode = menuDarkMode.IsChecked;
            UpdateBackground();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleLockWidgets_Click(object sender, RoutedEventArgs e)
        {
            if (!windowLoaded)
                return;

            settingsManager.LockWidgets = !settingsManager.LockWidgets;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleSnapCallHistory_Click(object sender, RoutedEventArgs e)
        {
            if (!windowLoaded)
                return;

            settingsManager.SnapCallHistoryToWindow = !settingsManager.SnapCallHistoryToWindow;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleKeepWindowOnTop_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;

            if (!windowLoaded)
                return;

            settingsManager.KeepWindowOnTop = !settingsManager.KeepWindowOnTop;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResizeCanvasToWindow_Click(object sender, RoutedEventArgs e)
        {
            const double widthOffset = 16;
            const double heightOffset = 115;

            foreach (UIElement child in channelsCanvas.Children)
            {
                double childLeft = Canvas.GetLeft(child) + child.RenderSize.Width;
                if (childLeft > ActualWidth)
                    Canvas.SetLeft(child, ActualWidth - (child.RenderSize.Width + widthOffset));
                double childBottom = Canvas.GetTop(child) + child.RenderSize.Height;
                if (childBottom > ActualHeight)
                    Canvas.SetTop(child, ActualHeight - (child.RenderSize.Height + heightOffset));
            }

            channelsCanvas.Width = ActualWidth;
            canvasScrollViewer.Width = ActualWidth;
            channelsCanvas.Height = ActualHeight;
            canvasScrollViewer.Height = ActualHeight;

            settingsManager.CanvasWidth = ActualWidth;
            settingsManager.CanvasHeight = ActualHeight;

            settingsManager.WindowWidth = ActualWidth;
            settingsManager.WindowHeight = ActualHeight;
        }

        /** Widget Controls */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChannelBox_HoldChannelButtonClicked(object sender, ChannelBox e)
        {
            if (e.SystemName == PLAYBACKSYS || e.ChannelName == PLAYBACKCHNAME || e.DstId == PLAYBACKTG)
                return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChannelBox_PageButtonClicked(object sender, ChannelBox e)
        {
            if (e.SystemName == PLAYBACKSYS || e.ChannelName == PLAYBACKCHNAME || e.DstId == PLAYBACKTG)
                return;

            Codeplug.System system = Codeplug.GetSystemForChannel(e.ChannelName);
            if (system == null)
            {
                MessageBox.Show($"{e.ChannelName} refers to an {INVALID_SYSTEM} {e.SystemName}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.IsSelected = false;
                selectedChannelsManager.RemoveSelectedChannel(e);
                return;
            }

            Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(e.ChannelName);
            if (cpgChannel == null)
            {
                // bryanb: this should actually never happen...
                MessageBox.Show($"{e.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.IsSelected = false;
                selectedChannelsManager.RemoveSelectedChannel(e);
                return;
            }

            PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
            if (fne == null)
            {
                MessageBox.Show($"{e.ChannelName} has a {ERR_INVALID_FNE_REF}. {PLEASE_RESTART_CONSOLE}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.IsSelected = false;
                selectedChannelsManager.RemoveSelectedChannel(e);
                return;
            }

            if (e.PageState)
                fne.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), true);
            else
                fne.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChannelBox_PTTButtonClicked(object sender, ChannelBox e)
        {
            if (e.SystemName == PLAYBACKSYS || e.ChannelName == PLAYBACKCHNAME || e.DstId == PLAYBACKTG)
                return;

            Codeplug.System system = Codeplug.GetSystemForChannel(e.ChannelName);
            if (system == null)
            {
                MessageBox.Show($"{e.ChannelName} refers to an {INVALID_SYSTEM} {e.SystemName}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.IsSelected = false;
                selectedChannelsManager.RemoveSelectedChannel(e);
                return;
            }

            Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(e.ChannelName);
            if (cpgChannel == null)
            {
                // bryanb: this should actually never happen...
                MessageBox.Show($"{e.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.IsSelected = false;
                selectedChannelsManager.RemoveSelectedChannel(e);
                return;
            }

            PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
            if (fne == null)
            {
                MessageBox.Show($"{e.ChannelName} has a {ERR_INVALID_FNE_REF}. {PLEASE_RESTART_CONSOLE}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.IsSelected = false;
                selectedChannelsManager.RemoveSelectedChannel(e);
                return;
            }

            if (!e.IsSelected)
                return;

            FneUtils.Memset(e.mi, 0x00, P25Defines.P25_MI_LENGTH);

            uint srcId = uint.Parse(system.Rid);
            uint dstId = uint.Parse(cpgChannel.Tgid);

            if (e.PttState)
            {
                if (e.TxStreamId != 0)
                    Log.WriteWarning($"{e.ChannelName} CHANNEL still had a TxStreamId? This shouldn't happen.");

                e.TxStreamId = fne.NewStreamId();
                Log.WriteLine($"({system.Name}) {e.ChannelMode.ToUpperInvariant()} Traffic *CALL START     * SRC_ID {srcId} TGID {dstId} [STREAM ID {e.TxStreamId}]");
                e.VolumeMeterLevel = 0;
                if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                    fne.SendP25TDU(srcId, dstId, true);
            }
            else
            {
                e.VolumeMeterLevel = 0;
                Log.WriteLine($"({system.Name}) {e.ChannelMode.ToUpperInvariant()} Traffic *CALL END       * SRC_ID {srcId} TGID {dstId} [STREAM ID {e.TxStreamId}]");
                if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                    fne.SendP25TDU(srcId, dstId, false);
                else if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.DMR)
                    fne.SendDMRTerminator(srcId, dstId, 1, e.dmrSeqNo, e.dmrN, e.embeddedData);

                // reset values
                ResetChannel();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ChannelBox_PTTButtonPressed()
        {


            if (!pttState)
            {
                Codeplug.System system = Codeplug.GetSystemForChannel(currentchannel);
                if (system == null)
                {
                    MessageBox.Show($"{currentchannel} refers to an {INVALID_SYSTEM} {currentsystem}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(currentchannel);
                if (cpgChannel == null)
                {
                    // bryanb: this should actually never happen...
                    MessageBox.Show($"{currentchannel} refers to an {INVALID_CODEPLUG_CHANNEL}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
 
                    return;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                if (fne == null)
                {
                    MessageBox.Show($"{currentchannel} has a {ERR_INVALID_FNE_REF}. {PLEASE_RESTART_CONSOLE}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
 
                    return;
                }

                FneUtils.Memset(mi, 0x00, P25Defines.P25_MI_LENGTH);
                string SSISISIS = Convert.ToInt64(headcode, 16).ToString();
				uint srcId = uint.Parse(SSISISIS);
				uint dstId = uint.Parse(currentdestID);

				if (TxStreamId != 0)
                    Log.WriteWarning($"{currentchannel} CHANNEL still had a TxStreamId? This shouldn't happen.");

                TxStreamId = fne.NewStreamId();
                Log.WriteLine($"({system.Name}) {currentmode.ToUpperInvariant()} Traffic *CALL START     * SRC_ID {srcId} TGID {dstId} [STREAM ID {TxStreamId}]");
                if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                    fne.SendP25TDU(srcId, dstId, true);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ChannelBox_PTTButtonReleased()
        {

            if (pttState)
            {
                Codeplug.System system = Codeplug.GetSystemForChannel(currentchannel);
                if (system == null)
                {
                    MessageBox.Show($"{currentchannel} refers to an {INVALID_SYSTEM} {currentsystem}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    return;
                }

                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(currentchannel);
                if (cpgChannel == null)
                {
                    // bryanb: this should actually never happen...
                    MessageBox.Show($"{currentchannel} refers to an {INVALID_CODEPLUG_CHANNEL}. {PLEASE_CHECK_CODEPLUG}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(currentsystem);
                if (fne == null)
                {
                    MessageBox.Show($"{currentchannel} has a {ERR_INVALID_FNE_REF}. {PLEASE_RESTART_CONSOLE}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    
                    return;
                }



				string SSISISIS = Convert.ToInt64(headcode, 16).ToString();
				uint srcId = uint.Parse(SSISISIS);
				uint dstId = uint.Parse(currentdestID);

                Log.WriteLine($"({currentsystem}) {currentmode.ToUpperInvariant()} Traffic *CALL END       * SRC_ID {srcId} TGID {dstId} [STREAM ID {TxStreamId}]");
                //e.VolumeMeterLevel = 0;
                if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.P25)
                    fne.SendP25TDU(srcId, dstId, false);
                else if (cpgChannel.GetChannelMode() == Codeplug.ChannelMode.DMR)
                    fne.SendDMRTerminator(srcId, dstId, 1, dmrSeqNo, dmrN, embeddedData);

                ResetChannel();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChannelBox_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.LockWidgets || !(sender is UIElement element))
                return;

            draggedElement = element;
            startPoint = e.GetPosition(channelsCanvas);
            offsetX = startPoint.X - Canvas.GetLeft(draggedElement);
            offsetY = startPoint.Y - Canvas.GetTop(draggedElement);
            isDragging = true;

            Cursor = Cursors.ScrollAll;

            element.CaptureMouse();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChannelBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.LockWidgets || !isDragging || draggedElement == null)
                return;

            Cursor = Cursors.Arrow;

            isDragging = false;
            draggedElement.ReleaseMouseCapture();
            draggedElement = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ChannelBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (settingsManager.LockWidgets || !isDragging || draggedElement == null) 
                return;

            Point currentPosition = e.GetPosition(channelsCanvas);

            // Calculate the new position with snapping to the grid
            double newLeft = Math.Round((currentPosition.X - offsetX) / GridSize) * GridSize;
            double newTop = Math.Round((currentPosition.Y - offsetY) / GridSize) * GridSize;

            // Ensure the box stays within canvas bounds
            newLeft = Math.Max(0, Math.Min(newLeft, channelsCanvas.ActualWidth - draggedElement.RenderSize.Width));
            newTop = Math.Max(0, Math.Min(newTop, channelsCanvas.ActualHeight - draggedElement.RenderSize.Height));

            // Apply snapped position
            Canvas.SetLeft(draggedElement, newLeft);
            Canvas.SetTop(draggedElement, newTop);

            // Save the new position if it's a ChannelBox
            if (draggedElement is ChannelBox channelBox)
                settingsManager.UpdateChannelPosition(channelBox.ChannelName, newLeft, newTop);
        }

        /// <summary>
        /// Activates Global PTT after a click or keyboard shortcut
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemStatusBox_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => ChannelBox_MouseRightButtonDown(sender, e);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemStatusBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.LockWidgets)
                return;

            if (sender is SystemStatusBox systemStatusBox)
            {
                double x = Canvas.GetLeft(systemStatusBox);
                double y = Canvas.GetTop(systemStatusBox);
                settingsManager.SystemStatusPositions[systemStatusBox.SystemName] = new ChannelPosition { X = x, Y = y };

                ChannelBox_MouseRightButtonUp(sender, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SystemStatusBox_MouseMove(object sender, MouseEventArgs e) => ChannelBox_MouseMove(sender, e);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AlertTone_MouseRightButtonDown(object sender, MouseButtonEventArgs e) => ChannelBox_MouseRightButtonDown(sender, e);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AlertTone_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.LockWidgets)
                return;

            if (sender is AlertTone alertTone)
            {
                double x = Canvas.GetLeft(alertTone);
                double y = Canvas.GetTop(alertTone);
                settingsManager.UpdateAlertTonePosition(alertTone.AlertFilePath, x, y);

                ChannelBox_MouseRightButtonUp(sender, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AlertTone_MouseMove(object sender, MouseEventArgs e) => ChannelBox_MouseMove(sender, e);

        /** WPF Ribbon Controls */

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void GlobalPTTActivate(object sender, RoutedEventArgs e)
        {
            if (globalPttState)
                await Task.Delay(500);

            ChannelBox primaryChannel = selectedChannelsManager.PrimaryChannel;

            if (primaryChannel != null)
            {
                Dispatcher.Invoke(() =>
                {
                    if (globalPttState)
                        btnGlobalPtt.Background = ChannelBox.RED_GRADIENT;
                    else
                        btnGlobalPtt.Background = btnGlobalPttDefaultBg;
                });
                
                primaryChannel.TriggerPTTState(globalPttState);

                return;
            }

            
            // Check for global PTT keys all preference, if not enabled, return early
            if (!settingsManager.GlobalPTTKeysAllChannels)
            {
                return;
            }

            foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
            {
                if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                    continue;

                Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                if (system == null)
                {
                    Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {ERR_INVALID_CODEPLUG}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                if (cpgChannel == null)
                {
                    Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {ERR_INVALID_CODEPLUG}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                PeerSystem fne = fneSystemManager.GetFneSystem(system.Name);
                if (fne == null)
                {
                    Log.WriteLine($"{channel.ChannelName} has a {ERR_INVALID_FNE_REF}. {ERR_INVALID_CODEPLUG}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                channel.TxStreamId = fne.NewStreamId();
                if (globalPttState)
                {
                    Dispatcher.Invoke(() =>
                    {
                        btnGlobalPtt.Background = ChannelBox.RED_GRADIENT;
                        channel.PttState = true;
                    });

                    fne.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), true);
                }
                else
                {
                    Dispatcher.Invoke(() =>
                    {
                        btnGlobalPtt.Background = btnGlobalPttDefaultBg;
                        channel.PttState = false;
                    });

                    fne.SendP25TDU(uint.Parse(system.Rid), uint.Parse(cpgChannel.Tgid), false);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void btnGlobalPtt_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.TogglePTTMode)
                return;

            globalPttState = !globalPttState;

            GlobalPTTActivate(sender, e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void btnGlobalPtt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.TogglePTTMode)
            {
                globalPttState = !globalPttState;
                GlobalPTTActivate(sender, e);
            }
            else
            {
                globalPttState = true;
                GlobalPTTActivate(sender, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void btnGlobalPtt_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (settingsManager.TogglePTTMode)
                return;

            globalPttState = false;
            GlobalPTTActivate(sender, e);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAlert1_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => {
                SendAlertTone(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/alert1.wav"));
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAlert2_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SendAlertTone(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/alert2.wav"));
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAlert3_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                SendAlertTone(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/alert3.wav"));
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            selectAll = !selectAll;
            foreach (ChannelBox channel in channelsCanvas.Children.OfType<ChannelBox>())
            {
                if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                    continue;

                Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                if (system == null)
                {
                    Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {ERR_INVALID_CODEPLUG}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                if (cpgChannel == null)
                {
                    Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {ERR_INVALID_CODEPLUG}.");
                    channel.IsSelected = false;
                    selectedChannelsManager.RemoveSelectedChannel(channel);
                    continue;
                }

                channel.IsSelected = selectAll;
                channel.Background = channel.IsSelected ? ChannelBox.BLUE_GRADIENT : ChannelBox.DARK_GRAY_GRADIENT;

                if (channel.IsSelected)
                    selectedChannelsManager.AddSelectedChannel(channel);
                else
                    selectedChannelsManager.RemoveSelectedChannel(channel);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void KeyStatus_Click(object sender, RoutedEventArgs e)
        {
            KeyStatusWindow keyStatus = new KeyStatusWindow(Codeplug, this);
            keyStatus.Owner = this;
            keyStatus.Show();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CallHist_Click(object sender, RoutedEventArgs e)
        {
            callHistoryWindow.Owner = this;
            if (callHistoryWindow.Visibility == Visibility.Visible)
                callHistoryWindow.Hide();
            else
            {
                callHistoryWindow.Show();

                if (settingsManager.SnapCallHistoryToWindow && WindowState != WindowState.Maximized)
                {
                    if (ActualHeight > callHistoryWindow.Height)
                        callHistoryWindow.Height = ActualHeight;

                    callHistoryWindow.Left = Left + ActualWidth + 5;
                    callHistoryWindow.Top = Top;
                }
            }
        }

        /** fnecore Hooks / Helpers */

        /// <summary>
        /// Handler for FNE key responses.
        /// </summary>
        /// <param name="e"></param>
        public void KeyResponseReceived(KeyResponseEvent e)
        {
            //Log.WriteLine($"Message ID: {e.KmmKey.MessageId}");
            //Log.WriteLine($"Decrypt Info Format: {e.KmmKey.DecryptInfoFmt}");
            //Log.WriteLine($"Algorithm ID: {e.KmmKey.AlgId}");
            //Log.WriteLine($"Key ID: {e.KmmKey.KeyId}");
            //Log.WriteLine($"Keyset ID: {e.KmmKey.KeysetItem.KeysetId}");
            //Log.WriteLine($"Keyset Alg ID: {e.KmmKey.KeysetItem.AlgId}");
            //Log.WriteLine($"Keyset Key Length: {e.KmmKey.KeysetItem.KeyLength}");
            //Log.WriteLine($"Number of Keys: {e.KmmKey.KeysetItem.Keys.Count}");

            foreach (var key in e.KmmKey.KeysetItem.Keys)
            {
                //Log.WriteLine($"  Key Format: {key.KeyFormat}");
                //Log.WriteLine($"  SLN: {key.Sln}");
                //Log.WriteLine($"  Key ID: {key.KeyId}");
                //Log.WriteLine($"  Key Data: {BitConverter.ToString(key.GetKey())}");

                Dispatcher.Invoke(() =>
                {
                    foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
                    {
                        if (channel.SystemName == PLAYBACKSYS || channel.ChannelName == PLAYBACKCHNAME || channel.DstId == PLAYBACKTG)
                            continue;

                        Codeplug.System system = Codeplug.GetSystemForChannel(channel.ChannelName);
                        if (system == null)
                        {
                            Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_SYSTEM} {channel.SystemName}. {ERR_INVALID_CODEPLUG}.");
                            channel.IsSelected = false;
                            selectedChannelsManager.RemoveSelectedChannel(channel);
                            continue;
                        }

                        Codeplug.Channel cpgChannel = Codeplug.GetChannelByName(channel.ChannelName);
                        if (cpgChannel == null)
                        {
                            Log.WriteLine($"{channel.ChannelName} refers to an {INVALID_CODEPLUG_CHANNEL}. {ERR_INVALID_CODEPLUG}.");
                            channel.IsSelected = false;
                            selectedChannelsManager.RemoveSelectedChannel(channel);
                            continue;
                        }

                        ushort keyId = cpgChannel.GetKeyId();
                        byte algoId = cpgChannel.GetAlgoId();
                        KeysetItem receivedKey = e.KmmKey.KeysetItem;

                        if (keyId != 0 && algoId != 0 && keyId == key.KeyId && algoId == receivedKey.AlgId)
                            channel.Crypter.SetKey(key.KeyId, receivedKey.AlgId, key.GetKey());
                    }
                });
            }
        }

        /** Keyboard Shortcuts */

        /// <summary>
        /// Sets the global PTT keybind
        /// Hooks a listener to listen for a keypress, then saves that as the global PTT keybind
        /// Global PTT keybind is effectively the same as pressing the Global PTT button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private async void SetGlobalPTTKeybind(object sender, RoutedEventArgs e)
        {
            
            // Create and show a MessageBox with no buttons or standard close behavior
            Window messageBox = new Window
            {
                Width = 500,
                Height = 150,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Topmost = true,
                Background = System.Windows.Media.Brushes.White,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Press any key to set the Global PTT shortcut...",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    FontSize = 16,
                    FontWeight = System.Windows.FontWeights.Bold,
                }
            };

            // Center messageBox on the main window
            messageBox.Owner = this; // Set the current window as owner
            messageBox.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            // Open and close the MessageBox after 500ms
            messageBox.Show();
            Keys keyPress = await keyboardManager.GetNextKeyPress();
            messageBox.Close();
            settingsManager.GlobalPTTShortcut = keyPress;
            InitializeKeyboardShortcuts();
            settingsManager.SaveSettings();
            MessageBox.Show("Global PTT shortcut set to " + keyPress.ToString(), "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Initializes global keyboard shortcut listener
        /// </summary>
        private void InitializeKeyboardShortcuts()
        {
            var listeningKeys = new List<Keys> { settingsManager.GlobalPTTShortcut };
            keyboardManager.SetListenKeys(listeningKeys);
            // Clear event listener
            keyboardManager.OnKeyEvent -= KeyboardManagerOnKeyEvent;
            // Re-add listener
            keyboardManager.OnKeyEvent += KeyboardManagerOnKeyEvent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pressedKey"></param>
        /// <param name="state"></param>
        private void KeyboardManagerOnKeyEvent(Keys pressedKey,GlobalKeyboardHook.KeyboardState state)
        {
            if (pressedKey == settingsManager.GlobalPTTShortcut)
            {
                if(state is GlobalKeyboardHook.KeyboardState.KeyDown or GlobalKeyboardHook.KeyboardState.SysKeyDown)
                {
                    globalPttState = true;
                    GlobalPTTActivate(null, null);
                }
                else
                {
                    globalPttState = false;
                    GlobalPTTActivate(null, null);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToggleGlobalPTTAllChannels_Click(object sender, RoutedEventArgs e)
        {
            settingsManager.GlobalPTTKeysAllChannels = !settingsManager.GlobalPTTKeysAllChannels;
        }
		
		private void regButton_Click(object sender, RoutedEventArgs e)
		{
            if (isregged == true)
            {
				UpdateRadioBackground("schcondereg.png");
                dereging = true;
				chnameP0.Text = "";
				chnameP1.Text = "";
				chnameP2.Text = "";
				chnameP3.Text = "";
				chnameP4.Text = "";
				chnameP5.Text = "";
				chnameP6.Text = "";
				chnameP7.Text = "";
				chnameP8.Text = "";
				chnameP9.Text = "";
				chnameP10.Text = "";
				chnameP11.Text = "";
			}
            else 
            {
                isbooted = true;
                isreging = true;
				UpdateRadioBackground("schaskRegCode.png");
                regbackgroundworker1.RunWorkerAsync();
			}
		}

		private void bootbackgroundworker1_DoWork(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;
			Thread.Sleep(1500);
			
		}

		private void bootbackgroundworker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			UpdateRadioBackground("bg_main_hd_light.png");
		}
        private int regBgWkrstatus { get; set; } //1-Reg Ok 2 - Reg Fail - 3 RegTimeout
		private void regbackgroundworker1_DoWork(object sender, DoWorkEventArgs e)
		{
			BackgroundWorker worker = sender as BackgroundWorker;
            do 
            {
				do
				{
					do
					{
						do
						{
							do
							{
								do
								{
								} while (hc6entered == false);
							} while (hc5entered == false);
						} while (hc4entered == false);
					} while (hc3entered == false);
				} while (hc2entered == false);
			} while (hc1entered == false);
		}

        private async void regtimeouttimerTick(object sender, ElapsedEventArgs e) 
        {
            if (sysregstate == false) 
            {
				UpdateRadioBackground("sChregfail.png");
			}
        }

		private void regbackgroundworker2_DoWork(object sender, DoWorkEventArgs e) 
        {
        }
        private void regbackgroundworker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) 
        {
		}
        private System.Timers.Timer regtimeouttimer = new System.Timers.Timer();
		private void regbackgroundworker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			string tmphc = regheadcode0.Text + regheadcode1.Text + regheadcode2.Text + regheadcode3.Text + regheadcode4.Text + regheadcode5.Text;
            if (IsHex(tmphc) == true && tmphc!= "")
            {
                long tmphcint = Convert.ToInt64(tmphc, 16);
                if (tmphcint > 0)
                {
                    if (tmphcint <= 16777214)
                    {
                        regtimeouttimer.Enabled= true;
                        regtimeouttimer.Interval = 30000;
						UpdateRadioBackground("registering.png");
                        regtimeouttimer.Elapsed += regtimeouttimerTick;
                        regtimeouttimer.AutoReset = false;
                        regtimeouttimer.Start();
						GenerateChannelWidgets();
                        EnableControls();
                        headcode = tmphc;
					}
                    else 
                    {
						MediaPlayer mediaPlayer = new MediaPlayer();
						mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
						mediaPlayer.Play();
						UpdateRadioBackground("bg_main_hd_light.png");
						isreging = false;
						isregged = false;
						hc6entered = false;
						hc5entered = false;
						hc4entered = false;
						hc3entered = false;
						hc2entered = false;
						hc1entered = false;

						//Max ID
						regheadcode0.Text = "M";
						regheadcode1.Text = "a";
						regheadcode2.Text = "x";
						regheadcode3.Text = "";
						regheadcode4.Text = "I";
						regheadcode5.Text = "D";
						ridaliasP6.Text = "";
						ridaliasP7.Text = "";
						ridaliasP8.Text = "";
						ridaliasP9.Text = "";
						ridaliasP10.Text = "";
						ridaliasP11.Text = "";
						errmsgbackgroundworker1.RunWorkerAsync();
					}

                }
                else 
                {
					MediaPlayer mediaPlayer = new MediaPlayer();
					mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
					mediaPlayer.Play();
					UpdateRadioBackground("bg_main_hd_light.png");
					isreging = false;
					isregged = false;
					hc6entered = false;
					hc5entered = false;
					hc4entered = false;
					hc3entered = false;
					hc2entered = false;
					hc1entered = false;

					//Invalid ID
					regheadcode0.Text = "I";
					regheadcode1.Text = "n";
					regheadcode2.Text = "v";
					regheadcode3.Text = "a";
					regheadcode4.Text = "l";
					regheadcode5.Text = "i";
					ridaliasP6.Text = "d";
					ridaliasP7.Text = "";
					ridaliasP8.Text = "I";
					ridaliasP9.Text = "D";
					ridaliasP10.Text = "";
					ridaliasP11.Text = "";
					errmsgbackgroundworker1.RunWorkerAsync();
				}
			}
            else 
            {
				MediaPlayer mediaPlayer = new MediaPlayer();
				mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
				mediaPlayer.Play();
				UpdateRadioBackground("bg_main_hd_light.png");
				isreging = false;
				isregged = false;
				hc6entered = false;
				hc5entered = false;
				hc4entered = false;
				hc3entered = false;
				hc2entered = false;
				hc1entered = false;

				//Hex Only
				regheadcode0.Text = "H";
				regheadcode1.Text = "e";
				regheadcode2.Text = "x";
				regheadcode3.Text = "";
				regheadcode4.Text = "O";
				regheadcode5.Text = "n";
				ridaliasP6.Text = "l";
				ridaliasP7.Text = "y";
				ridaliasP8.Text = "";
				ridaliasP9.Text = "";
				ridaliasP10.Text = "";
				ridaliasP11.Text = "";
                errmsgbackgroundworker1.RunWorkerAsync();
			}
		}
		public static bool IsHex(string text)
		{
			return text.All(c => "0123456789abcdefABCDEF\n".Contains(c));
		}
		/// <summary>
		/// Check Button
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void checkButton_Click(object sender, RoutedEventArgs e)
		{
            if (dereging == true) 
            {
				PeerSystem peer = fneSystemManager.GetFneSystem(currentsystem);
				peer.Stop();
				dereging = false;
				UpdateRadioBackground("bg_main_hd_light.png");
				headcodeb0.Visibility = Visibility.Hidden;
				headcodeb1.Visibility = Visibility.Hidden;
				headcodeb2.Visibility = Visibility.Hidden;
				headcodeb3.Visibility = Visibility.Hidden;
				headcodeb4.Visibility = Visibility.Hidden;
				headcodeb5.Visibility = Visibility.Hidden;
				hc1entered = false;
				hc2entered = false;
				hc3entered = false;
				hc4entered = false;
				hc5entered = false;
				hc6entered = false;
				DisableControls();
				isregged = false;
				isreging = false;
				updateHeadcode("00000");
				chnameP0.Text = "";
				chnameP1.Text = "";
				chnameP2.Text = "";
				chnameP3.Text = "";
				chnameP4.Text = "";
				chnameP5.Text = "";
				chnameP6.Text = "";
				chnameP7.Text = "";
				chnameP8.Text = "";
				chnameP9.Text = "";
				chnameP10.Text = "";
				chnameP11.Text = "";
				currentchannel = "";
                currentsystem = "";
                currentdestID = "";
                currentmode = "p25";
			}
            else if (isreging == true)
            {
                if (hc1entered == false) { hc1entered = true; }
                else if (hc2entered == false) { hc2entered = true; }
                else if (hc3entered == false) { hc3entered = true; }
                else if (hc4entered == false) { hc4entered = true; }
                else if (hc5entered == false) { hc5entered = true; }
				else if (hc6entered == false) { hc6entered = true; }
                HC1Click = 0;
				HC2Click = 0;
				HC3Click = 0;
				HC4Click = 0;
				HC5Click = 0;
				HC6Click = 0;
			}
            else if (selectingchannel == true) 
            {
				selectingchannel=false;
                UpdateRadioBackground("bg_main_hd_light.png");

                currentchannel = regheadcode0.Text + regheadcode1.Text+regheadcode2.Text + regheadcode3.Text + regheadcode4.Text + regheadcode5.Text + ridaliasP6.Text+ ridaliasP7.Text+ ridaliasP8.Text+ ridaliasP9.Text+ ridaliasP10.Text+ ridaliasP11.Text;
                currentchannel= currentchannel.Replace(" ", "");
                Channel SELCH =Codeplug.GetChannelByName(currentchannel);
                currentmode = SELCH.Mode;
				currentdestID = SELCH.Tgid;
				currentsystem = SELCH.System;
				chnameP0.Text = regheadcode0.Text;
				chnameP1.Text = regheadcode1.Text;
				chnameP2.Text = regheadcode2.Text;
				chnameP3.Text = regheadcode3.Text;
				chnameP4.Text = regheadcode4.Text;
				chnameP5.Text = regheadcode5.Text;
				chnameP6.Text = ridaliasP6.Text;
				chnameP7.Text = ridaliasP7.Text;
				chnameP8.Text = ridaliasP8.Text;
				chnameP9.Text = ridaliasP9.Text;
				chnameP10.Text = ridaliasP10.Text;
				chnameP11.Text = ridaliasP11.Text;


                #region Clearing out the lines no longer needed
                //Select Arrow
                chSelArrowP0.Text = "";
				chSelArrowP1.Text = "";

				//Row 1 Pixels
				chListR0P0.Text = "";
				chListR0P1.Text = "";
				chListR0P2.Text = "";
				chListR0P3.Text = "";
				chListR0P4.Text = "";
				chListR0P5.Text = "";
				chListR0P6.Text = "";
				chListR0P7.Text = "";
				chListR0P8.Text = "";
				chListR0P9.Text = "";
				chListR0P10.Text = "";
				chListR0P11.Text = "";

				//Row 2 Pixels
				chListP0.Text = "";
				chListP1.Text = "";
				chListP2.Text = "";
				chListP3.Text = "";
				chListP4.Text = "";
				chListP5.Text = "";
				chListP6.Text = "";
				chListP7.Text = "";
				chListP8.Text = "";
				chListP9.Text = "";
				chListP10.Text = "";
				chListP11.Text = "";

				//Row 3 Pixels
				regheadcode0.Text = "";
				regheadcode1.Text = "";
				regheadcode2.Text = "";
				regheadcode3.Text = "";
				regheadcode4.Text = "";
				regheadcode5.Text = "";
				ridaliasP6.Text = "";
				ridaliasP7.Text = "";
				ridaliasP8.Text = "";
				ridaliasP9.Text = "";
				ridaliasP10.Text = "";
				ridaliasP11.Text = "";

				/*
				//Row 4 Pixels
				chnameP0.Text = "";
				chnameP1.Text = "";
				chnameP2.Text = "";
				chnameP3.Text = "";
				chnameP4.Text = "";
				chnameP5.Text = "";
				chnameP6.Text = "";
				chnameP7.Text = "";
				chnameP8.Text = "";
				chnameP9.Text = "";
				chnameP10.Text = "";
				chnameP11.Text = "";
                */
				#endregion

				regheadcode0.Text = "R";
				regheadcode1.Text = "e";
				regheadcode2.Text = "g";
				regheadcode3.Text = "i";
				regheadcode4.Text = "s";
				regheadcode5.Text = "t";
				ridaliasP6.Text = "e";
				ridaliasP7.Text = "r";
				ridaliasP8.Text = "i";
				ridaliasP9.Text = "n";
				ridaliasP10.Text = "g";
				ridaliasP11.Text = "";
				chregbackgroundworker1.RunWorkerAsync();

			}
		}

        /// <summary>
        /// X Button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void cansbutton_Click(object sender, RoutedEventArgs e)
		{
			if (dereging == true)
			{
				dereging = false;
				UpdateRadioBackground("bg_main_hd_light.png");
                do { currentchannel = currentchannel + " "; } while (currentchannel.Length < 11);
                char[] tmpcurchname = currentchannel.ToCharArray();
				chnameP0.Text = tmpcurchname[0].ToString();
				chnameP1.Text = tmpcurchname[1].ToString();
				chnameP2.Text = tmpcurchname[2].ToString();
				chnameP3.Text = tmpcurchname[3].ToString();
				chnameP4.Text = tmpcurchname[4].ToString();
				chnameP5.Text = tmpcurchname[5].ToString();
				chnameP6.Text = tmpcurchname[6].ToString();
				chnameP7.Text = tmpcurchname[7].ToString();
				chnameP8.Text = tmpcurchname[8].ToString();
				chnameP9.Text = tmpcurchname[9].ToString();
				chnameP10.Text = tmpcurchname[10].ToString();
				chnameP11.Text = tmpcurchname[11].ToString();
			}
		}

        /// <summary>
        /// Update headcode entry
        /// </summary>
        /// <param name="newheadcode"></param>
        private void updateHeadcode(string newheadcode) 
        {
            switch (newheadcode.Length)
            {
                case 0:
                    newheadcode = "000000";
                    break;
                case 1:
					newheadcode = "00000"+newheadcode;
					break;
                case 2:
					newheadcode = "0000" + newheadcode;
					break;
                case 3:
					newheadcode = "000" + newheadcode;
					break;
                case 4:
					newheadcode = "00" + newheadcode;
					break;
				case 5:
					newheadcode = "0" + newheadcode;
					break;
			}
            char[] headcode=newheadcode.ToCharArray();

			headcodeb0.Text = headcode[0].ToString();
            headcodeb1.Text = headcode[1].ToString();
            headcodeb2.Text = headcode[2].ToString();
            headcodeb3.Text = headcode[3].ToString();
            headcodeb4.Text = headcode[4].ToString();
			headcodeb5.Text = headcode[5].ToString();
		}
        private int HC1Click;
		private int HC2Click;
		private int HC3Click;
		private int HC4Click;
		private int HC5Click;
		private int HC6Click;
		private void onebutton_Click(object sender, RoutedEventArgs e)
		{
            if (hc1entered==false) 
            {
				regheadcode0.Text = "1";
            }
			else if (hc2entered == false)
			{
				regheadcode1.Text = "1";
			}
			else if (hc3entered == false)
			{
				regheadcode2.Text = "1";
			}
			else if (hc4entered == false)
			{
				regheadcode3.Text = "1";
			}
			else if (hc5entered == false)
			{
				regheadcode4.Text = "1";
			}
			else if (hc6entered == false)
			{
				regheadcode5.Text = "1";
			}
		}

		private void twoabcButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "2"; HC1Click++; }
				else if(HC1Click == 1) { regheadcode0.Text = "A"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "B"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "C"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "2"; HC1Click=1; }

			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "2"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "A"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "B"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "C"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "2"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "2"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "A"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "B"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "C"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "2"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "2"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "A"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "B"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "C"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "2"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "2"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "A"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "B"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "C"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "2"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "2"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "A"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "B"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "C"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "2"; HC6Click = 1; }
			}
		}

		private void threedefButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "3"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "D"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "E"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "F"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "3"; HC1Click = 1; }
			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "3"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "D"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "E"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "F"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "3"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "3"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "D"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "E"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "F"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "3"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "3"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "D"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "E"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "F"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "3"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "3"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "D"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "E"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "F"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "3"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "3"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "D"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "E"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "F"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "3"; HC6Click = 1; }
			}
		}

		private void fourghiButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "4"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "G"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "H"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "I"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "4"; HC1Click = 1; }
			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "4"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "G"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "H"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "I"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "3"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "4"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "G"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "H"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "I"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "3"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "4"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "G"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "H"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "I"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "3"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "4"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "G"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "H"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "I"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "3"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "4"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "G"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "H"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "I"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "3"; HC6Click = 1; }
			}
		}

		private void fivejklButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "5"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "J"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "K"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "L"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "5"; HC1Click = 1; }

			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "5"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "J"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "K"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "L"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "5"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "5"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "J"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "K"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "L"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "5"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "5"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "J"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "K"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "L"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "5"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "5"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "J"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "K"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "L"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "5"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "5"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "J"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "K"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "L"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "5"; HC6Click = 1; }
			}
		}

		private void sixmnoButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "6"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "M"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "N"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "O"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "6"; HC1Click = 1; }
			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "6"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "M"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "N"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "O"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "6"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "6"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "M"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "N"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "O"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "6"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "6"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "M"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "N"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "O"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "6"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "6"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "M"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "N"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "O"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "6"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "6"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "M"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "N"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "O"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "6"; HC6Click = 1; }
			}
		}

		private void sevenpqrsButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "7"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "P"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "Q"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "R"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "S"; HC1Click++; }
				else if (HC1Click == 5) { regheadcode0.Text = "7"; HC1Click = 1; }
			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "7"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "P"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "Q"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "R"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "S"; HC2Click++; }
				else if (HC2Click == 5) { regheadcode1.Text = "7"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "7"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "P"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "Q"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "R"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "S"; HC3Click++; }
				else if (HC3Click == 5) { regheadcode2.Text = "7"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "7"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "P"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "Q"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "R"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "S"; HC4Click++; }
				else if (HC4Click == 5) { regheadcode3.Text = "7"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "7"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "P"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "Q"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "R"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "S"; HC5Click++; }
				else if (HC5Click == 5) { regheadcode4.Text = "7"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "7"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "P"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "Q"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "R"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "S"; HC6Click++; }
				else if (HC6Click == 5) { regheadcode5.Text = "7"; HC6Click = 1; }
			}
		}

		private void eighttuvButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "8"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "T"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "U"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "V"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "8"; HC1Click = 1; }
			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "8"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "T"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "U"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "V"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "8"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "8"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "T"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "U"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "V"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "8"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "8"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "T"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "U"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "V"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "8"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "8"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "T"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "U"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "V"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "8"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "8"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "T"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "U"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "V"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "8"; HC6Click = 1; }
			}
		}

		private void ninewxyzButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				if (HC1Click == 0) { regheadcode0.Text = "9"; HC1Click++; }
				else if (HC1Click == 1) { regheadcode0.Text = "W"; HC1Click++; }
				else if (HC1Click == 2) { regheadcode0.Text = "X"; HC1Click++; }
				else if (HC1Click == 3) { regheadcode0.Text = "Y"; HC1Click++; }
				else if (HC1Click == 4) { regheadcode0.Text = "Z"; HC1Click++; }
				else if (HC1Click == 5) { regheadcode0.Text = "9"; HC1Click = 1; }

			}
			else if (hc2entered == false)
			{
				if (HC2Click == 0) { regheadcode1.Text = "9"; HC2Click++; }
				else if (HC2Click == 1) { regheadcode1.Text = "W"; HC2Click++; }
				else if (HC2Click == 2) { regheadcode1.Text = "X"; HC2Click++; }
				else if (HC2Click == 3) { regheadcode1.Text = "Y"; HC2Click++; }
				else if (HC2Click == 4) { regheadcode1.Text = "Z"; HC2Click++; }
				else if (HC2Click == 5) { regheadcode1.Text = "9"; HC2Click = 1; }
			}
			else if (hc3entered == false)
			{
				if (HC3Click == 0) { regheadcode2.Text = "9"; HC3Click++; }
				else if (HC3Click == 1) { regheadcode2.Text = "W"; HC3Click++; }
				else if (HC3Click == 2) { regheadcode2.Text = "X"; HC3Click++; }
				else if (HC3Click == 3) { regheadcode2.Text = "Y"; HC3Click++; }
				else if (HC3Click == 4) { regheadcode2.Text = "Z"; HC3Click++; }
				else if (HC3Click == 5) { regheadcode2.Text = "9"; HC3Click = 1; }
			}
			else if (hc4entered == false)
			{
				if (HC4Click == 0) { regheadcode3.Text = "9"; HC4Click++; }
				else if (HC4Click == 1) { regheadcode3.Text = "W"; HC4Click++; }
				else if (HC4Click == 2) { regheadcode3.Text = "X"; HC4Click++; }
				else if (HC4Click == 3) { regheadcode3.Text = "Y"; HC4Click++; }
				else if (HC4Click == 4) { regheadcode3.Text = "Z"; HC4Click++; }
				else if (HC4Click == 5) { regheadcode3.Text = "9"; HC4Click = 1; }
			}
			else if (hc5entered == false)
			{
				if (HC5Click == 0) { regheadcode4.Text = "9"; HC5Click++; }
				else if (HC5Click == 1) { regheadcode4.Text = "W"; HC5Click++; }
				else if (HC5Click == 2) { regheadcode4.Text = "X"; HC5Click++; }
				else if (HC5Click == 3) { regheadcode4.Text = "Y"; HC5Click++; }
				else if (HC5Click == 4) { regheadcode4.Text = "Z"; HC5Click++; }
				else if (HC5Click == 5) { regheadcode4.Text = "9"; HC5Click = 1; }
			}
			else if (hc6entered == false)
			{
				if (HC6Click == 0) { regheadcode5.Text = "9"; HC6Click++; }
				else if (HC6Click == 1) { regheadcode5.Text = "W"; HC6Click++; }
				else if (HC6Click == 2) { regheadcode5.Text = "X"; HC6Click++; }
				else if (HC6Click == 3) { regheadcode5.Text = "Y"; HC6Click++; }
				else if (HC6Click == 4) { regheadcode5.Text = "Z"; HC6Click++; }
				else if (HC6Click == 5) { regheadcode5.Text = "9"; HC6Click = 1; }
			}
		}

		private void zeroButton_Click(object sender, RoutedEventArgs e)
		{
			if (hc1entered == false)
			{
				regheadcode0.Text = "0";
			}
			else if (hc2entered == false)
			{
				regheadcode1.Text = "0";
			}
			else if (hc3entered == false)
			{
				regheadcode2.Text = "0";
			}
			else if (hc4entered == false)
			{
				regheadcode3.Text = "0";
			}
			else if (hc5entered == false)
			{
				regheadcode4.Text = "0";
			}
			else if (hc6entered == false)
			{
				regheadcode5.Text = "0";
			}
		}

        private void RXCall() 
        {
            RXbgtimer.Start();
        }

        private void OnRXbgtimerTick(object sender, EventArgs e) 
        {
            if (curRXbg == 0) 
            {
                UpdateRadioBackground("shincomingfull.png");
                curRXbg = 1;
            }
            else if(curRXbg == 1)
            {
                UpdateRadioBackground("shincomingcall.png");
				curRXbg = 0;
			}
        }

		private void sgButton_Click(object sender, RoutedEventArgs e)
		{

            if (RXbgtimer.IsEnabled == true)
            {
                RXbgtimer.Stop();
            }
            else { RXCall(); }
		}

        /// <summary>
        /// Scroll offset for channel scroll list
        /// </summary>
        private int scrollvalue;

        /// <summary>
        /// Char Array for Line 1 During Channel List
        /// </summary>
        private char[] L0ChList= {' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ' };
		/// <summary>
		/// Char Array for Line 2 During Channel List
		/// </summary>
		private char[] L1ChList = { ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ' };
		/// <summary>
		/// Char Array for Line 3 During Channel List
		/// </summary>
		private char[] L2ChList = { ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ' };
		/// <summary>
		/// Char Array for Line 4 During Channel List
		/// </summary>
		private char[] L3ChList = { ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ' };

		/// <summary>
		/// Display TG List from codeplug
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void pbookButton_Click(object sender, RoutedEventArgs e)
		{
            if (isregged == true) 
            {
				channelNames.Clear();
				channelNames.Add("");
				channelNames.Add("");
                if ( Codeplug != null )
                {
                    foreach (var Zone in Codeplug.Zones)
                    {
                        foreach (var Channel in Zone.Channels)
                        {
                            channelNames.Add(Channel.Name);
                        }
                    }
                    if (channelNames[channelNames.Count - 1] != "End Of List")
                    {
                        channelNames.Add("End Of List");
                    }

                    selectingchannel = true;
                    chSelArrowP0.Text = "<";
                    chSelArrowP1.Text = "-";
                    updateChannelListDisplay();
                }
                else 
                {
					//No Codeplug
					regheadcode0.Text = "N";
					regheadcode1.Text = "o";
					regheadcode2.Text = "";
					regheadcode3.Text = "C";
					regheadcode4.Text = "o";
					regheadcode5.Text = "d";
					ridaliasP6.Text = "e";
					ridaliasP7.Text = "p";
					ridaliasP8.Text = "l";
					ridaliasP9.Text = "u";
					ridaliasP10.Text = "g";
					ridaliasP11.Text = "";
					MediaPlayer mediaPlayer = new MediaPlayer();
					mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
					mediaPlayer.Play();
					errmsgbackgroundworker1.RunWorkerAsync();

				}
				
			}
            else 
            {
				//No Register
				regheadcode0.Text = "N";
				regheadcode1.Text = "o";
				regheadcode2.Text = "";
				regheadcode3.Text = "R";
				regheadcode4.Text = "e";
				regheadcode5.Text = "g";
				ridaliasP6.Text = "i";
				ridaliasP7.Text = "s";
				ridaliasP8.Text = "t";
				ridaliasP9.Text = "e";
				ridaliasP10.Text = "r";
				ridaliasP11.Text = "";
				MediaPlayer mediaPlayer = new MediaPlayer();
				mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S2_warning.wav")));
				mediaPlayer.Play();
                errmsgbackgroundworker1.RunWorkerAsync();
			}

		}
        private bool newvolButPress;
        /// <summary>
        /// Volume/List up button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void volupButton_Click(object sender, RoutedEventArgs e)
		{
            if (selectingchannel == true)
            {
                if (scrollvalue != 0)
                {
                    scrollvalue--;
                    updateChannelListDisplay();
                }
            }
            else 
            {
                newvolButPress = true;
				regheadcode0.Text = "";
				regheadcode1.Text = "";
				regheadcode2.Text = "";
				regheadcode3.Text = "";
				regheadcode4.Text = "";
				regheadcode5.Text = "";
				if (volume == 4) { UpdateRadioBackground("schvol4.png"); }
				else if (volume == 3) { UpdateRadioBackground("schvol4.png"); volume = 4; }
				else if (volume == 2) { UpdateRadioBackground("schvol3.png"); volume = 3; }
				else if (volume == 1) { UpdateRadioBackground("schvol2.png"); volume = 2; }
				else if (volume == 0) { UpdateRadioBackground("schvol1.png"); volume = 1; }
                try { volbackgroundworker1.RunWorkerAsync(); } catch (Exception ex) { }
                
			}
		}

		/// <summary>
		/// Volume/List down button
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void voldnButton_Click(object sender, RoutedEventArgs e)
		{
            if (selectingchannel == true)
            {
                int currentL4 = scrollvalue + 4;
                if (currentL4 != channelNames.Count)
                {
                    scrollvalue++;
                    updateChannelListDisplay();
                }
            }
            else 
            {
                newvolButPress = true;
				regheadcode0.Text = "";
				regheadcode1.Text = "";
				regheadcode2.Text = "";
				regheadcode3.Text = "";
				regheadcode4.Text = "";
				regheadcode5.Text = "";
				if (volume == 0) { UpdateRadioBackground("schvol0.png"); }
				else if (volume == 1) { UpdateRadioBackground("schvol0.png"); volume = 0; }
				else if (volume == 2) { UpdateRadioBackground("schvol1.png"); volume = 1; }
				else if (volume == 3) { UpdateRadioBackground("schvol2.png"); volume = 2; }
				else if (volume == 4) { UpdateRadioBackground("schvol3.png"); volume = 3; }
				try { volbackgroundworker1.RunWorkerAsync(); } catch (Exception ex) { }
			}
		}
        private int bstimerthing;
		private void volbackgroundworker1_DoWork(object sender, DoWorkEventArgs e)
		{
            do { newvolButPress = false; Thread.Sleep(3500); } while (newvolButPress == true);
		}

        private void volbackgroundworker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
            audioManager.SetTalkgroupVolume(currentdestID, (float)volume);
		    UpdateRadioBackground("bg_main_hd_light.png");
		}

		private void chregbackgroundworker1_DoWork(object sender, DoWorkEventArgs e) 
        {
            Thread.Sleep(1500);
            
		}

        private void chregbackgroundworker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) 
        {
			MediaPlayer mediaPlayer = new MediaPlayer();
			mediaPlayer.Open(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Audio/S_info.wav")));
			mediaPlayer.Play();
			regheadcode0.Text = "";
			regheadcode1.Text = "";
			regheadcode2.Text = "";
			regheadcode3.Text = "";
			regheadcode4.Text = "";
			regheadcode5.Text = "";
			ridaliasP6.Text = "";
			ridaliasP7.Text = "";
			ridaliasP8.Text = "";
			ridaliasP9.Text = "";
			ridaliasP10.Text = "";
			ridaliasP11.Text = "";
		}

		private void errmsgbackgroundworker1_DoWork(object sender, DoWorkEventArgs e)
		{
			Thread.Sleep(1500);

		}

		private void errmsgbackgroundworker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			regheadcode0.Text = "";
			regheadcode1.Text = "";
			regheadcode2.Text = "";
			regheadcode3.Text = "";
			regheadcode4.Text = "";
			regheadcode5.Text = "";
			ridaliasP6.Text = "";
			ridaliasP7.Text = "";
			ridaliasP8.Text = "";
			ridaliasP9.Text = "";
			ridaliasP10.Text = "";
			ridaliasP11.Text = "";
		}

		/// <summary>
		/// Update the channel list display
		/// </summary>
		private void updateChannelListDisplay() 
        {
            UpdateRadioBackground("bg_main_hd_dark.png");
            int L1ScrollV = scrollvalue + 1;
			int L2ScrollV = scrollvalue + 2;
			int L3ScrollV = scrollvalue + 3;

            do 
            {
                channelNames[scrollvalue] = channelNames[scrollvalue] + " ";
			} while (channelNames[scrollvalue].Length < 12);
			L0ChList = channelNames[scrollvalue].ToCharArray();

			do
			{
				channelNames[L1ScrollV] = channelNames[L1ScrollV] + " ";
			} while (channelNames[L1ScrollV].Length < 12);
			L1ChList = channelNames[L1ScrollV].ToCharArray();

			do
			{
				channelNames[L2ScrollV] = channelNames[L2ScrollV] + " ";
			} while (channelNames[L2ScrollV].Length < 12);
			L2ChList = channelNames[L2ScrollV].ToCharArray();

			do
			{
				channelNames[L3ScrollV] = channelNames[L3ScrollV] + " ";
			} while (channelNames[L3ScrollV].Length < 12);
			L3ChList = channelNames[L3ScrollV].ToCharArray();

            //Row 1 Pixels
            chListR0P0.Text = L0ChList[0].ToString();
			chListR0P1.Text = L0ChList[1].ToString();
			chListR0P2.Text = L0ChList[2].ToString();
			chListR0P3.Text = L0ChList[3].ToString();
			chListR0P4.Text = L0ChList[4].ToString();
			chListR0P5.Text = L0ChList[5].ToString();
			chListR0P6.Text = L0ChList[6].ToString();
			chListR0P7.Text = L0ChList[7].ToString();
			chListR0P8.Text = L0ChList[8].ToString();
			chListR0P9.Text = L0ChList[9].ToString();
			chListR0P10.Text = L0ChList[10].ToString();
			chListR0P11.Text = L0ChList[11].ToString();

			//Row 2 Pixels
			chListP0.Text = L1ChList[0].ToString();
			chListP1.Text = L1ChList[1].ToString();
			chListP2.Text = L1ChList[2].ToString();
			chListP3.Text = L1ChList[3].ToString();
			chListP4.Text = L1ChList[4].ToString();
			chListP5.Text = L1ChList[5].ToString();
			chListP6.Text = L1ChList[6].ToString();
			chListP7.Text = L1ChList[7].ToString();
			chListP8.Text = L1ChList[8].ToString();
			chListP9.Text = L1ChList[9].ToString();
			chListP10.Text = L1ChList[10].ToString();
			chListP11.Text = L1ChList[11].ToString();

			//Row 3 Pixels
			regheadcode0.Text = L2ChList[0].ToString();
			regheadcode1.Text = L2ChList[1].ToString();
			regheadcode2.Text = L2ChList[2].ToString();
			regheadcode3.Text = L2ChList[3].ToString();
			regheadcode4.Text = L2ChList[4].ToString();
			regheadcode5.Text = L2ChList[5].ToString();
			ridaliasP6.Text = L2ChList[6].ToString();
			ridaliasP7.Text = L2ChList[7].ToString();
			ridaliasP8.Text = L2ChList[8].ToString();
			ridaliasP9.Text = L2ChList[9].ToString();
			ridaliasP10.Text = L2ChList[10].ToString();
			ridaliasP11.Text = L2ChList[11].ToString();

			//Row 4 Pixels
			chnameP0.Text = L3ChList[0].ToString();
			chnameP1.Text = L3ChList[1].ToString();
			chnameP2.Text = L3ChList[2].ToString();
			chnameP3.Text = L3ChList[3].ToString();
			chnameP4.Text = L3ChList[4].ToString();
			chnameP5.Text = L3ChList[5].ToString();
			chnameP6.Text = L3ChList[6].ToString();
			chnameP7.Text = L3ChList[7].ToString();
			chnameP8.Text = L3ChList[8].ToString();
			chnameP9.Text = L3ChList[9].ToString();
			chnameP10.Text = L3ChList[10].ToString();
			chnameP11.Text = L3ChList[11].ToString();
		}

        /// <summary>
        /// PTT Key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
		private void pttButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
            Log.WriteLog("PTTButtonPress");
			//if (IsTxEncrypted && !Crypter.HasKey())
			//{
			//	//TODO: Move this to a Radio Error
			//	MessageBox.Show($"{currentchannel} {"ERR_NO_LOADED_ENC_KEY"}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			//	pttState = false;
			//	return;
			//}

			//pttState = true;
			//ChannelBox_PTTButtonPressed();

		}


		/// <summary>
		/// PTT Release
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void pttButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			Log.WriteLog("PTTButtonPressRel");
			//ChannelBox_PTTButtonReleased();
			//pttState = false;
		}

        /// <summary>
        /// 
        /// </summary>
        private void SysRegStateChange() 
        {
            if (sysregstate == false && isregged==true)
            {
				//Registration Lost
				regheadcode0.Text = "S";
				regheadcode1.Text = "y";
				regheadcode2.Text = "s";
				regheadcode3.Text = "";
				regheadcode4.Text = "C";
				regheadcode5.Text = "o";
				ridaliasP6.Text = "n";
				ridaliasP7.Text = "n";
				ridaliasP8.Text = "";
				ridaliasP9.Text = "L";
				ridaliasP10.Text = "S";
				ridaliasP11.Text = "T";
			}
            else if (sysregstate == true) 
            {
            
            }
        }

		private void pttButton_Click(object sender, RoutedEventArgs e)
		{
			Log.WriteLog("PTTButtonClick");
			if (pttState == false) 
            {
				if (IsTxEncrypted && !Crypter.HasKey())
				{
					//TODO: Move this to a Radio Error
					MessageBox.Show($"{currentchannel} {"ERR_NO_LOADED_ENC_KEY"}.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					pttState = false;
					return;
				}

				
				//ChannelBox_PTTButtonPressed();
				foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
				{
                    object obj = new object();
                    RoutedEventArgs REA = new RoutedEventArgs();
                    channel.PttButton_Click(obj,REA);
				}
				pttState = true;
			}
            else if (pttState == true) 
            {
				foreach (ChannelBox channel in selectedChannelsManager.GetSelectedChannels())
				{
					object obj = new object();
					RoutedEventArgs REA = new RoutedEventArgs();
					channel.PttButton_Click(obj, REA);
				}
				//ChannelBox_PTTButtonReleased();
				pttState = false;
			}
			
		}
	} // public partial class MainWindow : Window
} // namespace dvmconsole
