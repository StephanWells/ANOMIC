using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class Event
    {
        protected uint time;
        protected MidiEventType eventType;

        public Event() {}

        public uint GetTime()               {   return time;        }
        public MidiEventType GetEventType() {   return eventType;   }
        public void SetTime(uint timeIn)                    {   time = timeIn;              }
        public void SetEventType(MidiEventType eventTypeIn) {   eventType = eventTypeIn;    }
    }
}