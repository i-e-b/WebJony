using System;
using System.Collections.Generic;

namespace WrapperRoleListener.Internal
{
    /// <summary>
    /// Logarithmic record of good and bad calls per second
    /// </summary>
    public class Timeslice
    {
        private readonly long[] sampleSum;
        private readonly int[] sampleCount;
        private readonly long ticksPerSample;

        private long lastRecord;

        public Timeslice()
        {
            sampleSum = new long[15];
            sampleCount = new int[15];
            
            for (int i = 0; i < 15; i++) { sampleCount[i] = 1 << i; }

            ticksPerSample = TimeSpan.TicksPerMinute;
            lastRecord = DateTime.UtcNow.Ticks / ticksPerSample;
        }

        /// <summary>
        /// Record an event now
        /// </summary>
        public void Record() {
            var newRecord = DateTime.UtcNow.Ticks / ticksPerSample;
            if (newRecord < lastRecord) return; // out of sequence
            if (newRecord != lastRecord) {
                var rolls = newRecord - lastRecord;
                for (int i = 0; i < rolls; i++)
                {
                    RollSamples();
                }
            }

            sampleSum[0]++;

            lastRecord = newRecord;
        }

        /// <summary>
        /// Show an approximate history
        /// </summary>
        public Dictionary<DateTime, double> View()
        {
            var history = new Dictionary<DateTime, double>();
            try
            {
                if (lastRecord == 0) return history;
                var dtBase = new DateTime(lastRecord * ticksPerSample);

                for (int i = 0; i < 15; i++)
                {
                    var offs = ticksPerSample << i;
                    DateTime dtEvt;
                    try { dtEvt = dtBase.AddTicks(-offs); }
                    catch { break; }
                    history.Add(dtEvt, ((double)sampleSum[i]) / sampleCount[i]);
                }
            }
            catch
            {
                // nothing
            }
            return history;
        }

        /// <summary>
        /// Merge samples back in time
        /// </summary>
        private void RollSamples()
        {
            sampleCount[0] = 2;
            for (int i = 0; i < 14; i++)
            {
                var lim = 1 << (i+1);
                var rep = 1 << i;
                if (sampleCount[i] < lim) break;

                sampleSum[i+1] += sampleSum[i];
                sampleCount[i+1] += rep;
                sampleCount[i] = rep;
                sampleSum[i] >>= 1;
            }
        }
    }
}