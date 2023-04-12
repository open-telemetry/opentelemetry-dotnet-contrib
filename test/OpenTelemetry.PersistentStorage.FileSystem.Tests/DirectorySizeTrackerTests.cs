// <copyright file="DirectorySizeTrackerTests.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.PersistentStorage.FileSystem.Tests;

public class DirectorySizeTrackerTests
{
    [Fact]
    public void VerifyDirectorySizeTracker()
    {
        var testDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        testDirectory.Create();

        var directorySizeTracker = new DirectorySizeTracker(maxSizeInBytes: 100, path: testDirectory.FullName);

        // new directory, expected to have space
        Assert.True(directorySizeTracker.IsSpaceAvailable(out long currentSize1));
        Assert.Equal(0, currentSize1);

        // add a file and check current space.
        directorySizeTracker.FileAdded(30);
        Assert.True(directorySizeTracker.IsSpaceAvailable(out long currentSize2));
        Assert.Equal(30, currentSize2);

        // add a file and check current space. Here we've exceeded the configured max size
        directorySizeTracker.FileAdded(100);
        Assert.False(directorySizeTracker.IsSpaceAvailable(out long currentSize3));
        Assert.Equal(130, currentSize3);

        // remove a file and check current space.
        directorySizeTracker.FileRemoved(50);
        Assert.True(directorySizeTracker.IsSpaceAvailable(out long currentSize4));
        Assert.Equal(80, currentSize4);

        // since we haven't actually written any files to disk,
        // Recount will reset to zero.
        directorySizeTracker.RecountCurrentSize();
        Assert.True(directorySizeTracker.IsSpaceAvailable(out long currentSize5));
        Assert.Equal(0, currentSize5);

        // cleanup
        testDirectory.Delete();
    }
}
