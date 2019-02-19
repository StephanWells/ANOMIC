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
            cnvPianoRoll.Children.Clear();
            OpenFileDialog browseDialog = new OpenFileDialog();
            browseDialog.Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*";

            if (browseDialog.ShowDialog() == true)
            {
                MIDIParser midiParse = new MIDIParser(File.ReadAllBytes(browseDialog.FileName));

                try
                {
                    midiParse.ParseFile();
                    NoteParser noteParse = new NoteParser(midiParse);
                    noteParse.ParseEvents();

                    /*string tracksText = "";

                    for (int i = 0; i < midiParse.header.trackNum; i++)
                    {
                        tracksText += "Track " + (i + 1) + ":\n";
                        tracksText += midiParse.TrackToString(i);
                        tracksText += "\n";
                    }

                    blkParseOutput.Text = midiParse.HeaderToString() + "\n\n" + tracksText;*/

                    for (int i = 0; i < noteParse.notes.Count; i++)
                    {
                        Note currentNote = noteParse.notes.ElementAt(i);

                        Rectangle noteBar = new Rectangle();
                        noteBar.Stroke = new SolidColorBrush(Colors.Black);
                        noteBar.Fill = new SolidColorBrush(Colors.Blue);
                        noteBar.Width = currentNote.GetDuration();
                        noteBar.Height = 10;
                        Canvas.SetLeft(noteBar, currentNote.GetTime());
                        Canvas.SetBottom(noteBar, (int)currentNote.GetPitch() * 10 - 200);
                        cnvPianoRoll.Width += currentNote.GetDuration();
                        cnvPianoRoll.Children.Add(noteBar);
                    }
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Error parsing MIDI file!", "Error");
                }
            }
        }
    }
}
