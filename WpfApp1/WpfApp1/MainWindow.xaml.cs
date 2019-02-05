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

namespace AnnotationTool
{
    // Interaction logic for MainWindow.xaml
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog browseDialog = new OpenFileDialog();
            browseDialog.Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*";

            if (browseDialog.ShowDialog() == true)
            {
                MIDIParser midiParse = new MIDIParser(File.ReadAllBytes(browseDialog.FileName));

                try
                {
                    midiParse.ParseFile();

                    string tracksText = "";

                    for (int i = 0; i < midiParse.header.trackNum; i++)
                    {
                        tracksText += "Track " + (i + 1) + ":\n";
                        tracksText += midiParse.TrackToString(i);
                        tracksText += "\n";
                    }

                    blkParseOutput.Text = midiParse.HeaderToString() + "\n\n" + tracksText;
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Error parsing MIDI file!", "Error");
                }
            }
        }
    }
}
