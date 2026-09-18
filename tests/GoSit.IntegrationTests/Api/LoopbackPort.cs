using System.Net;
using System.Net.Sockets;

namespace GoSit.IntegrationTests.Api;

/// <summary>
/// Finds a free loopback port for a test stub.
///
/// Picking one at random collides: two stubs running in parallel land on the same number
/// often enough to fail a suite intermittently, and the failure —
/// <c>HttpListenerException: the process cannot access the file</c> — looks nothing like
/// a port clash.
///
/// Asking the OS for port 0 and reading back what it assigned is far better. There is
/// still a gap between releasing the port and the listener claiming it, so the caller
/// retries.
/// </summary>
public static class LoopbackPort
{
    public static int Free()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);

        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        return port;
    }

    /// <summary>
    /// Starts a listener on a free port, retrying if something claimed it first.
    /// Returns the base URL it is listening on.
    /// </summary>
    public static string StartOnFreePort(HttpListener listener, int attempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            var baseUrl = $"http://127.0.0.1:{Free()}";

            listener.Prefixes.Clear();
            listener.Prefixes.Add($"{baseUrl}/");

            try
            {
                listener.Start();
                return baseUrl;
            }
            catch (HttpListenerException) when (attempt < attempts)
            {
                // Something took the port between the probe and the claim. Try again.
            }
        }
    }
}
