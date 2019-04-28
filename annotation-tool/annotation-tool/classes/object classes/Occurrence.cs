using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Controls;
using System.Windows.Controls;
using System.Windows.Media;

namespace AnnotationTool
{
    public class Occurrence
    {
        private int confidence;
        private double start;
        private double end;
        public Canvas occurrenceRect;
        public OccurrenceIcon occurrenceIcon;

        public Occurrence() { }

        public Occurrence(double startIn, double endIn)
        {
            start = startIn;
            end = endIn;
        }

        public int GetConfidence()  {   return confidence;  }
        public double GetStart()    {   return start;       }
        public double GetEnd()      {   return end;         }
        public void SetConfidence(int confidenceIn) {   confidence = confidenceIn;  }
        public void SetStart(double startIn)        {   start = startIn;            }
        public void SetEnd(double endIn)            {   end = endIn;                }
    }
}
