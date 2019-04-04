using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;

namespace AnnotationTool.views
{
    public partial class PianoRollView : UserControl
    {
        private const int pianoRollRows = 88;

        private int quarterNote;
        private int snapLength;

        private MIDIParser midiParse;
        private NoteParser noteParse;

        private string quantisedSong;

        private List<Pattern> patterns;
        private List<PatternIcon> patternIcons;
        private List<List<Canvas>> patternRects;
        private int currentPattern = -1;

        bool isLeftMouseButtonDownOnPianoRoll = false;
        bool isDraggingPatternRect = false;
        Point origMouseDownPoint;
        Point prevMouseDownPoint;

        private List<Brush> defaultPatternColours = new List<Brush>()
        {
            Brushes.DarkRed,
            Brushes.DarkGreen,
            Brushes.DarkBlue,
            Brushes.DarkOrange,
            Brushes.DarkCyan,
            Brushes.DarkMagenta,
            Brushes.DarkSlateGray,
            Brushes.DarkSalmon,
            Brushes.DarkSeaGreen,
            Brushes.DarkViolet
        };

        public PianoRollView(MIDIParser midiParseIn, NoteParser noteParseIn)
        {
            InitializeComponent();

            patterns = new List<Pattern>();
            patternRects = new List<List<Canvas>>();
            patternIcons = new List<PatternIcon>();

            midiParse = midiParseIn;
            noteParse = noteParseIn;

            quarterNote = (int)midiParse.header.timeDiv;
            snapLength = quarterNote / 8;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            double centre = srlPianoScroll.ScrollableHeight / 2.0;
            srlPianoScroll.ScrollToVerticalOffset(centre);

            ResetPianoRoll();
            PopulateNotesGrid(noteParse);

            quantisedSong = ConcatenateList(Quantise(0, (int)grdNotes.Width));
        }

        private List<String> Quantise(double start, double end)
        {
            List<String> quantisedSection = new List<string>();

            for (int i = 0; i < (end - start) / snapLength; i++)
            {
                quantisedSection.Add(" ");
            }

            for (int i = 0; i < noteParse.notes.Count; i++)
            {
                int j = 0;
                Note currentNote = noteParse.notes[i];

                if (((currentNote.GetTime() >= start) && (currentNote.GetTime() <= end)))
                {
                    int quantisedIndex = 0;

                    while (j < currentNote.GetDuration() && ((j + currentNote.GetTime() - start) / snapLength) < quantisedSection.Count)
                    {
                        quantisedIndex = (int)((j + currentNote.GetTime() - start) / snapLength);

                        if (quantisedSection[quantisedIndex].Equals(" "))
                        {
                            quantisedSection[quantisedIndex] = currentNote.GetPitch().ToString();
                        }
                        else
                        {
                            quantisedSection[quantisedIndex] += currentNote.GetPitch().ToString();
                        }

                        j += snapLength;
                    }               
                }
            }

            return quantisedSection;
        }

        private int SimilarOccurrences(List<String> quantisedSection)
        {
            string concatenatedSection = ConcatenateList(quantisedSection);

            return Regex.Matches(quantisedSong, concatenatedSection).Count;
        }

        private string ConcatenateList(List<String> stringList)
        {
            string result = "";

            for (int i = 0; i < stringList.Count; i++)
            {
                result += stringList[i] + ";";
            }

            return result;
        }

        private void AddPattern_Click(object sender, RoutedEventArgs e)
        {
            patterns.Add(new Pattern());
            patternRects.Add(new List<Canvas>());

            PatternIcon newPatternButton = new PatternIcon
            {
                Width = btnAddPattern.Width,
                Height = btnAddPattern.Height,
                TextContent = "" + patterns.Count,
                Name = "PatternButton" + patterns.Count,
                Background = defaultPatternColours[patterns.Count - 1 - (defaultPatternColours.Count * ((patterns.Count - 1) / defaultPatternColours.Count))]
            };

            newPatternButton.Click += PatternCheckbox_Click;

            patternIcons.Add(newPatternButton);
            itmPatternsView.Items.Add(newPatternButton);

            MoveButton(btnAddPattern, 0, btnAddPattern.Height);
        }

