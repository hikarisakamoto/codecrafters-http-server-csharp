using System.Net;
using System.Net.Sockets;
using System.Text;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

TcpListener server = new TcpListener(IPAddress.Any, 4221);
server.Start();
Console.WriteLine("Server started, waiting for client...");
using var client = await server.AcceptTcpClientAsync(); // wait for client
await using var stream = client.GetStream();
Console.WriteLine("Client connected, reading request...");
// Read the request before replying; closing with unread data would reset the connection.
var bytesRead = await stream.ReadAsync(new byte[1024]);
Console.WriteLine($"Request read ({bytesRead} bytes), sending response...");
var body = "HTTP/1.1 200 OK\r\n\r\n";
Console.WriteLine($"Responding with body: {body}");
await stream.WriteAsync(Encoding.UTF8.GetBytes(body));
