// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Resources.AWS.Tests;

public class AsyncHelperTests
{
    [Fact]
    public void RunSyncTaskCompletesSuccessfully()
    {
        // Arrange
        Func<Task> task = async () =>
        {
            await Task.Delay(100);
        };

        // Act
        var exception = Record.Exception(() => AsyncHelper.RunSync(task));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void RunSyncTaskThrowsException()
    {
        // Arrange
        Func<Task> task = async () =>
        {
            await Task.Delay(100);
            throw new InvalidOperationException("Test exception");
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => AsyncHelper.RunSync(task));
        Assert.Equal("Test exception", exception.Message);
    }

    [Fact]
    public void RunSyncTaskCancellationThrowsTaskCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> task = async () =>
        {
            await Task.Delay(100, cts.Token);
        };

        // Act & Assert
        var exception = Assert.Throws<TaskCanceledException>(() => AsyncHelper.RunSync(task));
        Assert.Equal(cts.Token, exception.CancellationToken);
    }

    [Fact]
    public void RunSyncTaskWithResultCompletesSuccessfully()
    {
        // Arrange
        Func<Task<string>> task = async () =>
        {
            await Task.Delay(100);
            return "Completed";
        };

        // Act
        var result = AsyncHelper.RunSync(task);

        // Assert
        Assert.Equal("Completed", result);
    }

    [Fact]
    public void RunSyncTaskWithResultThrowsException()
    {
        // Arrange
        Func<Task<string>> task = async () =>
        {
            await Task.Delay(100);
            throw new InvalidOperationException("Test exception");
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => AsyncHelper.RunSync(task));
        Assert.Equal("Test exception", exception.Message);
    }

    [Fact]
    public void RunSyncTaskWithResultCancellationThrowsTaskCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task<string>> task = async () =>
        {
            await Task.Delay(100, cts.Token);
            return "Completed";
        };

        // Act & Assert
        var exception = Assert.Throws<TaskCanceledException>(() => AsyncHelper.RunSync(task));
        Assert.Equal(cts.Token, exception.CancellationToken);
    }
}
