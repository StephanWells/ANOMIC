using System;
using System.Collections.Generic;
using System.IO;

namespace AnnotationTool
{
    public static class Analysis
    {
        private static string root = "C:\\Users\\steph\\Dropbox\\University - Master's\\Year 2\\Thesis Annotations\\";
        private static string[] midiNames = { "bach1", "bach2", "bee1", "hay1", "mo155", "mo458" };
        private static double inactivityThreshold = 120; // Threshold for time between logs which counts as inactivity.

        public static void Main(string[] args)
        {
            Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> data = ImportData();

            //List<Tuple<string, string, string, string>> participantOverview = GetParticipantsOverview(data.Item1);
            //TupleToCSV(participantOverview, root + "participantsoverview.csv");

            //List<Tuple<string, string, string, string>> fileOverview = GetFilesOverview(data.Item1);
            //TupleToCSV(fileOverview, root + "filesoverview.csv");

            //List<Tuple<string, string, string>> timeTakenPerParticipant = GetTimeTakenPerParticipant(data.Item2);
            //TupleToCSV(timeTakenPerParticipant, root + "timeparticipantseconds.csv");

            //List<Tuple<string, string>> timeTakenPerFile = GetTimeTakenPerFile(data.Item2);
            //TupleToCSV(timeTakenPerFile, root + "timefile.csv");


        }

        private static double CompareAnnotations(List<Pattern> annotation1, List<Pattern> annotation2)
        {
            double agreement = 0;

            return agreement;
        }

        private static Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> ImportData()
        {
            Dictionary<string, Dictionary<string, List<Pattern>>> importedPatterns = new Dictionary<string, Dictionary<string, List<Pattern>>>();
            Dictionary<string, Dictionary<string, List<Log>>> importedLogs = new Dictionary<string, Dictionary<string, List<Log>>>();

            string[] folders = Directory.GetDirectories(root);

            foreach (string folder in folders)
            {
                string folderName = new DirectoryInfo(folder).Name;
                Dictionary<string, List<Pattern>> annotations = new Dictionary<string, List<Pattern>>();
                Dictionary<string, List<Log>> logs = new Dictionary<string, List<Log>>();

                foreach (string jamsFile in Directory.EnumerateFiles(folder, "*.jams"))
                {
                    List<string> midiNamesList = new List<string>(midiNames);

                    foreach (string midiName in midiNamesList)
                    {
                        if (midiName.Equals(Path.GetFileNameWithoutExtension(jamsFile)))
                        {
                            FileParser fileParser = new FileParser(File.ReadAllText(jamsFile));
                            fileParser.ParseFile();
                            List<Pattern> filePatterns = fileParser.patterns;
                            List<Log> fileLogs = fileParser.logs;

                            annotations.Add(midiName, filePatterns);
                            logs.Add(midiName, fileLogs);
                            midiNamesList.Remove(midiName);

                            break;
                        }
                    }
                }

                importedPatterns.Add(folderName, annotations);
                importedLogs.Add(folderName, logs);
            }

            Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> data = new Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>>(importedPatterns, importedLogs);

            return data;
        }

        private static List<Tuple<string, string>> GetTimeTakenPerFile(Dictionary<string, Dictionary<string, List<Log>>> logs)
        {
            List<Tuple<string, string>> timeTakenPerFile = new List<Tuple<string, string>>();
            List<double> totalTimes = new List<double>();

            for (int i = 0; i < midiNames.Length; i++)
            {
                totalTimes.Add(0);
            }

            foreach (KeyValuePair<string, Dictionary<string, List<Log>>> participantLogs in logs)
            {
                foreach (KeyValuePair<string, List<Log>> file in participantLogs.Value)
                {
                    for (int i = 0; i < midiNames.Length; i++)
                    {
                        if (file.Key.Equals(midiNames[i]))
                        {
                            Tuple<TimeSpan, TimeSpan> time = GetTimeTaken(file.Key, file.Value);
                            totalTimes[i] += time.Item1.TotalSeconds;
                        }
                    }
                }
            }

            for (int i = 0; i < midiNames.Length; i++)
            {
                double averageTime = totalTimes[i] / logs.Count;
                Tuple<string, string> timeTaken = new Tuple<string, string>(midiNames[i], MakeTimeReadable(averageTime));
                timeTakenPerFile.Add(timeTaken);
            }

            return timeTakenPerFile;
        }

        private static List<Tuple<string, string, string>> GetTimeTakenPerParticipant(Dictionary<string, Dictionary<string, List<Log>>> logs)
        {
            List<Tuple<string, string, string>> timeTakenPerParticipant = new List<Tuple<string, string, string>>();

            foreach (KeyValuePair<string, Dictionary<string, List<Log>>> participantLogs in logs)
            {
                double totalActivitySeconds = 0;
                double totalInactivitySeconds = 0;

                foreach (KeyValuePair<string, List<Log>> file in participantLogs.Value)
                {
                    Tuple<TimeSpan, TimeSpan> time = GetTimeTaken(file.Key, file.Value);
                    totalActivitySeconds += time.Item1.TotalSeconds;
                    totalInactivitySeconds += time.Item2.TotalSeconds;
                }

                Tuple<string, string, string> timeTaken = new Tuple<string, string, string>(participantLogs.Key, MakeTimeReadable(totalActivitySeconds), MakeTimeReadable(totalInactivitySeconds));
                timeTakenPerParticipant.Add(timeTaken);
            }

            return timeTakenPerParticipant;
        }

        private static string MakeTimeReadable(double seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

            return "" + timeSpan.Hours + "h" + 
                (timeSpan.Minutes < 10 ? "0" : "") + timeSpan.Minutes + "m" +
                (timeSpan.Seconds < 10 ? "0" : "") + timeSpan.Seconds + "." +
                (timeSpan.Milliseconds < 100 ? "0" : "") + (timeSpan.Milliseconds < 10 ? "0" : "") + timeSpan.Milliseconds + "s"; 
        }

