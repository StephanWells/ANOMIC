using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class FileParser
    {
        private const string jamsVersion = "0.3.3";
        private const string jamsStartEndNamespace = "pattern";
        private const string jamsNotesNamespace = "pattern_jku";

        private string file;
        private JAMSObject jamsParse;
        public List<Pattern> patterns;
        public List<Log> logs;
        public string midiName;
        public string midiDuration;
        public string curatorName;
        public string curatorEmail;
        NumberFormatInfo nfi;
        CultureInfo customCulture;

        private struct JAMSObject
        {
            public FileMetaData fileMetaData; // Describes the audio file to which these annotations are attached.
            public List<Annotations> annotations; // A list of Annotation objects (described below).
            public Dictionary<string, string> sandbox; // An unrestricted place to store any additional data.
        }

        private struct FileMetaData
        {
            public Dictionary<string, string> identifiers; // An unstructured sandbox-type object for storing identifier mappings, e.g., MusicBrainz ID.
            public string artist; // Metadata string for the track in question.
            public string title; // Metadata string for the track in question.
            public string release; // Metadata string for the track in question.
            public string duration; // Non-negative number describing the length (in seconds) of the track.
            public string jams_version; // String describing the JAMS version for this file.
        }

        private struct Annotations
        {
            public string jamsNamespace; // A string describing the type of this annotation.
            public List<Data> data; // A list of observations (described below).
            public AnnotationMetaData annotationMetaData; // Describes the annotation (described below).
            public Dictionary<string, string> sandbox; // Additional unstructured storage space for this annotation.
            public string time; // Optional non-negative number indicating the beginning point at which this annotation is valid.
            public string duration; // Optional non-negative number indicating the duration of the valid portion of this annotation.
        }

        private struct Data
        {
            public string time; // Non-negative number denoting the time of the observation (in seconds).
            public string duration; // Non-negative number denoting the duration of the observation (in seconds).
            public Dictionary<string, string> value; // Actual annotation (e.g., chord, segment label).
            public string confidence; // Certainty of the annotation.
        }

        private struct AnnotationMetaData
        {
            public string corpus; // A string describing a corpus to which this annotation belongs.
            public string version; // String or number, the version of this annotation.
            public Curator curator; // A structured object containing contact information (name and email) for the curator of this data.
            public Dictionary<string, string> annotator; // A sandbox object to describe the individual annotator — which can be a person or a program — that generated this annotation.
            public string annotation_tools; // String to describe the process by which annotations were collected and pre-processed.
            public string annotation_rules; // String to describe the process by which annotations were collected and pre-processed.
            public string validation; // String to describe the process by which annotations were collected and pre-processed.
            public string data_source; // String describing the type of annotator, e.g., “program”, “expert human”, “crowdsource”.
        }

        private struct Curator
        {
            public string name; // A string of the name of the annotator.
            public string email; // A string of the email of the annotator.
        }

        public FileParser(string fileIn)
        {
            file = fileIn;
            midiDuration = "";
            curatorName = "";
            curatorEmail = "";

            nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";

            CultureInfo customCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = customCulture;
        }

        public FileParser(List<Pattern> patternsIn, string midiName, string midiDuration)
        {
            patterns = patternsIn;
            this.midiName = midiName;
            this.midiDuration = midiDuration;
            logs = new List<Log>();

            nfi = new NumberFormatInfo();
            nfi.NumberDecimalSeparator = ".";

            CultureInfo customCulture = (CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            Thread.CurrentThread.CurrentCulture = customCulture;
        }

		public FileParser(List<Pattern> patternsIn, double resolution, string midiName, string midiDuration)
		{
			patterns = new List<Pattern>();
            this.midiName = midiName;
            this.midiDuration = Math.Round(double.Parse(midiDuration) / resolution, 2).ToString();

            foreach (Pattern pattern in patternsIn)
			{
				Pattern newPattern = new Pattern();

				foreach (Occurrence occurrence in pattern.GetOccurrences())
				{
					Occurrence newOccurrence = new Occurrence();
					newOccurrence.isNotesMode = occurrence.isNotesMode;
					newOccurrence.SetConfidence(occurrence.GetConfidence());

					if (newOccurrence.isNotesMode)
					{
						for (int i = 0; i < occurrence.highlightedNotes.Count; i++)
						{
							Note oldNote = occurrence.highlightedNotes[i].note;

							NoteRect noteRect = new NoteRect();
							noteRect.note = new Note(oldNote.GetPitch(), Math.Round(oldNote.GetTime() / resolution, 2), Math.Round(oldNote.GetDuration() / resolution, 2), oldNote.GetChannel(), oldNote.GetVelocity());
							newOccurrence.highlightedNotes.Add(noteRect);
						}
					}
					else
					{
						newOccurrence.SetStart(Math.Round(occurrence.GetStart() / resolution, 2));
						newOccurrence.SetEnd(Math.Round(occurrence.GetEnd() / resolution, 2));
					}

					newPattern.AddOccurrence(newOccurrence);
				}

				patterns.Add(newPattern);
			}

			logs = new List<Log>();

			nfi = new NumberFormatInfo();
			nfi.NumberDecimalSeparator = ".";

			CultureInfo customCulture = (CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
			customCulture.NumberFormat.NumberDecimalSeparator = ".";

			Thread.CurrentThread.CurrentCulture = customCulture;
		}

		public void ParseFile()
        {
            FileToJAMS();
            JAMSToPatterns();
            JAMSToLogs();
        }

        private void FileToJAMS()
        {
            int index = 0;
            bool sandBoxFound = false;
            bool fileMetaDataFound = false;
            bool annotationsFound = false;

            file = file.Replace(" ", String.Empty);
            file = file.Replace("\n", String.Empty);
            file = file.Replace("\r", String.Empty);

            while (index < file.Length - 2)
            {
                Tuple<string, int> wordResults = ReadNextWord(index);
                string nextWord = wordResults.Item1;
                index = wordResults.Item2;

                switch (nextWord)
                {
                    case "sandbox":
                        if (!sandBoxFound)
                        {
                            sandBoxFound = true;
                            jamsParse.sandbox = new Dictionary<string, string>();
                            Tuple<Dictionary<string, string>, int> dictResults = ReadDictionary(index);

                            foreach (KeyValuePair<string, string> dictEntry in dictResults.Item1)
                            {
                                jamsParse.sandbox.Add(dictEntry.Key, dictEntry.Value);
                            }

                            index = dictResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "file_metadata":
                        if (!fileMetaDataFound)
                        {
                            fileMetaDataFound = true;
                            Tuple<FileMetaData, int> fileMetaDataResults = ReadFileMetaData(index);
                            jamsParse.fileMetaData = fileMetaDataResults.Item1;
                            index = fileMetaDataResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "annotations":
                        if (!annotationsFound)
                        {
                            annotationsFound = true;
                            Tuple<List<Annotations>, int> annotationsListResults = ReadAnnotationsList(index);
                            jamsParse.annotations = annotationsListResults.Item1;
                            index = annotationsListResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    default:
                        throw new InvalidOperationException("Corrupt JAMS file.");
                }
            }
        }

        private Tuple<FileMetaData, int> ReadFileMetaData(int index)
        {
            FileMetaData fileMetaData = new FileMetaData();
            bool durationFound, titleFound, releaseFound, identifiersFound, artistFound, jams_versionFound;
            durationFound = titleFound = releaseFound = identifiersFound = artistFound = jams_versionFound = false;

            while (file[index] != '{')
            {
                index++;
            }

            index++;

            while (file[index] != '}')
            {
                Tuple<string, int> wordResults = ReadNextWord(index);
                string nextWord = wordResults.Item1;
                index = wordResults.Item2;

                switch (nextWord)
                {
                    case "duration":
                        if (!durationFound)
                        {
                            durationFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            fileMetaData.duration = entryResults.Item1;
                            index = entryResults.Item2;
                            midiDuration = fileMetaData.duration;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "title":
                        if (!titleFound)
                        {
                            titleFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            fileMetaData.title = entryResults.Item1;
                            index = entryResults.Item2;
                            midiName = fileMetaData.title;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "release":
                        if (!releaseFound)
                        {
                            releaseFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            fileMetaData.release = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "identifiers":
                        if (!identifiersFound)
                        {
                            identifiersFound = true;
                            fileMetaData.identifiers = new Dictionary<string, string>();
                            Tuple<Dictionary<string, string>, int> dictResults = ReadDictionary(index);

                            foreach (KeyValuePair<string, string> dictEntry in dictResults.Item1)
                            {
                                fileMetaData.identifiers.Add(dictEntry.Key, dictEntry.Value);
                            }

                            index = dictResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "artist":
                        if (!artistFound)
                        {
                            artistFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            fileMetaData.artist = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "jams_version":
                        if (!jams_versionFound)
                        {
                            jams_versionFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            fileMetaData.jams_version = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    default:
                        throw new InvalidOperationException("Corrupt JAMS file.");
                }
            }

            index++;

            return new Tuple<FileMetaData, int>(fileMetaData, index);
        }

        private Tuple<List<Annotations>, int> ReadAnnotationsList(int index)
        {
            List<Annotations> annotationsList = new List<Annotations>();

            while (file[index] != '[')
            {
                index++;
            }

            index++;

            while (file[index] != ']')
            {
                Tuple<Annotations, int> annotationsResults = ReadAnnotations(index);
                annotationsList.Add(annotationsResults.Item1);
                index = annotationsResults.Item2;
            }

            return new Tuple<List<Annotations>, int>(annotationsList, index);
        }

        private Tuple<Annotations, int> ReadAnnotations(int index)
        {
            Annotations annotations = new Annotations();
            bool sandBoxFound, durationFound, dataFound, namespaceFound, timeFound, annotation_metaDataFound;
            sandBoxFound = durationFound = dataFound = namespaceFound = timeFound = annotation_metaDataFound = false;

            while (file[index] != '{')
            {
                index++;
            }

            index++;

            while (file[index] != '}')
            {
                Tuple<string, int> wordResults = ReadNextWord(index);
                string nextWord = wordResults.Item1;
                index = wordResults.Item2;

                switch (nextWord)
                {
                    case "sandbox":
                        if (!sandBoxFound)
                        {
                            sandBoxFound = true;
                            annotations.sandbox = new Dictionary<string, string>();
                            Tuple<Dictionary<string, string>, int> dictResults = ReadDictionary(index);

                            foreach (KeyValuePair<string, string> dictEntry in dictResults.Item1)
                            {
                                annotations.sandbox.Add(dictEntry.Key, dictEntry.Value);
                            }

                            index = dictResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "duration":
                        if (!durationFound)
                        {
                            durationFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotations.duration = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "data":
                        if (!dataFound)
                        {
                            dataFound = true;
                            annotations.data = new List<Data>();
                            Tuple<List<Data>, int> entryResults = ReadDataList(index);
                            
                            foreach (Data data in entryResults.Item1)
                            {
                                annotations.data.Add(data);
                            }

                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "namespace":
                        if (!namespaceFound)
                        {
                            namespaceFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotations.jamsNamespace = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "time":
                        if (!timeFound)
                        {
                            timeFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotations.time = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "annotation_metadata":
                        if (!annotation_metaDataFound)
                        {
                            annotation_metaDataFound = true;
                            Tuple<AnnotationMetaData, int> entryResults = ReadAnnotationMetaData(index);
                            annotations.annotationMetaData = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    default:
                        throw new InvalidOperationException("Corrupt JAMS file.");
                }
            }

            index++;

            return new Tuple<Annotations, int>(annotations, index);
        }

        private Tuple<List<Data>, int> ReadDataList(int index)
        {
            List<Data> dataList = new List<Data>();

            while (file[index] != '[')
            {
                index++;
            }

            index++;

            while (file[index] != ']')
            {
                Tuple<Data, int> dataResults = ReadData(index);
                dataList.Add(dataResults.Item1);
                index = dataResults.Item2;
            }

            return new Tuple<List<Data>, int>(dataList, index);
        }

        private Tuple<Data, int> ReadData(int index)
        {
            Data data = new Data();
            bool timeFound, durationFound, valueFound, confidenceFound;
            timeFound = durationFound = valueFound = confidenceFound = false;

            while (file[index] != '{')
            {
                index++;
            }

            index++;

            while (file[index] != '}')
            {
                Tuple<string, int> wordResults = ReadNextWord(index);
                string nextWord = wordResults.Item1;
                index = wordResults.Item2;

                switch (nextWord)
                {
                    case "time":
                        if (!timeFound)
                        {
                            timeFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            data.time = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "duration":
                        if (!durationFound)
                        {
                            durationFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            data.duration = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "value":
                        if (!valueFound)
                        {
                            valueFound = true;
                            data.value = new Dictionary<string, string>();
                            Tuple<Dictionary<string, string>, int> dictResults = ReadDictionary(index);

                            foreach (KeyValuePair<string, string> dictEntry in dictResults.Item1)
                            {
                                data.value.Add(dictEntry.Key, dictEntry.Value);
                            }

                            index = dictResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "confidence":
                        if (!confidenceFound)
                        {
                            confidenceFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            data.confidence = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    default:
                        throw new InvalidOperationException("Corrupt JAMS file.");
                }
            }

            index++;

            return new Tuple<Data, int>(data, index);
        }

        private Tuple<AnnotationMetaData, int> ReadAnnotationMetaData(int index)
        {
            AnnotationMetaData annotationMetaData = new AnnotationMetaData();
            bool corpusFound, validationFound, annotation_toolsFound, versionFound, curatorFound, annotation_rulesFound, annotatorFound, data_sourceFound;
            corpusFound = validationFound = annotation_toolsFound = versionFound = curatorFound = annotation_rulesFound = annotatorFound = data_sourceFound = false;

            while (file[index] != '{')
            {
                index++;
            }

            index++;

            while (file[index] != '}')
            {
                Tuple<string, int> wordResults = ReadNextWord(index);
                string nextWord = wordResults.Item1;
                index = wordResults.Item2;

                switch (nextWord)
                {
                    case "corpus":
                        if (!corpusFound)
                        {
                            corpusFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotationMetaData.corpus = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "validation":
                        if (!validationFound)
                        {
                            validationFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotationMetaData.validation = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "annotation_tools":
                        if (!annotation_toolsFound)
                        {
                            annotation_toolsFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotationMetaData.annotation_tools = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "version":
                        if (!versionFound)
                        {
                            versionFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotationMetaData.version = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "curator":
                        if (!curatorFound)
                        {
                            curatorFound = true;

							// Catering for incorrectly-formatted curators.

							int tempIndex = index + 1;

							while (file[tempIndex] != '{' && file[tempIndex] != '"' && file[tempIndex] != ',')
							{
								tempIndex++;
							}

							if (file[tempIndex] == '{')
							{
								Tuple<Curator, int> entryResults = ReadCurator(index);
								annotationMetaData.curator = entryResults.Item1;
								index = entryResults.Item2;
							}
							else if (file[tempIndex] == '"' || file[tempIndex] == ',')
							{
								Tuple<string, int> entryResults = ReadNextEntry(index);

								Curator curator = new Curator();
								curator.name = entryResults.Item1;
								curator.email = "";
								annotationMetaData.curator = curator;
								index = entryResults.Item2;
							}
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "annotation_rules":
                        if (!annotation_rulesFound)
                        {
                            annotation_rulesFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotationMetaData.annotation_rules = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "annotator":
                        if (!annotatorFound)
                        {
                            annotatorFound = true;
                            annotationMetaData.annotator = new Dictionary<string, string>();

							int tempIndex = index + 1;

							while (file[tempIndex] != '{' && file[tempIndex] != '"' && file[tempIndex] != ',')
							{
								tempIndex++;
							}

							if (file[tempIndex] == '{')
							{
								Tuple<Dictionary<string, string>, int> dictResults = ReadDictionary(index);

								foreach (KeyValuePair<string, string> dictEntry in dictResults.Item1)
								{
									annotationMetaData.annotator.Add(dictEntry.Key, dictEntry.Value);
								}

								index = dictResults.Item2;
							}
							else if (file[tempIndex] == '"' || file[tempIndex] == ',')
							{
								Tuple<string, int> entryResults = ReadNextEntry(index);

								annotationMetaData.annotator.Add("0", entryResults.Item1);
								index = entryResults.Item2;
							}
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "data_source":
                        if (!data_sourceFound)
                        {
                            data_sourceFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            annotationMetaData.data_source = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    default:
                        throw new InvalidOperationException("Corrupt JAMS file.");
                }
            }

            index++;

            return new Tuple<AnnotationMetaData, int>(annotationMetaData, index);
        }

        private Tuple<Curator, int> ReadCurator(int index)
        {
            Curator curator = new Curator();
            bool nameFound, emailFound;
            nameFound = emailFound = false;

            while (file[index] != '{')
            {
                index++;
            }

            index++;

            while (file[index] != '}')
            {
                Tuple<string, int> wordResults = ReadNextWord(index);
                string nextWord = wordResults.Item1;
                index = wordResults.Item2;

                switch (nextWord)
                {
                    case "name":
                        if (!nameFound)
                        {
                            nameFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            curator.name = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;

                    case "email":
                        if (!emailFound)
                        {
                            emailFound = true;
                            Tuple<string, int> entryResults = ReadNextEntry(index);
                            curator.email = entryResults.Item1;
                            index = entryResults.Item2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Corrupt JAMS file.");
                        }
                    break;
                    
                    default:
                        throw new InvalidOperationException("Corrupt JAMS file.");
                }
            }

            index++;

            return new Tuple<Curator, int>(curator, index);
        }

        private Tuple<Dictionary<string, string>, int> ReadDictionary(int index)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            
            while (file[index] != '{')
            {
                index++;
            }

            index++;

            while (file[index] != '}')
            {
                Tuple<string, int> keyResults = ReadNextWord(index);
                index = keyResults.Item2;
                Tuple<string, int> entryResults = ReadNextEntry(index);
                index = entryResults.Item2;
                dict.Add(keyResults.Item1, entryResults.Item1);
            }

            index++;

            return new Tuple<Dictionary<string, string>, int>(dict, index);
        }

        private Tuple<string, int> ReadNextWord(int index)
        {
            string word = "";

            while (file[index] != '\"')
            {
                index++;
            }

            index++;

            while (file[index] != '\"')
            {
                word += file[index];
                index++;
            }

            return new Tuple<string, int>(word, index);
        }

        private Tuple<string, int> ReadNextEntry(int index)
        {
            string entry = "";

            while (file[index] != ':')
            {
                index++;
            }

            index++;

            while (file[index] != ',' && file[index] != '}')
            {
                entry += file[index];
                index++;
            }

            entry = entry.Replace("\"", String.Empty);

            return new Tuple<string, int>(entry, index);
        }

        private void JAMSToPatterns()
        {
            patterns = new List<Pattern>();

            foreach (Annotations annotations in jamsParse.annotations)
            {
                foreach (Data data in annotations.data)
                {
                    int patternIndex = Int32.Parse(data.value["pattern_id"], NumberStyles.Any, customCulture);
                    int occurrenceIndex = Int32.Parse(data.value["occurrence_id"], NumberStyles.Any, customCulture);
                    int confidence = Int32.Parse(data.confidence, NumberStyles.Any, customCulture);
                    double start = Double.Parse(data.time, NumberStyles.Any, customCulture);
                    double duration = Double.Parse(data.duration, NumberStyles.Any, customCulture);

                    while (patternIndex >= patterns.Count)
                    {
                        patterns.Add(new Pattern());
                    }

                    while (occurrenceIndex >= patterns[patternIndex].GetOccurrences().Count)
                    {
                        patterns[patternIndex].AddOccurrence(new Occurrence());
                    }

                    patterns[patternIndex].GetOccurrences()[occurrenceIndex].SetConfidence(confidence);

                    if (annotations.jamsNamespace == jamsNotesNamespace)
                    {
                        int pitch = Int32.Parse(data.value["midi_pitch"]);
                        int channel = Int32.Parse(data.value["staff"]);

                        NoteRect noteRect = new NoteRect();
                        noteRect.note = new Note((NotePitch)pitch, start, duration, (uint)channel, 127);

                        patterns[patternIndex].GetOccurrences()[occurrenceIndex].highlightedNotes.Add(noteRect);
                    }
                    else if (annotations.jamsNamespace == jamsStartEndNamespace)
                    {
                        patterns[patternIndex].GetOccurrences()[occurrenceIndex].SetStart(start);
                        patterns[patternIndex].GetOccurrences()[occurrenceIndex].SetEnd(start + duration);
                    }
                }
            }
        }

        private void JAMSToLogs()
        {
            logs = new List<Log>();
            
            foreach (KeyValuePair<string, string> sandboxElement in jamsParse.sandbox)
            {
                Log tempLog = new Log();
                string[] logElements = sandboxElement.Value.Split(';');

                foreach (string logElement in logElements)
                {
                    int stringIndex = 0;
                    string tempString = "";

                    while (logElement[stringIndex] != ':')
                    {
                        tempString += logElement[stringIndex];
                        stringIndex++;
                    }

                    stringIndex++;
                    string tempString2 = "";

                    while (stringIndex != logElement.Length)
                    {
                        tempString2 += logElement[stringIndex];
                        stringIndex++;
                    }

                    switch (tempString)
                    {
                        case "type":
                            Enum.TryParse(tempString2, out tempLog.logType);
                        break;

                        case "time":
                            TimeSpan.TryParse(tempString2, out tempLog.time);
                        break;

                        case "value":
                            tempLog.value = tempString2;
                        break;
                    }
                }

                logs.Add(tempLog);
            }
        }

        public string[] ParseToJAMS()
        {
            PopulateFileData();
            PopulateAnnotationsData();
            string[] jamsParseText = DataToText();

            return jamsParseText;
        }

        private string[] DataToText()
        {
            List<string> textLines = new List<string>();

            textLines.Add("{");
            textLines.AddRange(JAMSToText());
            textLines.Add("}");

            return textLines.ToArray();
        }

        private string[] JAMSToText()
        {
            List<string> textLines = new List<string>();

            textLines.AddRange(DictionaryToText(jamsParse.sandbox, "sandbox", 1));
            textLines.AddRange(FileMetaDataToText());
            textLines.AddRange(AnnotationsToText());

            return textLines.ToArray();
        }

        private string[] DictionaryToText(Dictionary<string, string> dict, string dictName, int indentLevel)
        {
            List<string> textLines = new List<string>();

            string whitespace = "";

            for (int i = 0; i < indentLevel; i++)
            {
                whitespace += "  ";
            }

            if (dict.Count == 0)
            {
                textLines.Add(whitespace + "\"" + dictName + "\": {},");
            }
            else
            {
                textLines.Add(whitespace + "\"" + dictName + "\": {");

                foreach (KeyValuePair<string, string> dictLine in dict)
                {
                    textLines.Add(whitespace + "  \"" + dictLine.Key + "\": \"" + dictLine.Value + "\",");
                }

                textLines[textLines.Count() - 1] = textLines.Last().Remove(textLines.Last().Count() - 1); // Removing the comma of the last value.
                textLines.Add(whitespace +  "},");
            }

            return textLines.ToArray();
        }

        private string[] FileMetaDataToText()
        {
            List<string> textLines = new List<string>();

            textLines.Add("  \"file_metadata\": {");
            textLines.Add("    \"duration\": " + jamsParse.fileMetaData.duration.ToString(customCulture) + ",");
            textLines.Add("    \"title\": \"" + jamsParse.fileMetaData.title + "\",");
            textLines.Add("    \"release\": \"" + jamsParse.fileMetaData.release + "\",");
            textLines.AddRange(DictionaryToText(jamsParse.fileMetaData.identifiers, "identifiers", 2));
            textLines.Add("    \"artist\": \"" + jamsParse.fileMetaData.artist + "\",");
            textLines.Add("    \"jams_version\": \"" + jamsParse.fileMetaData.jams_version + "\"");
            textLines.Add("  },");

            return textLines.ToArray();
        }

        private string[] AnnotationsToText()
        {
            List<string> textLines = new List<string>();

            if (jamsParse.annotations.Count == 0)
            {
                textLines.Add("  \"annotations\": []");
            }
            else
            {
                textLines.Add("  \"annotations\": [");
                
                foreach (Annotations annotations in jamsParse.annotations)
                {
                    textLines.Add("    {");
                    textLines.AddRange(AnnotationListToText(annotations));
                    textLines.Add("    },");
                }

                textLines[textLines.Count() - 1] = textLines.Last().Remove(textLines.Last().Count() - 1); // Removing the comma of the last value.
                textLines.Add("  ]");
            }

            return textLines.ToArray();
        }

        private string[] AnnotationListToText(Annotations annotations)
        {
            List<string> textLines = new List<string>();

            textLines.AddRange(DictionaryToText(annotations.sandbox, "sandbox", 3));
            textLines.Add("      \"duration\": " + annotations.duration + ",");
            
            if (annotations.data.Count == 0)
            {
                textLines.Add("      \"data\": [],");
            }
            else
            {
                textLines.Add("      \"data\": [");

                foreach (Data data in annotations.data)
                {
                    textLines.Add("        {");
                    textLines.AddRange(AnnotationDataToText(data));
                    textLines.Add("        },");
                }

                textLines[textLines.Count() - 1] = textLines.Last().Remove(textLines.Last().Count() - 1); // Removing the comma of the last value.
                textLines.Add("      ],");
            }

            textLines.Add("      \"namespace\": \"" + annotations.jamsNamespace + "\",");
            textLines.Add("      \"time\": " + annotations.time + ",");
            textLines.AddRange(AnnotationMetaDataToText(annotations.annotationMetaData));

            return textLines.ToArray();
        }

        private string[] AnnotationDataToText(Data data)
        {
            List<string> textLines = new List<string>();

            if (data.value.Count == 0)
            {
                textLines.Add("          \"value\": null,");
            }
            else
            {
                textLines.AddRange(DictionaryToText(data.value, "value", 5));
            }

            textLines.Add("          \"confidence\": " + data.confidence.ToString() + ",");
            textLines.Add("          \"time\": " + data.time.ToString() + ",");
            textLines.Add("          \"duration\": " + data.duration.ToString());

            return textLines.ToArray();
        }

        private string[] AnnotationMetaDataToText(AnnotationMetaData annotationMetaData)
        {
            List<string> textLines = new List<string>();

            textLines.Add("      \"annotation_metadata\": {");
            textLines.Add("        \"corpus\": \"" + annotationMetaData.corpus + "\",");
            textLines.Add("        \"validation\": \"" + annotationMetaData.validation + "\",");
            textLines.Add("        \"annotation_tools\": \"" + annotationMetaData.annotation_tools + "\",");
            textLines.Add("        \"version\": \"" + annotationMetaData.version + "\",");
            textLines.AddRange(CuratorToText(annotationMetaData.curator));
            textLines.Add("        \"annotation_rules\": \"" + annotationMetaData.annotation_rules + "\",");
            textLines.AddRange(DictionaryToText(annotationMetaData.annotator, "annotator", 4));
            textLines.Add("        \"data_source\": \"" + annotationMetaData.data_source + "\"");
            textLines.Add("      }");

            return textLines.ToArray();
        }

        private string[] CuratorToText(Curator curator)
        {
            List<string> textLines = new List<string>();

            textLines.Add("        \"curator\": {");
            textLines.Add("          \"name\": \"" + curator.name + "\",");
            textLines.Add("          \"email\": \"" + curator.email + "\"");
            textLines.Add("        },");

            return textLines.ToArray();
        }

        private void PopulateFileData()
        {
            jamsParse = new JAMSObject();
            jamsParse.fileMetaData = new FileMetaData();
            jamsParse.fileMetaData.identifiers = new Dictionary<string, string>();
            jamsParse.fileMetaData.artist = "";
            jamsParse.fileMetaData.title = midiName;
            jamsParse.fileMetaData.release = "";
            jamsParse.fileMetaData.duration = midiDuration;
            jamsParse.fileMetaData.jams_version = jamsVersion;

            jamsParse.annotations = new List<Annotations>();

            jamsParse.sandbox = new Dictionary<string, string>();

            for (int i = 0; i < logs.Count; i++)
            {
                jamsParse.sandbox.Add("Log" + i, logs[i].ToString());
            }
        }

        private void PopulateAnnotationsData()
        {
            Annotations startEndAnnotations = new Annotations();
            Annotations notesAnnotations = new Annotations();

            startEndAnnotations.jamsNamespace = jamsStartEndNamespace;
            startEndAnnotations.data = new List<Data>();

            startEndAnnotations.annotationMetaData = new AnnotationMetaData();
            startEndAnnotations.annotationMetaData.corpus = "";
            startEndAnnotations.annotationMetaData.version = "1";
            startEndAnnotations.annotationMetaData.curator = new Curator();
            startEndAnnotations.annotationMetaData.curator.name = curatorName;
            startEndAnnotations.annotationMetaData.curator.email = curatorEmail;
            startEndAnnotations.annotationMetaData.annotation_tools = "";
            startEndAnnotations.annotationMetaData.annotation_rules = "";
            startEndAnnotations.annotationMetaData.validation = "";
            startEndAnnotations.annotationMetaData.data_source = "";
            startEndAnnotations.annotationMetaData.annotator = new Dictionary<string, string>();

            startEndAnnotations.sandbox = new Dictionary<string, string>();
            startEndAnnotations.time = "0";
            startEndAnnotations.duration = midiDuration;

            notesAnnotations.jamsNamespace = jamsNotesNamespace;
            notesAnnotations.data = new List<Data>();

            notesAnnotations.annotationMetaData = new AnnotationMetaData();
            notesAnnotations.annotationMetaData.corpus = "";
            notesAnnotations.annotationMetaData.version = "1";
            notesAnnotations.annotationMetaData.curator = new Curator();
            notesAnnotations.annotationMetaData.curator.name = curatorName;
            notesAnnotations.annotationMetaData.curator.email = curatorEmail;
            notesAnnotations.annotationMetaData.annotation_tools = "";
            notesAnnotations.annotationMetaData.annotation_rules = "";
            notesAnnotations.annotationMetaData.validation = "";
            notesAnnotations.annotationMetaData.data_source = "";
            notesAnnotations.annotationMetaData.annotator = new Dictionary<string, string>();

            notesAnnotations.sandbox = new Dictionary<string, string>();
            notesAnnotations.time = "0";
            notesAnnotations.duration = midiDuration;

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                for (int occurrenceIndex = 0; occurrenceIndex < patterns[patternIndex].GetOccurrences().Count; occurrenceIndex++)
                {
                    Occurrence occurrence = patterns[patternIndex].GetOccurrences()[occurrenceIndex];

                    if (!occurrence.isNotesMode)
                    {
                        Data annotationData = new Data();
                        annotationData.time = "" + occurrence.GetStart().ToString(nfi);
                        annotationData.duration = "" + (occurrence.GetEnd() - occurrence.GetStart()).ToString(nfi);
                        annotationData.confidence = "" + occurrence.GetConfidence().ToString(nfi);
                        annotationData.value = new Dictionary<string, string>();
                        annotationData.value.Add("pattern_id", "" + patternIndex);
                        annotationData.value.Add("occurrence_id", "" + occurrenceIndex);

                        startEndAnnotations.data.Add(annotationData);
                    }
                    else
                    {
                        foreach (NoteRect noteRect in occurrence.highlightedNotes)
                        {
                            Data annotationData = new Data();
                            annotationData.time = "" + noteRect.note.GetTime();
                            annotationData.duration = "" + noteRect.note.GetDuration();
                            annotationData.confidence = "" + occurrence.GetConfidence();
                            annotationData.value = new Dictionary<string, string>();

                            annotationData.value.Add("pattern_id", "" + patternIndex);
                            annotationData.value.Add("midi_pitch", "" + (int)noteRect.note.GetPitch());
                            annotationData.value.Add("occurrence_id", "" + occurrenceIndex);
                            annotationData.value.Add("morph_pitch", "" + (int)noteRect.note.GetPitch());
                            annotationData.value.Add("staff", "" + noteRect.note.GetChannel());

                            notesAnnotations.data.Add(annotationData);
                        }
                    }
                }
            }

            jamsParse.annotations.Add(startEndAnnotations);
            jamsParse.annotations.Add(notesAnnotations);
        }
    }
}
