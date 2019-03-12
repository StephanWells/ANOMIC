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
        private const int pianoRollRows = 88;
        private int quarterNote = 96;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            double centre = srlPianoScroll.ScrollableHeight / 2.0;
            srlPianoScroll.ScrollToVerticalOffset(centre);

            AddBar();
            AddBar();
            AddBar();
            AddBar();
        }

        private void AddBar()
        {
            double barStart = grdNotes.Width;
            float thirtySecondNote = quarterNote / 8;
            double thickness;
            double height = cnvGridLines.Height - 7;

            float barLength = quarterNote * 4;
            cnvPianoRoll.Width += barLength;
            cnvGridLines.Width += barLength;
            grdPianoRoll.Width += barLength;
            grdNotes.Width += barLength;

            for (int j = 1; j <= 32; j++)
            {
                if (j % 32 == 0)
                {
                    thickness = 2.3;
                }
                else if (j % 16 == 0)
                {
                    thickness = 1.1;
                }
                else if (j % 8 == 0)
                {
                    thickness = 0.9;
                }
                else if (j % 4 == 0)
                {
                    thickness = 0.6;
                }
                else if (j % 2 == 0)
                {
                    thickness = 0.4;
                }
                else
                {
                    thickness = 0.2;
                }

                Line gridLine = new Line
                {
                    Stroke = (Brush)this.Resources["GridLineColour"],
                    X1 = 0,
                    X2 = 0,
                    Y1 = 0,
                    Y2 = height,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    StrokeThickness = thickness,
                };

                Panel.SetZIndex(gridLine, 2);

                Canvas.SetLeft(gridLine, j * thirtySecondNote + barStart);
                Canvas.SetTop(gridLine, 7);
                cnvGridLines.Children.Add(gridLine);
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            ResetPianoRoll();

            OpenFileDialog browseDialog = new OpenFileDialog
            {
                Filter = "MIDI files (*.mid)|*.mid|All files (*.*)|*.*"
            };

            if (browseDialog.ShowDialog() == true)
            {
                MIDIParser midiParse = new MIDIParser(File.ReadAllBytes(browseDialog.FileName));

                try
                {
                    midiParse.ParseFile();
                    NoteParser noteParse = new NoteParser(midiParse);
                    noteParse.ParseEvents();

                    PopulateNotesGrid(noteParse);
                }
                catch (InvalidOperationException)
                {
                    MessageBox.Show("Error parsing MIDI file!", "Error");
                }
            }
        }

        private void PopulateNotesGrid(NoteParser noteParse)
        {
            for (int i = 0; i < noteParse.notes.Count; i++)
            {
                Note currentNote = noteParse.notes.ElementAt(i);

                while (grdNotes.Width < currentNote.GetTime() + currentNote.GetDuration())
                {
                    AddBar();
                }

                Rectangle noteBar = new Rectangle
                {
                    Stroke = new SolidColorBrush(Colors.Black),
                    Fill = new SolidColorBrush(Colors.LightGray),
                    Width = currentNote.GetDuration(),
                    Height = 18
                };
                //Grid.SetLeft(noteBar, currentNote.GetTime());
                Grid.SetRow(noteBar, PitchToRow((int)currentNote.GetPitch()));
                Panel.SetZIndex(noteBar, 3);
                noteBar.HorizontalAlignment = HorizontalAlignment.Left;
                noteBar.Margin = new Thickness(currentNote.GetTime(), 0, 0, 0);
                grdNotes.Children.Add(noteBar);
            }
        }

        private int PitchToRow(int pitch)
        {
            return 109 - pitch;
        }

        private void ResetPianoRoll()
        {
            int childrenCount = grdNotes.Children.Count - 1;

            for (int i = childrenCount; i >= 0; i--)
            {
                if (grdNotes.Children[i].GetType() == typeof(Rectangle))
                {
                    Rectangle ucCurrentChild = (Rectangle)grdNotes.Children[i];
                    grdNotes.Children.Remove(ucCurrentChild);
                }
            }

            cnvPianoRoll.Width = 0;
            cnvGridLines.Width = 0;
            grdPianoRoll.Width = 0;
            grdNotes.Width = 0;

            cnvGridLines.Children.Clear();

            AddBar();
            AddBar();
            AddBar();
            AddBar();
        }
    }
}
