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
        private const string root = "";
		private const string rootHEMAN = "";
		private const string matrixSaveFolder = "";
        private static string[] midiNames = { "bach1", "bach2", "bee1", "hay1", "mo155", "mo458" };
        private static double inactivityThreshold = 120; // Threshold for time between logs which counts as inactivity.
		private static double intervalMatchThreshold = 1; // Threshold for time intervals before two intervals are not considered a match.
		private static double resolution = 1; // Time resolution for importing JAMS files (one quarter note).
		private enum MatrixDirection
        {
            ROW,
            COLUMN
        };
		private enum MatrixMode
		{
			PRECISION,
			RECALL,
			FSCORE
		};
		private struct MatrixLine
        {
            public int val;
            public MatrixDirection dir;
        };

        private struct Interval
		{
			public double start;
			public double end;
			public int confidence;
		}

        public static void Main(string[] args)
        {
			Tuple<Dictionary<string, Dictionary<string, List<Pattern>>>, Dictionary<string, Dictionary<string, List<Log>>>> data = ImportData();
			//Dictionary<string, Dictionary<string, List<List<Interval>>>> data = ImportHEMAN();

			//List<Tuple<string, string, string, string>> participantOverview = GetParticipantsOverview(data.Item1);
			//TupleToCSV(participantOverview, root + "participantsoverview.csv");

			//List<Tuple<string, string, string, string>> fileOverview = GetFilesOverview(data.Item1);
			//TupleToCSV(fileOverview, root + "filesoverview.csv");

			//List<Tuple<string, string, string>> timeTakenPerParticipant = GetTimeTakenPerParticipant(data.Item2);
			//TupleToCSV(timeTakenPerParticipant, root + "timeparticipantseconds.csv");

			//List<Tuple<string, string>> logsCountPerParticipant = GetLogsCountPerParticipant(data.Item2, LogType.OccurrenceFindSimilar);
			//TupleToCSV(logsCountPerParticipant, root + "autofinderlogs.csv");

			//List<Tuple<string, string>> timeTakenPerFile = GetTimeTakenPerFile(data.Item2);
			//TupleToCSV(timeTakenPerFile, root + "timefile.csv");

			//Dictionary<string, double[,]> confMatrix = GetConfusionMatrix(data.Item1);
			//OutputConfusionMatrix(confMatrix["bach1"], "hFsc_bach1");
			//OutputConfusionMatrix(confMatrix["bach2"], "hFsc_bach2");
			//OutputConfusionMatrix(confMatrix["bee1"], "hFsc_bee1");
			//OutputConfusionMatrix(confMatrix["hay1"], "hFsc_hay1");
			//OutputConfusionMatrix(confMatrix["mo155"], "hFsc_mo155");
			//OutputConfusionMatrix(confMatrix["mo458"], "hFsc_mo458");

			//List<Tuple<string, List<Tuple<int, double>>>> averageAgreement = GetAverageAgreementOfAnnotators(confMatrix);
			//List<Tuple<string, string>> averageAgreementPerFile = GetAverageAgreementPerFile(averageAgreement);
			//List<Tuple<string, string>> averageAgreementPerAnnotator = GetAverageAgreementPerAnnotator(averageAgreement);

			//TupleToCSV(averageAgreementPerFile, root + "averageagreementperfileAUTOOUTLIERSREMOVED.csv");
			//TupleToCSV(averageAgreementPerAnnotator, root + "averageagreementperannotatorANOMIC.csv");
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

		private static Dictionary<string, Dictionary<string, List<List<Interval>>>> ImportHEMAN()
		{
			Console.WriteLine("- IMPORTING DATA:");

			Dictionary<string, Dictionary<string, List<List<Interval>>>> importedIntervals = new Dictionary<string, Dictionary<string, List<List<Interval>>>>();
			int fileCount = 0;

			foreach (string txtFile in Directory.EnumerateFiles(rootHEMAN))
			{
				Tuple<int, int> fileData = ParseHEMANFilename(Path.GetFileNameWithoutExtension(txtFile));
				int midiIndex = fileData.Item1 - 1;
				int annotator = fileData.Item2 - 1;

				Console.Write("-- File: " + Path.GetFileNameWithoutExtension(txtFile) + "...\t");

				if (importedIntervals.ContainsKey("" + annotator))
				{
					importedIntervals["" + annotator].Add(midiNames[midiIndex], ParseHEMANIntervals(txtFile));
				}
				else
				{
					Dictionary<string, List<List<Interval>>> intervals = new Dictionary<string, List<List<Interval>>>();
					intervals.Add(midiNames[midiIndex], ParseHEMANIntervals(txtFile));
					importedIntervals.Add("" + annotator, intervals);
				}

				fileCount++;

				Console.WriteLine("Loaded!");
			}

			Console.WriteLine("- DATA IMPORTED. File count: " + fileCount);
			Console.WriteLine();

			return importedIntervals;
		}

		private static List<Tuple<string, string>> GetLogsCountPerParticipant(Dictionary<string, Dictionary<string, List<Log>>> logs, LogType type)
		{
			List<Tuple<string, string>> logsCountPerParticipant = new List<Tuple<string, string>>();

			foreach (KeyValuePair<string, Dictionary<string, List<Log>>> participantLogs in logs)
			{
				double totalLogsCount = 0;

				foreach (KeyValuePair<string, List<Log>> file in participantLogs.Value)
				{
					int logsCount = GetLogsCountOfType(file.Key, file.Value, type);
					totalLogsCount += logsCount;
				}

				Tuple<string, string> logsCountForParticipant = new Tuple<string, string>(participantLogs.Key, "" + totalLogsCount);
				logsCountPerParticipant.Add(logsCountForParticipant);
			}

			return logsCountPerParticipant;
		}
		private static int GetLogsCountOfType(string fileName, List<Log> fileLogs, LogType type)
		{
			int logsCount = 0;
			bool counting = false;

			for (int i = 0; i < fileLogs.Count; i++)
			{
				if (counting)
				{
					if (fileLogs[i].logType == type)
					{
						logsCount++;
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

			return logsCount;
		}

		private static List<List<Interval>> ParseHEMANIntervals(string filepath)
		{
			List<List<Interval>> intervals = new List<List<Interval>>();
			string line;

			using (StreamReader sr = new StreamReader(filepath))
			{
				while ((line = sr.ReadLine()) != null)
				{
					List<Interval> tempIntervals = new List<Interval>();
					Interval tempInterval = new Interval();

					string[] startEnd = line.Split(',');
					tempInterval.start = double.Parse(startEnd[0]);

					string[] endConf = startEnd[1].Split(':');
					tempInterval.end = double.Parse(endConf[0]);
					tempInterval.confidence = int.Parse(endConf[1]);

					tempIntervals.Add(tempInterval);
					intervals.Add(tempIntervals);
				}
			}

			return intervals;
		}

		private static Tuple<int, int> ParseHEMANFilename(string filename)
		{
			string fileNumString = "";
			string annoNumString = "";
			bool fileNumFound = false;

			foreach (char character in filename)
			{
				if (!fileNumFound)
				{
					if (Char.IsNumber(character))
					{
						fileNumString += character;
					}
					else
					{
						fileNumFound = true;
					}
				}
				else
				{
					if (Char.IsNumber(character))
					{
						annoNumString += character;
					}
				}
			}

			int fileNum = int.Parse(fileNumString);
			int annoNum = int.Parse(annoNumString);

			return new Tuple<int, int>(fileNum, annoNum);
		}

		private static Dictionary<string, double[,]> GetIntervalConfusionMatrix(Dictionary<string, Dictionary<string, List<Pattern>>> data, MatrixMode mode)
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
						List<List<Interval>> intervals1 = PatternsToIntervals(annotations.Value[files.Key]);
						List<List<Interval>> intervals2 = PatternsToIntervals(annotations2.Value[files.Key]);

						switch (mode)
						{
							case MatrixMode.PRECISION:
								confuMatrix[files.Key][participantCount1, participantCount2] = Math.Round(IntervalsPrecision(intervals1, intervals2), 2);
							break;

							case MatrixMode.RECALL:
								confuMatrix[files.Key][participantCount1, participantCount2] = Math.Round(IntervalsRecall(intervals1, intervals2), 2);
							break;

							case MatrixMode.FSCORE:
								confuMatrix[files.Key][participantCount1, participantCount2] = Math.Round(IntervalsFScore(intervals1, intervals2), 2);
							break;
						}
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

		private static Dictionary<string, double[,]> GetIntervalConfusionMatrix(Dictionary<string, Dictionary<string, List<List<Interval>>>> data, MatrixMode mode)
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

			foreach (KeyValuePair<string, Dictionary<string, List<List<Interval>>>> annotations in data)
			{
				foreach (KeyValuePair<string, Dictionary<string, List<List<Interval>>>> annotations2 in data)
				{
					foreach (KeyValuePair<string, List<List<Interval>>> files in annotations.Value)
					{
						switch (mode)
						{
							case MatrixMode.PRECISION:
								confuMatrix[files.Key][int.Parse(annotations.Key) - 1, int.Parse(annotations2.Key) - 1] = Math.Round(IntervalsPrecision(annotations.Value[files.Key], annotations2.Value[files.Key]), 2);
							break;

							case MatrixMode.RECALL:
								confuMatrix[files.Key][int.Parse(annotations.Key) - 1, int.Parse(annotations2.Key) - 1] = Math.Round(IntervalsRecall(annotations.Value[files.Key], annotations2.Value[files.Key]), 2);
							break;

							case MatrixMode.FSCORE:
								confuMatrix[files.Key][int.Parse(annotations.Key) - 1, int.Parse(annotations2.Key) - 1] = Math.Round(IntervalsFScore(annotations.Value[files.Key], annotations2.Value[files.Key]), 2);
							break;
						}
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

		private static double IntervalsRecall(List<List<Interval>> intervals1, List<List<Interval>> intervals2)
		{
			double recall = 0;
			bool found = false;

			for (int i = 0; i < intervals1.Count; i++)
			{
				for (int j = 0; j < intervals2.Count; j++)
				{
					for (int k = 0; k < intervals1[i].Count; k++)
					{
						for (int l = 0; l < intervals2[j].Count; l++)
						{
							double starts = Math.Abs(intervals1[i][k].start - intervals2[j][l].start);
							double ends = Math.Abs(intervals1[i][k].end - intervals2[j][l].end);

							if (starts + ends <= intervalMatchThreshold)
							{
								recall++;
								found = true;
								break;
							}
						}

						if (found)
						{
							break;
						}
					}

					if (found)
					{
						found = false;
						break;
					}
				}
			}

			recall /= intervals2.Count;

			return recall > 1 ? 1 : recall;
		}

		private static double IntervalsPrecision(List<List<Interval>> intervals1, List<List<Interval>> intervals2)
		{
			double precision = 0;
			bool found = false;

			for (int i = 0; i < intervals2.Count; i++)
			{
				for (int j = 0; j < intervals1.Count; j++)
				{
					for (int k = 0; k < intervals2[i].Count; k++)
					{
						for (int l = 0; l < intervals1[j].Count; l++)
						{
							double starts = Math.Abs(intervals2[i][k].start - intervals1[j][l].start);
							double ends = Math.Abs(intervals2[i][k].end - intervals1[j][l].end);

							if (starts + ends <= intervalMatchThreshold)
							{
								precision++;
								found = true;
								break;
							}
						}

						if (found)
						{
							break;
						}
					}

					if (found)
					{
						found = false;
						break;
					}
				}
			}

			precision /= intervals1.Count;

			return precision > 1 ? 1 : precision;
		}

		private static double IntervalsFScore(List<List<Interval>> intervals1, List<List<Interval>> intervals2)
		{
			double precision = IntervalsPrecision(intervals1, intervals2);
			double recall = IntervalsRecall(intervals1, intervals2);
			double fScore = 0;

			if (precision != 0 && recall != 0)
			{
				fScore = ((precision * recall) / (precision + recall)) * 2;
			}

			return fScore;
		}

		private static List<List<Interval>> PatternsToIntervals(List<Pattern> patterns)
		{
			List<List<Interval>> intervals = new List<List<Interval>>();

			foreach (Pattern pattern in patterns)
			{
				List<Interval> patternIntervals = new List<Interval>();

				foreach (Occurrence occurrence in pattern.GetOccurrences())
				{
					patternIntervals.Add(OccurrenceToInterval(occurrence));
				}

				if (patternIntervals.Count > 0)
				{
					intervals.Add(patternIntervals);
				}
			}

			return intervals;
		}

		private static Interval OccurrenceToInterval(Occurrence occ)
		{
			Interval interval = new Interval();
			double minTime = double.MaxValue;
			double maxTime = double.MinValue;
			
			foreach (NoteRect note in occ.highlightedNotes)
			{
				if (note.note.GetTime() < minTime)
				{
					minTime = note.note.GetTime();
				}

				if ((note.note.GetTime() + note.note.GetDuration()) > maxTime)
				{
					maxTime = note.note.GetTime() + note.note.GetDuration();
				}
			}

			interval.start = Math.Round(minTime / resolution, 1);
			interval.end = Math.Round(maxTime / resolution, 1);
			interval.confidence = occ.GetConfidence();

			return interval;
		}

		private static List<Tuple<string, string>> GetAverageAgreementPerFile(List<Tuple<string, List<Tuple<int, double>>>> averageAgreement)
		{
			List<Tuple<string, string>> agreementPerFile = new List<Tuple<string, string>>();

			foreach (Tuple<string, List<Tuple<int, double>>> fileAgreement in averageAgreement)
			{
				double average = 0;

				foreach (Tuple<int, double> annotatorAgreement in fileAgreement.Item2)
				{
					average += annotatorAgreement.Item2;
				}

				average /= fileAgreement.Item2.Count;

				agreementPerFile.Add(new Tuple<string, string>(fileAgreement.Item1, Math.Round(average, 2).ToString()));
			}

			return agreementPerFile;
		}

		private static List<Tuple<string, string>> GetAverageAgreementPerAnnotator(List<Tuple<string, List<Tuple<int, double>>>> averageAgreement)
		{
			List<Tuple<string, string>> agreementPerAnnotator = new List<Tuple<string, string>>();

			for (int i = 0; i < averageAgreement[0].Item2.Count; i++)
			{
				double average = 0;

				for (int j = 0; j < averageAgreement.Count; j++)
				{
					average += averageAgreement[j].Item2[i].Item2;
				}

				average /= averageAgreement.Count;

				agreementPerAnnotator.Add(new Tuple<string, string>(averageAgreement[0].Item2[i].Item1.ToString(), Math.Round(average, 2).ToString()));
			}

			return agreementPerAnnotator;
		}
		private static List<Tuple<string, List<Tuple<int, double>>>> GetAverageAgreementOfAnnotators(Dictionary<string, double[,]> confMatrices)
		{
			List<Tuple<string, List<Tuple<int, double>>>> averageAgreement = new List<Tuple<string, List<Tuple<int, double>>>>();

			foreach (KeyValuePair<string, double[,]> confMatrix in confMatrices)
			{
				averageAgreement.Add(new Tuple<string, List<Tuple<int, double>>>(confMatrix.Key, GetAverageAgreementOfAnnotators(confMatrix.Value)));
			}

			return averageAgreement;
		}

		private static List<Tuple<int, double>> GetAverageAgreementOfAnnotators(double[,] confMatrix)
		{
			List<Tuple<int, double>> averageAgreement = new List<Tuple<int, double>>();

			for (int i = 0; i < confMatrix.GetLength(0); i++)
			{
				averageAgreement.Add(new Tuple<int, double>(i, GetAverageAgreementOfAnnotator(confMatrix, i)));
			}

			return averageAgreement;
		}

		private static double GetAverageAgreementOfAnnotator(double[,] confMatrix, int index)
		{
			double totalAgreement = 0;
			int totalValues = 0;
			
			for (int i = 0; i < confMatrix.GetLength(0); i++)
			{
				totalValues++;
				totalAgreement += confMatrix[i, index];
			}

			for (int i = 0; i < confMatrix.GetLength(1); i++)
			{
				totalValues++;
				totalAgreement += confMatrix[index, i];
			}
			

			return totalAgreement / totalValues;
		}

		private static void OutputConfusionMatrix(double[,] confMatrix, string title)
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
					Point location = new Point((j + 1) * size, (i + 1) * size);
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
			saveImage.Save(matrixSaveFolder + title + ".png", ImageFormat.Png);
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

        private static void TupleToCSV<T>(List<Tuple<string, string, string, string>> tuples, string filename)
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