using System.Collections.Generic;
using Kaitai;

namespace OpenTelemetry.Exporter.Geneva.UnitTest
{
    public partial class MetricsContract : KaitaiStruct
    {
        public static MetricsContract FromFile(string fileName)
        {
            return new MetricsContract(new KaitaiStream(fileName));
        }


        public enum MetricEventType
        {
            Old = 0,
            Uint64Metric = 50,
            DoubleScaledToLongMetric = 51,
            BatchMetric = 52,
            ExternallyAggregatedUlongMetric = 53,
            ExternallyAggregatedDoubleMetric = 54,
            DoubleMetric = 55,
            ExternallyAggregatedUlongDistributionMetric = 56,
            ExternallyAggregatedDoubleDistributionMetric = 57,
            ExternallyAggregatedDoubleScaledToLongDistributionMetric = 58,
        }

        public enum DistributionType
        {
            Bucketed = 0,
            MonBucketed = 1,
            ValueCountPairs = 2,
        }
        public MetricsContract(KaitaiStream p__io, KaitaiStruct p__parent = null, MetricsContract p__root = null) : base(p__io)
        {
            m_parent = p__parent;
            m_root = p__root ?? this;
            _read();
        }
        private void _read()
        {
            _eventId = m_io.ReadU2le();
            _lenBody = m_io.ReadU2le();
            __raw_body = m_io.ReadBytes(LenBody);
            var io___raw_body = new KaitaiStream(__raw_body);
            _body = new Userdata(EventId, io___raw_body, this, m_root);
        }

