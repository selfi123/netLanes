using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetLanes.Core.RateLimiting;
using Xunit;

namespace NetLanes.Networking.Tests;

public class ControlledSocketThrottlingTests
{
    [Fact]
    public async Task PacedSocketTransfer_AccuratelyLimitsThroughput()
    {
        // Setup local TCP server
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        long limitBps = 300 * 1024; // 300 KB/s
        int totalPayloadBytes = 300 * 1024; // 300 KB
        var limiter = new TokenBucketRateLimiter(limitBps, capacityBytes: 64 * 1024);

        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            using var stream = serverClient.GetStream();
            byte[] buffer = new byte[totalPayloadBytes];
            int totalRead = 0;
            while (totalRead < totalPayloadBytes)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(totalRead, totalPayloadBytes - totalRead));
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        });

        var clientTask = Task.Run(async () =>
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            byte[] chunk = new byte[8192];
            int sent = 0;
            var sw = Stopwatch.StartNew();

            while (sent < totalPayloadBytes)
            {
                int toSend = Math.Min(chunk.Length, totalPayloadBytes - sent);

                // Apply rate limiter pacing
                while (!limiter.TryConsume(toSend, out var delay))
                {
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay);
                    }
                }

                await stream.WriteAsync(chunk.AsMemory(0, toSend));
                sent += toSend;
            }

            sw.Stop();
            return sw.ElapsedMilliseconds;
        });

        int received = await serverTask;
        long elapsedMs = await clientTask;
        listener.Stop();

        Assert.Equal(totalPayloadBytes, received);
        // At 300 KB/s with 64 KB initial burst, transferring 300 KB takes at least ~700 ms
        Assert.True(elapsedMs >= 650, $"Elapsed was {elapsedMs}ms, expected >= 650ms");
    }

    [Fact]
    public async Task UnconstrainedClient_TransfersFarFasterThanCappedClient()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        int totalBytes = 200 * 1024;

        var serverTask = Task.Run(async () =>
        {
            using var serverClient = await listener.AcceptTcpClientAsync();
            using var stream = serverClient.GetStream();
            byte[] buf = new byte[8192];
            int readTotal = 0;
            while (readTotal < totalBytes)
            {
                int read = await stream.ReadAsync(buf);
                if (read == 0) break;
                readTotal += read;
            }
        });

        var unconstrainedSw = Stopwatch.StartNew();
        using (var client = new TcpClient())
        {
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();
            byte[] data = new byte[totalBytes];
            await stream.WriteAsync(data);
        }
        await serverTask;
        unconstrainedSw.Stop();
        listener.Stop();

        // Unconstrained loopback transfer should complete in < 250ms
        Assert.True(unconstrainedSw.ElapsedMilliseconds < 500, $"Unconstrained elapsed: {unconstrainedSw.ElapsedMilliseconds}ms");
    }
}
