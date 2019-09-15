using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Windows.Controls;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;

namespace AnnotationTool
{
    public class ReverseComparer : IComparer<double>
    {
        public int Compare(double x, double y)
        {
            // Compare y and x in reverse order.
            return y.CompareTo(x);
        }
    }

    public static class Analysis
    {
        private static string root = "C:\\Users\\steph\\Dropbox\\University - Master's\\Year 2\\Thesis Annotations\\";
        private static string[] midiNames = { "bach1", "bach2", "bee1", "hay1", "mo155", "mo458" };
        private static double inactivityThreshold = 120; // Threshold for time between logs which counts as inactivity.
        private enum MatrixDirection
        {
            ROW,
            COLUMN
        };
        private struct MatrixLine
        {
            public int val;
            public MatrixDirection dir;
        };

        private static int testCounter = 0;

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

			//double[,] test = new double[3, 3] { { 8, 10, 1 }, { 2, 9, 2 }, { 9, 4, 5 } };
			//HungarianAlgorithm(test);

			//double[,] test = new double[5, 5] { { 0, 1, 0, 1, 1 }, { 1, 1, 0, 1, 1 }, { 1, 0, 0, 0, 1 }, { 1, 1, 0, 1, 1 }, { 1, 0, 0, 1, 0 } };
			//double[,] test = new double[3, 3] { { 0, 0, 1 }, { 0, 1, 1 }, { 1, 0, 1 } };
			//double test = CompareAnnotations(data.Item1["Troisnyx"]["bach1"], data.Item1["Myriada"]["bach1"]);

			Dictionary<string, double[,]> confMatrix = GetConfusionMatrix(data.Item1);

			OutputConfusionMatrix(confMatrix["bach1"]);

			double agreement = CompareAnnotations(data.Item1["Andrew"]["bach2"], data.Item1["Alex"]["bach2"]);
        }

        private static Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> ImportData()
        {
            Console.WriteLine("- IMPORTING DATA:");

            Dictionary<string, Dictionary<string, List<Pattern>>> importedPatterns = new Dictionary<string, Dictionary<string, List<Pattern>>>();
            Dictionary<string, Dictionary<string, List<Log>>> importedLogs = new Dictionary<string, Dictionary<string, List<Log>>>();

            string[] folders = Directory.GetDirectories(root);

            foreach (string folder in folders)
            {
                string folderName = new DirectoryInfo(folder).Name;
                Dictionary<string, List<Pattern>> annotations = new Dictionary<string, List<Pattern>>();
                Dictionary<string, List<Log>> logs = new Dictionary<string, List<Log>>();

                Console.Write("-- Folder: " + folderName + "...\t" + (folderName.Length < 11 ? "\t" : ""));

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

                Console.WriteLine("Loaded!");
            }

            Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> data = new Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>>(importedPatterns, importedLogs);

            Console.WriteLine("- DATA IMPORTED. Folder count: " + data.Item1.Count);
            Console.WriteLine();

            return data;
        }

