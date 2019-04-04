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
        public MainWindow()
        {
            InitializeComponent();
            //this.Loaded += OnLoaded;
        }

        private void MIDIBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog browseDialog = new OpenFileDialog
            {
                Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*"
            };

            if (browseDialog.ShowDialog() == true)
            {
                try
                {
                    MIDIParser midiParse = new MIDIParser(File.ReadAllBytes(browseDialog.FileName));

                    midiParse.ParseFile();
                    NoteParser noteParse = new NoteParser(midiParse);
                    noteParse.ParseEvents();

                    DataContext = new PianoRollView(midiParse, noteParse);
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Error parsing MIDI file!", "Error");
                }
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
