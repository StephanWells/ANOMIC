using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
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

    public enum NotePitch
    {
        A0  = 21,
        As0 = 22,
        B0  = 23,
        C1  = 24,
        Cs1 = 25,
        D1  = 26,
        Ds1 = 27,
        E1  = 28,
        F1  = 29,
        Fs1 = 30,
        G1  = 31,
        Gs1 = 32,
        A1  = 33,
        As1 = 34,
        B1  = 35,
        C2  = 36,
        Cs2 = 37,
        D2  = 38,
        Ds2 = 39,
        E2  = 40,
        F2  = 41,
        Fs2 = 42,
        G2  = 43,
        Gs2 = 44,
        A2  = 45,
        As2 = 46,
        B2  = 47,
        C3  = 48,
        Cs3 = 49,
        D3  = 50,
        Ds3 = 51,
        E3  = 52,
        F3  = 53,
        Fs3 = 54,
        G3  = 55,
        Gs3 = 56,
        A3  = 57,
        As3 = 58,
        B3  = 59,
        C4  = 60,
        Cs4 = 61,
        D4  = 62,
        Ds4 = 63,
        E4  = 64,
        F4  = 65,
        Fs4 = 66,
        G4  = 67,
        Gs4 = 68,
        A4  = 69,
        As4 = 70,
        B4  = 71,
        C5  = 72,
        Cs5 = 73,
        D5  = 74,
        Ds5 = 75,
        E5  = 76,
        F5  = 77,
        Fs5 = 78,
        G5  = 79,
        Gs5 = 80,
        A5  = 81,
        As5 = 82,
        B5  = 83,
        C6  = 84,
        Cs6 = 85,
        D6  = 86,
        Ds6 = 87,
        E6  = 88,
        F6  = 89,
        Fs6 = 90,
        G6  = 91,
        Gs6 = 92,
        A6  = 93,
        As6 = 94,
        B6  = 95,
        C7  = 96,
        Cs7 = 97,
        D7  = 98,
        Ds7 = 99,
        E7  = 100,
        F7  = 101,
        Fs7 = 102,
        G7  = 103,
        Gs7 = 104,
        A7  = 105,
        As7 = 106,
        B7  = 107,
        C8  = 108
    };
}
