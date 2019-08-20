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
            Dictionary<string, Dictionary<string, List<Pattern>>> data = ImportPatterns();

            List<Tuple<string, string, string>> dataOverview = GetOverview(data);
            TupleToCSV(dataOverview, root + "overview.csv");
        }

        private static Dictionary<string, Dictionary<string, List<Pattern>>> ImportPatterns()
        {
            Dictionary<string, Dictionary<string, List<Pattern>>> importedPatterns = new Dictionary<string, Dictionary<string, List<Pattern>>>();
            string[] folders = Directory.GetDirectories(root);

            foreach (string folder in folders)
            {
                string folderName = new DirectoryInfo(folder).Name;
                Dictionary<string, List<Pattern>> annotations = new Dictionary<string, List<Pattern>>();

                foreach (string jamsFile in Directory.EnumerateFiles(folder, "*.jams"))
                {
                    List<string> midiNamesList = new List<string>(midiNames);

                    foreach (string midiName in midiNamesList)
                    {
                        if (midiName.Equals(Path.GetFileNameWithoutExtension(jamsFile)))
                        {
                            FileParser fileParser = new FileParser(File.ReadAllText(jamsFile));
                            List<Pattern> filePatterns = fileParser.ParseFile();

                            annotations.Add(midiName, filePatterns);
                            midiNamesList.Remove(midiName);

                            break;
                        }
                    }
                }

                importedPatterns.Add(folderName, annotations);
            }

            return importedPatterns;
        }

        private static List<Tuple<string, string, string>> GetOverview(Dictionary<string, Dictionary<string, List<Pattern>>> data)
        {
            List<Tuple<string, string, string>> dataOverview = new List<Tuple<string, string, string>>();

            foreach (KeyValuePair<string, Dictionary<string, List<Pattern>>> annotations in data)
            {
                int totalPatterns = 0;
                int totalOccurrences = 0;

                foreach (KeyValuePair<string, List<Pattern>> file in annotations.Value)
                {
                    totalPatterns += file.Value.Count;
                    
                    foreach (Pattern pattern in file.Value)
                    {
                        totalOccurrences += pattern.GetOccurrences().Count;
                    }
                }

                Tuple<string, string, string> tempData = new Tuple<string, string, string>(annotations.Key, "" + totalPatterns, "" + totalOccurrences);
                dataOverview.Add(tempData);
            }

            return dataOverview;
        }

        private static void TupleToCSV(List<Tuple<string, string, string>> tuples, string filename)
        {
            List<string> textLines = new List<string>();

            foreach (Tuple<string, string, string> tuple in tuples)
            {
                string textLine = tuple.Item1 + "; " + tuple.Item2 + "; " + tuple.Item3;
                textLines.Add(textLine);
            }

            File.WriteAllLines(filename, textLines);
        }
    }
}