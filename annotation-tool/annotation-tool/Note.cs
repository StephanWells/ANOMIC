using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class Note
    {
        private NotePitch pitch;
        private uint time;
        private long duration;
        private uint channel;
        private int velocity;

        public Note() { }

        public Note(NotePitch pitchIn, uint timeIn, int durationIn, uint channelIn, int velocityIn)
        {
            pitch = pitchIn;
            time = timeIn;
            duration = durationIn;
            channel = channelIn;
            velocity = velocityIn;
        }

        public NotePitch GetPitch() {   return pitch;       }
        public uint GetTime()       {   return time;        }
        public long GetDuration()   {   return duration;    }
        public uint GetChannel()    {   return channel;     }
        public int GetVelocity()    {   return velocity;    }
        public void SetPitch(NotePitch pitchIn)     {   pitch = pitchIn;        }
        public void SetTime(uint timeIn)            {   time = timeIn;          }
        public void SetDuration(long  durationIn)   {   duration = durationIn;  }
        public void SetChannel(uint channelIn)      {   channel = channelIn;    }
        public void SetVelocity(int velocityIn)     {   velocity = velocityIn;  }
    }
}
