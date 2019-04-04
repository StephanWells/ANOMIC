using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class Pattern
    {
        private List<Occurrence> occurrences;

        public Pattern()
        {
            occurrences = new List<Occurrence>();
        }

        public List<Occurrence> GetOccurrences()    {   return occurrences; }

        public void AddOccurrence(Occurrence occurrenceIn)
        {
            occurrences.Add(occurrenceIn);
        }
    }
}