        private void PatternCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if (((PatternIcon)sender).IsChecked == true)
            {
                for (int i = 0; i < patternIcons.Count; i++)
                {
                    if (!patternIcons[i].Name.Equals(((PatternIcon)sender).Name))
                    {
                        patternIcons[i].IsChecked = false;
                    }
                    else
                    {
                        currentPattern = i;
                    }
                }
            }
            else
            {
                currentPattern = -1;
            }
            
        }

        private void PianoRoll_LeftMouseDown(object sender, MouseButtonEventArgs e)
        {
            origMouseDownPoint = GetCurrentMousePosition(e);
            origMouseDownPoint.X = Snap(origMouseDownPoint.X, snapLength);

            prevMouseDownPoint = origMouseDownPoint;
            isLeftMouseButtonDownOnPianoRoll = true;

            Mouse.Capture(cnvMouseLayer);
        }

        private void PianoRoll_LeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            isLeftMouseButtonDownOnPianoRoll = false;
            isDraggingPatternRect = false;
            Mouse.Capture(null);

            if (currentPattern != -1)
            {
                Border currentPatternRect = (Border)(patternRects[currentPattern][patterns[currentPattern].GetOccurrences().Count - 1].Children[0]);

                Console.WriteLine(Canvas.GetLeft(currentPatternRect) + " - " + (Canvas.GetLeft(currentPatternRect) + currentPatternRect.Width));

                TextBlock textBlock = new TextBlock
                {
                    Text = "" + SimilarOccurrences(Quantise(Canvas.GetLeft(currentPatternRect), Canvas.GetLeft(currentPatternRect) + currentPatternRect.Width)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 30
                };

                Canvas.SetTop(textBlock, srlPianoScroll.VerticalOffset);

                cnvMouseLayer.Children.Add(textBlock);
            }
        }

        private void PianoRoll_MouseMove(object sender, MouseEventArgs e)
        {
            Point curMouseDownPoint = GetCurrentMousePosition(e);
            
            Vector dragDelta = curMouseDownPoint - prevMouseDownPoint;
            double dragDistance = Math.Abs(dragDelta.Length);

            if (dragDistance >= (snapLength))
            {
                if (isDraggingPatternRect)
                {
                    curMouseDownPoint = GetCurrentMousePosition(e);
                    prevMouseDownPoint = curMouseDownPoint;
                    UpdateDragPatternRect(origMouseDownPoint, curMouseDownPoint);

                    e.Handled = true;
                }
                else if (isLeftMouseButtonDownOnPianoRoll && (currentPattern != -1))
                {
                    isDraggingPatternRect = true;
                    InitDragPatternRect(origMouseDownPoint, curMouseDownPoint);
                }
            }
        }

        private Point GetCurrentMousePosition(MouseEventArgs e)
        {
            return e.GetPosition(cnvMouseLayer).X < 0 ? new Point(0, e.GetPosition(cnvMouseLayer).Y) : e.GetPosition(cnvMouseLayer);
        }

        private void InitDragPatternRect(Point pt1, Point pt2)
        {
            AddOccurrence();
            UpdateDragPatternRect(pt1, pt2);
            patternRects[currentPattern][patterns[currentPattern].GetOccurrences().Count - 1].Visibility = Visibility.Visible;
        }

        private void UpdateDragPatternRect(Point pt1, Point pt2)
        {
            double x, width;

            x = (pt1.X < pt2.X) ? pt1.X : pt2.X;
            width = Math.Abs(pt2.X - pt1.X);

            Border dragBorder = (Border)patternRects[currentPattern][patterns[currentPattern].GetOccurrences().Count - 1].Children[0];

            Canvas.SetLeft(dragBorder, x);
            Canvas.SetTop(dragBorder, 0);
            dragBorder.Width = width;
            dragBorder.Height = cnvMouseLayer.Height;
        }

