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
        private const int occurrenceIconHeight = 20;
        private const double resolution = 96;

        private double zoom = 1;
        private double timeDivRatio;
        private double snapLength;

        private MIDIParser midiParse;
        private NoteParser noteParse;

        private List<string> quantisedSong;

        private List<Pattern> patterns;
        private Canvas currentOccurrenceRect;
        private int patternIndex = -1;
        private int currentSolo = -1;

        bool isLeftMouseButtonDownOnPianoRoll = false;
        bool isDraggingPatternRect = false;
        bool isDraggingScroll = false;
        bool isUIMoving = false;

        Point origMouseDownPoint;
        Point prevMouseDownPoint;
        Point scrollMousePoint;

        double scrollHorOff = 1;
        double scrollVerOff = 1;

        private List<Line> gridLines = new List<Line>();
        private List<TextBlock> barNumbers = new List<TextBlock>();

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

            midiParse = midiParseIn;
            noteParse = noteParseIn;

            timeDivRatio = resolution / midiParse.header.timeDiv;
            snapLength = (zoom * resolution) / 8;

            foreach (Note note in noteParse.notes)
            {
                note.SetTime(note.GetTime() * timeDivRatio);
                note.SetDuration(note.GetDuration() * timeDivRatio);
            }

            MainWindow.SnapChange += new EventHandler(MainWindow_SnapChange);
            MainWindow.ZoomChange += new EventHandler(MainWindow_ZoomChange);
            MainWindow.ExpandAll += new EventHandler(MainWindow_ExpandAll);
            MainWindow.CollapseAll += new EventHandler(MainWindow_CollapseAll);
            MainWindow.ShowAll += new EventHandler(MainWindow_ShowAll);
            MainWindow.HideAll += new EventHandler(MainWindow_HideAll);
            MainWindow.AddPattern += new EventHandler(MainWindow_AddPattern);

            Loaded += OnLoaded;
        }

        public PianoRollView()
        {
            InitializeComponent();

            patterns = new List<Pattern>();
            snapLength = 12;

            MainWindow.SnapChange += new EventHandler(MainWindow_SnapChange);
            Loaded += OnLoaded;
        }

        private void MainWindow_SnapChange(object sender, EventArgs e)
        {
            string snapMenuChoice = ((MenuItem)(((RoutedEventArgs)e).Source)).Header.ToString();

            switch (snapMenuChoice)
            {
                //case "Note":
                //    snapLength = -1;
                //break;

                case "Step":
                    snapLength = Math.Round(zoom * resolution, 2);
                break;

                case "1/2 Step":
                    snapLength = Math.Round((zoom * resolution) / 2, 2);
                break;

                case "1/3 Step":
                    snapLength = Math.Round((zoom * resolution) / 3, 2);
                break;

                case "1/4 Step":
                    snapLength = Math.Round((zoom * resolution) / 4, 2);
                break;

                case "1/8 Step":
                    snapLength = Math.Round((zoom * resolution) / 8, 2);
                break;
            }

            quantisedSong = Quantise(0, (int)grdNotes.Width);
        }

        private void MainWindow_ZoomChange(object sender, EventArgs e)
        {
            string zoomMenuChoice = ((MenuItem)(((RoutedEventArgs)e).Source)).Header.ToString();

            switch (zoomMenuChoice)
            {
                case "100%":
                    UpdateZoom(1);
                    break;

                case "80%":
                    UpdateZoom(0.8);
                    break;

                case "60%":
                    UpdateZoom(0.6);
                    break;

                case "40%":
                    UpdateZoom(0.4);
                    break;

                case "20%":
                    UpdateZoom(0.2);
                    break;
            }
        }

        private void MainWindow_ExpandAll(object sender, EventArgs e)
        {
            ExpandAllPatternOccurrences();
        }

        private void MainWindow_CollapseAll(object sender, EventArgs e)
        {
            CollapseAllPatternOccurrences();
        }

        private void MainWindow_ShowAll(object sender, EventArgs e)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = true;
                ShowOccurrenceRects(i);
            }
        }

        private void MainWindow_HideAll(object sender, EventArgs e)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = false;
                HideOccurrenceRects(i);
            }
        }

        private void MainWindow_AddPattern(object sender, EventArgs e)
        {
            AddPattern();
        }

        private void ExpandAllPatternOccurrences()
        {
            double animMove = 0;

            foreach (Pattern pattern in patterns)
            {
                if (!pattern.patternIcon.CollExp)
                {
                    pattern.patternIcon.CollExp = true;

                    foreach (Occurrence occurrence in pattern.GetOccurrences())
                    {
                        occurrence.occurrenceIcon.Visibility = Visibility.Visible;
                        animMove += occurrenceIconHeight;
                    }
                }
            }

            MoveElement(btnAddPattern, animMove);
        }

        private void CollapseAllPatternOccurrences()
        {
            double animMove = 0;

            foreach (Pattern pattern in patterns)
            {
                if (pattern.patternIcon.CollExp)
                {
                    pattern.patternIcon.CollExp = false;

                    foreach (Occurrence occurrence in pattern.GetOccurrences())
                    {
                        occurrence.occurrenceIcon.Visibility = Visibility.Collapsed;
                        animMove -= occurrenceIconHeight;
                    }
                }
            }

            MoveElement(btnAddPattern, animMove);
        }

        private void UpdateZoom(double zoomSetting)
        {
            zoom = zoomSetting;
            double thirtySecondNote = Math.Round(zoom * (resolution / 8), 2);
            double width = Math.Round(barNumbers.Count * zoom * resolution * 4, 2);

            cnvPianoRoll.Width = width + grdPiano.Width;
            grdGridColours.Width = width;
            cnvGridLines.Width = width;
            cnvMouseLayer.Width = width;
            grdNotes.Width = width;
            grdTimeline.Width = width + 2;

            for (int i = 0; i < gridLines.Count; i++)
            {
                Canvas.SetLeft(gridLines[i], i * thirtySecondNote + thirtySecondNote);
            }

            foreach (Note note in noteParse.notes)
            {
                note.noteRect.Width = Math.Round(note.GetDuration() * zoom, 2);
                note.noteRect.Margin = new Thickness(Math.Round(note.GetTime() * zoom, 2), 0, 0, 0);
            }

            for (int i = 0; i < barNumbers.Count; i++)
            {
                barNumbers[i].Margin = new Thickness(Math.Round(i * zoom * resolution * 4, 2), 0, 0, 0);
            }

            foreach (Pattern pattern in patterns)
            {
                foreach (Occurrence occurrence in pattern.GetOccurrences())
                {
                    Border currentOccurrenceRect = (Border)occurrence.occurrenceRect.Children[0];

                    Canvas.SetLeft(currentOccurrenceRect, Math.Round(occurrence.GetStart() * zoom, 2));
                    currentOccurrenceRect.Width = Math.Round((occurrence.GetEnd() - occurrence.GetStart()) * zoom, 2);
                }
            }

            snapLength = Math.Round((zoom * resolution) / 8, 2);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            double centre = srlPianoScroll.ScrollableHeight / 2.0;

            if (noteParse.notes.Count > 0)
            {
                double test = (double)PitchToRow((int)noteParse.notes[0].GetPitch()) / 87 * srlPianoScroll.ScrollableHeight;

                srlPianoScroll.ScrollToVerticalOffset(test);
            }
            
            ResetPianoRoll();
            PopulateNotesGrid(noteParse);

            quantisedSong = Quantise(0, (int)grdNotes.Width);
        }

        // Adds a bar to the piano roll to make space for MIDI notes.
        private void AddBar()
        {
            double barStart = grdNotes.Width;
            double thirtySecondNote = zoom * (resolution / 8);
            double thickness;
            double height = cnvGridLines.Height - 7;
            double barLength = zoom * (resolution * 4);

            cnvPianoRoll.Width += barLength;
            grdGridColours.Width += barLength;
            cnvGridLines.Width += barLength;
            cnvMouseLayer.Width += barLength;
            grdNotes.Width += barLength;
            grdTimeline.Width += barLength;

            TextBlock barNumber = new TextBlock
            {
                FontSize = 12,
                Text = "" + (barNumbers.Count),
                Margin = new Thickness(barStart, 2, 0, 0),
                Height = 20
            };

            barNumbers.Add(barNumber);
            grdTimeline.Children.Add(barNumber);

            for (int j = 1; j <= 32; j++)
            {
                // Drawing the vertical grid lines with varying thickness.
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
                    StrokeThickness = thickness
                };

                Panel.SetZIndex(gridLine, 2);
                Canvas.SetLeft(gridLine, j * thirtySecondNote + barStart);
                Canvas.SetTop(gridLine, 7);

                gridLines.Add(gridLine);
                cnvGridLines.Children.Add(gridLine);
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
                    Width = Math.Round(currentNote.GetDuration() * zoom, 2),
                    Height = 18,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(Math.Round(currentNote.GetTime() * zoom, 2), 0, 0, 0)
                };

                Grid.SetRow(noteBar, PitchToRow((int)currentNote.GetPitch()));
                Panel.SetZIndex(noteBar, 3);

                currentNote.noteRect = noteBar;
                grdNotes.Children.Add(noteBar);
            }
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

            cnvPianoRoll.Width = grdPiano.Width;
            grdGridColours.Width += 0;
            cnvGridLines.Width += 0;
            cnvMouseLayer.Width += 0;
            grdNotes.Width += 0;
            grdTimeline.Width += 0;

            cnvGridLines.Children.Clear();

            AddBar();
            AddBar();
            AddBar();
            AddBar();
        }

        // Places each snap's data into a quantised "bucket" where it can be compared for pattern similarity. The "start" and "end" parameters have to be unaffected by zoom.
        private List<String> Quantise(double start, double end)
        {
            List<String> quantisedSection = new List<string>();

            double zoomRobustSnapLength = Math.Round(snapLength / zoom, 2);

            for (int i = 0; i < (end - start) / zoomRobustSnapLength; i++)
            {
                quantisedSection.Add(" ");
            }

            for (int i = 0; i < noteParse.notes.Count; i++)
            {
                double j = 0;
                Note currentNote = noteParse.notes[i];

                if (currentNote.GetTime() > end) break;

                if (currentNote.GetTime() + currentNote.GetDuration() >= start)
                {
                    int quantisedIndex = 0;

                    while (j < currentNote.GetDuration() && ((j + currentNote.GetTime() - start) / zoomRobustSnapLength) < quantisedSection.Count)
                    {
                        if (currentNote.GetTime() + j >= start)
                        {
                            quantisedIndex = (int)((j + currentNote.GetTime() - start) / zoomRobustSnapLength);

                            if (quantisedSection[quantisedIndex].Equals(" "))
                            {
                                quantisedSection[quantisedIndex] = currentNote.GetPitch().ToString();
                            }
                            else
                            {
                                quantisedSection[quantisedIndex] += currentNote.GetPitch().ToString();
                            }
                        }

                        j += zoomRobustSnapLength;
                    }
                }
            }

            return quantisedSection;
        }

        private int SimilarOccurrencesCount(List<String> quantisedSection)
        {
            return Regex.Matches(ConcatenateList(quantisedSong), ConcatenateList(quantisedSection)).Count;
        }

        private List<Occurrence> SimilarOccurrences(Occurrence occurrence)
        {
            List<Occurrence> similarOccurrences = new List<Occurrence>();
            List<String> quantisedOccurrence = Quantise(occurrence.GetStart(), occurrence.GetEnd());

            double start = -1;
            double end = -1;
            int occurrenceIndex = 0;
            int patternIndex = occurrence.occurrenceIcon.PatternNumOfOccurrence;

            for (int i = 0; i < quantisedSong.Count; i++)
            {
                if (quantisedSong[i].Equals(quantisedOccurrence[occurrenceIndex]))
                {
                    if (occurrenceIndex == 0)
                    {
                        start = i * snapLength;
                    }

                    if (occurrenceIndex == quantisedOccurrence.Count() - 1)
                    {
                        end = (i + 1) * snapLength;

                        double zoomRobustStart = Math.Round(start / zoom, 2);
                        double zoomRobustEnd = Math.Round(end / zoom, 2);

                        Occurrence newOccurrence = new Occurrence(zoomRobustStart, zoomRobustEnd);

                        similarOccurrences.Add(newOccurrence);

                        start = end = -1;
                        occurrenceIndex = 0;
                    }
                    else
                    {
                        occurrenceIndex++;
                    }
                }
                else
                {
                    start = end = -1;
                    occurrenceIndex = 0;
                }
            }

            return RemoveDuplicates(similarOccurrences, patternIndex);
        }

        private List<Occurrence> RemoveDuplicates(List<Occurrence> occurrences, int patternIndex)
        {
            List<Occurrence> occurrencesNoDuplicates = new List<Occurrence>();

            if (occurrences.Count > 0)
            {
                foreach (Occurrence similarOccurrence in occurrences)
                {
                    bool duplicateFound = false;

                    foreach (Occurrence occurrence in patterns[patternIndex].GetOccurrences())
                    {
                        if ((occurrence.GetStart() == similarOccurrence.GetStart()) && (occurrence.GetEnd() == similarOccurrence.GetEnd()))
                        {
                            duplicateFound = true;
                        }
                    }

                    if (!duplicateFound)
                    {
                        occurrencesNoDuplicates.Add(similarOccurrence);
                    }
                }
            }

            return occurrencesNoDuplicates;
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
            AddPattern();
        }

        private void AddPattern()
        {
            Pattern newPattern = new Pattern();

            PatternIcon newPatternButton = new PatternIcon
            {
                Width = btnAddPattern.Width,
                Height = btnAddPattern.Height,
                TextContent = "" + (patterns.Count + 1),
                Name = "PatternButton" + (patterns.Count),
                Background = defaultPatternColours[patterns.Count - (defaultPatternColours.Count * ((patterns.Count) / defaultPatternColours.Count))],
                CollExp = true,
                View = true,
                VerticalAlignment = VerticalAlignment.Top,
                PatternNum = patterns.Count
            };

            // Event handlers for the different actions of a pattern icon.
            newPatternButton.Click += PatternCheckbox_Click;
            newPatternButton.DeleteClick += PatternIcon_DeleteClick;
            newPatternButton.ViewToggle += PatternIcon_ViewToggle;
            newPatternButton.ViewSolo += PatternIcon_ViewSolo;
            newPatternButton.CollExpToggle += PatternIcon_CollExpToggle;

            newPattern.patternIcon = newPatternButton;
            patterns.Add(newPattern);
            itmPatternsView.Items.Add(newPatternButton);

            MoveElement(btnAddPattern, btnAddPattern.Height);
        }

        private Occurrence AddOccurrence(int patternIndex, Canvas occurrenceRect)
        {
            Occurrence newOccurrence = new Occurrence();

            patterns[patternIndex].AddOccurrence(newOccurrence);
            newOccurrence.occurrenceIcon = CreateOccurrenceIcon(patternIndex);
            newOccurrence.occurrenceRect = occurrenceRect;

            Border currentPatternRect = (Border)(newOccurrence.occurrenceRect.Children[0]);
            newOccurrence.SetStart(Math.Round(Canvas.GetLeft(currentPatternRect) / zoom, 2));
            newOccurrence.SetEnd(Math.Round((Canvas.GetLeft(currentPatternRect) + currentPatternRect.Width) / zoom, 2));

            return newOccurrence;
        }

        private OccurrenceIcon CreateOccurrenceIcon(int patternIndex)
        {
            OccurrenceIcon occurrenceIcon = new OccurrenceIcon
            {
                Name = "occurrenceIcon" + patternIndex + "s" + patterns[patternIndex].GetOccurrences().Count,
                Background = patterns[patternIndex].patternIcon.Background,
                OccurrenceText = "" + (patterns[patternIndex].GetOccurrences().Count),
                Height = occurrenceIconHeight,
                PatternNumOfOccurrence = patternIndex,
                OccurrenceNum = patterns[patternIndex].GetOccurrences().Count - 1
            };

            // Event handlers for the different actions of an occurrence icon.
            occurrenceIcon.DeleteClick += OccurrenceIcon_DeleteClick;
            occurrenceIcon.MouseIn += OccurrenceIcon_MouseIn;
            occurrenceIcon.MouseOut += OccurrenceIcon_MouseOut;
            occurrenceIcon.MouseLeftClick += OccurrenceIcon_MouseLeftClick;
            occurrenceIcon.FindSimilar += OccurrenceIcon_FindSimilar;

            return occurrenceIcon;
        }

        private Canvas CreateOccurrenceRect(int patternIndex)
        {
            SolidColorBrush occurrenceRectColour = new SolidColorBrush(((SolidColorBrush)(patterns[this.patternIndex].patternIcon.Background)).Color)
            {
                Opacity = 0.3
            };

            Canvas patternCanvas = new Canvas
            {
                Name = "dragSelectionCanvas" + patternIndex + "s" + patterns[patternIndex].GetOccurrences().Count,
                Visibility = Visibility.Visible,
                Width = 0
            };

            Border patternBorder = new Border
            {
                Name = "dragSelectionBorder" + patternIndex + "s" + patterns[patternIndex].GetOccurrences().Count,
                Background = occurrenceRectColour,
                CornerRadius = new CornerRadius(1),
                BorderBrush = patterns[patternIndex].patternIcon.Background
            };

            patternCanvas.Children.Add(patternBorder);

            return patternCanvas;
        }

        private void AddOccurrenceIcon(Occurrence occurrence)
        {
            // Figuring out which index to slot the new occurrence in.
            OccurrenceIcon occurrenceIcon = occurrence.occurrenceIcon;
            int patternIndex = occurrenceIcon.PatternNumOfOccurrence;
            int finalIndex = patternIndex;

            for (int i = 0; i <= patternIndex; i++)
            {
                finalIndex += patterns[i].GetOccurrences().Count;
            }

            if (patterns[this.patternIndex].patternIcon.CollExp)
            {
                MoveElement(btnAddPattern, occurrenceIconHeight);
            }
            else
            {
                occurrenceIcon.Visibility = Visibility.Collapsed;
            }

            itmPatternsView.Items.Insert(finalIndex, occurrenceIcon);
        }

        private void AddOccurrenceGraphics(List<Occurrence> occurrences, int patternIndex)
        {
            double animMove = 0;

            foreach (Occurrence occurrence in occurrences)
            {
                Canvas occurrenceRect = CreateOccurrenceRect(patternIndex);
                Border rectBorder = (Border)occurrenceRect.Children[0];

                Canvas.SetLeft(rectBorder, occurrence.GetStart() * zoom);
                Canvas.SetTop(rectBorder, 0);
                rectBorder.Width = (occurrence.GetEnd() - occurrence.GetStart()) * zoom;
                rectBorder.Height = cnvMouseLayer.Height;

                cnvMouseLayer.Children.Add(occurrenceRect);

                patterns[patternIndex].AddOccurrence(occurrence);
                occurrence.occurrenceIcon = CreateOccurrenceIcon(patternIndex);
                occurrence.occurrenceRect = occurrenceRect;
            }

            for (int i = 0; i < occurrences.Count; i++)
            {
                int index = patternIndex;

                for (int j = 0; j < patternIndex; j++)
                {
                    index += patterns[j].GetOccurrences().Count;
                }

                index += occurrences[i].occurrenceIcon.OccurrenceNum + 1;

                if (patterns[patternIndex].patternIcon.CollExp)
                {
                    animMove += occurrenceIconHeight;
                }
                else
                {
                    occurrences[i].occurrenceIcon.Visibility = Visibility.Collapsed;
                }

                itmPatternsView.Items.Insert(index, occurrences[i].occurrenceIcon);
            }

            MoveElement(btnAddPattern, animMove);
        }

        private void PatternIcon_DeleteClick(object sender, EventArgs e)
        {
            if (!isUIMoving)
            {
                int patternIndex = ((PatternIcon)sender).PatternNum;

                DeletePattern(patternIndex);
            }
        }

        private void PatternIcon_ViewToggle(object sender, EventArgs e)
        {
            int patternIndex = ((PatternIcon)sender).PatternNum;

            if (patterns[patternIndex].patternIcon.View)
            {
                ShowOccurrenceRects(patternIndex);
            }
            else
            {
                HideOccurrenceRects(patternIndex);
            }

            if (currentSolo != -1)
            {
                currentSolo = -1;
            }
        }

        private void PatternIcon_ViewSolo(object sender, EventArgs e)
        {
            int patternIndex = ((PatternIcon)sender).PatternNum;

            if (currentSolo != patternIndex)
            {
                currentSolo = patternIndex;

                SoloViewForPattern(patternIndex);
            }
            else
            {
                RestoreViews();
                currentSolo = -1;
            }
        }

        private void PatternIcon_CollExpToggle(object sender, EventArgs e)
        {
            int patternIndex = ((PatternIcon)sender).PatternNum;

            if (patterns[patternIndex].patternIcon.CollExp)
            {
                ExpandOccurrences(patternIndex);
            }
            else
            {
                CollapseOccurrences(patternIndex);
            }
        }

        private void OccurrenceIcon_DeleteClick(object sender, EventArgs e)
        {
            if (!isUIMoving)
            {
                DeleteOccurrence(((OccurrenceIcon)sender).OccurrenceNum, ((OccurrenceIcon)sender).PatternNumOfOccurrence);
                MoveElement(btnAddPattern, (-1) * occurrenceIconHeight);
            }
        }

        private void OccurrenceIcon_MouseIn(object sender, EventArgs e)
        {
            if (patterns[((OccurrenceIcon)sender).PatternNumOfOccurrence].GetOccurrences().Count > 0)
            {
                Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);
                HighlightRect(occurrence);
            }
        }

        private void OccurrenceIcon_MouseOut(object sender, EventArgs e)
        {
            if (patterns[((OccurrenceIcon)sender).PatternNumOfOccurrence].GetOccurrences().Count > ((OccurrenceIcon)sender).OccurrenceNum)
            {
                Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);
                NormaliseRect(occurrence);
            }
        }

        private void OccurrenceIcon_MouseLeftClick(object sender, EventArgs e)
        {
            Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);

            srlPianoScroll.ScrollToHorizontalOffset((occurrence.GetEnd() + occurrence.GetStart() - srlPianoScroll.ViewportWidth + grdPiano.Width) / 2);
        }

        private void OccurrenceIcon_FindSimilar(object sender, EventArgs e)
        {
            Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);
            patternIndex = occurrence.occurrenceIcon.PatternNumOfOccurrence;
            patterns[patternIndex].patternIcon.IsChecked = true;

            List<Occurrence> similarOccurrences = SimilarOccurrences(occurrence);
            AddOccurrenceGraphics(similarOccurrences, patternIndex);
        }

        private Occurrence GetOccurrence(OccurrenceIcon icon)
        {
            return patterns[icon.PatternNumOfOccurrence].GetOccurrences()[icon.OccurrenceNum];
        }

        private void HighlightRect(Occurrence occurrence)
        {
            ((Border)occurrence.occurrenceRect.Children[0]).BorderThickness = new Thickness(4);
        }

        private void NormaliseRect(Occurrence occurrence)
        {
            ((Border)occurrence.occurrenceRect.Children[0]).BorderThickness = new Thickness(0);
        }

        private int GetPatternIndexFromName(FrameworkElement input)
        {
            return Int32.Parse(Regex.Match((input).Name, @"\d+").Value);
        }

        private int GetPatternIndexInItemsList(int patternIndex)
        {
            int index = 0;

            foreach (Pattern pattern in patterns.GetRange(0, patternIndex))
            {
                index += 1 + pattern.GetOccurrences().Count;
            }

            return index;
        }

        private void DeletePattern(int patternIndex)
        {
            double animMove = 0;

            if (patterns[patternIndex].patternIcon.CollExp)
            {
                animMove -= occurrenceIconHeight * (patterns[patternIndex].GetOccurrences().Count);
            }

            for (int i = patterns[patternIndex].GetOccurrences().Count - 1; i >= 0; i--)
            {
                DeleteOccurrence(i, patternIndex);
            }

            animMove -= patterns[patternIndex].patternIcon.Height;
            MoveElement(btnAddPattern, animMove);
            patterns.RemoveAt(patternIndex);
            itmPatternsView.Items.RemoveAt(GetPatternIndexInItemsList(patternIndex));

            foreach (Pattern pattern in patterns.GetRange(patternIndex, patterns.Count - patternIndex))
            {
                int newIndex = pattern.patternIcon.PatternNum - 1;

                pattern.patternIcon.Name = "PatternButton" + newIndex;
                pattern.patternIcon.TextContent = "" + (newIndex + 1);
                pattern.patternIcon.Background = defaultPatternColours[newIndex - (defaultPatternColours.Count * ((newIndex) / defaultPatternColours.Count))];
                pattern.patternIcon.PatternNum--;

                foreach (Occurrence occurrence in pattern.GetOccurrences())
                {
                    occurrence.occurrenceIcon.Background = pattern.patternIcon.Background;
                    occurrence.occurrenceIcon.PatternNumOfOccurrence--;

                    SolidColorBrush occurrenceRectColour = new SolidColorBrush(((SolidColorBrush)(pattern.patternIcon.Background)).Color)
                    {
                        Opacity = 0.3
                    };

                    ((Border)occurrence.occurrenceRect.Children[0]).Background = occurrenceRectColour;
                    ((Border)occurrence.occurrenceRect.Children[0]).BorderBrush = pattern.patternIcon.Background;
                }
            }
        }

        private void DeleteOccurrence(int occurrenceIndex, int patternIndex)
        {
            cnvMouseLayer.Children.Remove(patterns[patternIndex].GetOccurrences()[occurrenceIndex].occurrenceRect);
            patterns[patternIndex].GetOccurrences().RemoveAt(occurrenceIndex);
            int indextoRemove = GetPatternIndexInItemsList(patternIndex) + occurrenceIndex + 1;
            itmPatternsView.Items.RemoveAt(indextoRemove);

            foreach (Occurrence occurrence in patterns[patternIndex].GetOccurrences().GetRange(occurrenceIndex, patterns[patternIndex].GetOccurrences().Count - occurrenceIndex))
            {
                int newIndex = occurrence.occurrenceIcon.OccurrenceNum - 1;

                occurrence.occurrenceIcon.Name = "occurrenceIcon" + patternIndex + "s" + occurrence.occurrenceIcon.OccurrenceNum;
                occurrence.occurrenceIcon.OccurrenceText = "" + (newIndex + 1);
                occurrence.occurrenceIcon.OccurrenceNum--;
            }
        }

        private void ShowOccurrenceRects(int patternIndex)
        {
            foreach (Occurrence occ in patterns[patternIndex].GetOccurrences())
            {
                occ.occurrenceRect.Visibility = Visibility.Visible;
            }
        }

        private void HideOccurrenceRects(int patternIndex)
        {
            foreach (Occurrence occ in patterns[patternIndex].GetOccurrences())
            {
                occ.occurrenceRect.Visibility = Visibility.Hidden;
            }
        }

        private void SoloViewForPattern(int patternIndex)
        {
            for (int i = 0; i < patternIndex; i++)
            {
                patterns[i].patternIcon.View = false;
                HideOccurrenceRects(i);
            }

            patterns[patternIndex].patternIcon.View = true;
            ShowOccurrenceRects(patternIndex);

            for (int i = patternIndex + 1; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = false;
                HideOccurrenceRects(i);
            }
        }

        private void RestoreViews()
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = true;
                ShowOccurrenceRects(i);
            }
        }

        private void CollapseOccurrences(int patternIndex)
        {
            double animMove = 0;

            foreach (Occurrence occ in patterns[patternIndex].GetOccurrences())
            {
                animMove -= occurrenceIconHeight;

                occ.occurrenceIcon.Visibility = Visibility.Hidden;
            }

            for (int i = GetPatternIndexInItemsList(patternIndex) + patterns[patternIndex].GetOccurrences().Count; i < itmPatternsView.Items.Count; i++)
            {
                MoveCollapseElement((FrameworkElement)itmPatternsView.Items[i], animMove, patterns[patternIndex].GetOccurrences());
            }

            MoveElement(btnAddPattern, animMove);
        }

        private void ExpandOccurrences(int patternIndex)
        {
            double animMove = 0;

            animMove += occurrenceIconHeight * (patterns[patternIndex].GetOccurrences().Count);

            for (int i = GetPatternIndexInItemsList(patternIndex) + patterns[patternIndex].GetOccurrences().Count; i < itmPatternsView.Items.Count; i++)
            {
                MoveExpandElement((FrameworkElement)itmPatternsView.Items[i], animMove, patterns[patternIndex].GetOccurrences());
            }

            MoveElement(btnAddPattern, animMove);
        }

        private void PatternCheckbox_Click(object sender, RoutedEventArgs e)
        {
            if (((PatternIcon)sender).IsChecked == true)
            {
                for (int i = 0; i < patterns.Count; i++)
                {
                    if (!patterns[i].patternIcon.Name.Equals(((PatternIcon)sender).Name))
                    {
                        patterns[i].patternIcon.IsChecked = false;
                    }
                    else
                    {
                        patternIndex = i;
                    }
                }
            }
            else
            {
                patternIndex = -1;
            }
        }

        private void PianoScroll_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                var sv = sender as ScrollViewer;
                scrollHorOff = sv.HorizontalOffset;
                scrollVerOff = sv.VerticalOffset;
                scrollMousePoint = e.GetPosition(sv);
                isDraggingScroll = true;
            }
        }

        private void PianoScroll_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
            {
                isDraggingScroll = false;
            }
        }

        private void PianoScroll_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingScroll)
            {
                var sv = sender as ScrollViewer;
                sv.ScrollToHorizontalOffset(scrollHorOff + (scrollMousePoint.X - e.GetPosition(sv).X));
                sv.ScrollToVerticalOffset(scrollVerOff + (scrollMousePoint.Y - e.GetPosition(sv).Y));
            }
        }

        private void PianoRoll_LeftMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isUIMoving)
            {
                origMouseDownPoint = GetCurrentMousePosition(e);
                origMouseDownPoint.X = Snap(origMouseDownPoint.X, snapLength);
                prevMouseDownPoint = origMouseDownPoint;

                isLeftMouseButtonDownOnPianoRoll = true;
                Mouse.Capture(cnvMouseLayer);
            }
        }

        private void PianoRoll_LeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            isLeftMouseButtonDownOnPianoRoll = false;
            Mouse.Capture(null);

            if (!isUIMoving && isDraggingPatternRect)
            {
                if (patternIndex != -1)
                {
                    Occurrence newOccurrence = AddOccurrence(patternIndex, currentOccurrenceRect);
                    AddOccurrenceIcon(newOccurrence);
                    currentOccurrenceRect = null;
                }
            }

            isDraggingPatternRect = false;
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
                    curMouseDownPoint.X = Snap(curMouseDownPoint.X, snapLength);
                    prevMouseDownPoint = curMouseDownPoint;
                    UpdateDragPatternRect(origMouseDownPoint, curMouseDownPoint);

                    e.Handled = true;
                }
                else if (isLeftMouseButtonDownOnPianoRoll && (patternIndex != -1))
                {
                    curMouseDownPoint.X = Snap(curMouseDownPoint.X, snapLength);
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
            if (!patterns[patternIndex].patternIcon.View)
            {
                patterns[patternIndex].patternIcon.View = true;

                ShowOccurrenceRects(patternIndex);
            }

            currentOccurrenceRect = CreateOccurrenceRect(patternIndex);
            cnvMouseLayer.Children.Add(currentOccurrenceRect);
            UpdateDragPatternRect(pt1, pt2);
        }

        private void UpdateDragPatternRect(Point pt1, Point pt2)
        {
            double x, width;

            x = (pt1.X < pt2.X) ? pt1.X : pt2.X;
            width = Math.Abs(pt2.X - pt1.X);

            Border dragBorder = (Border)currentOccurrenceRect.Children[0];

            Canvas.SetLeft(dragBorder, x);
            Canvas.SetTop(dragBorder, 0);
            dragBorder.Width = width;
            dragBorder.Height = cnvMouseLayer.Height;
        }

        private int PitchToRow(int pitch)
        {
            return 109 - pitch;
        }

        // Moves a given element a specified number of units downwards.
        private void MoveElement(FrameworkElement target, double down)
        {
            TranslateTransform moveTransform = new TranslateTransform();
            target.RenderTransform = moveTransform;

            DoubleAnimation verticalAnim = new DoubleAnimation { By = down, Duration = TimeSpan.FromSeconds(0.2) };
            verticalAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };

            DisableUI();

            verticalAnim.Completed += (s, e) =>
            {
                target.Margin = new Thickness(target.Margin.Left, target.Margin.Top + target.RenderTransform.Value.OffsetY, target.Margin.Right, target.Margin.Bottom);
                target.RenderTransform = new TranslateTransform();
                EnableUI();
            };

            moveTransform.BeginAnimation(TranslateTransform.YProperty, verticalAnim);
        }

        // Move element and collapse it afterwards.
        private void MoveCollapseElement(FrameworkElement target, double down, List<Occurrence> occurrences)
        {
            TranslateTransform moveTransform = new TranslateTransform();
            target.RenderTransform = moveTransform;

            DoubleAnimation verticalAnim = new DoubleAnimation { By = down, Duration = TimeSpan.FromSeconds(0.2) };
            verticalAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };

            DisableUI();

            verticalAnim.Completed += (s, e) =>
            {
                target.Margin = new Thickness(0,0,0,0);
                target.RenderTransform = new TranslateTransform();
                EnableUI();

                foreach (Occurrence occurrence in occurrences)
                {
                    occurrence.occurrenceIcon.Visibility = Visibility.Collapsed;
                }
            };

            moveTransform.BeginAnimation(TranslateTransform.YProperty, verticalAnim);
        }

        // Move element and make it visible afterwards.
        private void MoveExpandElement(FrameworkElement target, double down, List<Occurrence> occurrences)
        {
            TranslateTransform moveTransform = new TranslateTransform();
            target.RenderTransform = moveTransform;

            DoubleAnimation verticalAnim = new DoubleAnimation { By = down, Duration = TimeSpan.FromSeconds(0.2) };
            verticalAnim.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };

            DisableUI();

            verticalAnim.Completed += (s, e) =>
            {
                target.Margin = new Thickness(0, 0, 0, 0);
                target.RenderTransform = new TranslateTransform();
                EnableUI();

                foreach (Occurrence occurrence in occurrences)
                {
                    occurrence.occurrenceIcon.Visibility = Visibility.Visible;
                }
            };

            moveTransform.BeginAnimation(TranslateTransform.YProperty, verticalAnim);
        }

        // Snaps a value to the nearest multiple.
        private double Snap(double value, double multiple)
        {
            return Math.Round(value / multiple) * multiple;
        }

        private void DisableUI()
        {
            foreach (Pattern pattern in patterns)
            {
                pattern.patternIcon.IsEnabled = false;
            }

            isUIMoving = true;
            btnAddPattern.IsEnabled = false;
        }

        private void EnableUI()
        {
            foreach (Pattern pattern in patterns)
            {
                pattern.patternIcon.IsEnabled = true;
            }

            isUIMoving = false;
            btnAddPattern.IsEnabled = true;
        }
    }
}