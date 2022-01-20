using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTelemetry.Contrib.EventCounterListener.EventPipe
{
    internal interface ICounterPayload
    {
        string Name
        {
            get;
        }

        double Value
        {
            get;
        }

        CounterType CounterType
        {
            get;
        }

        string Provider
        {
            get;
        }

        string DisplayName
        {
            get;
        }

        string Unit
        {
            get;
        }

        DateTime Timestamp
        {
            get;
        }

        float Interval
        {
            get;
        }
    }
}
