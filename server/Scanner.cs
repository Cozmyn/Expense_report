using System.Buffers.Binary;
using System.Net.Sockets;

namespace Decont;

public interface IDocumentScanner
{
    Task<bool> Clean(byte[] bytes, CancellationToken ct);
}

public class ClamScanner(IConfiguration config) : IDocumentScanner
{
    public async Task<bool> Clean(byte[] bytes, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(45));
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(
            config["Scanner:Host"] ?? "localhost",
            config.GetValue<int?>("Scanner:Port") ?? 3310,
            limit.Token
        );
        await using var stream = tcp.GetStream();
        await stream.WriteAsync("zINSTREAM\0"u8.ToArray(), limit.Token);
        for (int offset = 0; offset < bytes.Length; offset += 65536)
        {
            int count = Math.Min(65536, bytes.Length - offset);
            byte[] head = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(head, count);
            await stream.WriteAsync(head, limit.Token);
            await stream.WriteAsync(bytes.AsMemory(offset, count), limit.Token);
        }
        await stream.WriteAsync(new byte[4], limit.Token);
        using var reply = new MemoryStream();
        byte[] buffer = new byte[1024];
        while (reply.Length < 4096)
        {
            var n = await stream.ReadAsync(buffer, limit.Token);
            if (n == 0)
                break;
            await reply.WriteAsync(buffer.AsMemory(0, n), limit.Token);
            if (buffer.AsSpan(0, n).Contains((byte)0))
                break;
        }
        var verdict = System.Text.Encoding.UTF8.GetString(reply.ToArray());
        if (verdict.Contains("FOUND"))
            return false;
        if (verdict.Contains("OK"))
            return true;
        throw new IOException("Scanner returned no definitive verdict");
    }
}
