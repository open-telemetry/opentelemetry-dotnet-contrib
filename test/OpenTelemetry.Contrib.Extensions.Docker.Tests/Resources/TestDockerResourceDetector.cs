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

using System.Linq;
using OpenTelemetry.Contrib.Extensions.Docker.Resources;
using Xunit;

namespace OpenTelemetry.Contrib.Extensions.Docker.Tests.Resources
{
    public class TestDockerResourceDetector
    {
        private const string DOCKERSAMPLEFILEPATH = "Resources/SampleFile/testcgroup";

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
        public void TestExtractResourceAttributes()
        {
            var dockerResourceDetector = new DockerResourceDetector();
            var resourceAttributes = dockerResourceDetector.BuildResourceAttributes(CONTAINERID).ToDictionary(x => x.Key, x => x.Value);
            Assert.Equal(CONTAINERID, resourceAttributes[DockerSemanticConventions.AttributeContainerID]);
        }

        [Fact]
        public void TestExtractContainerId()
        {
            var dockerResourceDetector = new DockerResourceDetector();
            Assert.Equal("da261b882cd310e64ced2e31b938f9289261e4c7d59efb2d651ca2fe802b7764", dockerResourceDetector.ExtractContainerId(DOCKERSAMPLEFILEPATH));
        }

        [Fact]
        public void TestGetIdFromLine()
        {
            var dockerResourceDetector = new DockerResourceDetector();
            Assert.Null(dockerResourceDetector.GetIdFromLine(INVALIDCGROUPLINE));
            Assert.Equal(CONTAINERIDWITHPREFIXREMOVED, dockerResourceDetector.GetIdFromLine(CGROUPLINEWITHPREFIX));
            Assert.Equal(CONTAINERIDWITHSUFFIXREMOVED, dockerResourceDetector.GetIdFromLine(CGROUPLINEWITHSUFFIX));
            Assert.Equal(CONTAINERIDWITHPREFIXANDSUFFIXREMOVED, dockerResourceDetector.GetIdFromLine(CGROUPLINEWITHPREFIXandSUFFIX));
            Assert.Equal(CONTAINERID, dockerResourceDetector.GetIdFromLine(CGROUPLINE));
        }
    }
}
