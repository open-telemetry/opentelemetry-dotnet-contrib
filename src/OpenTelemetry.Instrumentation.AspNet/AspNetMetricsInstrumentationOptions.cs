using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace OpenTelemetry.Instrumentation.AspNet;
public class AspNetMetricsInstrumentationOptions
{
    /// <summary>
    /// Delegate for enrichment of recorded metric with additional tags.
    /// </summary>
    /// <param name="context"><see cref="HttpContext"/>: the HttpContext object. Both Request and Response are available.</param>
    /// <param name="tags"><see cref="TagList"/>: List of current tags. You can add additional tags to this list. </param>
    public delegate void AspNetMetricEnrichmentFunc(string name, HttpContext context, ref TagList tags);

    /// <summary>
    /// Gets or sets a Filter function that determines whether or not to collect telemetry about requests on a per request basis.
    /// The Filter gets the HttpContext, and should return a boolean.
    /// If Filter returns true, the request is collected.
    /// If Filter returns false or throw exception, the request is filtered out.
    /// </summary>
    public Func<HttpContext, bool> Filter { get; set; }

    /// <summary>
    /// Gets or sets an function to enrich a recorded metric with additional custom tags.
    /// </summary>
    public AspNetMetricEnrichmentFunc Enrich { get; set; }
}
