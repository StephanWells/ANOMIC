using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class NumMetaEvent : Event
    {
        private byte[] num;

        public NumMetaEvent() { }
        public NumMetaEvent(byte[] numIn) { num = numIn; }

        public byte[] GetNum() { return num; }
        public void SetNum(byte[] numIn) { num = numIn; }
    }
}
