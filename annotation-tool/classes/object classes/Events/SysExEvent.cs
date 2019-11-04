using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class SysExEvent : Event
    {
        private byte[] data;

        public SysExEvent() {}

        public byte[] GetData() { return data; }
        public void SetData(byte[] dataIn) { data = dataIn; }
    }
}