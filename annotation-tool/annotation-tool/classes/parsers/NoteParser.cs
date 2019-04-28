using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class NoteParser
    {
        public List<Event> midiEvents;
        public List<Note> notes;

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

                if (tempEvent.GetEventType() == MidiEventType.NoteOn)
                {
                    ChannelEvent tempNoteEvent = (ChannelEvent)tempEvent;
                    Note currentNote = EventToNote(tempNoteEvent);
                    currentNote.SetTime((double)timeElapsed);
                    
                    // If there is already an active note in this pitch, add the new note to the existing stack.
                    if (activeNotes.ContainsKey(currentNote.GetPitch()))
                    {
                        activeNotes[currentNote.GetPitch()].Push(currentNote);
                    }
                    else // If there is no active note in this pitch, create the stack and then add the note.
                    {
                        Stack<Note> notesStack = new Stack<Note>();
                        notesStack.Push(currentNote);
                        activeNotes.Add(currentNote.GetPitch(), notesStack);
                    }
                }
                else if (tempEvent.GetEventType() == MidiEventType.NoteOff)
                {
                    ChannelEvent tempNoteEvent = (ChannelEvent)tempEvent;
                    Note currentNote = EventToNote(tempNoteEvent);

                    // If there is already at least one active note in this pitch, pop the latest one and finalise its duration.
                    if (activeNotes.ContainsKey(currentNote.GetPitch()))
                    {
                        Note tempNote = activeNotes[currentNote.GetPitch()].Pop();
                        tempNote.SetDuration((double)timeElapsed - tempNote.GetTime());
                        notes.Add(tempNote);

                        if (activeNotes[currentNote.GetPitch()].Count == 0) // If the stack of notes is empty, remove the entry from the dictionary of active notes.
                        {
                            activeNotes.Remove(currentNote.GetPitch());
                        }
                    }
                    else // If there was a note off event at a certain pitch with no note on events at that pitch.
                    {
                        throw new InvalidOperationException("Note off without corresponding note on.");
                    }
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
    }
}