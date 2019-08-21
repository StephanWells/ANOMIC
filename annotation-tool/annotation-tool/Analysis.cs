using System;
using System.Collections.Generic;
using System.IO;

namespace AnnotationTool
{
    public static class Analysis
    {
        private static string root = "C:\\Users\\steph\\Dropbox\\University - Master's\\Year 2\\Thesis Annotations\\";
        private static string[] midiNames = { "bach1", "bach2", "bee1", "hay1", "mo155", "mo458" };

        public static void Main(string[] args)
        {
            Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> data = ImportData();

            List<Tuple<string, string, string, string>> dataOverview = GetFilesOverview(data.Item1);
            TupleToCSV(dataOverview, root + "filesoverview.csv");
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

                            annotations.Add(midiName, filePatterns);
                            midiNamesList.Remove(midiName);

                            break;
                        }
                    }
                }

                importedPatterns.Add(folderName, annotations);
            }

            Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> data = new Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>>(importedPatterns, importedLogs);

            return data;
        }

        private static List<Tuple<string, string, string, string>> GetFilesOverview(Dictionary<string, Dictionary<string, List<Pattern>>> data)
        {
            List<Tuple<string, string, string, string>> filesOverview = new List<Tuple<string, string, string, string>>();
            List<int> totalPatterns = new List<int>();
            List<int> totalOccurrences = new List<int>();
            List<int> totalNotes = new List<int>();
            List<double> averageNotes = new List<double>();

            foreach (string midiName in midiNames)
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