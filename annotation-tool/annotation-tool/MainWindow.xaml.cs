using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using AnnotationTool.views;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;

namespace AnnotationTool
{
    // Interaction logic for MainWindow.xaml
    public partial class MainWindow : Window
    {
        public static event EventHandler MIDIBrowseClick;
        public static event EventHandler Exit;
        public static event EventHandler OpenAnnotationsClick;
        public static event EventHandler SaveAnnotationsClick;
        public static event EventHandler SnapChange;
        public static event EventHandler HorizZoomChange;
        public static event EventHandler VertiZoomChange;
        public static event EventHandler ExpandAll;
        public static event EventHandler CollapseAll;
        public static event EventHandler ShowAll;
        public static event EventHandler HideAll;
        public static event EventHandler AddPattern;
        public static event EventHandler DeletePattern;
        public static event EventHandler KeyVisibilityChange;
        public static event EventHandler GridVisibilityOn;
        public static event EventHandler GridVisibilityOff;
        public static event EventHandler NoteSelectOn;
        public static event EventHandler NoteSelectOff;
        public static event EventHandler AutomaticIconsOn;
        public static event EventHandler AutomaticIconsOff;
        //public static event EventHandler DarkModeOn;
        //public static event EventHandler DarkModeOff;
        public static event EventHandler Play;
        public static event EventHandler Pause;
        public static event EventHandler Stop;
        public static event EventHandler NormaliseVelocitiesOn;
        public static event EventHandler NormaliseVelocitiesOff;
        public static event EventHandler ClosingApp;
        public static Settings settings = new Settings();
        public PianoRollView pianoRoll;

        public MainWindow()
        {
            InitializeComponent();
            SetDefaults();
        }