		private static void OutputConfusionMatrix(double[,] confMatrix)
		{
			int size = 100;
			Bitmap bmp = new Bitmap(confMatrix.GetLength(0) * size + size, confMatrix.GetLength(1) * size + size);
			Graphics graph = Graphics.FromImage(bmp);
			graph.SmoothingMode = SmoothingMode.AntiAlias;
			graph.InterpolationMode = InterpolationMode.HighQualityBicubic;
			graph.PixelOffsetMode = PixelOffsetMode.HighQuality;

			for (int i = 1; i <= confMatrix.GetLength(0); i++)
			{
				graph.DrawString(i.ToString(), new Font("Tahoma", 40), Brushes.Black, new Point(i * size, 0));
			}

			for (int i = 1; i <= confMatrix.GetLength(1); i++)
			{
				graph.DrawString(i.ToString(), new Font("Tahoma", 40), Brushes.Black, new Point(0, i * size));
			}

			for (int i = 0; i < confMatrix.GetLength(0); i++)
			{
				for (int j = 0; j < confMatrix.GetLength(1); j++)
				{
					Point location = new Point((i + 1) * size, (j + 1) * size);
					Size dimensions = new Size(size, size);

					Rectangle matrixSquare = new Rectangle(location, dimensions);
					byte r = (byte)(0);
					byte g = (byte)(0);
					byte b = (byte)(confMatrix[i, j] * 255);
					Color squareColor = Color.FromArgb(255, r, g, b);
					graph.FillRectangle(new SolidBrush(squareColor), matrixSquare);
					graph.DrawString((confMatrix[i, j] * 100).ToString(), new Font("Agency FB", 40), Brushes.White, location);
				}
			}

			Bitmap saveImage = (Bitmap)bmp.Clone();
			bmp.Dispose();
			saveImage.Save("C:\\Users\\steph\\Desktop\\matrix.png", ImageFormat.Png);
		}

        private static List<Occurrence> PatternListToOccurrenceList(List<Pattern> patterns)
        {
            List<Occurrence> occurrences = new List<Occurrence>();

            foreach (Pattern pattern in patterns)
            {
                foreach (Occurrence occurrence in pattern.GetOccurrences())
                {
                    occurrences.Add(occurrence);
                }
            }

            return occurrences;
        }

        private static double CompareOccurrences(Occurrence occ1, Occurrence occ2)
        {
            double agreement = 0;
            bool found = false;

			Occurrence largerOccurrence = occ1.highlightedNotes.Count > occ2.highlightedNotes.Count ? occ1 : occ2;
			Occurrence smallerOccurrence = occ1.highlightedNotes.Count > occ2.highlightedNotes.Count ? occ2 : occ1;

			foreach (NoteRect noteRect1 in largerOccurrence.highlightedNotes)
            {
                foreach (NoteRect noteRect2 in smallerOccurrence.highlightedNotes)
                {
                    if (NoteRect.AreTwoNotesEqual(noteRect1.note, noteRect2.note))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    found = false;
                    agreement += 1;
                }
            }

            agreement = Math.Round(agreement / (largerOccurrence.highlightedNotes.Count), 2);

            return agreement;
        }

        private static double CompareAnnotations(List<Pattern> patterns1, List<Pattern> patterns2)
        {
            List<Occurrence> annotation1 = PatternListToOccurrenceList(patterns1);
            List<Occurrence> annotation2 = PatternListToOccurrenceList(patterns2);
            double[,] agreementMatrix = new double[annotation1.Count, annotation2.Count];

            for (int i = 0; i < annotation1.Count; i++)
            {
                for (int j = 0; j < annotation2.Count; j++)
                {
                    agreementMatrix[i, j] = CompareOccurrences(annotation1[i], annotation2[j]);
                }
            }

			//OutputConfusionMatrix(agreementMatrix);

            List<Tuple<int, int>> assignments = Assign(agreementMatrix);
            double totalAgreement = 0;

            foreach (Tuple<int, int> assignment in assignments)
            {
                totalAgreement += agreementMatrix[assignment.Item1, assignment.Item2];
            }

            return totalAgreement / assignments.Count;
        }

