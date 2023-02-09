# StratifiedSampling in OpenTelemetry .NET - Example

This is a proof of concept for how we can achieve stratified sampling in OpenTelemetry.
This is based on an example scenario of requiring different sampling rates depending on
whether the query is user-initiated or programmatic query.

Stratified sampling is a way to divide a population (e.g., "all queries to a service") into mutually
exclusive sub-populations aka "strata". For example, the strata here are "all user-initiated queries",
"all programmatic queries". Each stratum is then sampled using a probabilistic sampling method.
This ensures that all sub-populations are represented.

We use disproportionate stratified sampling here - i.e., the sample size of each sub-population here
is not proportionate to their occurrence in the overall population - in this example, we want to ensure
that all user-initiated queries are represented, so we use a 100% sampling rate for it, while the sampling
rate chosen for programmatic queries is much lower.

You should see the following output on the Console when you run this application. This shows that the
two sub-populations (strata) are being sampled independently.

```text
StratifiedSampler handling userinitiated query
Activity.TraceId:            1a122d63e5f8d32cb8ebd3e402eb5389
Activity.SpanId:             83bdc6bbebea1df8
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       1ddd00d845ad645e
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8156879Z
Activity.Duration:           00:00:00.0008656
Activity.Tags:
    queryType: userInitiated
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            1a122d63e5f8d32cb8ebd3e402eb5389
Activity.SpanId:             1ddd00d845ad645e
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8115186Z
Activity.Duration:           00:00:00.0424036
Activity.Tags:
    queryType: userInitiated
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
Activity.TraceId:            03cddefbc0e0f61851135f814522a2df
Activity.SpanId:             8d4fa3e27a12f666
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       8c46e4dc6d0f418c
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8553756Z
Activity.Duration:           00:00:00.0000019
Activity.Tags:
    queryType: programmatic
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            03cddefbc0e0f61851135f814522a2df
Activity.SpanId:             8c46e4dc6d0f418c
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8553725Z
Activity.Duration:           00:00:00.0069444
Activity.Tags:
    queryType: programmatic
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
Activity.TraceId:            10f215a7ee6407da59d2601e39592c2a
Activity.SpanId:             ec3e18694b8cd7cc
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       4df9f9b40b3d009b
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8851028Z
Activity.Duration:           00:00:00.0000013
Activity.Tags:
    queryType: programmatic
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            10f215a7ee6407da59d2601e39592c2a
Activity.SpanId:             4df9f9b40b3d009b
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.8850839Z
Activity.Duration:           00:00:00.0099928
Activity.Tags:
    queryType: programmatic
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
Activity.TraceId:            e8659bfb274033b680783301ba46d406
Activity.SpanId:             e03667a2841594b5
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       99888344a37cb573
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9198094Z
Activity.Duration:           00:00:00.0000006
Activity.Tags:
    queryType: programmatic
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            e8659bfb274033b680783301ba46d406
Activity.SpanId:             99888344a37cb573
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9198062Z
Activity.Duration:           00:00:00.0091424
Activity.Tags:
    queryType: programmatic
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
Activity.TraceId:            e80614a36538d9c101d39dc2c449cde6
Activity.SpanId:             729f8c6d00b7dd4c
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       f76127b28c07d082
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9485531Z
Activity.Duration:           00:00:00.0000009
Activity.Tags:
    queryType: programmatic
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            e80614a36538d9c101d39dc2c449cde6
Activity.SpanId:             f76127b28c07d082
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9485488Z
Activity.Duration:           00:00:00.0078570
Activity.Tags:
    queryType: programmatic
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling userinitiated query
Activity.TraceId:            8a5894524f1bea2a7bd8271fef9ec22d
Activity.SpanId:             94b5b004287bd678
Activity.TraceFlags:         Recorded
Activity.ParentSpanId:       99600e9fe011c1cc
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9660777Z
Activity.Duration:           00:00:00.0000005
Activity.Tags:
    queryType: userInitiated
    foo: child
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

Activity.TraceId:            8a5894524f1bea2a7bd8271fef9ec22d
Activity.SpanId:             99600e9fe011c1cc
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: StratifiedSampling.POC
Activity.DisplayName:        Main
Activity.Kind:               Internal
Activity.StartTime:          2023-02-09T05:19:30.9660744Z
Activity.Duration:           00:00:00.0230182
Activity.Tags:
    queryType: userInitiated
    foo: bar
Resource associated with Activity:
    service.name: unknown_service:Examples.StratifiedSamplingByQueryType

StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
StratifiedSampler handling programmatic query
```
