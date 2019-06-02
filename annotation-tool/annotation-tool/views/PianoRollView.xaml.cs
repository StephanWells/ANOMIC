using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using Midi;
using System.Windows.Threading;
using System.Security;

namespace AnnotationTool.views
{
    public partial class PianoRollView : UserControl
    {
        private const int pianoRollRows = 88;
        private const int occurrenceIconHeight = 20;
        private const double resolution = 96;
        private const double barNumOffset = 4;
        private const double moveScrollThreshold = 20;
        private const double moveScrollSpeed = 0.5;
        private const bool logging = true;

        private static OutputDevice outputDevice;
        private Midi.Clock scheduler;
        private TranslateTransform trackerTransform = new TranslateTransform();
        private static IList<DispatcherTimer> timers = new List<DispatcherTimer>();

        private List<NoteRect> currentNotes = new List<NoteRect>();
        private List<NoteRect>[] notes = new List<NoteRect>[17];

        private double timeDivRatio;
        private double horizSnap;
        private double vertiSnap;
        private double currentHorizZoom;
        private double currentVertiZoom;

        private MIDIParser midiParse;
        private NoteParser noteParse;

        private List<List<NotePitch>> quantisedSong;

        private List<Pattern> patterns;
        private List<NoteRect> currentSelection = new List<NoteRect>();
        private Canvas currentOccurrenceRect;
        private Occurrence currentOccurrence;
        private int currentPattern = -1;
        private int currentSolo = -1;
        private int currentChannel = 0;

        bool isLeftMouseButtonDownOnPianoRoll = false;
        bool isLeftMouseButtonDownOnTimeline = false;
        bool isDraggingPatternRect = false;
        bool isSelectingOccurrence = false;
        bool isDraggingScroll = false;
        bool isUIMoving = false;
        bool isPlaying = false;

        Point origMouseDownPoint;
        Point prevMouseDownPoint;
        Point scrollMousePoint;

        double scrollHorOff = 1;
        double scrollVerOff = 1;

        private List<Line> gridLines;
        private List<TextBlock> barNumbers;
        private List<Log> logs = new List<Log>();
        private Stopwatch sw = new Stopwatch();

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
            sw.Start();
            if (logging) logs.Add(new Log() { logType = LogType.Session, time = sw.Elapsed, value = "Session start" });

            InitializeComponent();
            Init(midiParseIn, noteParseIn);

            MainWindow.MIDIBrowseClick += new EventHandler(MainWindow_MIDIBrowseClick);
            MainWindow.Exit += new EventHandler(MainWindow_Exit);
            MainWindow.OpenAnnotationsClick += new EventHandler(MainWindow_OpenAnnotationsClick);
            MainWindow.SaveAnnotationsClick += new EventHandler(MainWindow_SaveAnnotationsClick);
            MainWindow.SnapChange += new EventHandler(MainWindow_SnapChange);
            MainWindow.HorizZoomChange += new EventHandler(MainWindow_HorizZoomChange);
            MainWindow.VertiZoomChange += new EventHandler(MainWindow_VertiZoomChange);
            MainWindow.ExpandAll += new EventHandler(MainWindow_ExpandAll);
            MainWindow.CollapseAll += new EventHandler(MainWindow_CollapseAll);
            MainWindow.ShowAll += new EventHandler(MainWindow_ShowAll);
            MainWindow.HideAll += new EventHandler(MainWindow_HideAll);
            MainWindow.AddPattern += new EventHandler(MainWindow_AddPattern);
            MainWindow.DeletePattern += new EventHandler(MainWindow_DeletePattern);
            MainWindow.KeyVisibilityChange += new EventHandler(MainWindow_KeyVisibilityChange);
            MainWindow.GridVisibilityOn += new EventHandler(MainWindow_GridVisibilityOn);
            MainWindow.GridVisibilityOff += new EventHandler(MainWindow_GridVisibilityOff);
            MainWindow.SongSelectOn += new EventHandler(MainWindow_SongSelectOn);
            MainWindow.SongSelectOff += new EventHandler(MainWindow_SongSelectOff);
            MainWindow.AutomaticIconsOn += new EventHandler(MainWindow_AutomaticIconsOn);
            MainWindow.AutomaticIconsOff += new EventHandler(MainWindow_AutomaticIconsOff);
            //MainWindow.DarkModeOn += new EventHandler(MainWindow_DarkModeOn);
            //MainWindow.DarkModeOff += new EventHandler(MainWindow_DarkModeOff);
            MainWindow.Play += new EventHandler(MainWindow_Play);
            MainWindow.Pause += new EventHandler(MainWindow_Pause);
            MainWindow.Stop += new EventHandler(MainWindow_Stop);
            MainWindow.NormaliseVelocitiesOn += new EventHandler(MainWindow_NormaliseVelocitiesOn);
            MainWindow.NormaliseVelocitiesOff += new EventHandler(MainWindow_NormaliseVelocitiesOff);
            MainWindow.ClosingApp += new EventHandler(MainWindow_Closing);

            Loaded += OnLoaded;
        }

        public void Reset(MIDIParser midiParseIn, NoteParser noteParseIn)
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

            childrenCount = grdTimeline.Children.Count - 1;

            for (int i = childrenCount; i>= 0; i--)
            {
                if (grdTimeline.Children[i].GetType() == typeof(TextBlock) && grdTimeline.Children[i] != txtTrackerTriangle)
                {
                    TextBlock currentChild = (TextBlock)grdTimeline.Children[i];
                    grdTimeline.Children.Remove(currentChild);
                }
            }

            cnvPianoRoll.Width = grdPiano.Width;
            this.Resources["PianoRollWidth"] = (double)0;

            cnvGridLinesBar.Children.Clear();
            cnvGridLinesHalfBar.Children.Clear();
            cnvGridLinesQuarterNote.Children.Clear();
            cnvGridLinesEighthNote.Children.Clear();
            cnvGridLinesSixteenthNote.Children.Clear();
            cnvGridLinesThirtySecondNote.Children.Clear();

