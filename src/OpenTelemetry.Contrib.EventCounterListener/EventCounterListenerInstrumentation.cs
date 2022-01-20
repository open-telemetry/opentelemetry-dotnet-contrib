using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace OpenTelemetry.Contrib.Instrumentation.EventCounterListener
{
    internal class EventCounterListenerInstrumentation : IDisposable
    {

        private readonly EventCounterListener eventCounterListener;

        public EventCounterListenerInstrumentation(EventCounterListenerOptions options)
        {
            this.eventCounterListener = new EventCounterListener(options);
        }

        public void Dispose()
        {
            this.eventCounterListener.Dispose();
        }
    }
}
