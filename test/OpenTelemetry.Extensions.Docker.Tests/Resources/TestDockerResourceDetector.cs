// <copyright file="TestDockerResourceDetector.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.IO;
using System.Linq;
using OpenTelemetry.Extensions.Docker.Resources;
using OpenTelemetry.Resources;
using Xunit;

namespace OpenTelemetry.Extensions.Docker.Tests.Resources
{
    public class TestDockerResourceDetector
    {
        // Invalid cgroup line
        private const string INVALIDCGROUPLINE =
      "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23zzzz";

        // cgroup line with prefix
        private const string CGROUPLINEWITHPREFIX =
      "13:name=systemd:/podruntime/docker/kubepods/crio-e2cc29debdf85dde404998aa128997a819ff";

        // Expected Container Id with prefix removed
        private const string CONTAINERIDWITHPREFIXREMOVED = "e2cc29debdf85dde404998aa128997a819ff";

        // cgroup line with suffix
        private const string CGROUPLINEWITHSUFFIX =
      "13:name=systemd:/podruntime/docker/kubepods/ac679f8a8319c8cf7d38e1adf263bc08d23.aaaa";

        // Expected Container Id with suffix removed
        private const string CONTAINERIDWITHSUFFIXREMOVED = "ac679f8a8319c8cf7d38e1adf263bc08d23";

        // cgroup line with prefix and suffix
        private const string CGROUPLINEWITHPREFIXandSUFFIX =
            "13:name=systemd:/podruntime/docker/kubepods/crio-dc679f8a8319c8cf7d38e1adf263bc08d23.stuff";

        // Expected Container Id with both prefix and suffix removed
        private const string CONTAINERIDWITHPREFIXANDSUFFIXREMOVED = "dc679f8a8319c8cf7d38e1adf263bc08d23";

        // cgroup line with container Id
        private const string CGROUPLINE =
            "13:name=systemd:/pod/d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

        // Expected Container Id
        private const string CONTAINERID =
            "d86d75589bf6cc254f3e2cc29debdf85dde404998aa128997a819ff991827356";

        [Fact]
        public void TestValidContainer()
        {
            var dockerResourceDetector = new DockerResourceDetector();

            using (TempFile tempFile = new TempFile())
            {
                tempFile.Write(CGROUPLINEWITHPREFIX);
                Assert.Equal(CONTAINERIDWITHPREFIXREMOVED, this.GetContainerId(dockerResourceDetector.BuildResource(tempFile.FilePath)));
            }

            using (TempFile tempFile = new TempFile())
            {
                tempFile.Write(CGROUPLINEWITHSUFFIX);
                Assert.Equal(CONTAINERIDWITHSUFFIXREMOVED, this.GetContainerId(dockerResourceDetector.BuildResource(tempFile.FilePath)));
            }

            using (TempFile tempFile = new TempFile())
            {
                tempFile.Write(CGROUPLINEWITHPREFIXandSUFFIX);
                Assert.Equal(CONTAINERIDWITHPREFIXANDSUFFIXREMOVED, this.GetContainerId(dockerResourceDetector.BuildResource(tempFile.FilePath)));
            }

            using (TempFile tempFile = new TempFile())
            {
                tempFile.Write(CGROUPLINE);
                Assert.Equal(CONTAINERID, this.GetContainerId(dockerResourceDetector.BuildResource(tempFile.FilePath)));
            }
        }

        [Fact]
        public void TestInvalidContainer()
        {
            var dockerResourceDetector = new DockerResourceDetector();

            // test invalid containerId (non-hex)
            using (TempFile tempFile = new TempFile())
            {
                tempFile.Write(INVALIDCGROUPLINE);
                Assert.Equal(dockerResourceDetector.BuildResource(tempFile.FilePath), Resource.Empty);
            }

            // test invalid file
            Assert.Equal(dockerResourceDetector.BuildResource(Path.GetTempPath()), Resource.Empty);
        }

        private string GetContainerId(Resource resource)
        {
            var resourceAttributes = resource.Attributes.ToDictionary(x => x.Key, x => x.Value);
            return resourceAttributes[DockerSemanticConventions.AttributeContainerID]?.ToString();
        }
    }
}
