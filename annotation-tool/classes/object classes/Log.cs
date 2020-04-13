using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnnotationTool
{
    public class Log
    {
        public LogType logType;
        public TimeSpan time;
        public string value;

        public Log() { }

        public override string ToString()
        {
            return "type: " + logType.ToString() + ";time: " + TimeToString() + ";value: " + value;
        }

        public string TimeToString()
        {
            return String.Format("{0:00}:{1:00}:{2:00}.{3:00}", time.Hours, time.Minutes, time.Seconds, time.Milliseconds / 10);
        }
    }
}
