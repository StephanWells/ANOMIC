using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Controls;

namespace AnnotationTool
{
    public class Pattern
    {
        private List<Occurrence> occurrences;
        public PatternIcon patternIcon;

        public Pattern()
        {
            occurrences = new List<Occurrence>();
        }

        public List<Occurrence> GetOccurrences()    {   return occurrences; }
        public void SetOccurrences(List<Occurrence> occurrencesIn)  {   occurrences = occurrencesIn;    }

        public void AddOccurrence(Occurrence occurrenceIn)
        {
            occurrences.Add(occurrenceIn);
        }

        public void ClearOccurrences()
        {
            occurrences.Clear();
        }
    }
}
