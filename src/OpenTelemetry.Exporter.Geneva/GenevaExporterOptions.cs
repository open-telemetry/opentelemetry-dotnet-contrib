using System;
using System.Collections.Generic;

namespace OpenTelemetry.Exporter.Geneva
{
    public class GenevaExporterOptions
    {
        private IReadOnlyDictionary<string, object> _fields = new Dictionary<string, object>(1)
        {
            [Schema.V40.PartA.Ver] = "4.0",
        };

        public string ConnectionString { get; set; }

        public IEnumerable<string> CustomFields { get; set; }

        public IReadOnlyDictionary<string, string> TableNameMappings { get; set; }

        public IReadOnlyDictionary<string, object> PrepopulatedFields
        {
            get => this._fields;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var schemaVersion = "4.0";

                if (value.ContainsKey(Schema.V40.PartA.Ver))
                {
                    schemaVersion = value[Schema.V40.PartA.Ver] as string;
                }

                if (schemaVersion != "2.1" && schemaVersion != "4.0")
                {
                    throw new ArgumentException("Unsupported schema version, only 2.1 and 4.0 are supported.");
                }

                if (value.ContainsKey(Schema.V40.PartA.Name))
                {
                    throw new ArgumentException("Event name cannot be pre-populated.");
                }

                if (value.ContainsKey(Schema.V40.PartA.Time))
                {
                    throw new ArgumentException("Event timestamp cannot be pre-populated.");
                }

                var copy = new Dictionary<string, object>(value.Count + 1) { [Schema.V40.PartA.Ver] = schemaVersion };
                foreach (var entry in value)
                {
                    copy[entry.Key] = entry.Value; // shallow copy
                }

                this._fields = copy;
            }
        }

        internal Func<object, string> ConvertToJson = obj => "ERROR: GenevaExporterOptions.ConvertToJson not configured.";
    }
}