        private void MainWindow_MIDIBrowseClick(object sender, RoutedEventArgs e)
        {
            MIDIBrowseClick?.Invoke(this, e);

            OpenFileDialog browseDialog = new OpenFileDialog
            {
                Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*"
            };

            if (browseDialog.ShowDialog() == true)
            {
                try
                {
                    Cursor = Cursors.AppStarting;

                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { OpenPianoRoll(browseDialog); })).Wait();

                    mnuView.Visibility = Visibility.Visible;
                    mnuPatterns.Visibility = Visibility.Visible;
                    mnuPlayback.Visibility = Visibility.Visible;
                    txtLoading.Visibility = Visibility.Hidden;

                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { Cursor = Cursors.Arrow; })).Wait();
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Error parsing MIDI file!", "Error");
                }
            }
        }

        private void OpenPianoRoll(OpenFileDialog browseDialog)
        {
            MIDIParser midiParse = new MIDIParser(File.ReadAllBytes(browseDialog.FileName));

            midiParse.fileName = browseDialog.SafeFileName;

            midiParse.ParseFile();
            NoteParser noteParse = new NoteParser(midiParse);
            noteParse.ParseEvents();

            if (pianoRoll == null)
            {
                pianoRoll = new PianoRollView(midiParse, noteParse);
                cntPianoRoll.DataContext = pianoRoll;
            }
            else
            {
                pianoRoll.Reset(midiParse, noteParse);
            }
        }

        private void SetDefaults()
        {
            settings.horizZoom = 1;
            settings.vertiZoom = 1;
            settings.keyNames = 1;
            settings.snap = 3;
            settings.noteSelect = false;
            settings.gridLines = true;
            settings.automaticIcons = true;
            settings.normaliseVelocities = false;

            mnuDefaultKeyNames.IsChecked = true;
            mnuDefaultSnap.IsChecked = true;
            mnuNoteSelect.IsChecked = false;
            mnuGridLines.IsChecked = true;
            mnuAutomaticIcons.IsChecked = true;
            mnuNormaliseVelocities.IsChecked = false;
        }

        private void MainWindow_Exit(object sender, RoutedEventArgs e)
        {
            Exit?.Invoke(this, e);
            Close();
        }

        private void MainWindow_OpenAnnotationsClick(object sender, RoutedEventArgs e)
        {
            OpenAnnotationsClick?.Invoke(this, e);
        }

        private void MainWindow_SaveAnnotationsClick(object sender, RoutedEventArgs e)
        {
            SaveAnnotationsClick?.Invoke(this, e);
        }

        private void MainWindow_SnapChange(object sender, RoutedEventArgs e)
        {
            string snapMenuChoice = ((MenuItem)(((RoutedEventArgs)e).Source)).Header.ToString();

            switch (snapMenuChoice)
            {
                case "Step":
                    settings.snap = 0;
                    break;

                case "1/2 Step":
                    settings.snap = 1;
                    break;

                case "1/3 Step":
                    settings.snap = 2;
                    break;

                case "1/4 Step":
                    settings.snap = 3;
                    break;

                case "1/8 Step":
                    settings.snap = 4;
                    break;
            }

            SnapChange?.Invoke(this, e);
        }

        private void MainWindow_HorizZoomChange(object sender, RoutedEventArgs e)
        {
            string zoomMenuChoice = ((MenuItem)(e.Source)).Header.ToString();

            switch (zoomMenuChoice)
            {
                case "200%":
                    settings.horizZoom = 2.0;
                break;

                case "180%":
                    settings.horizZoom = 1.8;
                break;

                case "160%":
                    settings.horizZoom = 1.6;
                break;

                case "140%":
                    settings.horizZoom = 1.4;
                break;

                case "120%":
                    settings.horizZoom = 1.2;
                break;

                case "100%":
                    settings.horizZoom = 1.0;
                break;

                case "80%":
                    settings.horizZoom = 0.8;
                break;

                case "60%":
                    settings.horizZoom = 0.6;
                break;

                case "40%":
                    settings.horizZoom = 0.4;
                break;

                case "20%":
                    settings.horizZoom = 0.2;
                break;
            }

            HorizZoomChange?.Invoke(this, e);
        }

        private void MainWindow_VertiZoomChange(object sender, RoutedEventArgs e)
        {
            string zoomMenuChoice = ((MenuItem)(e.Source)).Header.ToString();

            switch (zoomMenuChoice)
            {
                case "100%":
                    settings.vertiZoom = 1;
                break;

                case "80%":
                    settings.vertiZoom = 0.8;
                break;

                case "60%":
                    settings.vertiZoom = 0.6;
                break;

                case "40%":
                    settings.vertiZoom = 0.4;
                break;

                case "20%":
                    settings.vertiZoom = 0.2;
                break;
            }

            VertiZoomChange?.Invoke(this, e);
        }

        private void MainWindow_ExpandAll(object sender, RoutedEventArgs e)
        {
            ExpandAll?.Invoke(this, e);
        }

        private void MainWindow_CollapseAll(object sender, RoutedEventArgs e)
        {
            CollapseAll?.Invoke(this, e);
        }

        private void MainWindow_ShowAll(object sender, RoutedEventArgs e)
        {
            ShowAll?.Invoke(this, e);
        }

        private void MainWindow_HideAll(object sender, RoutedEventArgs e)
        {
            HideAll?.Invoke(this, e);
        }

        private void MainWindow_AddPattern(object sender, RoutedEventArgs e)
        {
            AddPattern?.Invoke(this, e);
        }

        private void MainWindow_DeletePattern(object sender, RoutedEventArgs e)
        {
            DeletePattern?.Invoke(this, e);
        }

        private void MainWindow_KeyVisibilityChange(object sender, RoutedEventArgs e)
        {
            string keyVisibilityMenuChoice = ((MenuItem)(e.Source)).Header.ToString();

            switch (keyVisibilityMenuChoice)
            {
                case "_All Keys":
                    settings.keyNames = 0;
                    break;

                case "_Only Octaves":
                    settings.keyNames = 1;
                    break;

                case "_None":
                    settings.keyNames = 2;
                    break;
            }

            KeyVisibilityChange?.Invoke(this, e);
        }

        private void MainWindow_GridVisibilityOn(object sender, RoutedEventArgs e)
        {
            settings.gridLines = true;
            GridVisibilityOn?.Invoke(this, e);
        }

        private void MainWindow_GridVisibilityOff(object sender, RoutedEventArgs e)
        {
            settings.gridLines = false;
            GridVisibilityOff?.Invoke(this, e);
        }

        private void MainWindow_NoteSelectOn(object sender, RoutedEventArgs e)
        {
            settings.noteSelect = true;
            NoteSelectOn?.Invoke(this, e);
        }

        private void MainWindow_NoteSelectOff(object sender, RoutedEventArgs e)
        {
            settings.noteSelect = false;
            NoteSelectOff?.Invoke(this, e);
        }

        private void MainWindow_AutomaticIconsOn(object sender, RoutedEventArgs e)
        {
            settings.automaticIcons = true;
            AutomaticIconsOn?.Invoke(this, e);
        }

        private void MainWindow_AutomaticIconsOff(object sender, RoutedEventArgs e)
        {
            settings.automaticIcons = false;
            AutomaticIconsOff?.Invoke(this, e);
        }

        //private void MainWindow_DarkModeOn(object sender, RoutedEventArgs e)
        //{
        //    settings.darkMode = true;
        //    DarkModeOn?.Invoke(this, e);
        //}

        //private void MainWindow_DarkModeOff(object sender, RoutedEventArgs e)
        //{
        //    settings.darkMode = false;
        //    DarkModeOff?.Invoke(this, e);
        //}

        private void MainWindow_Play(object sender, RoutedEventArgs e)
        {
            Play?.Invoke(this, e);
        }

        private void MainWindow_Pause(object sender, RoutedEventArgs e)
        {
            Pause?.Invoke(this, e);
        }

        private void MainWindow_Stop(object sender, RoutedEventArgs e)
        {
            Stop?.Invoke(this, e);
        }

        private void MainWindow_NormaliseVelocitiesOn(object sender, RoutedEventArgs e)
        {
            settings.normaliseVelocities = true;
            NormaliseVelocitiesOn?.Invoke(this, e);
        }

        private void MainWindow_NormaliseVelocitiesOff(object sender, RoutedEventArgs e)
        {
            settings.normaliseVelocities = false;
            NormaliseVelocitiesOff?.Invoke(this, e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            ClosingApp?.Invoke(this, e);
        }
    }
}