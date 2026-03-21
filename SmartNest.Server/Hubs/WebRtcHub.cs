using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SmartNest.Server.Hubs;

[Authorize]
public class WebRtcHub : Hub
{
    private static readonly Dictionary<string, string> _rooms = new();
    private readonly ILogger<WebRtcHub> _log;

    public WebRtcHub(ILogger<WebRtcHub> log) => _log = log;

    public async Task JoinRoom(string roomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        _rooms[Context.ConnectionId] = roomId;
        await Clients.OthersInGroup(roomId).SendAsync("PeerJoined", Context.ConnectionId);
        _log.LogInformation("[WebRTC] {Conn} → room {Room}", Context.ConnectionId[..8], roomId);
    }

    public async Task RequestOffer(string roomId) =>
        await Clients.OthersInGroup(roomId).SendAsync("OfferRequested", Context.ConnectionId);

    public async Task SendOffer(string roomId, string sdp, string targetId) =>
        await Clients.Client(targetId).SendAsync("ReceiveOffer", sdp, Context.ConnectionId);

    public async Task SendAnswer(string roomId, string sdp, string targetId) =>
        await Clients.Client(targetId).SendAsync("ReceiveAnswer", sdp);

    public async Task SendIceCandidate(string roomId, string candidate, string? targetId = null)
    {
        if (targetId != null)
            await Clients.Client(targetId).SendAsync("ReceiveIceCandidate", candidate);
        else
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveIceCandidate", candidate);
    }

    public async Task NotifyRecordingStarted(string roomId, string sessionId) =>
        await Clients.OthersInGroup(roomId).SendAsync("RecordingStarted", sessionId);

    public async Task NotifyRecordingSaved(string roomId, string fileName) =>
        await Clients.OthersInGroup(roomId).SendAsync("RecordingSaved", fileName);

    public override async Task OnDisconnectedAsync(Exception? ex)
    {
        if (_rooms.TryGetValue(Context.ConnectionId, out var room))
        {
            _rooms.Remove(Context.ConnectionId);
            await Clients.OthersInGroup(room).SendAsync("PeerLeft", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(ex);
    }
}
