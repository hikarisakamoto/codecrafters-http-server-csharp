using System.IO.Compression;
using System.Text;

/// <summary>An HTTP/1.1 response with a text body, optionally gzip-encoded.</summary>
internal sealed record HttpResponse(string Status, string Body = "", string ContentType = "text/plain", bool Gzip = false)
{
    /// <summary>Serializes the response; <paramref name="closeConnection"/> adds <c>Connection: close</c>.</summary>
    public byte[] ToBytes(bool closeConnection)
    {
        var body = Encoding.UTF8.GetBytes(Body);
        if (Gzip)
        {
            body = Compress(body);
        }

        var head =
            $"HTTP/1.1 {Status}\r\n" +
            $"Content-Type: {ContentType}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            (Gzip ? "Content-Encoding: gzip\r\n" : "") +
            (closeConnection ? "Connection: close\r\n" : "") +
            "\r\n";

        return [.. Encoding.ASCII.GetBytes(head), .. body];
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(data);
        }

        return output.ToArray();
    }
}
