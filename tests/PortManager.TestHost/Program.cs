using System.Net;
using System.Net.Sockets;

var count = 1;
var countIndex = Array.FindIndex(args, argument => argument == "--count");
if (countIndex >= 0 && countIndex + 1 < args.Length)
{
    int.TryParse(args[countIndex + 1], out count);
}

count = Math.Clamp(count, 1, 8);
var listeners = new List<TcpListener>();

try
{
    for (var index = 0; index < count; index++)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        listeners.Add(listener);
    }

    var ports = listeners
        .Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port)
        .OrderBy(port => port);

    Console.WriteLine($"{Environment.ProcessId};{string.Join(',', ports)}");
    await Console.Out.FlushAsync();
    await Task.Delay(Timeout.InfiniteTimeSpan);
}
finally
{
    foreach (var listener in listeners)
    {
        listener.Stop();
    }
}
