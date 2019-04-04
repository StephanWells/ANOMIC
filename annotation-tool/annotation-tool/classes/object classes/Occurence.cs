using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class Occurrence
    {
        private float start;
        private float end;
        private int confidence;

        public Occurrence() { }

        public Occurrence(float startIn, float endIn)
        {
            start = startIn;
            end = endIn;
        }

        public float getStart()     {   return start;       }
        public float getEnd()       {   return end;         }
        public int getConfidence()  {   return confidence;  }
        public void setStart(float startIn)             {   start = startIn;            }
        public void setEnd(float endIn)                 {   end = endIn;                }
        public void setConfidence(int confidenceIn)     {   confidence = confidenceIn;  }
    }
}
