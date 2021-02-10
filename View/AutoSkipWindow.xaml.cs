using System;
using System.Windows;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;

namespace AutoskipExample
{
    /// <summary>
    /// Interaction logic for AutoSkipWindow.xaml
    /// </summary>
    public partial class AutoSkipWindow : Window
    {
        ObservableCollection<SkipProfile> list;
        List<SkipProfile> profiles;
        string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string filename;

        public AutoSkipWindow()
        {
            InitializeComponent();

            list = new ObservableCollection<SkipProfile>();

            // Load autoskip profiles from file if it exists
            filename = Path.Combine(myDocuments, "Autoskip_Profiles");
            if (File.Exists(filename))
            {
                profiles = XmlSerialization.ReadFromXmlFile<List<SkipProfile>>(filename);

                foreach (SkipProfile profile in profiles)
                {
                    list.Add(profile);
                }

                // Add profiles to combobox via ObservableCollection
                profileSelect.ItemsSource = list;
            }
            else
            {
                //create profiles list
                profiles = new List<SkipProfile>();
            }

            #region If profile value is changed, enable saving, otherwise disable
            introLengthMin.ValueChanged += (s, e) => saveProfileBtn.IsEnabled = true;
            introLengthSec.ValueChanged += (s, e) => saveProfileBtn.IsEnabled = true;
            profileSelect.TextInput += (s, e) => saveProfileBtn.IsEnabled = true; // doesn't work when changing profile name, needs fix
            introLoadRefBtn.Click += (s, e) => saveProfileBtn.IsEnabled = true;
            outroLoadRefBtn.Click += (s, e) => saveProfileBtn.IsEnabled = true;
            #endregion
        }

        private const uint Width = 720;
        private const uint Height = 480;

        public SkipProfile currentProfile;

        private Image<Rgba32> introImage = null;
        private Image<Rgba32> endingImage = null;

        private void introLoadRefBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog();
            o.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\AutoSkip";
            o.Title = "Select a frame from the opening";
            o.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";

            if ((bool)o.ShowDialog())
            {
                introRefSuccessLabel.Content = "Selected: " + o.SafeFileName;
                introImage = (Image<Rgba32>)Image.Load(o.FileName);
                introImage.Mutate(ctx => ctx.Resize((int)Width, (int)Height));
            }
        }

        private void outroLoadRefBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog o = new OpenFileDialog();
            o.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + "\\AutoSkip";
            o.Title = "Select a frame from the ending";
            o.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.jpe; *.jfif; *.png";

            if ((bool)o.ShowDialog())
            {
                endingRefSuccessLabel.Content = "Selected: " + o.SafeFileName;
                endingImage = (Image<Rgba32>)Image.Load(o.FileName);
                endingImage.Mutate(ctx => ctx.Resize((int)Width, (int)Height));
            }
        }

        private void LoadProfile(SkipProfile profile)
        {
            introLengthMin.Value = profile.introMin;
            introLengthSec.Value = profile.introSec;
            outroLengthMin.Value = profile.outroLength;
            outroLengthSec.Value = profile.outroLength;

            introImage = Image.Load<Rgba32>(profile.serializedIntro);

            currentProfile = profile;

            saveProfileBtn.IsEnabled = false;
        }

        private void saveProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!String.IsNullOrEmpty(profileSelect.Text))
            {
                // create profile using populated fields
                SkipProfile newSkipProfile = new SkipProfile();
                newSkipProfile.name = profileSelect.Text;
                newSkipProfile.introMin = (int)introLengthMin.Value;
                newSkipProfile.introSec = (int)introLengthSec.Value;
                newSkipProfile.outroLength = (int)outroLengthMin.Value;

                // serialize frame reference so it can be saved
                JpegEncoder jencoder = new JpegEncoder();
                using (var memoryStream = new MemoryStream())
                {
                    introImage.Save(memoryStream, jencoder);
                    memoryStream.Flush();
                    newSkipProfile.serializedIntro = memoryStream.ToArray();
                    newSkipProfile.serializedOutro = memoryStream.ToArray();
                }

                SaveProfile(newSkipProfile);

                currentProfile = newSkipProfile;
                list.Add(newSkipProfile);
                saveProfileBtn.IsEnabled = false;
            }
            else
                MessageBox.Show("Please fill out everything.");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Visibility = Visibility.Hidden;
            e.Cancel = true;
        }

        public void SaveProfile(SkipProfile profile)
        {
            profiles.Add(profile);
            XmlSerialization.WriteToXmlFile<List<SkipProfile>>(filename, profiles);
        }

        private void profileSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            //currentProfile = (SkipProfile)profileSelect.SelectedItem;
            if (profileSelect.SelectedIndex > -1)
                LoadProfile((SkipProfile)profileSelect.SelectedItem);
        }

        private void newProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            profileSelect.SelectedIndex = -1;
            introLengthMin.Value = 0;
            introLengthSec.Value = 0;
            outroLengthMin.Value = 0;
            outroLengthSec.Value = 0;
            saveProfileBtn.IsEnabled = true;
        }
    }
}


public class SkipProfile
{
    public string name { get; set; }
    public int introMin;
    public int introSec;
    public int outroLength;

    public long introLength
    {
        // convert int min and sec fields to timespan, then convert total ms to long as per VLC MediaPlayer.Time requirements
        get { return (long)(TimeSpan.FromMinutes(introMin) + TimeSpan.FromSeconds(introSec)).TotalMilliseconds; }
    }

    public byte[] serializedIntro;
    public byte[] serializedOutro;

    public Image<Rgba32> introScreenshot
    {
        get { return Image.Load<Rgba32>(serializedIntro); }
    }
    public Image<Rgba32> outroScreenshot
    {
        get { return Image.Load<Rgba32>(serializedOutro); }
    }
}