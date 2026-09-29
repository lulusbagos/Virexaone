using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Virexaone.FMS.Backend.Services;

public sealed class LiveCabinCommsService
{
    private sealed record Ticket(string Unit, string Role, DateTimeOffset ExpiresAt);
    private sealed class Peer(string unit, string role, WebSocket socket)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Unit { get; } = unit;
        public string Role { get; } = role;
        public WebSocket Socket { get; } = socket;
        public SemaphoreSlim SendLock { get; } = new(1, 1);
        public bool Speaking { get; set; }
    }

    private readonly ConcurrentDictionary<string, Ticket> tickets = new();
    private readonly ConcurrentDictionary<Guid, Peer> peers = new();
    private readonly ConcurrentDictionary<string, Guid> speakers = new();

    public string IssueTicket(string unit, string role)
    {
        foreach (var entry in tickets)
            if (entry.Value.ExpiresAt < DateTimeOffset.UtcNow)
                tickets.TryRemove(entry.Key, out _);
        if (tickets.Count >= 500 || peers.Count >= 250)
            throw new InvalidOperationException("live_comms_capacity_reached");
        string secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        tickets[secret] = new Ticket(unit, role, DateTimeOffset.UtcNow.AddSeconds(30));
        return secret;
    }

    public async Task HandleAsync(HttpContext context, string? ticket)
    {
        if (!context.WebSockets.IsWebSocketRequest || string.IsNullOrWhiteSpace(ticket) ||
            !tickets.TryRemove(ticket, out var claim) || claim.ExpiresAt < DateTimeOffset.UtcNow)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var peer = new Peer(claim.Unit, claim.Role, socket);
        peers[peer.Id] = peer;
        await SendTextAsync(peer, JsonSerializer.Serialize(new
        {
            type = "ready", unit_name = peer.Unit, role = peer.Role,
            sample_rate = 16000, audio_format = "pcm_s16le_mono"
        }), context.RequestAborted);
        var buffer = new byte[8192];
        try
        {
            while (socket.State == WebSocketState.Open && !context.RequestAborted.IsCancellationRequested)
            {
                var frame = await socket.ReceiveAsync(buffer, context.RequestAborted);
                if (frame.MessageType == WebSocketMessageType.Close) break;
                if (!frame.EndOfMessage || frame.Count == 0) continue;
                if (frame.MessageType == WebSocketMessageType.Text)
                {
                    string command;
                    try
                    {
                        using var json = JsonDocument.Parse(buffer.AsMemory(0, frame.Count));
                        command = json.RootElement.ValueKind == JsonValueKind.Object &&
                            json.RootElement.TryGetProperty("type", out var kind) &&
                            kind.ValueKind == JsonValueKind.String ? kind.GetString() ?? "" : "";
                    }
                    catch (JsonException) { continue; }
                    if (command == "ptt_start")
                    {
                        if (speakers.TryAdd(peer.Unit, peer.Id))
                        {
                            peer.Speaking = true;
                            await BroadcastAsync(peer, "voice_start", context.RequestAborted);
                            await SendTextAsync(peer, "{\"type\":\"ptt_ready\"}", context.RequestAborted);
                        }
                        else await SendTextAsync(peer, "{\"type\":\"busy\"}", context.RequestAborted);
                    }
                    else if (command == "ptt_stop")
                        await StopSpeakingAsync(peer, context.RequestAborted);
                }
                else if (frame.MessageType == WebSocketMessageType.Binary &&
                    peer.Speaking && frame.Count <= 8192 && frame.Count % 2 == 0)
                {
                    foreach (var listener in peers.Values)
                        if (listener.Unit == peer.Unit && listener.Role != peer.Role)
                            await SendAsync(listener, buffer.AsMemory(0, frame.Count),
                                WebSocketMessageType.Binary, context.RequestAborted);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        finally
        {
            await StopSpeakingAsync(peer, CancellationToken.None);
            peers.TryRemove(peer.Id, out _);
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    using var closeTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    if (socket.State == WebSocketState.CloseReceived)
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", closeTimeout.Token);
                    else
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", closeTimeout.Token);
                }
                catch (OperationCanceledException) { }
                catch (WebSocketException) { }
            }
        }
    }

    public async Task PublishMessageAsync(CabinMessage message, CancellationToken token)
    {
        string json = JsonSerializer.Serialize(new { type = "message", data = message });
        foreach (var peer in peers.Values)
            if (peer.Role == "dispatcher" || peer.Unit == message.unit_name || message.unit_name == "ALL")
                await SendTextAsync(peer, json, token);
    }

    private async Task StopSpeakingAsync(Peer peer, CancellationToken token)
    {
        if (!peer.Speaking) return;
        peer.Speaking = false;
        speakers.TryRemove(new KeyValuePair<string, Guid>(peer.Unit, peer.Id));
        await BroadcastAsync(peer, "voice_stop", token);
    }

    private async Task BroadcastAsync(Peer sender, string type, CancellationToken token)
    {
        string json = JsonSerializer.Serialize(new { type, unit_name = sender.Unit, sender_role = sender.Role });
        foreach (var peer in peers.Values)
            if (peer.Unit == sender.Unit && peer.Role != sender.Role)
                await SendTextAsync(peer, json, token);
    }

    private Task SendTextAsync(Peer peer, string text, CancellationToken token) =>
        SendAsync(peer, Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, token);

    private static async Task SendAsync(Peer peer, ReadOnlyMemory<byte> data,
        WebSocketMessageType type, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(750));
        try
        {
            await peer.SendLock.WaitAsync(timeout.Token);
            try
            {
                if (peer.Socket.State == WebSocketState.Open)
                    await peer.Socket.SendAsync(data, type, true, timeout.Token);
            }
            finally { peer.SendLock.Release(); }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }
}
