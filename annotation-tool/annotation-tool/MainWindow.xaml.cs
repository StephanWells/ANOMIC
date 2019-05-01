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

namespace AnnotationTool
{
    // Interaction logic for MainWindow.xaml
    public partial class MainWindow : Window
    {
        public static event EventHandler MIDIBrowseClick;
        public static event EventHandler Exit;
        public static event EventHandler SnapChange;
        public static event EventHandler ZoomChange;
        public static event EventHandler ExpandAll;
        public static event EventHandler CollapseAll;
        public static event EventHandler ShowAll;
        public static event EventHandler HideAll;
        public static event EventHandler AddPattern;
        public static event EventHandler DeletePattern;
        public static event EventHandler KeyVisibilityChange;
        public static event EventHandler GridVisibilityChange;
        public static event EventHandler NoteSelect;

        public MainWindow()
        {
            InitializeComponent();
            //this.Loaded += OnLoaded;
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
                    MIDIParser midiParse = new MIDIParser(File.ReadAllBytes(browseDialog.FileName));

                    midiParse.fileName = browseDialog.SafeFileName;
                    midiParse.ParseFile();
                    NoteParser noteParse = new NoteParser(midiParse);
                    noteParse.ParseEvents();

                    DataContext = new PianoRollView(midiParse, noteParse);
                    mnuView.Visibility = Visibility.Visible;
                    mnuPatterns.Visibility = Visibility.Visible;
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Error parsing MIDI file!", "Error");
                }
            }
        }

        private void MainWindow_Exit(object sender, RoutedEventArgs e)
        {
            Exit?.Invoke(this, e);
            Close();
        }

        private void MainWindow_SnapChange(object sender, RoutedEventArgs e)
        {
            SnapChange?.Invoke(this, e);
        }

        private void MainWindow_ZoomChange(object sender, RoutedEventArgs e)
        {
            ZoomChange?.Invoke(this, e);
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
            KeyVisibilityChange?.Invoke(this, e);
        }

        private void MainWindow_GridVisibilityChange(object sender, RoutedEventArgs e)
        {
            GridVisibilityChange?.Invoke(this, e);
        }

        private void MainWindow_NoteSelect(object sender, RoutedEventArgs e)
        {
            NoteSelect?.Invoke(this, e);
        }
    }
}