        private static List<Tuple<int, int>> Assign(double[,] matrix)
        {
            double[,] tempMatrix = (double[,])matrix.Clone();
            List<Tuple<int, int>> assignments = new List<Tuple<int, int>>();
            List<Tuple<Tuple<int, int>, double>> remainingList = GetMaxOfEachRow(tempMatrix);

            do
            {
                Tuple<Tuple<int, int>[], double[]> maximumArrays = ListToParallelArrays(remainingList);
                Tuple<int, int>[] indices = maximumArrays.Item1;
                double[] values = maximumArrays.Item2;

                Array.Sort(values, indices, new ReverseComparer());
                List<int> usedColumns = new List<int>();
                remainingList = new List<Tuple<Tuple<int, int>, double>>();

                for (int i = 0; i < indices.Length; i++)
                {
                    if (usedColumns.Contains(indices[i].Item2))
                    {
                        remainingList.Add(new Tuple<Tuple<int, int>, double>(indices[i], values[i]));
                    }
                    else
                    {
                        usedColumns.Add(indices[i].Item2);
                        assignments.Add(new Tuple<int, int>(indices[i].Item1, indices[i].Item2));

                        for (int j = 0; j < tempMatrix.GetLength(0); j++)
                        {
                            tempMatrix[j, indices[i].Item2] = 0;
                        }
                    }
                }

                List<Tuple<Tuple<int, int>, double>> tempList = new List<Tuple<Tuple<int, int>, double>>(remainingList);
                remainingList = new List<Tuple<Tuple<int, int>, double>>();

                for (int j = 0; j < tempList.Count; j++)
                {
                    remainingList.Add(GetRowMax(tempMatrix, tempList[j].Item1.Item1));
                }
            } while (remainingList.Count > 0);

            return assignments;
        }

        private static List<Tuple<Tuple<int, int>, double>> GetMaxOfEachRow(double[,] matrix)
        {
            List<Tuple<Tuple<int, int>, double>> maximums = new List<Tuple<Tuple<int, int>, double>>();

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                Tuple<Tuple<int, int>, double> rowMax = GetRowMax(matrix, i);
                maximums.Add(rowMax);
            }

