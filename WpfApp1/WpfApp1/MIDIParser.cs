using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class MIDIParser
    {
        public const int MICROSECONDS_PER_MINUTE = 60000000;

        private byte[] midi;
        public HeaderChunk header;
        public TrackChunk[] tracks;
        public List<Event> midiEvents = new List<Event>();

        public MIDIParser(byte[] file)
        {
            midi = file;
        }

        // Method to call to parse the MIDI file from beginning to end.
        public void ParseFile()
        {
            Console.WriteLine("Begin parsing!");
            int index = 0;

            Console.WriteLine("Parsing header...");
            index = ParseHeader(index);
            if (index == -1) throw new InvalidOperationException("Corrupt MIDI file.");

            Console.WriteLine("Parsing tracks...");
            tracks = new TrackChunk[header.trackNum];

            for (int i = 0; i < header.trackNum; i++)
            {
                index = ParseTrack(index, i);
                if (index == -1) throw new InvalidOperationException("Corrupt MIDI file.");
            }
        }

        private int ParseHeader(int index)
        {
            // Stores the ID of the header chunk. If the ID isn't "MThd", then the MIDI file is corrupted or in an incorrect format.
            char[] chunkID = new char[4];

            for (int i = 0; i < 4; i++)
            {
                chunkID[i] = (char)midi[index];
                index++;
            }

            Console.WriteLine("- Header chunk ID: " + chunkID[0] + chunkID[1] + chunkID[2] + chunkID[3]);

            if (!(new string(chunkID)).Equals("MThd")) return -1;

            // Stores the chunk size - always 6 for MIDI files.
            uint chunkSize = ByteArrayToUInt(SubArray(index, 4)); index += 4;

            Console.WriteLine("- Header chunk size: " + chunkSize);

            if (chunkSize != 6) return -1;

            // Store the format type and track number. If the format type is 0, then the MIDI file will consist of only one track. Otherwise, it will consist of multiple.
            uint formatType = ByteArrayToUInt(SubArray(index, 2)); index += 2;
            uint trackNum = ByteArrayToUInt(SubArray(index, 2)); index += 2;

            Console.WriteLine("- Format type: " + formatType + "; Track number: " + trackNum);

            if (formatType == 0 && trackNum != 1) return -1;

            uint timeDiv = ByteArrayToUInt(SubArray(index, 2)); index += 2;
            TimeDivType timeDivType;

            if ((timeDiv & 0x8000) == 0) timeDivType = TimeDivType.PPQ;
            else timeDivType = TimeDivType.SMPTE;

            Console.WriteLine("- Time div: " + timeDiv + "; Time div type: " + timeDivType);

            header = new HeaderChunk(chunkID, chunkSize, formatType, trackNum, timeDiv, timeDivType);

            return index;
        }

        private int ParseTrack(int index, int trackNum)
        {
            // Stores the ID of the track chunk. If the ID isn't "MTrk", then the MIDI file is corrupted or in an incorrect format.
            char[] chunkID = new char[4];

            for (int i = 0; i < 4; i++)
            {
                chunkID[i] = (char)midi[index];
                index++;
            }

            Console.WriteLine("- Track " + (trackNum + 1) + " chunk ID: " + chunkID[0] + chunkID[1] + chunkID[2] + chunkID[3]);

            if (!(new string(chunkID)).Equals("MTrk")) return -1;

            // Stores the chunk size of the track.
            uint chunkSize = ByteArrayToUInt(SubArray(index, 4)); index += 4;

            Console.WriteLine("- Track " + (trackNum + 1) + " chunk size: " + chunkSize);
            tracks[trackNum] = new TrackChunk(chunkID, chunkSize);
            Console.WriteLine("- PARSING TRACK " + (trackNum + 1) + ":");
            index = ParseTrackContent(index, trackNum);

            return index;
        }

        private int ParseTrackContent(int index, int trackNum)
        {
            int trackIndex = index;

            while (index < trackIndex + tracks[trackNum].chunkSize)
            {
                Console.WriteLine("-- Event:");

                byte[] deltaTimeArray = GetVariableLengthData(index);
                uint time = ByteArrayToUInt(deltaTimeArray);
                index += deltaTimeArray.Length;
                byte type = midi[index++];

                switch (type)
                {
                    case 0xFF: // Meta event.
                        Console.WriteLine("--- Type: Meta event.");
                        index = ParseMetaEvent(index, time);
                    break;

                    default:
                        Console.WriteLine("--- Type: Channel event.");
                        index = ParseChannelEvent(index, time, type);
                    break;
                }
            }

            return index;
        }

        private int ParseMetaEvent(int index, uint time)
        {
            byte metaEventType = midi[index++];

            if ((MidiEventType)metaEventType == MidiEventType.EndOfTrack)
            {
                NumMetaEvent numEvent = new NumMetaEvent();
                numEvent.SetTime(time);
                numEvent.SetEventType((MidiEventType)metaEventType);

                Console.WriteLine("--- End of track!");
            }

            uint numBytes = midi[index++];

            if (((metaEventType >= 0x01) && (metaEventType <= 0x07)) || (metaEventType == 0x7F))
            {
                TextMetaEvent textEvent = new TextMetaEvent();
                textEvent.SetTime(time);
                textEvent.SetEventType((MidiEventType)metaEventType);
                string textTemp = "";

                for (int i = 0; i < numBytes; i++)
                {
                    textTemp += (char)midi[index++];
                }

                textEvent.SetText(textTemp);
                midiEvents.Add(textEvent);

                Console.WriteLine("--- Text: " + textTemp);
            }
            else
            {
                NumMetaEvent numEvent = new NumMetaEvent();
                numEvent.SetTime(time);
                numEvent.SetEventType((MidiEventType)metaEventType);
                byte[] numTemp = new byte[numBytes];

                for (int i = 0; i < numBytes; i++)
                {
                    numTemp[i] = midi[index++];

                    Console.WriteLine("--- Num " + i + ": " + numTemp[i]);
                }

                numEvent.SetNum(numTemp);
                midiEvents.Add(numEvent);
            }

            Console.WriteLine("--- Time: " + time);
            Console.WriteLine("--- Event Type: " + (MidiEventType)metaEventType);

            return index;
        }

        private int ParseChannelEvent(int index, uint time, byte type)
        {
            uint channel = (uint)(type & 0x0F);
            MidiEventType eventType = (MidiEventType)(type & 0xF0);
            ChannelEvent channelEvent = new ChannelEvent();

            channelEvent.SetChannel(channel);
            channelEvent.SetTime(time);
            channelEvent.SetEventType(eventType);

            Console.WriteLine("--- Channel: " + channel);
            Console.WriteLine("--- Time: " + time);
            Console.WriteLine("--- Event Type: " + eventType);

            byte param1;
            byte param2;

            switch (eventType)
            {
                case MidiEventType.NoteOn:
                    param1 = midi[index++];
                    param2 = midi[index++];
                    channelEvent.SetParam1(param1);
                    channelEvent.SetParam2(param2);
                    Console.WriteLine("--- Note: " + param1);
                    Console.WriteLine("--- Velocity: " + param2);
                break;

                case MidiEventType.NoteOff:
                    param1 = midi[index++];
                    param2 = midi[index++];
                    channelEvent.SetParam1(param1);
                    channelEvent.SetParam2(param2);
                    Console.WriteLine("--- Note: " + param1);
                    Console.WriteLine("--- Velocity: " + param2);
                break;

                case MidiEventType.NoteAftertouch:
                    param1 = midi[index++];
                    param2 = midi[index++];
                    channelEvent.SetParam1(param1);
                    channelEvent.SetParam2(param2);
                    Console.WriteLine("--- Note: " + param1);
                    Console.WriteLine("--- Amount: " + param2);
                break;

                case MidiEventType.Controller:
                    param1 = midi[index++];
                    param2 = midi[index++];
                    channelEvent.SetParam1(param1);
                    channelEvent.SetParam2(param2);
                    Console.WriteLine("--- Controller: " + param1);
                    Console.WriteLine("--- Value: " + param2);
                break;

                case MidiEventType.ProgramChange:
                    param1 = midi[index++];
                    channelEvent.SetParam1(param1);
                    Console.WriteLine("--- Program: " + param1);
                break;

                case MidiEventType.ChannelAftertouch:
                    param1 = midi[index++];
                    channelEvent.SetParam1(param1);
                    Console.WriteLine("--- Amount: " + param1);
                break;

                case MidiEventType.PitchBend:
                    param1 = midi[index++];
                    param2 = midi[index++];
                    channelEvent.SetParam1(param1);
                    channelEvent.SetParam2(param2);
                    Console.WriteLine("--- LSB Value: " + param1);
                    Console.WriteLine("--- MSB Value: " + param2);
                break;

                default:
                    return -1;
            }

            return index;
        }

        // Returns the array of bytes from the MIDI file that represent a piece of data of variable length.
        private byte[] GetVariableLengthData(int index)
        {
            int oldIndex = index;

            while ((midi[index] & 0x80) != 0)
            {
                index++;
            }

            int offset = index - oldIndex + 1;
            byte[] data = new byte[offset];

            for (int i = 0; i < offset; i++)
            {
                data[i] = midi[oldIndex + i];
            }

            return data;
        }

        // Calculates the BPM from a byte array.
        private float CalculateBPM(byte[] tempoArray)
        {
            uint tempo = ByteArrayToUInt(tempoArray);

            return MICROSECONDS_PER_MINUTE / tempo;
        }

        // Converts a byte array of any size to an unsigned integer.
        private uint ByteArrayToUInt(byte[] byteArray)
        {
            uint result = 0;

            for (int i = byteArray.Length - 1, j = 0; i >= 0; i--, j++)
            {
                result += byteArray[i] * (uint)Math.Pow(0x0100, j);
            }

            return result;
        }

        // Retrieves a subarray from the main byte array.
        private byte[] SubArray(int start, int size)
        {
            byte[] subArray = new byte[size];

            for (int i = start; i < start + size; i++)
            {
                subArray[i - start] = midi[i];
            }

            return subArray;
        }

        // Reverses endianness (direction between most/least significant byte).
        private uint ReverseEndianness(uint value)
        {
            uint b1 = (value >> 0) & 0xff;
            uint b2 = (value >> 8) & 0xff;
            uint b3 = (value >> 16) & 0xff;
            uint b4 = (value >> 24) & 0xff;

            return b1 << 24 | b2 << 16 | b3 << 8 | b4 << 0;
        }

        // Outputs the header chunk as a string for debugging purposes.
        public string HeaderToString()
        {
            return "chunkID: " + new string(header.chunkID) + "\n" +
                   "chunkSize: " + header.chunkSize + "\n" +
                   "formatType: " + header.formatType + "\n" +
                   "trackNum: " + header.trackNum + "\n" +
                   "timeDiv: " + header.timeDiv + "\n" +
                   "timeDivType: " + header.timeDivType;
        }

        public string TrackToString(int trackNum)
        {
            return "chunkID: " + new string(tracks[trackNum].chunkID) + "\n" +
                   "chunkSize: " + tracks[trackNum].chunkSize + "\n";
        }
    }
}