            ClearPatterns();
            cmbChannelSelect.SelectedIndex = 0;
            Init(midiParseIn, noteParseIn);
        }

        public void Init(MIDIParser midiParseIn, NoteParser noteParseIn)
        {
            if (logging) logs.Add(new Log() { logType = LogType.LoadedFile, time = sw.Elapsed, value = midiParseIn.fileName });

            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = new List<NoteRect>();
            }

            gridLines = new List<Line>();
            barNumbers = new List<TextBlock>();

            if (MainWindow.settings.gridLines)
            {
                GridVisibilityOn();
            }
            else
            {
                GridVisibilityOff();
            }

            UpdateKeyNames();
            UpdateSnap();
            InitVertiZoom();
            currentHorizZoom = MainWindow.settings.horizZoom;

            patterns = new List<Pattern>();
            currentOccurrence = new Occurrence();

            midiParse = midiParseIn;
            noteParse = noteParseIn;

            timeDivRatio = resolution / midiParse.header.timeDiv;

            foreach (Note note in noteParse.notes)
            {
                note.SetTime(note.GetTime() * timeDivRatio);
                note.SetDuration(note.GetDuration() * timeDivRatio);
            }

            horizSnap = Math.Round((MainWindow.settings.horizZoom * resolution) / 8, 2);
            vertiSnap = Math.Round((MainWindow.settings.vertiZoom * (double)this.Resources["PianoRollHeight"]) / grdNotes.RowDefinitions.Count, 2);

            scheduler = new Midi.Clock(noteParse.bpm);

            try
            {
                outputDevice = OutputDevice.InstalledDevices[0];
            }
            catch (NullReferenceException)
            {

            }
            catch (InvalidOperationException)
            {

            }

            txtFileName.Text = midiParse.fileName.Length >= 30 ? midiParse.fileName.Substring(0, 30) + "..." : midiParse.fileName;
            txtBPM.Text = "BPM: " + noteParse.bpm;

            foreach (UIElement textBlock in grdKeyNames.Children)
            {
                textBlock.Visibility = ((TextBlock)textBlock).Text.Contains("C") && !((TextBlock)textBlock).Text.Contains("#") ? Visibility.Visible : Visibility.Collapsed;
            }

            InitScroll();

            PopulateNotes(noteParse);
            scheduler.Reset();
            ScheduleNotes();

            quantisedSong = Quantise(0, (int)grdNotes.Width);

            try
            {
                if (!outputDevice.IsOpen) outputDevice.Open();
            }
            catch (InvalidOperationException)
            {

            }

            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { Cursor = Cursors.Arrow; })).Wait();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitScroll();
        }

        private void InitScroll()
        {
            double centre = srlPianoScroll.ScrollableHeight / 2.0;

            if (noteParse.notes.Count > 0)
            {
                double scroll = (double)PitchToRow((int)noteParse.notes[0].GetPitch()) / 87 * srlPianoScroll.ScrollableHeight;

                srlPianoScroll.ScrollToVerticalOffset(scroll);
            }

            srlPianoScroll.Focus();
        }

        private void ScheduleNotes()
        {
            foreach (NoteRect noteRect in notes[currentChannel])
            {
                scheduler.Schedule(new NoteOnMessage(outputDevice, (Midi.Channel)noteRect.note.GetChannel(), (Midi.Pitch)noteRect.note.GetPitch(), MainWindow.settings.normaliseVelocities ? 100 : noteRect.note.GetVelocity(), (float)(noteRect.note.GetTime() / resolution)));
                scheduler.Schedule(new NoteOffMessage(outputDevice, (Midi.Channel)noteRect.note.GetChannel(), (Midi.Pitch)noteRect.note.GetPitch(), MainWindow.settings.normaliseVelocities ? 100 : noteRect.note.GetVelocity(), (float)((noteRect.note.GetTime() + noteRect.note.GetDuration()) / resolution)));
            }
        }

        private void ScheduleNotes(double start, double end)
        {
            foreach (NoteRect noteRect in notes[currentChannel])
            {
                if (noteRect.note.GetTime() >= start && noteRect.note.GetTime() <= end)
                {
                    scheduler.Schedule(new NoteOnMessage(outputDevice, (Midi.Channel)noteRect.note.GetChannel(), (Midi.Pitch)noteRect.note.GetPitch(), MainWindow.settings.normaliseVelocities ? 100 : noteRect.note.GetVelocity(), (float)((noteRect.note.GetTime() - start) / resolution)));
                    scheduler.Schedule(new NoteOffMessage(outputDevice, (Midi.Channel)noteRect.note.GetChannel(), (Midi.Pitch)noteRect.note.GetPitch(), MainWindow.settings.normaliseVelocities ? 100 : noteRect.note.GetVelocity(), (float)((noteRect.note.GetTime() - start + noteRect.note.GetDuration()) / resolution)));
                }
            }
        }

        private void HighlightNotes(List<NoteRect> notesIn, int patternIndex, int occurrenceIndex)
        {
            foreach (NoteRect noteRect in notesIn)
            {
                HighlightNote(noteRect, patternIndex, occurrenceIndex);
            }
        }

        private void HighlightNote(NoteRect noteRect, int patternIndex, int occurrenceIndex)
        {
            if (noteRect.noteOutlines.ContainsKey(patternIndex + "n" + occurrenceIndex))
            {
                noteRect.noteOutlines[patternIndex + "n" + occurrenceIndex].Visibility = Visibility.Visible;
            }
            else
            {
                Rectangle noteOutline = new Rectangle
                {
                    Fill = Brushes.Transparent,
                    Stroke = patterns[patternIndex].patternIcon.Background,
                    StrokeThickness = 2.5,
                    Width = noteRect.noteBar.Width,
                    Height = noteRect.noteBar.Height,
                    Margin = noteRect.noteBar.Margin,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Visibility = Visibility.Visible,
                    Name = "outl" + GetNoteIndex(noteRect.noteBar)
                };

                Panel.SetZIndex(noteOutline, 3);
                Grid.SetRow(noteOutline, Grid.GetRow(noteRect.noteBar));
                noteOutline.MouseDown += NoteRect_SelectNote;
                noteOutline.MouseEnter += NoteRect_MouseIn;
                noteOutline.MouseLeave += NoteRect_MouseOut;
                noteRect.noteOutlines.Add(patternIndex + "n" + occurrenceIndex, noteOutline);
                grdNotes.Children.Add(noteOutline);
            }
        }

        private void RemoveNoteHighlights(List<NoteRect> notesIn, int patternIndex, int occurrenceIndex)
        {
            foreach (NoteRect noteRect in notesIn)
            {
                RemoveNoteHighlight(noteRect, patternIndex, occurrenceIndex);
            }
        }

        private void RemoveNoteHighlight(NoteRect noteRect, int patternIndex, int occurrenceIndex)
        {
            grdNotes.Children.Remove(noteRect.noteOutlines[patternIndex + "n" + occurrenceIndex]);
            noteRect.noteOutlines.Remove(patternIndex + "n" + occurrenceIndex);
        }

        private void HighlightNoteRects()
        {
            double bpmFactor = 60 / noteParse.bpm;

            foreach (NoteRect noteRect in notes[0])
            {
                DelayedExecute(() => HighlightNoteRect(noteRect), bpmFactor * noteRect.note.GetTime() / resolution);
                DelayedExecute(() => RemoveHighlight(noteRect), (bpmFactor * (noteRect.note.GetTime() + noteRect.note.GetDuration())) / resolution);
            }
        }

        private void HighlightNoteRects(double start, double end)
        {
            double bpmFactor = 60 / noteParse.bpm;

            foreach (NoteRect noteRect in notes[0])
            {
                if (noteRect.note.GetTime() >= start && noteRect.note.GetTime() <= end)
                {
                    DelayedExecute(() => HighlightNoteRect(noteRect), bpmFactor * (noteRect.note.GetTime() - start) / resolution);
                    DelayedExecute(() => RemoveHighlight(noteRect), (bpmFactor * (noteRect.note.GetTime() - start + noteRect.note.GetDuration())) / resolution);
                }
            }
        }

        private void CancelNoteRectHighlights()
        {
            foreach (NoteRect note in currentNotes)
            {
                note.noteBar.Fill = new SolidColorBrush(Colors.LightGray);
            }

            currentNotes.Clear();
            
            foreach (DispatcherTimer timer in timers)
            {
                timer.Stop();
            }
        }

        private void HighlightNoteRect(NoteRect noteRect)
        {
            noteRect.noteBar.Fill = (Brush)this.Resources["ButtonMouseOverColour"];
            currentNotes.Add(noteRect);
        }

        private void RemoveHighlight(NoteRect noteRect)
        {
            noteRect.noteBar.Fill = new SolidColorBrush(Colors.LightGray);
            currentNotes.Remove(noteRect);
        }

        public static void DelayedExecute(Action action, double delay)
        {
            DispatcherTimer dispatcherTimer = new DispatcherTimer();

            timers.Add(dispatcherTimer);

            EventHandler handler = null;
            handler = (sender, e) =>
            {
                dispatcherTimer.Tick -= handler;
                dispatcherTimer.Stop();
                timers.Remove(dispatcherTimer);
                action();
            };

            dispatcherTimer.Tick += handler;
            dispatcherTimer.Interval = TimeSpan.FromSeconds(delay);
            dispatcherTimer.Start();
        }

        private void MainWindow_MIDIBrowseClick(object sender, EventArgs e)
        {
            StopMusic();
        }

        private void MainWindow_Exit(object sender, EventArgs e)
        {
            HaltSound();
        }

        private void MainWindow_OpenAnnotationsClick(object sender, EventArgs e)
        {
            OpenFileDialog browseDialog = new OpenFileDialog
            {
                Filter = "JAMS files (*.jams)|*.jams|All files (*.*)|*.*"
            };

            if (browseDialog.ShowDialog() == true)
            {
                string fileInput = "";

                using (StreamReader sr = new StreamReader(browseDialog.FileName.ToString()))
                {
                    fileInput = sr.ReadToEnd();
                }

                FileParser fileParser = new FileParser(fileInput);
                List<Pattern> filePatterns = fileParser.ParseFile();

                if (fileParser.midiName != midiParse.fileName || Double.Parse(fileParser.midiDuration) != noteParse.midiLength)
                {
                    MessageBox.Show("Annotation data does not match MIDI file!", "Warning", MessageBoxButton.OK);
                }

                ClearPatterns();
                ImportPatterns(filePatterns);

                if (logging) logs.Add(new Log() { logType = LogType.PatternImport, time = sw.Elapsed, value = "Filename: " + browseDialog.SafeFileName.ToString() });
            }
        }

        private void MainWindow_SaveAnnotationsClick(object sender, EventArgs e)
        {
            SaveFileDialog browseDialog = new SaveFileDialog
            {
                Filter = "JAMS files (*.jams)|*.jams|All files (*.*)|*.*"
            };

            if (browseDialog.ShowDialog() == true)
            {
                FileParser fileParser = new FileParser(patterns);
                fileParser.midiName = midiParse.fileName;
                fileParser.midiDuration = "" + noteParse.midiLength;

                if (logging) fileParser.logs = logs;
                if (logging) logs.Add(new Log() { logType = LogType.Session, time = sw.Elapsed, value = "Session end" });
                string[] file = fileParser.ParseToJAMS();

                try
                {
                    File.WriteAllLines(browseDialog.FileName, file);
                }
                catch (ArgumentException)
                {
                    MessageBox.Show("Invalid filepath!", "Error");
                }
                catch (NotSupportedException)
                {
                    MessageBox.Show("Invalid filepath!", "Error");
                }
                catch (PathTooLongException)
                {
                    MessageBox.Show("Filepath too long!", "Error");
                }
                catch (DirectoryNotFoundException)
                {
                    MessageBox.Show("Directory not found!", "Error");
                }
                catch (IOException)
                {
                    MessageBox.Show("Error writing to file!", "Error");
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Permissions error when writing to file!", "Error");
                }
                catch (SecurityException)
                {
                    MessageBox.Show("Permissions error when writing to file!", "Error");
                }
            }
        }

        private void PianoRoll_OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    if (isPlaying && linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX != grdNotes.Width)
                    {
                        PauseMusic();
                    }
                    else
                    {
                        PlayMusic();
                    }
                break;

                case Key.Escape:
                    if (isSelectingOccurrence)
                    {
                        CancelOccurrenceCreation();
                    }
                break;

                case Key.Enter:
                    if (isSelectingOccurrence && !isUIMoving)
                    {
                        ConfirmOccurrenceCreation(currentPattern);
                    }
                break;
            }
        }

        private void HaltSound()
        {
            if (scheduler.IsRunning)
            {
                MuteCurrentNotes();
                scheduler.Stop();
                scheduler.Reset();
            }
        }
        
        private void MuteCurrentNotes()
        {
            foreach (NoteRect noteRect in currentNotes)
            {
                outputDevice.SendNoteOff((Channel)noteRect.note.GetChannel(), (Midi.Pitch)noteRect.note.GetPitch(), noteRect.note.GetVelocity());
            }
        }

        private void MainWindow_SnapChange(object sender, EventArgs e)
        {
            UpdateSnap();

            if (logging) logs.Add(new Log() { logType = LogType.SnapUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.snap });
        }

        private void MainWindow_HorizZoomChange(object sender, EventArgs e)
        {
            UpdateHorizZoom(MainWindow.settings.horizZoom);

            if (logging) logs.Add(new Log() { logType = LogType.HorizZoomUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.horizZoom });
        }

        private void MainWindow_VertiZoomChange(object sender, EventArgs e)
        {
            UpdateVertiZoom(MainWindow.settings.vertiZoom);

            if (logging) logs.Add(new Log() { logType = LogType.VertiZoomUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.vertiZoom });
        }

        private void MainWindow_KeyVisibilityChange(object sender, EventArgs e)
        {
            UpdateKeyNames();

            if (logging) logs.Add(new Log() { logType = LogType.KeyVisibilityUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.keyNames });
        }

        private void MainWindow_GridVisibilityOn(object sender, EventArgs e)
        {
            GridVisibilityOn();

            if (logging) logs.Add(new Log() { logType = LogType.GridVisibilityUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.gridLines });
        }

        private void MainWindow_GridVisibilityOff(object sender, EventArgs e)
        {
            GridVisibilityOff();

            if (logging) logs.Add(new Log() { logType = LogType.GridVisibilityUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.gridLines });
        }

        private void MainWindow_SongSelectOn(object sender, EventArgs e)
        {
            Panel.SetZIndex(grdNotes, 3);

            if (logging) logs.Add(new Log() { logType = LogType.SongSelectUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.songSelect });
        }

        private void MainWindow_SongSelectOff(object sender, EventArgs e)
        {
            Panel.SetZIndex(grdNotes, 5);

            if (isSelectingOccurrence)
            {
                CancelOccurrenceCreation();
            }

            if (logging) logs.Add(new Log() { logType = LogType.SongSelectUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.songSelect });
        }

        private void MainWindow_AutomaticIconsOn(object sender, EventArgs e)
        {
            foreach (Pattern pattern in patterns)
            {
                foreach (Occurrence occurrence in pattern.GetOccurrences())
                {
                    if (occurrence.isAutomatic)
                    {
                        occurrence.occurrenceIcon.AutomaticIcon = Visibility.Visible;
                    }
                }
            }

            if (logging) logs.Add(new Log() { logType = LogType.AutomaticIconsUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.automaticIcons });
        }

        private void MainWindow_AutomaticIconsOff(object sender, EventArgs e)
        {
            foreach (Pattern pattern in patterns)
            {
                foreach (Occurrence occurrence in pattern.GetOccurrences())
                {
                    if (occurrence.isAutomatic)
                    {
                        occurrence.occurrenceIcon.AutomaticIcon = Visibility.Hidden;
                    }
                }
            }

            if (logging) logs.Add(new Log() { logType = LogType.AutomaticIconsUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.automaticIcons });
        }

        //private void MainWindow_DarkModeOn(object sender, EventArgs e)
        //{
        //    this.Resources["ButtonColour"] = (new BrushConverter()).ConvertFrom("#6D7081");
        //    this.Resources["ButtonMouseOverColour"] = (new BrushConverter()).ConvertFrom("#00CCCC");
        //    this.Resources["ButtonPressedColour"] = (new BrushConverter()).ConvertFrom("#00B7B7");
        //    this.Resources["MenuButtonMouseOverColour"] = (new BrushConverter()).ConvertFrom("#393C46");
        //    this.Resources["GridLineColour"] = (new BrushConverter()).ConvertFrom("#07070A");
        //    this.Resources["GridExtraLightColour"] = (new BrushConverter()).ConvertFrom("#74778B");
        //    this.Resources["GridLightColour"] = (new BrushConverter()).ConvertFrom("#25262C");
        //    this.Resources["GridDarkColour"] = (new BrushConverter()).ConvertFrom("#1D1E22");
        //    this.Resources["GridExtraDarkColour"] = (new BrushConverter()).ConvertFrom("#0C0C0E");
        //}

        //private void MainWindow_DarkModeOff(object sender, EventArgs e)
        //{
        //    this.Resources["ButtonColour"] = (new BrushConverter()).ConvertFrom("#e3e5e8");
        //    this.Resources["ButtonMouseOverColour"] = (new BrushConverter()).ConvertFrom("#FF5C33");
        //    this.Resources["ButtonPressedColour"] = (new BrushConverter()).ConvertFrom("#CC2900");
        //    this.Resources["MenuButtonMouseOverColour"] = (new BrushConverter()).ConvertFrom("#838795");
        //    this.Resources["GridLineColour"] = (new BrushConverter()).ConvertFrom("#07070A");
        //    this.Resources["GridExtraLightColour"] = (new BrushConverter()).ConvertFrom("#232529");
        //    this.Resources["GridLightColour"] = (new BrushConverter()).ConvertFrom("#f1f2f3");
        //    this.Resources["GridDarkColour"] = (new BrushConverter()).ConvertFrom("#e3e5e8");
        //    this.Resources["GridExtraDarkColour"] = (new BrushConverter()).ConvertFrom("#0C0C0E");
        //}

        private void MainWindow_ExpandAll(object sender, EventArgs e)
        {
            ExpandAllPatternOccurrences();

            if (logging) logs.Add(new Log() { logType = LogType.ExpandAll, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_CollapseAll(object sender, EventArgs e)
        {
            CollapseAllPatternOccurrences();

            if (logging) logs.Add(new Log() { logType = LogType.CollapseAll, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_ShowAll(object sender, EventArgs e)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = true;
                ShowOccurrenceVisuals(i);
            }

            if (logging) logs.Add(new Log() { logType = LogType.ShowAll, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_HideAll(object sender, EventArgs e)
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = false;
                HideOccurrenceVisuals(i);
            }

            if (logging) logs.Add(new Log() { logType = LogType.HideAll, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_AddPattern(object sender, EventArgs e)
        {
            if (!isSelectingOccurrence)
            {
                MoveElement(btnAddPattern, AddPattern());
            }
            else
            {
                MessageBox.Show("Cannot add pattern while currently selecting an occurrence.", "Error", MessageBoxButton.OK);
            }
        }

        private void MainWindow_DeletePattern(object sender, EventArgs e)
        {
            if (currentPattern != -1)
            {
                if (!isSelectingOccurrence)
                {
                    MoveElement(btnAddPattern, DeletePattern(currentPattern));
                }
            }
        }

        private void MainWindow_Play(object sender, EventArgs e)
        {
            PlayMusic();

            if (logging) logs.Add(new Log() { logType = LogType.Play, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_Pause(object sender, EventArgs e)
        {
            PauseMusic();

            if (logging) logs.Add(new Log() { logType = LogType.Pause, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_Stop(object sender, EventArgs e)
        {
            StopMusic();

            if (logging) logs.Add(new Log() { logType = LogType.Stop, time = sw.Elapsed, value = "n/a" });
        }

        private void MainWindow_NormaliseVelocitiesOn(object sender, EventArgs e)
        {
            MuteCurrentNotes();
            RefreshMusic();

            if (logging) logs.Add(new Log() { logType = LogType.NormaliseVelocitiesUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.normaliseVelocities });
        }

        private void MainWindow_NormaliseVelocitiesOff(object sender, EventArgs e)
        {
            MuteCurrentNotes();
            RefreshMusic();

            if (logging) logs.Add(new Log() { logType = LogType.NormaliseVelocitiesUpdate, time = sw.Elapsed, value = "" + MainWindow.settings.normaliseVelocities });
        }

        private void MainWindow_Closing(object sender, EventArgs e)
        {
            HaltSound();

            if (logging) logs.Add(new Log() { logType = LogType.Session, time = sw.Elapsed, value = "Session end" });
        }

        private void GridVisibilityOn()
        {
            cnvGridLinesBar.Visibility = Visibility.Visible;
            cnvGridLinesHalfBar.Visibility = Visibility.Visible;
            cnvGridLinesQuarterNote.Visibility = Visibility.Visible;
            cnvGridLinesEighthNote.Visibility = Visibility.Visible;
            cnvGridLinesSixteenthNote.Visibility = MainWindow.settings.horizZoom < 0.3 ? Visibility.Hidden : Visibility.Visible;
            cnvGridLinesThirtySecondNote.Visibility = MainWindow.settings.horizZoom < 0.6 ? Visibility.Hidden : Visibility.Visible;
        }

        private void GridVisibilityOff()
        {
            cnvGridLinesBar.Visibility = Visibility.Hidden;
            cnvGridLinesHalfBar.Visibility = Visibility.Hidden;
            cnvGridLinesQuarterNote.Visibility = Visibility.Hidden;
            cnvGridLinesEighthNote.Visibility = Visibility.Hidden;
            cnvGridLinesSixteenthNote.Visibility = Visibility.Hidden;
            cnvGridLinesThirtySecondNote.Visibility = Visibility.Hidden;
        }

        private void UpdateKeyNames()
        {
            switch (MainWindow.settings.keyNames)
            {
                case 0:
                    foreach (UIElement textBlock in grdKeyNames.Children)
                    {
                        textBlock.Visibility = Visibility.Visible;
                    }
                break;

                case 1:
                    foreach (UIElement textBlock in grdKeyNames.Children)
                    {
                        textBlock.Visibility = ((TextBlock)textBlock).Text.Contains("C") && !((TextBlock)textBlock).Text.Contains("#") ? Visibility.Visible : Visibility.Collapsed;
                    }
                break;

                case 2:
                    foreach (UIElement textBlock in grdKeyNames.Children)
                    {
                        textBlock.Visibility = Visibility.Collapsed;
                    }
                break;
            }
        }

        private void UpdateSnap()
        {
            switch (MainWindow.settings.snap)
            {
                case 0:
                    horizSnap = Math.Round(MainWindow.settings.horizZoom * resolution, 2);
                break;

                case 1:
                    horizSnap = Math.Round((MainWindow.settings.horizZoom * resolution) / 2, 2);
                break;

                case 2:
                    horizSnap = Math.Round((MainWindow.settings.horizZoom * resolution) / 3, 2);
                break;

                case 3:
                    horizSnap = Math.Round((MainWindow.settings.horizZoom * resolution) / 4, 2);
                break;

                case 4:
                    horizSnap = Math.Round((MainWindow.settings.horizZoom * resolution) / 8, 2);
                break;
            }

            quantisedSong = Quantise(0, (int)grdNotes.Width);
        }

        private void ExpandAllPatternOccurrences()
        {
            if (!isUIMoving)
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
        }

        private void CollapseAllPatternOccurrences()
        {
            if (!isUIMoving)
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
        }

        private void UpdateHorizZoom(double zoomSetting)
        {
            double oldZoom = currentHorizZoom;
            MainWindow.settings.horizZoom = zoomSetting;
            double mousePosOnPianoRoll = Math.Round(Mouse.GetPosition(srlPianoScroll).X - grdPiano.Width < 0 ? 0 : Mouse.GetPosition(srlPianoScroll).X - grdPiano.Width, 2);
            double scrollAnchorOffset = Math.Round(mousePosOnPianoRoll - (MainWindow.settings.horizZoom / oldZoom) * mousePosOnPianoRoll, 2);

            srlPianoScroll.ScrollToHorizontalOffset(Math.Round((MainWindow.settings.horizZoom / oldZoom) * (srlPianoScroll.HorizontalOffset) - scrollAnchorOffset, 2));

            double thirtySecondNote = Math.Round(MainWindow.settings.horizZoom * (resolution / 8), 2);
            double width = Math.Round(barNumbers.Count * MainWindow.settings.horizZoom * resolution * 4, 2);

            cnvPianoRoll.Width = width + grdPiano.Width;
            this.Resources["PianoRollWidth"] = width;
            grdTimeline.Width = width + barNumOffset * 2;

            UpdateTrackerPosition(Math.Round(((linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX) / oldZoom) * MainWindow.settings.horizZoom, 2));

            for (int i = 0; i < gridLines.Count; i++)
            {
                Canvas.SetLeft(gridLines[i], i * thirtySecondNote + thirtySecondNote);
            }

            foreach (NoteRect noteRect in notes[0])
            {
                double noteWidth = Math.Round(noteRect.note.GetDuration() * MainWindow.settings.horizZoom, 2);
                Thickness noteMargin = new Thickness(Math.Round(noteRect.note.GetTime() * MainWindow.settings.horizZoom, 2), 0, 0, 0);

                noteRect.noteBar.Width = noteWidth;
                noteRect.noteBar.Margin = noteMargin;

                foreach (KeyValuePair<string, Rectangle> outline in noteRect.noteOutlines)
                {
                    outline.Value.Width = noteWidth;
                    outline.Value.Margin = noteMargin;
                }
            }

            for (int i = 0; i < barNumbers.Count; i++)
            {
                barNumbers[i].Margin = new Thickness(Math.Round((i * resolution * 4) * MainWindow.settings.horizZoom + barNumOffset, 2), 0, 0, 10);
            }

            foreach (Pattern pattern in patterns)
            {
                foreach (Occurrence occurrence in pattern.GetOccurrences())
                {
                    if (!occurrence.isNotesMode)
                    {
                        Border currentOccurrenceRect = (Border)occurrence.occurrenceRect.Children[0];
                        Canvas.SetLeft(currentOccurrenceRect, Math.Round(occurrence.GetStart() * MainWindow.settings.horizZoom, 2));
                        currentOccurrenceRect.Width = Math.Round((occurrence.GetEnd() - occurrence.GetStart()) * MainWindow.settings.horizZoom, 2);
                    }
                }
            }

            horizSnap = Math.Round((MainWindow.settings.horizZoom * resolution) / 8, 2);

            currentHorizZoom = zoomSetting;

            if (isPlaying)
            {
                MoveTracker();
            }

            if (currentHorizZoom < 0.6)
            {
                cnvGridLinesThirtySecondNote.Visibility = Visibility.Hidden;
            }
            else
            {
                cnvGridLinesThirtySecondNote.Visibility = MainWindow.settings.gridLines == true ? Visibility.Visible : Visibility.Hidden;
            }

            if (currentHorizZoom < 0.3)
            {
                cnvGridLinesSixteenthNote.Visibility = Visibility.Hidden;
            }
            else
            {
                cnvGridLinesSixteenthNote.Visibility = MainWindow.settings.gridLines == true ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void UpdateVertiZoom(double zoomSetting)
        {
            double oldZoom = currentVertiZoom;
            MainWindow.settings.vertiZoom = zoomSetting;
            double mousePosOnPianoRoll = Mouse.GetPosition(srlPianoScroll).Y;
            double scrollAnchorOffset = Math.Round(mousePosOnPianoRoll - (MainWindow.settings.vertiZoom / oldZoom) * mousePosOnPianoRoll, 2);

            srlPianoScroll.ScrollToVerticalOffset(Math.Round((MainWindow.settings.vertiZoom / oldZoom) * (srlPianoScroll.VerticalOffset) - scrollAnchorOffset, 2));

            this.Resources["CanvasHeight"] = Math.Round((MainWindow.settings.vertiZoom / oldZoom) * (double)this.Resources["CanvasHeight"], 2);
            this.Resources["PianoRollHeight"] = Math.Round((MainWindow.settings.vertiZoom / oldZoom) * (double)this.Resources["PianoRollHeight"], 2);

            if (MainWindow.settings.vertiZoom < 1)
            {
                grdKeyNames.Visibility = Visibility.Hidden;
                grdPianoBackground.Fill = Brushes.White;
            }
            else
            {
                grdKeyNames.Visibility = Visibility.Visible;
                grdPianoBackground.Fill = (Brush)(this.Resources["GridDarkColour"]);
            }

            vertiSnap = Math.Round(((double)this.Resources["PianoRollHeight"]) / grdNotes.RowDefinitions.Count, 2);
            currentVertiZoom = zoomSetting;
        }

        private void InitVertiZoom()
        {
            this.Resources["CanvasHeight"] = Math.Round((MainWindow.settings.vertiZoom) * (double)this.Resources["CanvasHeight"], 2);
            this.Resources["PianoRollHeight"] = Math.Round((MainWindow.settings.vertiZoom) * (double)this.Resources["PianoRollHeight"], 2);
            currentVertiZoom = MainWindow.settings.vertiZoom;

            if (MainWindow.settings.vertiZoom < 1)
            {
                grdKeyNames.Visibility = Visibility.Hidden;
                grdPianoBackground.Fill = Brushes.White;
            }
            else
            {
                grdKeyNames.Visibility = Visibility.Visible;
                grdPianoBackground.Fill = (Brush)(this.Resources["GridDarkColour"]);
            }

            vertiSnap = Math.Round(((double)this.Resources["PianoRollHeight"]) / grdNotes.RowDefinitions.Count, 2);
        }

        // Adds a bar to the piano roll to make space for MIDI notes.
        private void AddBar()
        {
            double barStart = grdNotes.Width;
            double thirtySecondNote = MainWindow.settings.horizZoom * (resolution / 8);
            double thickness;
            double barLength = MainWindow.settings.horizZoom * (resolution * noteParse.timeSig[0]);

            cnvPianoRoll.Width += barLength;
            this.Resources["PianoRollWidth"] = (double)this.Resources["PianoRollWidth"] + barLength;
            grdTimeline.Width += barLength;

            TextBlock barNumber = new TextBlock
            {
                FontSize = 12,
                Text = "" + (barNumbers.Count + 1),
                Margin = new Thickness(barStart + barNumOffset, 0, 0, 10),
                Height = 20
            };

            barNumbers.Add(barNumber);
            grdTimeline.Children.Add(barNumber);

            for (int j = 1; j <= noteParse.timeSig[0] * 8; j++)
            {
                Canvas parent = new Canvas();

                // Drawing the vertical grid lines with varying thickness.
                if (j % (noteParse.timeSig[0] * 8) == 0)                    {   thickness = 2.3; parent = cnvGridLinesBar;              }
                else if (j % 16 == 0 && ((noteParse.timeSig[0] % 4) == 0))  {   thickness = 1.1; parent = cnvGridLinesHalfBar;          }
                else if (j % 8 == 0)                                        {   thickness = 0.9; parent = cnvGridLinesQuarterNote;      }
                else if (j % 4 == 0)                                        {   thickness = 0.6; parent = cnvGridLinesEighthNote;       }
                else if (j % 2 == 0)                                        {   thickness = 0.4; parent = cnvGridLinesSixteenthNote;    }
                else                                                        {   thickness = 0.2; parent = cnvGridLinesThirtySecondNote; }

                Line gridLine = new Line
                {
                    Stroke = (Brush)this.Resources["GridLineColour"],
                    X1 = 0,
                    X2 = 0,
                    Y1 = 0,
                    Y2 = (double)this.Resources["PianoRollHeight"] * (1 / currentVertiZoom),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    StrokeThickness = thickness
                };

                Panel.SetZIndex(gridLine, 2);
                Canvas.SetLeft(gridLine, j * thirtySecondNote + barStart);
                Canvas.SetTop(gridLine, 7);

                gridLines.Add(gridLine);
                parent.Children.Add(gridLine);
            }
        }

        private void PopulateNotes(NoteParser noteParse)
        {
            for (int i = 0; i < noteParse.notes.Count; i++)
            {
                Note currentNote = noteParse.notes.ElementAt(i);

                while (Math.Round(grdNotes.Width / MainWindow.settings.horizZoom, 2) < currentNote.GetTime() + currentNote.GetDuration())
                {
                    AddBar();
                }

                Rectangle noteBar = new Rectangle
                {
                    Stroke = new SolidColorBrush(Colors.Black),
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(Colors.LightGray),
                    Width = Math.Round(currentNote.GetDuration() * MainWindow.settings.horizZoom, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(Math.Round(currentNote.GetTime() * MainWindow.settings.horizZoom, 2), 0, 0, 0),
                    Name = "note" + i
                };

                Dictionary<string, Rectangle> noteOutlines = new Dictionary<string, Rectangle>();

                Grid.SetRow(noteBar, PitchToRow((int)currentNote.GetPitch()));
                Panel.SetZIndex(noteBar, 3);
                noteBar.MouseDown += NoteRect_SelectNote;
                noteBar.MouseEnter += NoteRect_MouseIn;
                noteBar.MouseLeave += NoteRect_MouseOut;

                grdNotes.Children.Add(noteBar);

                NoteRect noteRect = new NoteRect();
                noteRect.note = currentNote;
                noteRect.noteBar = noteBar;
                noteRect.noteOutlines = noteOutlines;

                notes[0].Add(noteRect);
                notes[noteRect.note.GetChannel() + 1].Add(noteRect);
            }
        }

        // Places each snap's data into a quantised "bucket" where it can be compared for pattern similarity. The "start" and "end" parameters have to be unaffected by zoom.
        private List<List<NotePitch>> Quantise(double start, double end)
        {
            List<List<NotePitch>> quantisedSection = new List<List<NotePitch>>();

            double zoomRobustSnapLength = Math.Round(horizSnap / MainWindow.settings.horizZoom, 2);

            for (int i = 0; i < (end - start) / zoomRobustSnapLength; i++)
            {
                quantisedSection.Add(new List<NotePitch>());
            }

            for (int i = 0; i < notes[0].Count; i++)
            {
                double j = 0;
                NoteRect currentNote = notes[0][i];

                if (currentNote.note.GetTime() > end) break;

                if (currentNote.note.GetTime() + currentNote.note.GetDuration() >= start)
                {
                    int quantisedIndex = 0;

                    while (j < currentNote.note.GetDuration() && ((j + currentNote.note.GetTime() - start) / zoomRobustSnapLength) < quantisedSection.Count)
                    {
                        if (currentNote.note.GetTime() + j >= start)
                        {
                            quantisedIndex = (int)((j + currentNote.note.GetTime() - start) / zoomRobustSnapLength);
                            quantisedSection[quantisedIndex].Add(currentNote.note.GetPitch());
                        }

                        j += zoomRobustSnapLength;
                    }
                }
            }

            return quantisedSection;
        }

        private List<Occurrence> SimilarOccurrences(Occurrence occurrence)
        {
            List<Occurrence> similarOccurrences = new List<Occurrence>();
            List<List<NotePitch>> quantisedOccurrence = new List<List<NotePitch>>();
            List<List<int>> transposInvariantOccurrence = new List<List<int>>();
            List<List<int>> transposInvariantSubList = new List<List<int>>();
            double start = -1;
            double end = -1;
            int patternIndex = occurrence.occurrenceIcon.PatternNumOfOccurrence;

            if (!occurrence.isNotesMode)
            {
                quantisedOccurrence = Quantise(occurrence.GetStart(), occurrence.GetEnd());
                transposInvariantOccurrence = NormaliseQuantisation(quantisedOccurrence);

                for (int i = 0; i < quantisedSong.Count; i++)
                {
                    if ((quantisedSong.Count - i) >= transposInvariantOccurrence.Count)
                    {
                        transposInvariantSubList = NormaliseQuantisation(quantisedSong.GetRange(i, transposInvariantOccurrence.Count));

                        if (PatternMatch(transposInvariantOccurrence, transposInvariantSubList))
                        {
                            start = i * horizSnap;
                            end = (i + transposInvariantOccurrence.Count) * horizSnap;

                            double zoomRobustStart = Math.Round(start / MainWindow.settings.horizZoom, 2);
                            double zoomRobustEnd = Math.Round(end / MainWindow.settings.horizZoom, 2);

                            Occurrence newOccurrence = new Occurrence(zoomRobustStart, zoomRobustEnd);
                            newOccurrence.isNotesMode = false;
                            newOccurrence.isAutomatic = true;
                            newOccurrence.SetConfidence(3);

                            similarOccurrences.Add(newOccurrence);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            else
            {
                List<NoteRect> highlightedNotes = new List<NoteRect>();
                NoteRect note;
                bool found = false;

                for (int i = 0; i < notes[0].Count; i++)
                {
                    note = notes[0][i];
                    highlightedNotes = new List<NoteRect>();

                    if (note.note.GetDuration() == occurrence.highlightedNotes[0].note.GetDuration())
                    {
                        highlightedNotes.Add(note);

                        for (int j = 1; j < occurrence.highlightedNotes.Count; j++)
                        {
                            Note currentNote = occurrence.highlightedNotes[j].note;
                            Note previousNote = occurrence.highlightedNotes[j - 1].note;
                            List<NoteRect> nextNotes = GetAllNotesAtTime(note.note.GetTime() + currentNote.GetTime() - previousNote.GetTime());
                            found = false;

                            foreach (NoteRect nextNote in nextNotes)
                            {
                                if (((int)nextNote.note.GetPitch() - (int)note.note.GetPitch()) == ((int)currentNote.GetPitch() - (int)previousNote.GetPitch()) && (nextNote.note.GetDuration() == currentNote.GetDuration()))
                                {
                                    note = nextNote;
                                    highlightedNotes.Add(note);
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                break;
                            }
                            else
                            {
                                if (j == occurrence.highlightedNotes.Count - 1)
                                {
                                    Occurrence newOccurrence = new Occurrence();
                                    newOccurrence.highlightedNotes = highlightedNotes;
                                    newOccurrence = FindStartAndEnd(newOccurrence);
                                    newOccurrence.isNotesMode = true;
                                    newOccurrence.isAutomatic = true;
                                    newOccurrence.SetConfidence(3);

                                    similarOccurrences.Add(newOccurrence);
                                }
                            }
                        }
                    }
                }
            }

            return RemoveDuplicates(similarOccurrences, patternIndex);
        }

        private List<NoteRect> GetAllNotesAtTime(double time)
        {
            List<NoteRect> notesAtTime = new List<NoteRect>();

            foreach (NoteRect note in notes[0])
            {
                if (note.note.GetTime() == time)
                {
                    notesAtTime.Add(note);
                }
            }

            return notesAtTime;
        }

        private List<List<int>> NormaliseQuantisation(List<List<NotePitch>> quantisation)
        {
            List<List<int>> normalisedResult = new List<List<int>>();
            int baseValue = 0;
            bool foundFirstNote = false;

            for (int i = 0; i < quantisation.Count; i++)
            {
                normalisedResult.Add(new List<int>());

                for (int j = 0; j < quantisation[i].Count; j++)
                {
                    int currentNoteNum = (int)quantisation[i][j];

                    if (!foundFirstNote)
                    {
                        baseValue = currentNoteNum;
                        foundFirstNote = true;
                    }

                    normalisedResult[i].Add(currentNoteNum - baseValue);
                }
            }

            return normalisedResult;
        }

        private bool PatternMatch<T>(List<List<T>> occurrence, List<List<T>> subList)
        {
            if (occurrence.Count != subList.Count)
            {
                return false;
            }

            bool patternMatch = true;

            for (int i = 0; i < subList.Count; i++)
            {
                bool unitMatch = false;

                if ((subList[i].Count == 0) && (occurrence[i].Count == 0))
                {
                    unitMatch = true;
                }
                else
                {
                    unitMatch = true;

                    for (int j = 0; j < occurrence[i].Count; j++)
                    {
                        if (!subList[i].Contains(occurrence[i][j]))
                        {
                            unitMatch = false;
                            break;
                        }
                    }

                    for (int j = 0; j < subList[i].Count; j++)
                    {
                        if (!occurrence[i].Contains(subList[i][j]))
                        {
                            unitMatch = false;
                            break;
                        }
                    }
                }

                if (!unitMatch)
                {
                    patternMatch = false;
                    break;
                }
            }

            return patternMatch;
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
                        if (occurrence.isNotesMode && similarOccurrence.isNotesMode)
                        {
                            bool duplicateMatch = true;

                            if (occurrence.highlightedNotes.Count == similarOccurrence.highlightedNotes.Count)
                            {
                                List<NoteRect> tempNotes = new List<NoteRect>(similarOccurrence.highlightedNotes);

                                for (int i = 0; i < occurrence.highlightedNotes.Count; i++)
                                {
                                    for (int j = 0; j < tempNotes.Count; j++)
                                    {
                                        if (AreTwoNotesEqual(occurrence.highlightedNotes[i].note, tempNotes[j].note))
                                        {
                                            tempNotes.RemoveAt(j);
                                            break;
                                        }
                                    }

                                    //if (!AreTwoNotesEqual(occurrence.highlightedNotes[i].note, similarOccurrence.highlightedNotes[i].note))
                                    //{
                                    //    match = false;
                                    //}
                                }

                                if (tempNotes.Count != 0)
                                {
                                    duplicateMatch = false;
                                }
                            }
                            else
                            {
                                duplicateMatch = false;
                            }

                            if (duplicateMatch == true)
                            {
                                duplicateFound = true;
                            }
                        }
                        else if (!occurrence.isNotesMode && !similarOccurrence.isNotesMode)
                        {
                            if ((occurrence.GetStart() == similarOccurrence.GetStart()) && (occurrence.GetEnd() == similarOccurrence.GetEnd()))
                            {
                                duplicateFound = true;
                            }
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

        private bool AreTwoNotesEqual(Note note1, Note note2)
        {
            bool pitchesEqual = note1.GetPitch() == note2.GetPitch();
            bool timesEqual = note1.GetTime() == note2.GetTime();
            bool durationsEqual = note1.GetDuration() == note2.GetDuration();

            return pitchesEqual && timesEqual && durationsEqual;
        }

        private string ConcatenateList(List<String> stringList)
        {
            string result = "";

            for (int i = 0; i < stringList.Count; i++)
            {
                result += stringList[i] + "n";
            }

            return result;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            PlayMusic();

            if (logging) logs.Add(new Log() { logType = LogType.Play, time = sw.Elapsed, value = "n/a" });
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            PauseMusic();

            if (logging) logs.Add(new Log() { logType = LogType.Pause, time = sw.Elapsed, value = "n/a" });
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopMusic();

            if (logging) logs.Add(new Log() { logType = LogType.Stop, time = sw.Elapsed, value = "n/a" });
        }

        private void PlayMusic()
        {
            isPlaying = true;
            MuteCurrentNotes();

            if (!scheduler.IsRunning)
            {
                scheduler.Start();
                MoveTracker();
                HighlightNoteRects((linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX) / MainWindow.settings.horizZoom, grdNotes.Width / MainWindow.settings.horizZoom);
            }
            else
            {
                scheduler.Stop();
                scheduler.Reset();
                ScheduleNotes();
                CancelNoteRectHighlights();
                scheduler.Start();
                UpdateTrackerPosition(0);
                MoveTracker();
                HighlightNoteRects((linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX) / MainWindow.settings.horizZoom, grdNotes.Width / MainWindow.settings.horizZoom);
            }
        }

        private void PauseMusic()
        {
            isPlaying = false;

            if (scheduler.IsRunning)
            {
                scheduler.Stop();
                PauseTracker();
                MuteCurrentNotes();
                CancelNoteRectHighlights();
            }
        }

        private void StopMusic()
        {
            isPlaying = false;
            UpdateTrackerPosition(0);

            if (scheduler.IsRunning)
            {
                MuteCurrentNotes();
                scheduler.Stop();
                scheduler.Reset();
                ScheduleNotes();
                CancelNoteRectHighlights();
            }
            else
            {
                scheduler.Reset();
                ScheduleNotes();
            }
        }

        private void RefreshMusic()
        {
            if (scheduler.IsRunning)
            {
                scheduler.Stop();
                scheduler.Reset();
                ScheduleNotes(Math.Round((linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX) / MainWindow.settings.horizZoom, 2), Math.Round(grdNotes.Width / MainWindow.settings.horizZoom, 2));
                scheduler.Start();
            }
        }

        private void MoveTracker()
        {
            txtTrackerTriangle.RenderTransform = trackerTransform;
            linTrackerLine.RenderTransform = trackerTransform;

            double distance = cnvMouseLayer.Width - linTrackerLine.Margin.Left;
            TimeSpan duration = TimeSpan.FromSeconds(Math.Round(((60 / noteParse.bpm) * ((grdNotes.Width - (linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX)) / resolution)) / MainWindow.settings.horizZoom, 2));

            DoubleAnimation horizAnim = new DoubleAnimation { By = distance, Duration = duration };

            trackerTransform.BeginAnimation(TranslateTransform.XProperty, horizAnim);
        }

        private void UpdateTrackerPosition(double pos)
        {
            if (pos <= grdNotes.Width)
            {
                txtTrackerTriangle.Margin = new Thickness(pos, txtTrackerTriangle.Margin.Top, txtTrackerTriangle.Margin.Right, txtTrackerTriangle.Margin.Bottom);
                linTrackerLine.Margin = new Thickness(pos, linTrackerLine.Margin.Top, linTrackerLine.Margin.Right, linTrackerLine.Margin.Bottom);
                trackerTransform = new TranslateTransform();
                txtTrackerTriangle.RenderTransform = trackerTransform;
                linTrackerLine.RenderTransform = trackerTransform;

                trackerTransform.BeginAnimation(TranslateTransform.XProperty, null);
            }
        }

        private void PauseTracker()
        {
            txtTrackerTriangle.Margin = new Thickness(txtTrackerTriangle.Margin.Left + txtTrackerTriangle.RenderTransform.Value.OffsetX, txtTrackerTriangle.Margin.Top, txtTrackerTriangle.Margin.Right, txtTrackerTriangle.Margin.Bottom);
            linTrackerLine.Margin = new Thickness(linTrackerLine.Margin.Left + linTrackerLine.RenderTransform.Value.OffsetX, linTrackerLine.Margin.Top, linTrackerLine.Margin.Right, linTrackerLine.Margin.Bottom);
            trackerTransform = new TranslateTransform();
            txtTrackerTriangle.RenderTransform = trackerTransform;
            linTrackerLine.RenderTransform = trackerTransform;
        }

        private void AddPattern_Click(object sender, RoutedEventArgs e)
        {
            MoveElement(btnAddPattern, AddPattern());
        }

        private void ChannelSelect_SelectChannel(object sender, RoutedEventArgs e)
        {
            int oldChannel = currentChannel;
            currentChannel = ((ComboBox)sender).SelectedIndex;

            if (oldChannel != 0 && currentChannel == 0)
            {
                for (int i = 1; i < oldChannel; i++)
                {
                    ShowNotesInChannel(i);
                }

                for (int i = oldChannel + 1; i < notes.Length; i++)
                {
                    ShowNotesInChannel(i);
                }
            }
            else if (oldChannel == 0 && currentChannel != 0)
            {
                for (int i = 1; i < currentChannel; i++)
                {
                    HideNotesInChannel(i);
                }

                for (int i = currentChannel + 1; i < notes.Length; i++)
                {
                    HideNotesInChannel(i);
                }
            }
            else if (oldChannel != 0 && currentChannel != 0)
            {
                HideNotesInChannel(oldChannel);
                ShowNotesInChannel(currentChannel);
            }

            if (scheduler != null)
            {
                RefreshMusic();
            }

            if (logging) logs.Add(new Log() { logType = LogType.ChannelUpdate, time = sw.Elapsed, value = "Channel: " + currentChannel });
        }

        private void HideNotesInChannel(int channelIndex)
        {
            foreach (NoteRect noteRect in notes[channelIndex])
            {
                noteRect.noteBar.Visibility = Visibility.Hidden;
            }
        }

        private void ShowNotesInChannel(int channelIndex)
        {
            foreach (NoteRect noteRect in notes[channelIndex])
            {
                noteRect.noteBar.Visibility = Visibility.Visible;
            }
        }

        private double AddPattern()
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
            newPatternButton.Click += PatternIcon_Click;
            newPatternButton.DeleteClick += PatternIcon_DeleteClick;
            newPatternButton.ViewToggle += PatternIcon_ViewToggle;
            newPatternButton.ViewSolo += PatternIcon_ViewSolo;
            newPatternButton.CollExpToggle += PatternIcon_CollExpToggle;

            newPattern.patternIcon = newPatternButton;
            patterns.Add(newPattern);
            itmPatternsView.Items.Add(newPatternButton);

            if (logging) logs.Add(new Log() { logType = LogType.PatternAdd, time = sw.Elapsed, value = "PatternNum: " + patterns.Count });

            return newPattern.patternIcon.Height;
        }

        private Occurrence AddOccurrence(int patternIndex, Canvas occurrenceRect)
        {
            Occurrence newOccurrence = new Occurrence();

            patterns[patternIndex].AddOccurrence(newOccurrence);
            newOccurrence.occurrenceIcon = CreateOccurrenceIcon(patternIndex);
            newOccurrence.occurrenceRect = occurrenceRect;
            newOccurrence.isNotesMode = false;
            newOccurrence.SetConfidence(3);

            Border currentPatternRect = (Border)(newOccurrence.occurrenceRect.Children[0]);
            newOccurrence.SetStart(Math.Round(Canvas.GetLeft(currentPatternRect) / MainWindow.settings.horizZoom, 2));
            newOccurrence.SetEnd(Math.Round((Canvas.GetLeft(currentPatternRect) + currentPatternRect.Width) / MainWindow.settings.horizZoom, 2));

            if (logging) logs.Add(new Log() { logType = LogType.OccurrenceAdd, time = sw.Elapsed, value = "PatternNum: " + patternIndex + ", OccurrenceNum: " + newOccurrence.occurrenceIcon.OccurrenceNum });

            return newOccurrence;
        }

        private Occurrence AddOccurrence(int patternIndex, List<NoteRect> highlightedNotes)
        {
            Occurrence newOccurrence = new Occurrence();

            patterns[patternIndex].AddOccurrence(newOccurrence);
            newOccurrence.occurrenceIcon = CreateOccurrenceIcon(patternIndex);
            newOccurrence.highlightedNotes = highlightedNotes;
            newOccurrence = FindStartAndEnd(newOccurrence);
            newOccurrence.isNotesMode = true;
            newOccurrence.SetConfidence(3);

            if (logging) logs.Add(new Log() { logType = LogType.OccurrenceAdd, time = sw.Elapsed, value = "PatternNum: " + patternIndex + ", OccurrenceNum: " + newOccurrence.occurrenceIcon.OccurrenceNum });

            return newOccurrence;
        }

        private Occurrence FindStartAndEnd(Occurrence occurrence)
        {
            double minTime = Math.Round(grdNotes.Width / MainWindow.settings.horizZoom, 2);
            double maxTime = 0;

            foreach (NoteRect noteRect in occurrence.highlightedNotes)
            {
                double currentStart = noteRect.note.GetTime();
                double currentEnd = noteRect.note.GetTime() + noteRect.note.GetDuration();

                if (minTime > currentStart)
                {
                    minTime = currentStart;
                }

                if (maxTime < currentEnd)
                {
                    maxTime = currentEnd;
                }
            }

            occurrence.SetStart(minTime);
            occurrence.SetEnd(maxTime);

            return occurrence;
        }

        private OccurrenceIcon CreateOccurrenceIcon(int patternIndex)
        {
            OccurrenceIcon occurrenceIcon = new OccurrenceIcon
            {
                Name = "occurrenceIcon" + patternIndex + "n" + patterns[patternIndex].GetOccurrences().Count,
                Background = patterns[patternIndex].patternIcon.Background,
                OccurrenceText = "" + (patterns[patternIndex].GetOccurrences().Count),
                Height = occurrenceIconHeight,
                PatternNumOfOccurrence = patternIndex,
                OccurrenceNum = patterns[patternIndex].GetOccurrences().Count - 1,
                AutomaticIcon = Visibility.Hidden
            };

            if (!patterns[patternIndex].patternIcon.CollExp)
            {
                occurrenceIcon.Visibility = Visibility.Collapsed;
            }

            occurrenceIcon.UpdateConfidenceGroupName();

            // Event handlers for the different actions of an occurrence icon.
            occurrenceIcon.DeleteClick += OccurrenceIcon_DeleteClick;
            occurrenceIcon.MouseIn += OccurrenceIcon_MouseIn;
            occurrenceIcon.MouseOut += OccurrenceIcon_MouseOut;
            occurrenceIcon.MouseLeftClick += OccurrenceIcon_MouseLeftClick;
            occurrenceIcon.FindSimilar += OccurrenceIcon_FindSimilar;
            occurrenceIcon.ConfidenceChange += OccurrenceIcon_ConfidenceChange;

            return occurrenceIcon;
        }

        private OccurrenceIcon CreateOccurrenceInProgress(int patternIndex)
        {
            OccurrenceIcon occurrenceIcon = new OccurrenceIcon
            {
                Name = "occurrenceIconInProgress" + patternIndex + "n" + patterns[patternIndex].GetOccurrences().Count,
                Background = patterns[patternIndex].patternIcon.Background,
                OccurrenceText = "Confirm",
                Height = occurrenceIconHeight,
                PatternNumOfOccurrence = patternIndex,
                OccurrenceNum = patterns[patternIndex].GetOccurrences().Count,
                Visibility = Visibility.Visible,
                AutomaticIcon = Visibility.Hidden
            };

            currentOccurrence.occurrenceIcon = occurrenceIcon;
            currentOccurrence.isNotesMode = true;
            AddOccurrenceIconInProgress(occurrenceIcon);
            MoveElement(btnAddPattern, occurrenceIconHeight);

            for (int i = 0; i < patterns.Count; i++)
            {
                HideOccurrenceVisuals(i);
                patterns[i].patternIcon.DisableButtons();

                foreach (Occurrence occurrence in patterns[i].GetOccurrences())
                {
                    occurrence.occurrenceIcon.DisableButtons();
                }
            }

            btnAddPattern.IsEnabled = false;

            // Event handlers for the different actions of an occurrence icon.
            occurrenceIcon.DeleteClick += OccurrenceIconInProgress_DeleteClick;
            occurrenceIcon.MouseLeftClick += OccurrenceIconInProgress_MouseLeftClick;
            occurrenceIcon.MouseEnter += OccurrenceIconInProgress_MouseIn;
            occurrenceIcon.MouseLeave += OccurrenceIconInProgress_MouseOut;
            occurrenceIcon.ContextMenu.Visibility = Visibility.Collapsed;

            return occurrenceIcon;
        }

        private void OccurrenceIconInProgress_DeleteClick(object sender, EventArgs e)
        {
            CancelOccurrenceCreation();
        }

        private void OccurrenceIconInProgress_MouseLeftClick(object sender, EventArgs e)
        {
            int patternIndex = ((OccurrenceIcon)sender).PatternNumOfOccurrence;
            ConfirmOccurrenceCreation(patternIndex);
        }

        private void ConfirmOccurrenceCreation(int patternIndex)
        {
            isSelectingOccurrence = false;

            if (currentOccurrence.highlightedNotes.Count > 0)
            {
                Occurrence newOccurrence = AddOccurrence(patternIndex, currentOccurrence.highlightedNotes);
                newOccurrence.occurrenceIcon = CreateOccurrenceIcon(patternIndex);
                AddOccurrenceIcon(newOccurrence.occurrenceIcon);

                if (!patterns[patternIndex].patternIcon.CollExp)
                {
                    MoveElement(btnAddPattern, (-1) * occurrenceIconHeight);
                }

                if (!patterns[patternIndex].patternIcon.View)
                {
                    patterns[patternIndex].patternIcon.View = true;
                }
            }
            else
            {
                MoveElement(btnAddPattern, (-1) * occurrenceIconHeight);
            }

            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i].patternIcon.View)
                {
                    ShowOccurrenceVisuals(i);
                }
            }

            DeleteOccurrenceInProgress();
        }

        private void CancelOccurrenceCreation()
        {
            if (!isUIMoving)
            {
                RemoveNoteHighlights(currentOccurrence.highlightedNotes, currentPattern, patterns[currentPattern].GetOccurrences().Count);
                DeleteOccurrenceInProgress();
                MoveElement(btnAddPattern, (-1) * occurrenceIconHeight);
                isSelectingOccurrence = false;

                for (int i = 0; i < patterns.Count; i++)
                {
                    ShowOccurrenceVisuals(i);
                }
            }
        }

        private Canvas CreateOccurrenceRect(int patternIndex)
        {
            SolidColorBrush occurrenceRectColour = new SolidColorBrush(((SolidColorBrush)(patterns[patternIndex].patternIcon.Background)).Color)
            {
                Opacity = 0.3
            };

            Canvas patternCanvas = new Canvas
            {
                Name = "dragSelectionCanvas" + patternIndex + "n" + patterns[patternIndex].GetOccurrences().Count,
                Visibility = Visibility.Visible,
                Width = 0
            };

            Border patternBorder = new Border
            {
                Name = "dragSelectionBorder" + patternIndex + "n" + patterns[patternIndex].GetOccurrences().Count,
                Background = occurrenceRectColour,
                CornerRadius = new CornerRadius(1),
                BorderBrush = patterns[patternIndex].patternIcon.Background
            };

            patternCanvas.Children.Add(patternBorder);

            return patternCanvas;
        }

        private void AddOccurrenceIcon(OccurrenceIcon occurrenceIcon)
        {
            // Figuring out which index to slot the new occurrence in.
            int patternIndex = occurrenceIcon.PatternNumOfOccurrence;
            int finalIndex = patternIndex;

            for (int i = 0; i <= patternIndex; i++)
            {
                finalIndex += patterns[i].GetOccurrences().Count;
            }

            itmPatternsView.Items.Insert(finalIndex, occurrenceIcon);
        }

        private void AddOccurrenceIconInProgress(OccurrenceIcon occurrenceIcon)
        {
            // Figuring out which index to slot the new occurrence in.
            int patternIndex = occurrenceIcon.PatternNumOfOccurrence;
            int finalIndex = patternIndex;

            for (int i = 0; i <= patternIndex; i++)
            {
                finalIndex += patterns[i].GetOccurrences().Count;
            }

            finalIndex++;
            itmPatternsView.Items.Insert(finalIndex, occurrenceIcon);
        }

        private double AddOccurrenceGraphics(List<Occurrence> occurrences, int patternIndex)
        {
            double animMove = 0;

            foreach (Occurrence occurrence in occurrences)
            {
                patterns[patternIndex].AddOccurrence(occurrence);
                occurrence.occurrenceIcon = CreateOccurrenceIcon(patternIndex);
                occurrence.occurrenceIcon.AutomaticIcon = occurrence.isAutomatic && MainWindow.settings.automaticIcons ? Visibility.Visible : Visibility.Hidden;

                if (occurrence.isNotesMode)
                {
                    HighlightNotes(occurrence.highlightedNotes, patternIndex, occurrence.occurrenceIcon.OccurrenceNum);
                }
                else
                {
                    Canvas occurrenceRect = CreateOccurrenceRect(patternIndex);
                    Border rectBorder = (Border)occurrenceRect.Children[0];

                    Canvas.SetLeft(rectBorder, occurrence.GetStart() * MainWindow.settings.horizZoom);
                    Canvas.SetTop(rectBorder, 0);
                    rectBorder.Width = (occurrence.GetEnd() - occurrence.GetStart()) * MainWindow.settings.horizZoom;
                    rectBorder.Height = cnvMouseLayer.Height;

                    cnvMouseLayer.Children.Add(occurrenceRect);
                    occurrence.occurrenceRect = occurrenceRect;
                }
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

            return animMove;
        }

        private void PatternIcon_DeleteClick(object sender, EventArgs e)
        {
            if (!isUIMoving)
            {
                int patternIndex = ((PatternIcon)sender).PatternNum;

                MoveElement(btnAddPattern, DeletePattern(patternIndex));
            }
        }

        private void PatternIcon_ViewToggle(object sender, EventArgs e)
        {
            int patternIndex = ((PatternIcon)sender).PatternNum;

            if (patterns[patternIndex].patternIcon.View)
            {
                ShowOccurrenceVisuals(patternIndex);
            }
            else
            {
                HideOccurrenceVisuals(patternIndex);
            }

            if (currentSolo != -1)
            {
                currentSolo = -1;
            }

            if (logging) logs.Add(new Log() { logType = LogType.PatternViewUpdate, time = sw.Elapsed, value = "PatternNum: " + patternIndex });
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

            if (logging) logs.Add(new Log() { logType = LogType.PatternSoloUpdate, time = sw.Elapsed, value = "PatternNum: " + patternIndex });
        }

        private void PatternIcon_CollExpToggle(object sender, EventArgs e)
        {
            int patternIndex = ((PatternIcon)sender).PatternNum;

            if (patterns[patternIndex].patternIcon.CollExp)
            {
                ExpandOccurrences(patternIndex);

                if (logging) logs.Add(new Log() { logType = LogType.PatternExpand, time = sw.Elapsed, value = "PatternNum: " + patternIndex });
            }
            else
            {
                CollapseOccurrences(patternIndex);

                if (logging) logs.Add(new Log() { logType = LogType.PatternCollapse, time = sw.Elapsed, value = "PatternNum: " + patternIndex });
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
            Cursor = Cursors.Hand;

            if (patterns[((OccurrenceIcon)sender).PatternNumOfOccurrence].GetOccurrences().Count > 0)
            {
                Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);
                HighlightOccurrence(occurrence);
            }
        }

        private void OccurrenceIcon_MouseOut(object sender, EventArgs e)
        {
            Cursor = Cursors.Arrow;

            if (patterns[((OccurrenceIcon)sender).PatternNumOfOccurrence].GetOccurrences().Count > ((OccurrenceIcon)sender).OccurrenceNum)
            {
                Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);
                NormaliseOccurrence(occurrence);
            }
        }

        private void OccurrenceIconInProgress_MouseIn(object sender, EventArgs e)
        {
            Cursor = Cursors.Hand;
        }

        private void OccurrenceIconInProgress_MouseOut(object sender, EventArgs e)
        {
            Cursor = Cursors.Arrow;
        }

        private void OccurrenceIcon_MouseLeftClick(object sender, EventArgs e)
        {
            Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);

            srlPianoScroll.ScrollToHorizontalOffset(Math.Round((((occurrence.GetEnd() + occurrence.GetStart()) * MainWindow.settings.horizZoom - srlPianoScroll.ViewportWidth + grdPiano.Width) / 2), 2));
        }

        private void OccurrenceIcon_FindSimilar(object sender, EventArgs e)
        {
            Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);

            List<Occurrence> similarOccurrences = SimilarOccurrences(occurrence);
            MoveElement(btnAddPattern, AddOccurrenceGraphics(similarOccurrences, ((OccurrenceIcon)sender).PatternNumOfOccurrence));

            if (logging) logs.Add(new Log() { logType = LogType.OccurrenceFindSimilar, time = sw.Elapsed, value = "PatternNum: " + occurrence.occurrenceIcon.PatternNumOfOccurrence + ", OccurrenceNum: " + occurrence.occurrenceIcon.OccurrenceNum });
        }

        private void OccurrenceIcon_ConfidenceChange(object sender, EventArgs e)
        {
            string confidenceMenuChoice = ((MenuItem)(((RoutedEventArgs)e).Source)).Header.ToString();
            Occurrence occurrence = GetOccurrence((OccurrenceIcon)sender);

            occurrence.SetConfidence(Int32.Parse(confidenceMenuChoice));

            if (logging) logs.Add(new Log() { logType = LogType.OccurrenceConfidenceUpdate, time = sw.Elapsed, value = "PatternNum: " + occurrence.occurrenceIcon.PatternNumOfOccurrence + ", OccurrenceNum: " + occurrence.occurrenceIcon.OccurrenceNum + ", Confidence: " + Int32.Parse(confidenceMenuChoice) });
        }

        private Occurrence GetOccurrence(OccurrenceIcon icon)
        {
            return patterns[icon.PatternNumOfOccurrence].GetOccurrences()[icon.OccurrenceNum];
        }

        private void HighlightOccurrence(Occurrence occurrence)
        {
            if (!occurrence.isNotesMode)
            {
                ((Border)occurrence.occurrenceRect.Children[0]).BorderThickness = new Thickness(4);
            }
            else
            {
                foreach (NoteRect noteRect in occurrence.highlightedNotes)
                {
                    if (noteRect.noteOutlines.ContainsKey(occurrence.occurrenceIcon.PatternNumOfOccurrence + "n" + occurrence.occurrenceIcon.OccurrenceNum))
                    {
                        Rectangle noteOutline = noteRect.noteOutlines[occurrence.occurrenceIcon.PatternNumOfOccurrence + "n" + occurrence.occurrenceIcon.OccurrenceNum];
                        noteOutline.Fill = patterns[occurrence.occurrenceIcon.PatternNumOfOccurrence].patternIcon.Background;
                        noteOutline.Stroke = Brushes.Black;
                        noteOutline.StrokeThickness = 1;
                    }
                }
            }
        }

        private void NormaliseOccurrence(Occurrence occurrence)
        {
            if (!occurrence.isNotesMode)
            {
                ((Border)occurrence.occurrenceRect.Children[0]).BorderThickness = new Thickness(0);
            }
            else
            {
                foreach (NoteRect noteRect in occurrence.highlightedNotes)
                {
                    if (noteRect.noteOutlines.ContainsKey(occurrence.occurrenceIcon.PatternNumOfOccurrence + "n" + occurrence.occurrenceIcon.OccurrenceNum))
                    {
                        Rectangle noteOutline = noteRect.noteOutlines[occurrence.occurrenceIcon.PatternNumOfOccurrence + "n" + occurrence.occurrenceIcon.OccurrenceNum];
                        noteOutline.Stroke = patterns[occurrence.occurrenceIcon.PatternNumOfOccurrence].patternIcon.Background;
                        noteOutline.Fill = Brushes.Transparent;
                        noteOutline.StrokeThickness = 2;
                    }
                }
            }
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

        private void ClearPatterns()
        {
            if (isSelectingOccurrence)
            {
                DeleteOccurrenceInProgress();
                isSelectingOccurrence = false;
            }

            for (int i = patterns.Count - 1; i >= 0; i--)
            {
                DeletePattern(i);
            }

            btnAddPattern.Margin = new Thickness(0, 0, 0, 0);
        }

        private void ImportPatterns(List<Pattern> patternsIn)
        {
            double animMove = 0;

            for (int i = 0; i < patternsIn.Count; i++)
            {
                animMove += AddPattern();
                List<Occurrence> patternOccurrences = new List<Occurrence>();

                for (int j = 0; j < patternsIn[i].GetOccurrences().Count; j++)
                {
                    Occurrence currentOccurrence = patternsIn[i].GetOccurrences()[j];

                    if (currentOccurrence.highlightedNotes.Count > 0)
                    {
                        List<NoteRect> highlightedNotes = new List<NoteRect>();

                        foreach (NoteRect highlightedNote in currentOccurrence.highlightedNotes)
                        {
                            foreach (NoteRect noteRect in notes[0])
                            {
                                if (AreTwoNotesEqual(noteRect.note, highlightedNote.note))
                                {
                                    highlightedNotes.Add(noteRect);
                                }
                            }
                        }

                        if (highlightedNotes.Count > 0)
                        {
                            Occurrence newOccurrence = new Occurrence();
                            newOccurrence.highlightedNotes = highlightedNotes;
                            newOccurrence = FindStartAndEnd(newOccurrence);
                            newOccurrence.isNotesMode = true;
                            newOccurrence.isAutomatic = false;
                            newOccurrence.SetConfidence(3);
                            patternOccurrences.Add(newOccurrence);
                        } 
                    }
                    else
                    {
                        if (currentOccurrence.GetStart() > 0 && currentOccurrence.GetEnd() < Math.Round((double)this.Resources["PianoRollWidth"] / MainWindow.settings.horizZoom, 2))
                        {
                            Occurrence newOccurrence = new Occurrence(currentOccurrence.GetStart(), currentOccurrence.GetEnd());
                            newOccurrence.isNotesMode = false;
                            newOccurrence.isAutomatic = false;
                            newOccurrence.SetConfidence(3);
                            patternOccurrences.Add(newOccurrence);
                        }
                    }
                }

                animMove += AddOccurrenceGraphics(patternOccurrences, i);
            }

            MoveElement(btnAddPattern, animMove);
        }

        private double DeletePattern(int patternIndex)
        {
            double animMove = 0;

            currentPattern = patternIndex == currentPattern ? -1 : currentPattern;

            if (patterns[patternIndex].patternIcon.CollExp)
            {
                animMove -= occurrenceIconHeight * (patterns[patternIndex].GetOccurrences().Count);
            }

            /*foreach (NoteRect noteRect in notes[0])
            {
                foreach (Occurrence occurrence in patterns[patternIndex].GetOccurrences())
                {
                    noteRect.noteOutlines.Remove(patternIndex + "n" + occurrence.occurrenceIcon.OccurrenceNum);
                }
            }*/

            for (int i = patterns[patternIndex].GetOccurrences().Count - 1; i >= 0; i--)
            {
                DeleteOccurrence(i, patternIndex);
            }

            animMove -= patterns[patternIndex].patternIcon.Height;
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

            if (logging) logs.Add(new Log() { logType = LogType.PatternDelete, time = sw.Elapsed, value = "PatternNum: " + patternIndex });

            return animMove;
        }

        private void DeleteOccurrence(int occurrenceIndex, int patternIndex)
        {
            Occurrence occurrenceToRemove = patterns[patternIndex].GetOccurrences()[occurrenceIndex];

            if (!occurrenceToRemove.isNotesMode)
            {
                cnvMouseLayer.Children.Remove(occurrenceToRemove.occurrenceRect);
            }
            else
            {
                NormaliseOccurrence(occurrenceToRemove);
                RemoveNoteHighlights(occurrenceToRemove.highlightedNotes, patternIndex, occurrenceIndex);
            }
            
            patterns[patternIndex].GetOccurrences().RemoveAt(occurrenceIndex);
            int indextoRemove = GetPatternIndexInItemsList(patternIndex) + occurrenceIndex + 1;
            itmPatternsView.Items.RemoveAt(indextoRemove);

            foreach (Occurrence occurrence in patterns[patternIndex].GetOccurrences().GetRange(occurrenceIndex, patterns[patternIndex].GetOccurrences().Count - occurrenceIndex))
            {
                int newIndex = occurrence.occurrenceIcon.OccurrenceNum - 1;

                occurrence.occurrenceIcon.Name = "occurrenceIcon" + patternIndex + "n" + occurrence.occurrenceIcon.OccurrenceNum;
                occurrence.occurrenceIcon.OccurrenceText = "" + (newIndex + 1);
                occurrence.occurrenceIcon.OccurrenceNum--;
                occurrence.occurrenceIcon.UpdateConfidenceGroupName();

                foreach (NoteRect noteRect in occurrence.highlightedNotes)
                {
                    noteRect.noteOutlines.Add(patternIndex + "n" + newIndex, noteRect.noteOutlines[patternIndex + "n" + (newIndex + 1)]);
                    noteRect.noteOutlines.Remove(patternIndex + "n" + (newIndex + 1));
                }
            }

            if (logging) logs.Add(new Log() { logType = LogType.OccurrenceFindSimilar, time = sw.Elapsed, value = "PatternNum: " + patternIndex + ", OccurrenceNum: " + occurrenceIndex });
        }

        private void DeleteOccurrenceInProgress()
        {
            itmPatternsView.Items.Remove(currentOccurrence.occurrenceIcon);
            currentOccurrence = new Occurrence();

            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.EnableButtons();

                for (int j = 0; j < ((i == currentPattern) ? patterns[i].GetOccurrences().Count - 1 : patterns[i].GetOccurrences().Count); j++)
                {
                    patterns[i].GetOccurrences()[j].occurrenceIcon.EnableButtons();
                }
            }

            btnAddPattern.IsEnabled = true;
        }

        private void ShowOccurrenceVisuals(int patternIndex)
        {
            foreach (Occurrence occurrence in patterns[patternIndex].GetOccurrences())
            {
                if (!occurrence.isNotesMode)
                {
                    occurrence.occurrenceRect.Visibility = Visibility.Visible;
                }
                else
                {
                    HighlightNotes(occurrence.highlightedNotes, patternIndex, occurrence.occurrenceIcon.OccurrenceNum);
                }
            }
        }

        private void HideOccurrenceVisuals(int patternIndex)
        {
            foreach (Occurrence occurrence in patterns[patternIndex].GetOccurrences())
            {
                if (!occurrence.isNotesMode)
                {
                    occurrence.occurrenceRect.Visibility = Visibility.Hidden;
                }
                else
                {
                    foreach (NoteRect noteRect in occurrence.highlightedNotes)
                    {
                        if (noteRect.noteOutlines.ContainsKey(patternIndex + "n" + occurrence.occurrenceIcon.OccurrenceNum))
                        {
                            noteRect.noteOutlines[patternIndex + "n" + occurrence.occurrenceIcon.OccurrenceNum].Visibility = Visibility.Hidden;
                        }
                    }
                }
            }
        }

        private void SoloViewForPattern(int patternIndex)
        {
            for (int i = 0; i < patternIndex; i++)
            {
                patterns[i].patternIcon.View = false;
                HideOccurrenceVisuals(i);
            }

            patterns[patternIndex].patternIcon.View = true;
            ShowOccurrenceVisuals(patternIndex);

            for (int i = patternIndex + 1; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = false;
                HideOccurrenceVisuals(i);
            }
        }

        private void RestoreViews()
        {
            for (int i = 0; i < patterns.Count; i++)
            {
                patterns[i].patternIcon.View = true;
                ShowOccurrenceVisuals(i);
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

        private void PatternIcon_Click(object sender, RoutedEventArgs e)
        {
            if (isSelectingOccurrence)
            {
                CancelOccurrenceCreation();
            }

            SelectPattern(((PatternIcon)sender).PatternNum);
        }

        private void SelectPattern(int patternIndex)
        {
            if (patternIndex < patterns.Count && patterns[patternIndex] != null)
            {
                for (int i = 0; i < patterns.Count; i++)
                {
                    patterns[i].patternIcon.IsChecked = false;
                }

                patterns[patternIndex].patternIcon.IsChecked = true;
                currentPattern = patternIndex;
            }
        }

        private void Timeline_MouseDown(object sender, MouseEventArgs e)
        {
            Point mouseDown = GetCurrentMousePosition(e);
            UpdateTrackerPosition(mouseDown.X);
            isLeftMouseButtonDownOnTimeline = true;
            Mouse.Capture(grdTimeline);
        }

        private void Timeline_MouseMove(object sender, MouseEventArgs e)
        {
            if (isLeftMouseButtonDownOnTimeline)
            {
                Point mouseDown = GetCurrentMousePosition(e);
                UpdateTrackerPosition(mouseDown.X);
                Cursor = Cursors.None;
            }
        }

        private void Timeline_MouseUp(object sender, MouseEventArgs e)
        {
            Point mouseDown = GetCurrentMousePosition(e);
            isLeftMouseButtonDownOnTimeline = false;

            double start = Math.Round(mouseDown.X / MainWindow.settings.horizZoom, 2);
            double end = Math.Round(grdNotes.Width / MainWindow.settings.horizZoom, 2);

            if (scheduler.IsRunning)
            {
                MuteCurrentNotes();
                scheduler.Stop();
                scheduler.Reset();
                ScheduleNotes(start, end);
                scheduler.Start();
                CancelNoteRectHighlights();
                HighlightNoteRects(start, end);
                MoveTracker();
            }
            else
            {
                scheduler.Reset();
                ScheduleNotes(start, end);
            }

            Mouse.Capture(null);

            if (grdTimeline.IsMouseOver)
            {
                Cursor = Cursors.ScrollWE;
            }
            else
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void Timeline_MouseIn(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.ScrollWE;
        }

        private void Timeline_MouseOut(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.Arrow;
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
                Mouse.Capture(srlPianoScroll);
                Cursor = Cursors.ScrollAll;
            }
        }

        private void PianoScroll_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
            {
                isDraggingScroll = false;
                Mouse.Capture(null);
                Cursor = Cursors.Arrow;
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
            else if (isDraggingPatternRect)
            {
                var sv = sender as ScrollViewer;

                if (e.GetPosition(sv).X > (sv.ViewportWidth - moveScrollThreshold))
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset + moveScrollSpeed);
                }
                else if (e.GetPosition(sv).X < (moveScrollThreshold))
                {
                    sv.ScrollToHorizontalOffset(sv.HorizontalOffset - moveScrollSpeed);
                }

                if (e.GetPosition(sv).Y > (sv.ViewportHeight - moveScrollThreshold))
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset + moveScrollSpeed);
                }
                else if (e.GetPosition(sv).Y < (moveScrollThreshold))
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - moveScrollSpeed);
                }
            }
        }

        private void PianoScroll_MouseEnter(object sender, MouseEventArgs e)
        {
            srlPianoScroll.Focus();
        }

        private void PianoScroll_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;

                double newZoom = 1;

                if (e.GetPosition(srlPianoScroll).X < grdPiano.Width)
                {
                    if (e.Delta > 0)
                    {
                        newZoom = MainWindow.settings.vertiZoom >= 1 ? 1 : Math.Round(MainWindow.settings.vertiZoom + 0.05, 2);
                    }
                    else if (e.Delta < 0)
                    {
                        newZoom = MainWindow.settings.vertiZoom <= 0.5 ? 0.5 : Math.Round(MainWindow.settings.vertiZoom - 0.05, 2);
                    }

                    UpdateVertiZoom(newZoom);
                }
                else
                {
                    if (e.Delta > 0)
                    {
                        newZoom = MainWindow.settings.horizZoom >= 2 ? 2 : Math.Round(MainWindow.settings.horizZoom + 0.05, 2);
                    }
                    else if (e.Delta < 0)
                    {
                        newZoom = MainWindow.settings.horizZoom <= 0.1 ? 0.1 : Math.Round(MainWindow.settings.horizZoom - 0.05, 2);
                    }

                    UpdateHorizZoom(newZoom);
                }
            }
        }

        private void NoteRect_SelectNote(object sender, MouseEventArgs e)
        {
            if (!MainWindow.settings.songSelect && !isSelectingOccurrence && !isUIMoving && currentPattern != -1)
            {
                CreateOccurrenceInProgress(currentPattern);
                isSelectingOccurrence = true;
            }

            if (isSelectingOccurrence)
            {
                var noteRect = sender as Rectangle;
                int noteIndex = GetNoteIndex(noteRect);

                if (currentOccurrence.highlightedNotes.Contains(notes[0][noteIndex]))
                {
                    RemoveNoteHighlight(notes[0][noteIndex], currentPattern, patterns[currentPattern].GetOccurrences().Count);
                    currentOccurrence.highlightedNotes.Remove(notes[0][noteIndex]); 
                }
                else
                {
                    HighlightNote(notes[0][noteIndex], currentPattern, patterns[currentPattern].GetOccurrences().Count);
                    currentOccurrence.highlightedNotes.Add(notes[0][noteIndex]);
                }
            }
        }

        private void NoteRect_MouseIn(object sender, MouseEventArgs e)
        {
            if (!MainWindow.settings.songSelect)
            {
                Cursor = Cursors.Hand;
            }
        }

        private void NoteRect_MouseOut(object sender, MouseEventArgs e)
        {
            if (!MainWindow.settings.songSelect)
            {
                Cursor = Cursors.Arrow;
            }
        }

        private void PianoRoll_LeftMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isUIMoving && currentPattern != -1)
            {
                origMouseDownPoint = GetCurrentMousePosition(e);
                origMouseDownPoint.X = Snap(origMouseDownPoint.X, horizSnap);

                if (!MainWindow.settings.songSelect && !isSelectingOccurrence)
                {
                    origMouseDownPoint.Y = Snap(origMouseDownPoint.Y, vertiSnap);
                    CreateOccurrenceInProgress(currentPattern);
                }
                
                prevMouseDownPoint = origMouseDownPoint;

                isLeftMouseButtonDownOnPianoRoll = true;
                isSelectingOccurrence = true;
                Mouse.Capture(cnvMouseLayer);
            }
        }

        private void PianoRoll_LeftMouseUp(object sender, MouseButtonEventArgs e)
        {
            isLeftMouseButtonDownOnPianoRoll = false;
            Mouse.Capture(null);

            if (MainWindow.settings.songSelect)
            {
                isSelectingOccurrence = false;
            }

            if (isDraggingPatternRect && currentPattern != -1)
            {
                if (!MainWindow.settings.songSelect)
                {
                    currentOccurrenceRect.Visibility = Visibility.Collapsed;
                    currentOccurrenceRect = null;
                    currentSelection.Clear();
                }
                else
                {
                    if (!isUIMoving)
                    {
                        Occurrence newOccurrence = AddOccurrence(currentPattern, currentOccurrenceRect);
                        AddOccurrenceIcon(newOccurrence.occurrenceIcon);

                        if (patterns[currentPattern].patternIcon.CollExp)
                        {
                            MoveElement(btnAddPattern, occurrenceIconHeight);
                        }
                        else
                        {
                            newOccurrence.occurrenceIcon.Visibility = Visibility.Collapsed;
                        }

                        currentOccurrenceRect = null;
                    }
                }
            }

            isDraggingPatternRect = false;
        }

        private void PianoRoll_MouseMove(object sender, MouseEventArgs e)
        {
            Point curMouseDownPoint = GetCurrentMousePosition(e);
            Vector dragDelta = curMouseDownPoint - prevMouseDownPoint;
            double dragDistanceX = Math.Abs(dragDelta.X);
            double dragDistanceY = Math.Abs(dragDelta.Y);
            bool isDistanceFarEnough = false;

            if (!MainWindow.settings.songSelect)
            {
                isDistanceFarEnough = dragDistanceX >= horizSnap || dragDistanceY >= vertiSnap;
            }
            else
            {
                isDistanceFarEnough = dragDistanceX >= horizSnap;
            }

            if (isDistanceFarEnough)
            {
                if (isDraggingPatternRect)
                {
                    curMouseDownPoint.X = Snap(curMouseDownPoint.X, horizSnap);

                    if (!MainWindow.settings.songSelect)
                    {
                        curMouseDownPoint.Y = Snap(curMouseDownPoint.Y, vertiSnap);
                    }
                    
                    prevMouseDownPoint = curMouseDownPoint;
                    UpdateDragPatternRect(origMouseDownPoint, curMouseDownPoint);

                    e.Handled = true;
                }
                else if (isLeftMouseButtonDownOnPianoRoll && (currentPattern != -1))
                {
                    curMouseDownPoint.X = Snap(curMouseDownPoint.X, horizSnap);

                    if (!MainWindow.settings.songSelect)
                    {
                        curMouseDownPoint.Y = Snap(curMouseDownPoint.Y, vertiSnap);
                    }

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
            if (!patterns[currentPattern].patternIcon.View && !isSelectingOccurrence)
            {
                patterns[currentPattern].patternIcon.View = true;
                ShowOccurrenceVisuals(currentPattern);
            }

            currentOccurrenceRect = CreateOccurrenceRect(currentPattern);
            cnvMouseLayer.Children.Add(currentOccurrenceRect);
            UpdateDragPatternRect(pt1, pt2);
        }

        private void UpdateDragPatternRect(Point pt1, Point pt2)
        {
            Border dragBorder = (Border)currentOccurrenceRect.Children[0];
            double x, y, width, height;

            x = (pt1.X < pt2.X) ? pt1.X : pt2.X;
            width = Math.Abs(pt2.X - pt1.X);
            Canvas.SetLeft(dragBorder, x);
            dragBorder.Width = x + width > (double)this.Resources["PianoRollWidth"] ? (double)this.Resources["PianoRollWidth"] - x : width;

            if (!MainWindow.settings.songSelect)
            {
                y = (pt1.Y < pt2.Y) ? pt1.Y : pt2.Y;
                height = Math.Abs(pt2.Y - pt1.Y);
                Canvas.SetTop(dragBorder, y);
                dragBorder.Height = height;

                Rect dragRect = new Rect(x, y, width, height);

                foreach (NoteRect noteRect in notes[currentChannel])
                {
                    Rect noteShape = new Rect(noteRect.noteBar.Margin.Left, Grid.GetRow(noteRect.noteBar) * vertiSnap, noteRect.noteBar.Width, vertiSnap);

                    if (dragRect.Contains(noteShape))
                    {
                        if (!currentOccurrence.highlightedNotes.Contains(noteRect))
                        {
                            HighlightNote(noteRect, currentPattern, patterns[currentPattern].GetOccurrences().Count);
                            currentOccurrence.highlightedNotes.Add(noteRect);
                            currentSelection.Add(noteRect);
                        }
                    }
                }

                List<NoteRect> tempNoteRects = new List<NoteRect>(currentSelection);

                foreach (NoteRect noteRect in tempNoteRects)
                {
                    Rect noteShape = new Rect(noteRect.noteBar.Margin.Left, Grid.GetRow(noteRect.noteBar) * vertiSnap, noteRect.noteBar.Width, vertiSnap);

                    if (!dragRect.Contains(noteShape))
                    {
                        RemoveNoteHighlight(noteRect, currentPattern, patterns[currentPattern].GetOccurrences().Count);
                        currentOccurrence.highlightedNotes.Remove(noteRect);
                        currentSelection.Remove(noteRect);
                    }
                }
            }
            else
            {
                Canvas.SetTop(dragBorder, 0);
                dragBorder.Height = cnvMouseLayer.Height;
            }
        }

        private int PitchToRow(int pitch)
        {
            return 109 - pitch;
        }

        private NotePitch GetLowestPitch(List<NoteRect> notesIn)
        {
            NotePitch lowestPitch = NotePitch.C8;

            foreach (NoteRect noteRect in notesIn)
            {
                if (noteRect.note.GetPitch() < lowestPitch)
                {
                    lowestPitch = noteRect.note.GetPitch();
                }
            }

            return lowestPitch;
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

        // Gets a note index from its name.
        private int GetNoteIndex(Rectangle noteRect)
        {
            return Int32.Parse(noteRect.Name.Remove(0, 4));
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

            if (!isSelectingOccurrence)
            {
                btnAddPattern.IsEnabled = true;
            }
        }
    }
}