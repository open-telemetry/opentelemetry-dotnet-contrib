# OpenTelemetry .NET Contrib

[![Slack](https://img.shields.io/badge/slack-@cncf/otel/dotnet-brightgreen.svg?logo=slack)](https://cloud-native.slack.com/archives/C01N3BC2W7Q)
[![Build](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/actions/workflows/ci.yml)

This project is intended to provide helpful libraries and standalone
OpenTelemetry-based utilities that don't fit the express scope of the
[OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet) or
[OpenTelemetry .NET Automatic
Instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation)
projects.

## Contributing

For information on how to contribute, consult [the contributing
guidelines](./CONTRIBUTING.md).

### Project status

Components shipped from this repository are at different maturity levels. The
status for each component is mentioned in their respective Readme files. The
component status falls under one of the following categories:

### Development

Component is currently in development and is not available on
[Nuget](https://www.nuget.org/).

#### Alpha

The component is ready to be used for limited non-critical workloads and the
authors of this component would welcome your feedback. Bugs and performance
problems should be reported, but component owners might not work on them right
away. Components can go through significant breaking changes and there are no
backward compatibility guarantees. Package in this status is appended by
`-alpha` extension.

#### Beta

Same as Alpha, but comparatively more stable. Package in this status is appended
by `-beta` extension.

#### RC

Component is close to stability. There might be minimal breaking changes between
releases. A component at this stage is expected to have had exposure to
non-critical production workloads already during its **Alpha/Beta** phase,
making it suitable for broader usage. Package in this status is appended by
`-rc` extension.

#### Stable

The component is ready for general availability. Bugs and performance problems
should be reported and there's an expectation that the component owners will
work on them. Breaking changes, including configuration options and the
component's output are not expected to happen without prior notice, unless under
special circumstances such as security related fixes.

## Support

This repository is maintained by [.NET Contrib maintainers](#maintainers) team
and [.NET Contrib approvers](#approvers) who can help with reviews and code
approval. However, as individual components are developed by numerous
contributors, approvers and maintainers are not expected to directly contribute
to every component. The list of owners for each component can be found
[here](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/.github/component_owners.yml).

### Triagers

[@open-telemetry/dotnet-contrib-triagers](https://github.com/orgs/open-telemetry/teams/dotnet-contrib-triagers):

* [Martin Thwaites](https://github.com/martinjt), Honeycomb

*Find more about the triager role in [community
repository](https://github.com/open-telemetry/community/blob/main/community-membership.md#triager).*

### Approvers

[@open-telemetry/dotnet-contrib-approvers](https://github.com/orgs/open-telemetry/teams/dotnet-contrib-approvers):

There are no approvers today.

*Find more about the approver role in [community
repository](https://github.com/open-telemetry/community/blob/main/community-membership.md#approver).*

### Maintainers

[@open-telemetry/dotnet-contrib-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-contrib-maintainers):

* [Alan West](https://github.com/alanwest), New Relic
* [Cijo Thomas](https://github.com/cijothomas), Microsoft
* [Mikel Blanchard](https://github.com/CodeBlanch), Microsoft
* [Piotr Kie&#x142;kowicz](https://github.com/Kielek), Splunk
* [Vishwesh Bankwar](https://github.com/vishweshbankwar), Microsoft

*Find more about the maintainer role in [community
repository](https://github.com/open-telemetry/community/blob/main/community-membership.md#maintainer).*

### Emeritus

[Emeritus Maintainer/Approver/Triager](https://github.com/open-telemetry/community/blob/main/community-membership.md#emeritus-maintainerapprovertriager):

* [Prashant Srivastava](https://github.com/srprash)
* [Sergey Kanzhelev](https://github.com/SergeyKanzhelev)
* [Utkarsh Umesan Pillai](https://github.com/utpilla)

Even though, anybody can contribute, there are benefits of being a member of our
community. See to the [community membership
document](https://github.com/open-telemetry/community/blob/master/community-membership.md)
on how to become a
[**Member**](https://github.com/open-telemetry/community/blob/master/community-membership.md#member),
[**Triager**](https://github.com/open-telemetry/community/blob/main/community-membership.md#triager),
[**Approver**](https://github.com/open-telemetry/community/blob/master/community-membership.md#approver),
and
[**Maintainer**](https://github.com/open-telemetry/community/blob/master/community-membership.md#maintainer).

## Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=open-telemetry/opentelemetry-dotnet-contrib)](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/graphs/contributors)