        private static Tuple<TimeSpan, TimeSpan> GetTimeTaken(string fileName, List<Log> fileLogs)
        {
            TimeSpan time = new TimeSpan(0);
            TimeSpan inactivity = new TimeSpan(0);
            bool counting = false;

            for (int i = 0; i < fileLogs.Count; i++)
            {
                if (counting)
                {
                    double difference = fileLogs[i].time.TotalSeconds - fileLogs[i - 1].time.TotalSeconds;

                    if (difference < inactivityThreshold)
                    {
                        time = time.Add(TimeSpan.FromSeconds(difference));
                    }
                    else
                    {
                        inactivity = inactivity.Add(TimeSpan.FromSeconds(difference));
                    }
                }

                if (fileLogs[i].logType == LogType.LoadedFile)
                {
                    if (fileLogs[i].value.Split('.')[0].Equals(fileName))
                    {
                        counting = true;
                    }
                    else if (counting) break;
                }

                if (counting && fileLogs[i].logType == LogType.Session && fileLogs[i].value.Equals("Sessionend"))
                {
                    break;
                }
            }

            Tuple<TimeSpan, TimeSpan> timeResults = new Tuple<TimeSpan, TimeSpan>(time, inactivity);

            return timeResults;
        }

        private static List<Tuple<string, string, string, string>> GetFilesOverview(Dictionary<string, Dictionary<string, List<Pattern>>> data)
        {
            List<Tuple<string, string, string, string>> filesOverview = new List<Tuple<string, string, string, string>>();
            List<int> totalPatterns = new List<int>();
            List<int> totalOccurrences = new List<int>();
            List<int> totalNotes = new List<int>();
            List<double> averageNotes = new List<double>();

            for (int i = 0; i < midiNames.Length; i++)
            {
                totalPatterns.Add(0);
                totalOccurrences.Add(0);
                totalNotes.Add(0);
            }

            foreach (KeyValuePair<string, Dictionary<string, List<Pattern>>> annotations in data)
            {
                foreach (KeyValuePair<string, List<Pattern>> file in annotations.Value)
                {
                    for (int i = 0; i < midiNames.Length; i++)
                    {
                        if (file.Key.Equals(midiNames[i]))
                        {
                            totalPatterns[i] += file.Value.Count;

                            foreach (Pattern pattern in file.Value)
                            {
                                totalOccurrences[i] += pattern.GetOccurrences().Count;

                                foreach (Occurrence occurrence in pattern.GetOccurrences())
                                {
                                    totalNotes[i] += occurrence.highlightedNotes.Count;
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < totalNotes.Count; i++)
            {
                averageNotes.Add(Math.Round((double)totalNotes[i] / (double)totalOccurrences[i], 2));
            }

            for (int i = 0; i < midiNames.Length; i++)
            {
                Tuple<string, string, string, string> tempData = new Tuple<string, string, string, string>(midiNames[i], "" + totalPatterns[i], "" + totalOccurrences[i], averageNotes[i].ToString());
                filesOverview.Add(tempData);
            }

            return filesOverview;
        }

        private static List<Tuple<string, string, string, string>> GetParticipantsOverview(Dictionary<string, Dictionary<string, List<Pattern>>> data)
        {
            List<Tuple<string, string, string, string>> participantsOverview = new List<Tuple<string, string, string, string>>();

            foreach (KeyValuePair<string, Dictionary<string, List<Pattern>>> annotations in data)
            {
                int totalPatterns = 0;
                int totalOccurrences = 0;
                int totalNotes = 0;

                foreach (KeyValuePair<string, List<Pattern>> file in annotations.Value)
                {
                    totalPatterns += file.Value.Count;
                    
                    foreach (Pattern pattern in file.Value)
                    {
                        totalOccurrences += pattern.GetOccurrences().Count;

                        foreach (Occurrence occurrence in pattern.GetOccurrences())
                        {
                            totalNotes += occurrence.highlightedNotes.Count;
                        }
                    }
                }

                double averageNotes = Math.Round((double)totalNotes / (double)totalOccurrences, 2);

                Tuple<string, string, string, string> tempData = new Tuple<string, string, string, string>(annotations.Key, "" + totalPatterns, "" + totalOccurrences, averageNotes.ToString());
                participantsOverview.Add(tempData);
            }

            return participantsOverview;
        }

        private static void TupleToCSV(List<Tuple<string, string>> tuples, string filename)
        {
            List<string> textLines = new List<string>();

            foreach (Tuple<string, string> tuple in tuples)
            {
                string textLine = tuple.Item1 + ", " + tuple.Item2;
                textLines.Add(textLine);
            }

            File.WriteAllLines(filename, textLines);
        }

        private static void TupleToCSV(List<Tuple<string, string, string>> tuples, string filename)
        {
            List<string> textLines = new List<string>();

            foreach (Tuple<string, string, string> tuple in tuples)
            {
                string textLine = tuple.Item1 + ", " + tuple.Item2 + ", " + tuple.Item3;
                textLines.Add(textLine);
            }

            File.WriteAllLines(filename, textLines);
        }

        private static void TupleToCSV(List<Tuple<string, string, string, string>> tuples, string filename)
        {
            List<string> textLines = new List<string>();

            foreach (Tuple<string, string, string, string> tuple in tuples)
            {
                string textLine = tuple.Item1 + ", " + tuple.Item2 + ", " + tuple.Item3 + ", " + tuple.Item4;
                textLines.Add(textLine);
            }

            File.WriteAllLines(filename, textLines);
        }
    }
}