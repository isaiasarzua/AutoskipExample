using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using LibVLCSharp.Shared;

namespace AutoskipExample
{
    /// <summary>
    /// Represents the main viewmodel.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel() { }

        ProcessAutoskip processAutoskip;

        private LibVLC _libVLC;
        public LibVLC LibVLC
        {
            get => _libVLC;
            private set => Set(nameof(LibVLC), ref _libVLC, value);
        }

        private CastModel castModel { get; set; }
        public CastModel CastModel { get => castModel; }

        private MediaPlayer _mediaPlayer;
        public MediaPlayer MediaPlayer
        {
            get => _mediaPlayer;
            private set => Set(nameof(MediaPlayer), ref _mediaPlayer, value);
        }

        /// <summary>
        /// Initialize LibVLC MainWindow is created
        /// </summary>
        public void OnLoad()
        {
            Core.Initialize();

            castModel = new CastModel();

            LibVLC = new LibVLC();

            Play(new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4"));


            castModel.DiscoverRenderers(LibVLC);

            processAutoskip = new ProcessAutoskip();
            processAutoskip.MatchFound += ShowSkipBtn;
            ButtonUpdateVisibility = "Hidden";
        }

        private string _buttonUpdateVisibility;
        public string ButtonUpdateVisibility
        {
            get => _buttonUpdateVisibility;
            set
            {
                _buttonUpdateVisibility = value;
                PropertyChanged(this, new PropertyChangedEventArgs(nameof(ButtonUpdateVisibility)));
            }
        }
        private void ShowSkipBtn()
        {
            matchFoundTimestamp = (long)MediaPlayer.Time;
            ButtonUpdateVisibility = "Visible";
        }

        private void Set<T>(string propertyName, ref T field, T value)
        {
            if (field == null && value != null || field != null && !field.Equals(value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void videoviewDrop(string fileName)
        {
            ButtonUpdateVisibility = "Hidden";

            StartAutoSkip(fileName);

            Uri uri = new Uri(fileName);

            Play(uri);
        }

        #region When playing file dispose old media player and create new one
        private void Play(Uri mediaUri)
        {
            Stop();
            var media = new Media(LibVLC, mediaUri.AbsoluteUri, FromType.FromLocation);
            MediaPlayer = new MediaPlayer(LibVLC);
            //videoView.MediaPlayer = _mediaPlayer;
            MediaPlayer.TimeChanged += MediaPlayer_TimeChanged;
            MediaPlayer.Play(media);
        }
        private void Stop()
        {
            var toDispose = MediaPlayer;
            //videoView.MediaPlayer = null;
            MediaPlayer = null;
            Task.Run(() =>
            {
                toDispose?.Dispose();
            });
        }
        #endregion

        public void StartAutoSkip(string grabMedia)
        {
            if (skipWindow.currentProfile != null)
            {
                processAutoskip.introScreenshot = skipWindow.currentProfile.introScreenshot;
                processAutoskip.StartGrab(grabMedia);
            }
        }

        #region Timestamp  

        private string playbackTime;
        public string PlaybackTime
        {
            get { return playbackTime; }
            set { playbackTime = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(PlaybackTime))); }
        }

        private string fileLength;
        public string FileLength
        {
            get { return fileLength; }
            set { fileLength = value; PropertyChanged(this, new PropertyChangedEventArgs(nameof(FileLength))); }
        }

        private void MediaPlayer_TimeChanged(object sender, MediaPlayerTimeChangedEventArgs e)
        {
            PlaybackTime = TimeSpan.FromMilliseconds(_mediaPlayer.Time).ToString().Substring(0, 8);
            FileLength = TimeSpan.FromMilliseconds(_mediaPlayer.Length).ToString().Substring(0, 8);
        }

        #endregion

        #region Commands  
        public bool CanExecute
        {
            get
            {
                // check if executing is allowed, i.e., validate, check if a process is running, etc. 
                return true || false;
            }
        }

        #region Video Controls
        private ICommand _togglePause;
        public ICommand TogglePause
        {
            get
            {
                return _togglePause ?? (_togglePause = new CommandHandler(() => MediaPlayer.Pause(), () => CanExecute));
            }
        }

        private ICommand _stopPlayback;
        public ICommand StopPlayback
        {
            get
            {
                return _stopPlayback ?? (_stopPlayback = new CommandHandler(() => StopPlay(), () => CanExecute));
            }
        }
        private void StopPlay()
        {
            if (_mediaPlayer != null)
                _mediaPlayer.Stop();

            if (processAutoskip.mediaPlayer != null)
                processAutoskip.mediaPlayer.Stop();
        }
        #endregion

        // Handle skip setup window
        readonly AutoSkipWindow skipWindow = new AutoSkipWindow();
        private ICommand _setupSkip;
        public ICommand SetupSkip
        {
            get
            {
                return _setupSkip ?? (_setupSkip = new CommandHandler(() => skipWindow.Show(), () => CanExecute));
            }
        }

        // Handle render discover and casting to render
        private RendererItem _renderer;
        public RendererItem NewRenderer
        {
            get
            {
                return this._renderer;
            }
            set
            {
                this._renderer = value;
            }
        }

        public ICommand StartCasting
        {
            get
            {
                { return new RelayCommand<RendererItem>(CastingTo); }
            }
        }

        void CastingTo(RendererItem e)
        {
            // set the previously discovered renderer item (chromecast) on the mediaplayer
            // if you set it to null, it will start to render normally (i.e. locally) again
            MediaPlayer.SetRenderer(e);
        }

        long matchFoundTimestamp;
        private ICommand _skipIntro;
        public ICommand SkipIntro
        {
            get
            {
                return _skipIntro ?? (_skipIntro = new CommandHandler(() => Skip_Clicked(), () => CanExecute));
            }
        }


        private void Skip_Clicked()
        {
            _mediaPlayer.Time = skipWindow.currentProfile.introLength + matchFoundTimestamp;
            //SkipBtn.Visibility = Visibility.Hidden;
        }


        #endregion
    }
}