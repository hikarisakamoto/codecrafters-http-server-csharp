using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

TcpListener server = new(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Server started, waiting for client...");
while (true)
{
    using var client = await server.AcceptTcpClientAsync(); // wait for client
    await using var stream = client.GetStream();
    Console.WriteLine("Client connected, reading request...");
    // Read the request before replying; closing with unread data would reset the connection.
    var buffer = new byte[1_024];
    var bytesRead = await stream.ReadAsync(buffer);
    Console.WriteLine($"Request read ({bytesRead} bytes), sending response...");
    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    var requestLines = request.Split("\r\n");
    var requestLine = requestLines[0];
    var requestParts = requestLine.Split(' ');
    var target = requestParts[1];
    var body = string.Empty;
    if (target == "/index.html")
    {
        body = "HTTP/1.1 200 OK\r\n\r\n";
    }
    else
    {
        body = "HTTP/1.1 404 Not Found\r\n\r\n";
    }
    Console.WriteLine($"Responding with body: {body}");
    await stream.WriteAsync(Encoding.UTF8.GetBytes(body));
}