        // Adds a bar to the piano roll to make space for MIDI notes.
        private void AddBar()
        {
            double barStart = grdNotes.Width;
            float thirtySecondNote = quarterNote / 8;
            double thickness;
            double height = cnvGridLines.Height - 7;
            float barLength = quarterNote * 4;

            cnvPianoRoll.Width += barLength;
            grdGridColours.Width += barLength;
            cnvGridLines.Width += barLength;
            cnvMouseLayer.Width += barLength;
            grdNotes.Width += barLength;

            for (int j = 1; j <= 32; j++)
            {
                // Drawing the grid lines with varying thickness.
                if (j % 32 == 0) thickness = 2.3;
                else if (j % 16 == 0) thickness = 1.1;
                else if (j % 8 == 0) thickness = 0.9;
                else if (j % 4 == 0) thickness = 0.6;
                else if (j % 2 == 0) thickness = 0.4;
                else thickness = 0.2;

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

        private void AddOccurrence()
        {
            patterns[currentPattern].AddOccurrence(new Occurrence());

            Canvas patternCanvas = new Canvas
            {
                Name = "dragSelectionCanvas" + currentPattern + "s" + patterns[currentPattern].GetOccurrences().Count,
                Visibility = Visibility.Collapsed,
                Width = 0
            };

            Border patternBorder = new Border
            {
                Name = "dragSelectionBorder" + currentPattern + "s" + patterns[currentPattern].GetOccurrences().Count,
                Background = patternIcons[currentPattern].Background,
                CornerRadius = new CornerRadius(1),
                Opacity = 0.3
            };

            patternCanvas.Children.Add(patternBorder);
            patternRects[currentPattern].Add(patternCanvas);
            cnvMouseLayer.Children.Add(patternCanvas);
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
                    Rectangle currentChild = (Rectangle)grdNotes.Children[i];
                    grdNotes.Children.Remove(currentChild);
                }
            }

            cnvPianoRoll.Width = 0;
            cnvGridLines.Width = 0;
            grdGridColours.Width = 0;
            grdNotes.Width = 0;

            cnvGridLines.Children.Clear();

            AddBar();
            AddBar();
            AddBar();
            AddBar();
        }

        // Moves a given element a specified number of units to the right and downwards.
        private void MoveButton(FrameworkElement target, double right, double down)
        {
            TranslateTransform moveTransform = new TranslateTransform();
            target.RenderTransform = moveTransform;

            DoubleAnimation horizontalAnim = new DoubleAnimation { By = right, Duration = TimeSpan.FromSeconds(0.2) };
            horizontalAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
            DoubleAnimation verticalAnim = new DoubleAnimation { By = down, Duration = TimeSpan.FromSeconds(0.2) };
            verticalAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };

            if (target.GetType() == typeof(Button))
            {
                ((Button)target).IsEnabled = false;

                horizontalAnim.Completed += (s, e) =>
                {
                    ((Button)target).IsEnabled = true;
                    btnAddPattern.Margin = new Thickness(btnAddPattern.Margin.Left + btnAddPattern.RenderTransform.Value.OffsetX, btnAddPattern.Margin.Top, btnAddPattern.Margin.Right, btnAddPattern.Margin.Bottom);
                };

                verticalAnim.Completed += (s, e) =>
                {
                    ((Button)target).IsEnabled = true;
                    btnAddPattern.Margin = new Thickness(btnAddPattern.Margin.Left, btnAddPattern.Margin.Top + btnAddPattern.RenderTransform.Value.OffsetY, btnAddPattern.Margin.Right, btnAddPattern.Margin.Bottom);
                    btnAddPattern.RenderTransform = new TranslateTransform();
                };
            }

            moveTransform.BeginAnimation(TranslateTransform.XProperty, horizontalAnim);
            moveTransform.BeginAnimation(TranslateTransform.YProperty, verticalAnim);
        }

        // Snaps a value to the nearest multiple.
        private int Snap(double value, int multiple)
        {
            return (int)Math.Round(value / multiple) * multiple;
        }
    }
}