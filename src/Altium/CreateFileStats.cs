using System;
using System.Collections.Generic;

namespace Altium
{
    public class CreateFileStats
    {
        public int BatchCount { get; set; }
        public IEnumerable<TimeSpan> WriteTimes { get; set; }
        public IEnumerable<TimeSpan> WaitTimes { get; set; }
        public IEnumerable<TimeSpan> GenTimes { get; set; }
    }
}