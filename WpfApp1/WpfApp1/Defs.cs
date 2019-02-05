using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
   public enum TimeDivType
    {
        PPQ,
        SMPTE
    };

    public enum MidiEventType
    {
        // Channel events.
        NoteOff = 0x80,
        NoteOn = 0x90,
        NoteAftertouch = 0xA0,
        Controller = 0xB0,
        ProgramChange = 0xC0,
        ChannelAftertouch = 0xD0,
        PitchBend = 0xE0,
        Meta = 0xFF,
        System = 0xF0,

        // Meta events.
        SequenceNumber = 0x00,
        TextEvent = 0x01,
        CopyrightNotice = 0x02,
        TrackName = 0x03,
        InstrumentName = 0x04,
        Lyrics = 0x05,
        Marker = 0x06,
        CuePoint = 0x07,
        EndOfTrack = 0x2F,
        SetTempo = 0x51,
        TimeSignature = 0x58,
        KeySignature = 0x59,
        SequencerSpecific = 0x7F,
        TimingClock = 0xF8,
        StartSequence = 0xFA,
        ContinueSequence = 0xFB,
        StopSequence = 0xFC,

        Unknown = 0xDD
    };

    public struct HeaderChunk
    {
        public char[] chunkID;
        public uint chunkSize;
        public uint formatType;
        public uint trackNum;
        public uint timeDiv;
        public TimeDivType timeDivType;

        public HeaderChunk(char[] chunkIDIn, uint chunkSizeIn, uint formatTypeIn, uint trackNumIn, uint timeDivIn, TimeDivType timeDivTypeIn)
        {
            chunkID = chunkIDIn;
            chunkSize = chunkSizeIn;
            formatType = formatTypeIn;
            trackNum = trackNumIn;
            timeDiv = timeDivIn;
            timeDivType = timeDivTypeIn;
        }
    }

    public struct TrackChunk
    {
        public char[] chunkID;
        public uint chunkSize;

        public TrackChunk(char[] chunkIDIn, uint chunkSizeIn)
        {
            chunkID = chunkIDIn;
            chunkSize = chunkSizeIn;
        }
    }
}