        /// <summary>
        /// This type represents "UserData" or "body" portion of Metrics message.
        /// </summary>
        public partial class Userdata : KaitaiStruct
        {
            public Userdata(ushort p_eventId, KaitaiStream p__io, MetricsContract p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _eventId = p_eventId;
                f_eventType = false;
                _read();
            }
            private void _read()
            {
                _numDimensions = m_io.ReadU2le();
                _padding = m_io.ReadBytes(2);
                switch (EventType)
                {
                    case MetricsContract.MetricEventType.ExternallyAggregatedDoubleScaledToLongDistributionMetric:
                        {
                            _valueSection = new ExtAggregatedDoubleValue(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.DoubleMetric:
                        {
                            _valueSection = new SingleDoubleValue(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.ExternallyAggregatedUlongMetric:
                        {
                            _valueSection = new ExtAggregatedUint64Value(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.ExternallyAggregatedUlongDistributionMetric:
                        {
                            _valueSection = new ExtAggregatedUint64Value(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.ExternallyAggregatedDoubleDistributionMetric:
                        {
                            _valueSection = new ExtAggregatedDoubleValue(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.DoubleScaledToLongMetric:
                        {
                            _valueSection = new SingleDoubleValue(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.Uint64Metric:
                        {
                            _valueSection = new SingleUint64Value(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.ExternallyAggregatedDoubleMetric:
                        {
                            _valueSection = new ExtAggregatedDoubleValue(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.MetricEventType.Old:
                        {
                            _valueSection = new SingleUint64Value(m_io, this, m_root);
                            break;
                        }
                }
                _metricAccount = new LenString(m_io, this, m_root);
                _metricNamespace = new LenString(m_io, this, m_root);
                _metricName = new LenString(m_io, this, m_root);
                _dimensionsNames = new List<LenString>((int)(NumDimensions));
                for (var i = 0; i < NumDimensions; i++)
                {
                    _dimensionsNames.Add(new LenString(m_io, this, m_root));
                }
                _dimensionsValues = new List<LenString>((int)(NumDimensions));
                for (var i = 0; i < NumDimensions; i++)
                {
                    _dimensionsValues.Add(new LenString(m_io, this, m_root));
                }
                if (!(M_Io.IsEof))
                {
                    _apContainer = new LenString(m_io, this, m_root);
                }
                if (((((EventType == MetricsContract.MetricEventType.ExternallyAggregatedUlongDistributionMetric) || (EventType == MetricsContract.MetricEventType.ExternallyAggregatedDoubleDistributionMetric) || (EventType == MetricsContract.MetricEventType.ExternallyAggregatedDoubleScaledToLongDistributionMetric))) && (!(M_Io.IsEof))))
                {
                    _histogram = new Histogram(m_io, this, m_root);
                }
            }
            private bool f_eventType;
            private MetricEventType _eventType;
            public MetricEventType EventType
            {
                get
                {
                    if (f_eventType)
                        return _eventType;
                    _eventType = (MetricEventType)(((MetricsContract.MetricEventType)EventId));
                    f_eventType = true;
                    return _eventType;
                }
            }
            private ushort _numDimensions;
            private byte[] _padding;
            private KaitaiStruct _valueSection;
            private LenString _metricAccount;
            private LenString _metricNamespace;
            private LenString _metricName;
            private List<LenString> _dimensionsNames;
            private List<LenString> _dimensionsValues;
            private LenString _apContainer;
            private Histogram _histogram;
            private ushort _eventId;
            private MetricsContract m_root;
            private MetricsContract m_parent;

            /// <summary>
            /// Number of dimensions specified in this event.
            /// </summary>
            public ushort NumDimensions { get { return _numDimensions; } }
            public byte[] Padding { get { return _padding; } }

            /// <summary>
            /// Value section of the body, stores fixed numeric metric value(s), as per event type.
            /// </summary>
            public KaitaiStruct ValueSection { get { return _valueSection; } }

            /// <summary>
            /// Geneva Metrics account name to be used for this metric.
            /// </summary>
            public LenString MetricAccount { get { return _metricAccount; } }

            /// <summary>
            /// Geneva Metrics namespace name to be used for this metric.
            /// </summary>
            public LenString MetricNamespace { get { return _metricNamespace; } }

            /// <summary>
            /// Geneva Metrics metric name to be used.
            /// </summary>
            public LenString MetricName { get { return _metricName; } }

            /// <summary>
            /// Dimension names strings ("key" parts of key-value pairs). Must be sorted,
            /// unless MetricsExtenion's option `enableDimensionSortingOnIngestion` is
            /// enabled.
            /// </summary>
            public List<LenString> DimensionsNames { get { return _dimensionsNames; } }

            /// <summary>
            /// Dimension values strings ("value" parts of key-value pairs).
            /// </summary>
            public List<LenString> DimensionsValues { get { return _dimensionsValues; } }

            /// <summary>
            /// AutoPilot container string, required for correct AP PKI certificate loading
            /// in AutoPilot containers environment.
            /// </summary>
            public LenString ApContainer { get { return _apContainer; } }
            public Histogram Histogram { get { return _histogram; } }

            /// <summary>
            /// Type of message, affects format of the body.
            /// </summary>
            public ushort EventId { get { return _eventId; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract M_Parent { get { return m_parent; } }
        }

        /// <summary>
        /// Bucket with an explicitly-defined value coordinate `value`, claiming to
        /// hold `count` hits. Normally used to represent non-linear (e.g. exponential)
        /// histograms payloads.
        /// </summary>
        public partial class PairValueCount : KaitaiStruct
        {
            public static PairValueCount FromFile(string fileName)
            {
                return new PairValueCount(new KaitaiStream(fileName));
            }

            public PairValueCount(KaitaiStream p__io, MetricsContract.HistogramValueCountPairs p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _value = m_io.ReadU8le();
                _count = m_io.ReadU4le();
            }
            private ulong _value;
            private uint _count;
            private MetricsContract m_root;
            private MetricsContract.HistogramValueCountPairs m_parent;
            public ulong Value { get { return _value; } }
            public uint Count { get { return _count; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.HistogramValueCountPairs M_Parent { get { return m_parent; } }
        }

        /// <summary>
        /// Payload of a histogram with linear distribution of buckets. Such histogram
        /// is defined by the parameters specified in `min`, `bucket_size` and
        /// `bucket_count`. It is modelled as a series of buckets. First (index 0) and
        /// last (indexed `bucket_count - 1`) buckets are special and are supposed to
        /// catch all "underflow" and "overflow" values. Buckets with indexes 1 up to
        /// `bucket_count - 2` are regular buckets of size `bucket_size`.
        /// </summary>
        public partial class HistogramUint16Bucketed : KaitaiStruct
        {
            public static HistogramUint16Bucketed FromFile(string fileName)
            {
                return new HistogramUint16Bucketed(new KaitaiStream(fileName));
            }

            public HistogramUint16Bucketed(KaitaiStream p__io, MetricsContract.Histogram p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _min = m_io.ReadU8le();
                _bucketSize = m_io.ReadU4le();
                _bucketCount = m_io.ReadU4le();
                _distributionSize = m_io.ReadU2le();
                _columns = new List<PairUint16>((int)(DistributionSize));
                for (var i = 0; i < DistributionSize; i++)
                {
                    _columns.Add(new PairUint16(m_io, this, m_root));
                }
            }
            private ulong _min;
            private uint _bucketSize;
            private uint _bucketCount;
            private ushort _distributionSize;
            private List<PairUint16> _columns;
            private MetricsContract m_root;
            private MetricsContract.Histogram m_parent;
            public ulong Min { get { return _min; } }
            public uint BucketSize { get { return _bucketSize; } }
            public uint BucketCount { get { return _bucketCount; } }
            public ushort DistributionSize { get { return _distributionSize; } }
            public List<PairUint16> Columns { get { return _columns; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Histogram M_Parent { get { return m_parent; } }
        }
        public partial class HistogramValueCountPairs : KaitaiStruct
        {
            public static HistogramValueCountPairs FromFile(string fileName)
            {
                return new HistogramValueCountPairs(new KaitaiStream(fileName));
            }

            public HistogramValueCountPairs(KaitaiStream p__io, MetricsContract.Histogram p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _distributionSize = m_io.ReadU2le();
                _columns = new List<PairValueCount>((int)(DistributionSize));
                for (var i = 0; i < DistributionSize; i++)
                {
                    _columns.Add(new PairValueCount(m_io, this, m_root));
                }
            }
            private ushort _distributionSize;
            private List<PairValueCount> _columns;
            private MetricsContract m_root;
            private MetricsContract.Histogram m_parent;
            public ushort DistributionSize { get { return _distributionSize; } }
            public List<PairValueCount> Columns { get { return _columns; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Histogram M_Parent { get { return m_parent; } }
        }
        public partial class Histogram : KaitaiStruct
        {
            public static Histogram FromFile(string fileName)
            {
                return new Histogram(new KaitaiStream(fileName));
            }

            public Histogram(KaitaiStream p__io, MetricsContract.Userdata p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _version = m_io.ReadU1();
                _type = ((MetricsContract.DistributionType)m_io.ReadU1());
                switch (Type)
                {
                    case MetricsContract.DistributionType.Bucketed:
                        {
                            _body = new HistogramUint16Bucketed(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.DistributionType.MonBucketed:
                        {
                            _body = new HistogramUint16Bucketed(m_io, this, m_root);
                            break;
                        }
                    case MetricsContract.DistributionType.ValueCountPairs:
                        {
                            _body = new HistogramValueCountPairs(m_io, this, m_root);
                            break;
                        }
                }
            }
            private byte _version;
            private DistributionType _type;
            private KaitaiStruct _body;
            private MetricsContract m_root;
            private MetricsContract.Userdata m_parent;
            public byte Version { get { return _version; } }
            public DistributionType Type { get { return _type; } }
            public KaitaiStruct Body { get { return _body; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Userdata M_Parent { get { return m_parent; } }
        }

        /// <summary>
        /// Bucket #index, claiming to hold exactly `count` hits. See notes in
        /// `histogram_uint16_bucketed` for interpreting index.
        /// </summary>
        public partial class PairUint16 : KaitaiStruct
        {
            public static PairUint16 FromFile(string fileName)
            {
                return new PairUint16(new KaitaiStream(fileName));
            }

            public PairUint16(KaitaiStream p__io, MetricsContract.HistogramUint16Bucketed p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _index = m_io.ReadU2le();
                _count = m_io.ReadU2le();
            }
            private ushort _index;
            private ushort _count;
            private MetricsContract m_root;
            private MetricsContract.HistogramUint16Bucketed m_parent;
            public ushort Index { get { return _index; } }
            public ushort Count { get { return _count; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.HistogramUint16Bucketed M_Parent { get { return m_parent; } }
        }
        public partial class SingleUint64Value : KaitaiStruct
        {
            public static SingleUint64Value FromFile(string fileName)
            {
                return new SingleUint64Value(new KaitaiStream(fileName));
            }

            public SingleUint64Value(KaitaiStream p__io, MetricsContract.Userdata p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _padding = m_io.ReadBytes(4);
                _timestamp = m_io.ReadU8le();
                _value = m_io.ReadU8le();
            }
            private byte[] _padding;
            private ulong _timestamp;
            private ulong _value;
            private MetricsContract m_root;
            private MetricsContract.Userdata m_parent;
            public byte[] Padding { get { return _padding; } }

            /// <summary>
            /// Timestamp in Windows FILETIME format, i.e. number of 100 ns ticks passed since 1601-01-01 00:00:00 UTC.
            /// </summary>
            public ulong Timestamp { get { return _timestamp; } }

            /// <summary>
            /// Metric value as 64-bit unsigned integer.
            /// </summary>
            public ulong Value { get { return _value; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Userdata M_Parent { get { return m_parent; } }
        }
        public partial class ExtAggregatedDoubleValue : KaitaiStruct
        {
            public static ExtAggregatedDoubleValue FromFile(string fileName)
            {
                return new ExtAggregatedDoubleValue(new KaitaiStream(fileName));
            }

            public ExtAggregatedDoubleValue(KaitaiStream p__io, MetricsContract.Userdata p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _count = m_io.ReadU4le();
                _timestamp = m_io.ReadU8le();
                _sum = m_io.ReadF8le();
                _min = m_io.ReadF8le();
                _max = m_io.ReadF8le();
            }
            private uint _count;
            private ulong _timestamp;
            private double _sum;
            private double _min;
            private double _max;
            private MetricsContract m_root;
            private MetricsContract.Userdata m_parent;

            /// <summary>
            /// Count of events aggregated in this event.
            /// </summary>
            public uint Count { get { return _count; } }

            /// <summary>
            /// Timestamp in Windows FILETIME format, i.e. number of 100 ns ticks passed since 1601-01-01 00:00:00 UTC.
            /// </summary>
            public ulong Timestamp { get { return _timestamp; } }

            /// <summary>
            /// Sum of all metric values aggregated in this event.
            /// </summary>
            public double Sum { get { return _sum; } }

            /// <summary>
            /// Minimum of all metric values aggregated in this event.
            /// </summary>
            public double Min { get { return _min; } }

            /// <summary>
            /// Maximum of all metric values aggregated in this event.
            /// </summary>
            public double Max { get { return _max; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Userdata M_Parent { get { return m_parent; } }
        }
        public partial class ExtAggregatedUint64Value : KaitaiStruct
        {
            public static ExtAggregatedUint64Value FromFile(string fileName)
            {
                return new ExtAggregatedUint64Value(new KaitaiStream(fileName));
            }

            public ExtAggregatedUint64Value(KaitaiStream p__io, MetricsContract.Userdata p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _count = m_io.ReadU4le();
                _timestamp = m_io.ReadU8le();
                _sum = m_io.ReadU8le();
                _min = m_io.ReadU8le();
                _max = m_io.ReadU8le();
            }
            private uint _count;
            private ulong _timestamp;
            private ulong _sum;
            private ulong _min;
            private ulong _max;
            private MetricsContract m_root;
            private MetricsContract.Userdata m_parent;

            /// <summary>
            /// Count of events aggregated in this event.
            /// </summary>
            public uint Count { get { return _count; } }

            /// <summary>
            /// Timestamp in Windows FILETIME format, i.e. number of 100 ns ticks passed since 1601-01-01 00:00:00 UTC.
            /// </summary>
            public ulong Timestamp { get { return _timestamp; } }

            /// <summary>
            /// Sum of all metric values aggregated in this event.
            /// </summary>
            public ulong Sum { get { return _sum; } }

            /// <summary>
            /// Minimum of all metric values aggregated in this event.
            /// </summary>
            public ulong Min { get { return _min; } }

            /// <summary>
            /// Maximum of all metric values aggregated in this event.
            /// </summary>
            public ulong Max { get { return _max; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Userdata M_Parent { get { return m_parent; } }
        }
        public partial class SingleDoubleValue : KaitaiStruct
        {
            public static SingleDoubleValue FromFile(string fileName)
            {
                return new SingleDoubleValue(new KaitaiStream(fileName));
            }

            public SingleDoubleValue(KaitaiStream p__io, MetricsContract.Userdata p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _padding = m_io.ReadBytes(4);
                _timestamp = m_io.ReadU8le();
                _value = m_io.ReadF8le();
            }
            private byte[] _padding;
            private ulong _timestamp;
            private double _value;
            private MetricsContract m_root;
            private MetricsContract.Userdata m_parent;
            public byte[] Padding { get { return _padding; } }

            /// <summary>
            /// Timestamp in Windows FILETIME format, i.e. number of 100 ns ticks passed since 1601-01-01 00:00:00 UTC.
            /// </summary>
            public ulong Timestamp { get { return _timestamp; } }

            /// <summary>
            /// Metric value as double.
            /// </summary>
            public double Value { get { return _value; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Userdata M_Parent { get { return m_parent; } }
        }

        /// <summary>
        /// A simple string, length-prefixed with a 2-byte integer.
        /// </summary>
        public partial class LenString : KaitaiStruct
        {
            public static LenString FromFile(string fileName)
            {
                return new LenString(new KaitaiStream(fileName));
            }

            public LenString(KaitaiStream p__io, MetricsContract.Userdata p__parent = null, MetricsContract p__root = null) : base(p__io)
            {
                m_parent = p__parent;
                m_root = p__root;
                _read();
            }
            private void _read()
            {
                _lenValue = m_io.ReadU2le();
                _value = System.Text.Encoding.GetEncoding("UTF-8").GetString(m_io.ReadBytes(LenValue));
            }
            private ushort _lenValue;
            private string _value;
            private MetricsContract m_root;
            private MetricsContract.Userdata m_parent;
            public ushort LenValue { get { return _lenValue; } }
            public string Value { get { return _value; } }
            public MetricsContract M_Root { get { return m_root; } }
            public MetricsContract.Userdata M_Parent { get { return m_parent; } }
        }
        private ushort _eventId;
        private ushort _lenBody;
        private Userdata _body;
        private MetricsContract m_root;
        private KaitaiStruct m_parent;
        private byte[] __raw_body;

        /// <summary>
        /// Type of message, affects format of the body.
        /// </summary>
        public ushort EventId { get { return _eventId; } }

        /// <summary>
        /// Size of body in bytes.
        /// </summary>
        public ushort LenBody { get { return _lenBody; } }

        /// <summary>
        /// Body of Metrics binary protocol message.
        /// </summary>
        public Userdata Body { get { return _body; } }
        public MetricsContract M_Root { get { return m_root; } }
        public KaitaiStruct M_Parent { get { return m_parent; } }
        public byte[] M_RawBody { get { return __raw_body; } }
    }
}
