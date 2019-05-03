using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace AnnotationTool
{
    public class Note
    {
        private NotePitch pitch;
        private double time;
        private double duration;
        private uint channel;
        private int velocity;

        public Note() { }

        public Note(NotePitch pitchIn, double timeIn, double durationIn, uint channelIn, int velocityIn)
        {
            pitch = pitchIn;
            time = timeIn;
            duration = durationIn;
            channel = channelIn;
            velocity = velocityIn;
        }

        public NotePitch GetPitch() {   return pitch;       }
        public double GetTime()     {   return time;        }
        public double GetDuration() {   return duration;    }
        public uint GetChannel()    {   return channel;     }
        public int GetVelocity()    {   return velocity;    }
        public void SetPitch(NotePitch pitchIn)     {   pitch = pitchIn;        }
        public void SetTime(double timeIn)          {   time = timeIn;          }
        public void SetDuration(double durationIn)  {   duration = durationIn;  }
        public void SetChannel(uint channelIn)      {   channel = channelIn;    }
        public void SetVelocity(int velocityIn)     {   velocity = velocityIn;  }
    }
}