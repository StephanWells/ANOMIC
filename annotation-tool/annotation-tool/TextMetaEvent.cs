using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class TextMetaEvent : Event
    {
        private string text;

        public TextMetaEvent() {}

        public string GetText() { return text; }
        public void SetText(string textIn) { text = textIn; }
    }
}
