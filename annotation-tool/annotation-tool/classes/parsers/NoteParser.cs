using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class NoteParser
    {
        public const int MICROSECONDS_PER_MINUTE = 60000000;
        public List<Event> midiEvents;
        public List<Note> notes;
        public float bpm = 120f;
        public int[] timeSig = new int[2] { 4, 4 }; // Index 0 is numerator, index 1 is denominator;
        public double midiLength = 0;

        public NoteParser(MIDIParser midiParseIn)
        {
            midiEvents = midiParseIn.midiEvents;
            notes = new List<Note>();
        }

        public void ParseEvents()
        {
            Dictionary<NotePitch, Stack<Note>> activeNotes = new Dictionary<NotePitch, Stack<Note>>(); // Dictionary of all notes that have a NoteOn but no NoteOff.
            uint timeElapsed = 0;

            for (int i = 0; i < midiEvents.Count; i++)
            {
                Event tempEvent = midiEvents.ElementAt(i);
                timeElapsed += tempEvent.GetTime();

                switch (tempEvent.GetEventType())
                {
                    case MidiEventType.NoteOn:
                        ChannelEvent tempNoteOnEvent = (ChannelEvent)tempEvent;
                        Note currentNoteOn = EventToNote(tempNoteOnEvent);
                        currentNoteOn.SetTime(timeElapsed);

                        // If there is already an active note in this pitch, add the new note to the existing stack.
                        if (activeNotes.ContainsKey(currentNoteOn.GetPitch()))
                        {
                            activeNotes[currentNoteOn.GetPitch()].Push(currentNoteOn);
                        }
                        else // If there is no active note in this pitch, create the stack and then add the note.
                        {
                            Stack<Note> notesStack = new Stack<Note>();
                            notesStack.Push(currentNoteOn);
                            activeNotes.Add(currentNoteOn.GetPitch(), notesStack);
                        }
                    break;

                    case MidiEventType.NoteOff:
                        ChannelEvent tempNoteOffEvent = (ChannelEvent)tempEvent;
                        Note currentNoteOff = EventToNote(tempNoteOffEvent);

                        // If there is already at least one active note in this pitch, pop the latest one and finalise its duration.
                        if (activeNotes.ContainsKey(currentNoteOff.GetPitch()))
                        {
                            Note tempNote = activeNotes[currentNoteOff.GetPitch()].Pop();
                            tempNote.SetDuration(timeElapsed - tempNote.GetTime());
                            notes.Add(tempNote);

                            if (activeNotes[currentNoteOff.GetPitch()].Count == 0) // If the stack of notes is empty, remove the entry from the dictionary of active notes.
                            {
                                activeNotes.Remove(currentNoteOff.GetPitch());
                            }
                        }
                        else // If there was a note off event at a certain pitch with no note on events at that pitch.
                        {
                            throw new InvalidOperationException("Note off without corresponding note on.");
                        }
                    break;

                    case MidiEventType.SetTempo:
                        bpm = CalculateBPM(((NumMetaEvent)tempEvent).GetNum());
                    break;

                    case MidiEventType.TimeSignature:
                        timeSig = CalculateTimeSignature(((NumMetaEvent)tempEvent).GetNum());
                    break;

                    case MidiEventType.EndOfTrack:
                        midiLength = timeElapsed > midiLength ? timeElapsed : midiLength;
                        timeElapsed = 0;
                    break;
                }
            }
        }

        // Turns a channel event into a musical note object.
        public Note EventToNote(ChannelEvent eventIn)
        {
            Note note = new Note();

            note.SetPitch((NotePitch)eventIn.GetParam1());
            note.SetVelocity(eventIn.GetParam2());
            note.SetChannel(eventIn.GetChannel());

            return note;
        }

        // Calculates the BPM from a byte array.
        private float CalculateBPM(byte[] tempoArray)
        {
            uint tempo = MIDIParser.FixedLengthArrayToUInt(tempoArray);

            return MICROSECONDS_PER_MINUTE / tempo;
        }

        // Calculates the time signature from a byte array.
        private int[] CalculateTimeSignature(byte[] timeSigArray)
        {
            int[] timeSig = new int[2];

            timeSig[0] = timeSigArray[0];
            timeSig[1] = (int)Math.Pow(2, timeSigArray[1]);

            return timeSig;
        }
    }
}