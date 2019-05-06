using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public string midiName;
        public string midiDuration;
        public string curatorName;
        public string curatorEmail;

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
        }

        public FileParser(List<Pattern> patternsIn)
        {
            patterns = patternsIn;
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

                textLines.Last().Remove(textLines.Last().Count() - 1); // Removing the comma of the last value.
                textLines.Add(whitespace +  "},");
            }

            return textLines.ToArray();
        }

        private string[] FileMetaDataToText()
        {
            List<string> textLines = new List<string>();

            textLines.Add("  \"file_metadata\": {");
            textLines.Add("    \"duration\": " + jamsParse.fileMetaData.duration + ",");
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
                    textLines.AddRange(AnnotationGroupToText(annotations));
                    textLines.Add("    },");
                }

                textLines.Last().Remove(textLines.Last().Count() - 1); // Removing the comma of the last value.

                textLines.Add("  ]");
            }

            return textLines.ToArray();
        }

        private string[] AnnotationGroupToText(Annotations annotations)
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

                textLines.Last().Remove(textLines.Last().Count() - 1); // Removing the comma of the last value.
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

            textLines.Add("          \"confidence\": " + data.confidence + ",");
            textLines.Add("          \"time\": " + data.time + ",");
            textLines.Add("          \"duration\": " + data.duration);

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
            textLines.Add("  }");

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
                        annotationData.time = "" + occurrence.GetStart();
                        annotationData.duration = "" + (occurrence.GetEnd() - occurrence.GetStart());
                        annotationData.confidence = "" + occurrence.GetConfidence();
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