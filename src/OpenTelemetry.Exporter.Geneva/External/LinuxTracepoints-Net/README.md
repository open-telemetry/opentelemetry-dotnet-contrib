# LinuxTracepoints-Net

The code in this folder came from the
[LinuxTracepoints-Net](https://github.com/microsoft/LinuxTracepoints-Net) repo
([commit
974c475](https://github.com/microsoft/LinuxTracepoints-Net/blob/974c47522d053c915009ef5112840026eaf22adb)).

Only the files required to build
`Microsoft.LinuxTracepoints.Provider.PerfTracepoint` were included.

The following changes were made:

* `#nullable enabled` added at the top of all files. This is because
  LinuxTracepoints-Net has `<nullable>enabled</nullable>` repo-wide but
  GenevaExporter has `<nullable>disabled</nullable>` at the moment.

* `public` types made `internal`.
