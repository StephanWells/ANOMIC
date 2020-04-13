using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class ChannelEvent : Event
    {
        private uint channel;
        private byte param1;
        private byte param2;

        public ChannelEvent() {}

        public uint GetChannel()    {   return channel; }
        public byte GetParam1()     {   return param1;  }
        public byte GetParam2()     {   return param2;  }
        public void SetChannel(uint channelIn)  {   channel = channelIn;    }
        public void SetParam1(byte param1In)    {   param1 = param1In;      }
        public void SetParam2(byte param2In)    {   param2 = param2In;      }
    }
}