            return maximums;
        }

        private static Tuple<K[], V[]> ListToParallelArrays<K, V>(List<Tuple<K, V>> list)
        {
            K[] indices = new K[list.Count];
            V[] values = new V[list.Count];

            for (int i = 0; i < list.Count; i++)
            {
                indices[i] = list[i].Item1;
                values[i] = list[i].Item2;
            }

            return new Tuple<K[], V[]>(indices, values);
        }

        private static Tuple<Tuple<int, int>, double> GetRowMax(double[,] matrix, int rowIndex)
        {
            int maxIndex = -1;
            double maxValue = 0;
            double[] row = GetRow(matrix, rowIndex);

            for (int i = 0; i < row.Length; i++)
            {
                if (maxValue <= row[i])
                {
                    maxValue = row[i];
                    maxIndex = i;
                }
            }

            return new Tuple<Tuple<int, int>, double>(new Tuple<int, int>(rowIndex, maxIndex), maxValue);
        }

        private static void OutputMatrix<T>(T[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Console.Write("[" + matrix[i, j] + "]");
                }

                Console.WriteLine();
            }

            Console.WriteLine();
        }

        private static Dictionary<string, double[,]> GetConfusionMatrix(Dictionary<string, Dictionary<string, List<Pattern>>> data)
        {
            Console.WriteLine("- CALCULATING CONFUSION MATRIX:");

            Dictionary<string, double[,]> confuMatrix = new Dictionary<string, double[,]>();

            for (int i = 0; i < midiNames.Length; i++)
            {
                confuMatrix.Add(midiNames[i], new double[data.Count, data.Count]);
            }

            int participantCount1 = 0, participantCount2 = 0;
            int totalMatches = data.Count * data.Count;
            int runningMatches = 0;
            Dictionary<string, List<Pattern>> annotationsPerParticipant = new Dictionary<string, List<Pattern>>();

            foreach (KeyValuePair<string, Dictionary<string, List<Pattern>>> annotations in data)
            {
                foreach (KeyValuePair<string, Dictionary<string, List<Pattern>>> annotations2 in data)
                {
                    foreach (KeyValuePair<string, List<Pattern>> files in annotations.Value)
                    {
                        confuMatrix[files.Key][participantCount1, participantCount2] = Math.Round(CompareAnnotations(annotations.Value[files.Key], annotations2.Value[files.Key]), 2);
                    }

                    participantCount2++;
                    runningMatches++;

                    Console.Write("\r-- " + ((runningMatches * 100) / (totalMatches)) + "%");
                }

                participantCount1++;
                participantCount2 = 0;
            }

            Console.WriteLine(": Done!");
            Console.WriteLine();

            return confuMatrix;
        }

        private static T[] GetRow<T>(T[,] matrix, int rowIndex)
        {
            T[] row = new T[matrix.GetLength(1)];

            for (int i = 0; i < matrix.GetLength(1); i++)
            {
                row[i] = matrix[rowIndex, i];
            }

            return row;
        }

        private static T[] GetColumn<T>(T[,] matrix, int columnIndex)
        {
            T[] column = new T[matrix.GetLength(0)];

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                column[i] = matrix[i, columnIndex];
            }

            return column;
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

        /*private static int CountZeros(string[] nums)
        {
            int zeroCount = 0;

            foreach (string num in nums)
            {
                if (num == "0")
                {
                    zeroCount++;
                }
            }

            return zeroCount;
        }*/

        /*private static double[,] SubtractRowMinima(double[,] matrix)
        {
            double min = double.MaxValue;

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                // Finding minimum value in a row.
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (matrix[i, j] < min)
                    {
                        min = matrix[i, j];
                    }
                }

                // Subtracting all row elements by that value
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    matrix[i, j] -= min;
                }

                min = double.MaxValue;
            }

            return matrix;
        }*/

        /*private static double[,] SubtractColumnMinima(double[,] matrix)
        {
            double min = double.MaxValue;

            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                // Finding minimum value in a column.
                for (int i = 0; i < matrix.GetLength(0); i++)
                {
                    if (matrix[i, j] < min)
                    {
                        min = matrix[i, j];
                    }
                }

                // Subtracting all column elements by that value
                for (int i = 0; i < matrix.GetLength(1); i++)
                {
                    matrix[i, j] -= min;
                }

                min = double.MaxValue;
            }

            return matrix;
        }*/

        /*private static void HungarianAlgorithm(double[,] matrix)
        {
            double[,] tempMatrix = (double[,])matrix.Clone();

            matrix = NegativeMatrix(matrix);
            matrix = AddDummyValues(matrix);
            matrix = SubtractRowMinima(matrix);
            matrix = SubtractColumnMinima(matrix);

            Tuple<string[,], int> lines = MarkMatrix(matrix);

            if (lines.Item2 == matrix.GetLength(0)) // If assignment is possible.
            {
                List<Tuple<int, int>> assignments = Assign(matrix);
            }
        }*/

        /*private static List<Tuple<int, int>> Assign(double[,] matrix)
        {
            List<Tuple<int, int>> assignments = new List<Tuple<int, int>>();

            string[,] markedMatrix = new string[matrix.GetLength(0), matrix.GetLength(1)];

            for (int i = 0; i < markedMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < markedMatrix.GetLength(1); j++)
                {
                    markedMatrix[i, j] = "" + matrix[i, j];
                }
            }

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                double[] row = GetRow(matrix, i);
                List<int> zeroIndices = GetZeroIndices(row);

                if (zeroIndices.Count == 1)
                {
                    assignments.Add(new Tuple<int, int>(i, zeroIndices[0]));

                    for (int j = 0; j < matrix.GetLength(1); j++)
                    {
                        matrix[i, j] = 0;
                    }
                }
            }

            return assignments;
        }*/

        /*private static Tuple<string[,], int> MarkMatrix(double[,] matrix)
        {
            string[,] markedMatrix = new string[matrix.GetLength(0), matrix.GetLength(1)];

            for (int i = 0; i < markedMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < markedMatrix.GetLength(1); j++)
                {
                    markedMatrix[i, j] = "" + matrix[i, j];
                }
            }

            MatrixLine[] zeroCounts = GetZeroCounts(markedMatrix);
            List<MatrixLine> maxZeroResults = GetMaxZeroLines(zeroCounts, markedMatrix);
            int runningCount = 0;

            if (maxZeroResults.Count > 0)
            {
                int matrixIndex = maxZeroResults[0].dir == MatrixDirection.ROW ? maxZeroResults[0].val : maxZeroResults[0].val - matrix.GetLength(0);

                if (zeroCounts[matrixIndex].val == matrix.GetLength(0))
                {
                    foreach (MatrixLine line in maxZeroResults)
                    {
                        markedMatrix = MarkLine(markedMatrix, line);
                        runningCount++;
                    }
                }
            }

            Tuple<string[,], int> markResults = MarkLines(markedMatrix, runningCount, -1);

            return markResults;
        }*/

        /*private static Tuple<string[,], int> MarkLines(string[,] markedMatrix, int runningCount, int testingCount)
        {
            MatrixLine[] zeroCounts = GetZeroCounts(markedMatrix);
            List<MatrixLine> maxZeroResults = GetMaxZeroLines(zeroCounts, markedMatrix);
            string[,] resultMatrix = (string[,])markedMatrix.Clone();

            //Console.WriteLine("-- Running Count: " + runningCount + " - Max Zero Counts: " + maxZeroResults.Count);

            // If only one line with the maximum count of zeros was found.
            if (maxZeroResults.Count == 1)
            {
                runningCount++;

                if (runningCount > testingCount && testingCount != -1)
                {
                    return new Tuple<string[,], int>((string[,])resultMatrix.Clone(), runningCount);
                }

                resultMatrix = MarkLine(markedMatrix, maxZeroResults[0]);
                Tuple<string[,], int> markResults = MarkLines(resultMatrix, runningCount, -1);
                resultMatrix = markResults.Item1;
                runningCount = markResults.Item2;
            }
            else if (maxZeroResults.Count > 1)
            {
                if (zeroCounts[maxZeroResults[0].val].val == 1) // Early out: if the remaining possible lines are just singular 0s.
                {
                    foreach (MatrixLine matrixLine in maxZeroResults) // Fill all the rows.
                    {
                        if (matrixLine.dir == MatrixDirection.COLUMN) break; // At this point all 0s would have been filled by rows.

                        resultMatrix = MarkLine(markedMatrix, matrixLine);
                        runningCount++;
                    }
                }
                else
                {
                    runningCount++;

                    if (runningCount > testingCount && testingCount != -1)
                    {
                        return new Tuple<string[,], int>((string[,])resultMatrix.Clone(), runningCount);
                    }

                    List<int> potentialLineCounts = new List<int>();
                    List<string[,]> potentialMatrices = new List<string[,]>();

                    if (runningCount <= 1)
                    {
                        Console.WriteLine("- Test: " + testCounter++ + ", Potentials: " + maxZeroResults.Count);
                    }

                    foreach (MatrixLine matrixLine in maxZeroResults)
                    {
                        string[,] potentialMatrix = (string[,])markedMatrix.Clone();
                        potentialMatrix = MarkLine(potentialMatrix, matrixLine);

                        Tuple<string[,], int> markResults = MarkLines(potentialMatrix, runningCount, testingCount);
                        potentialMatrices.Add((string[,])markResults.Item1.Clone());
                        potentialLineCounts.Add(markResults.Item2);

                        if (testingCount == -1)
                        {
                            testingCount = markResults.Item2;
                        }
                        else
                        {
                            if (testingCount > markResults.Item2)
                            {
                                testingCount = markResults.Item2;
                            }
                        }
                    }

                    int minLineCount = int.MaxValue;
                    int minIndex = -1;

                    for (int i = 0; i < potentialLineCounts.Count; i++)
                    {
                        if (minLineCount >= potentialLineCounts[i])
                        {
                            minLineCount = potentialLineCounts[i];
                            minIndex = i;
                        }
                    }

                    resultMatrix = (string[,])potentialMatrices[minIndex].Clone();
                    runningCount = potentialLineCounts[minIndex];
                }
            }
            else
            {
                resultMatrix = (string[,])markedMatrix.Clone();
            }

            return new Tuple<string[,], int>((string[,])resultMatrix.Clone(), runningCount);
        }*/

        /*private static MatrixLine[] GetZeroCounts(string[,] matrix)
        {
            MatrixLine[] zeroCounts = new MatrixLine[matrix.GetLength(0) + matrix.GetLength(1)];

            // Initialising row zero counts.
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                string[] row = GetRow(matrix, i);
                zeroCounts[i].val = CountZeros(row);
                zeroCounts[i].dir = MatrixDirection.ROW;
            }

            // Initialising column zero counts.
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                string[] col = GetColumn(matrix, j);
                zeroCounts[j + matrix.GetLength(0)].val = CountZeros(col);
                zeroCounts[j + matrix.GetLength(0)].dir = MatrixDirection.COLUMN;
            }

            return zeroCounts;
        }*/

        /*private static List<int> GetZeroIndices(double[] row)
        {
            List<int> zeroIndices = new List<int>();

            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == 0)
                {
                    zeroIndices.Add(i);
                }
            }

            return zeroIndices;
        }*/

        /*private static List<MatrixLine> GetMaxZeroLines(MatrixLine[] zeroCounts, string[,] markedMatrix)
        {
            // Finding the row or column with the most zeros.
            int maxCount = 0;
            List<MatrixLine> maxIndex = new List<MatrixLine>();

            for (int i = 0; i < zeroCounts.Length; i++)
            {
                if (zeroCounts[i].val > maxCount)
                {
                    maxCount = zeroCounts[i].val;
                    maxIndex = new List<MatrixLine>();
                    MatrixLine matrixLine = new MatrixLine();
                    matrixLine.val = i - ((zeroCounts[i].dir == MatrixDirection.COLUMN) ? markedMatrix.GetLength(0) : 0);
                    matrixLine.dir = zeroCounts[i].dir;
                    maxIndex.Add(matrixLine);
                }
                else if (zeroCounts[i].val == maxCount && maxCount != 0)
                {
                    maxCount = zeroCounts[i].val;
                    MatrixLine matrixLine = new MatrixLine();
                    matrixLine.val = i - ((zeroCounts[i].dir == MatrixDirection.COLUMN) ? markedMatrix.GetLength(0) : 0);
                    matrixLine.dir = zeroCounts[i].dir;
                    maxIndex.Add(matrixLine);
                }
            }

            return maxIndex;
        }*/

        /*private static string[,] MarkLine(string[,] markedMatrix, MatrixLine line)
        {
            if (line.dir == MatrixDirection.ROW)
            {
                for (int i = 0; i < markedMatrix.GetLength(1); i++)
                {
                    markedMatrix[line.val, i] = "x";
                }
            }
            else
            {
                for (int i = 0; i < markedMatrix.GetLength(0); i++)
                {
                    markedMatrix[i, line.val] = "x";
                }
            }

            return markedMatrix;
        }*/

        /*private static double[,] AddDummyValues(double[,] matrix)
        {
            int max = matrix.GetLength(0) > matrix.GetLength(1) ? matrix.GetLength(0) : matrix.GetLength(1);
            double[,] resultMatrix = new double[max, max];

            for (int i = 0; i < max; i++)
            {
                for (int j = 0; j < max; j++)
                {
                    if (i < matrix.GetLength(0) && j < matrix.GetLength(1))
                    {
                        resultMatrix[i, j] = matrix[i, j];
                    }
                    else
                    {
                        resultMatrix[i, j] = 0;
                    }
                }
            }

            return resultMatrix;
        }*/

        /*private static double[,] NegativeMatrix(double[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    matrix[i, j] *= -1;
                }
            }

            return matrix;
        }*/
    }
}