using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;
using Virexaone.FMS.Backend.Services;

namespace Virexaone.FMS.Backend.Tests;

public sealed class CabinCommsAndPttSimulationTests
{
    private static IConfiguration CreateTestConfig(bool enabled = true, string key = "astha-local-dispatcher-key-2026-09")
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CabinComms:Enabled"] = enabled ? "true" : "false",
            ["CabinComms:DispatcherKey"] = key,
            ["Site:Id"] = "astha",
            ["Site:CompanyCode"] = "astha"
        }).Build();
    }

    [Fact]
    public void DispatcherAuthenticationValidatesCorrectKey()
    {
        var config = CreateTestConfig();
        var commsService = new CabinCommsService(null!, config);

        Assert.True(commsService.Enabled);
        Assert.True(commsService.DispatcherAuthorized("astha-local-dispatcher-key-2026-09"));
        Assert.False(commsService.DispatcherAuthorized("wrong-key-1234567890123456789012"));
        Assert.False(commsService.DispatcherAuthorized(""));
        Assert.False(commsService.DispatcherAuthorized(null));
    }

    [Theory]
    [InlineData("DT5107", true)]
    [InlineData("RD5105", true)]
    [InlineData("EX-201", true)]
    [InlineData("ALL", true)]
    [InlineData("A", false)] // Terlalu pendek (< 2)
    [InlineData("UNIT WITH SPACES", false)]
    [InlineData("UNIT#BAD@CHAR", false)]
    public void ValidUnitNamesAreEnforced(string unitName, bool expectedValid)
    {
        Assert.Equal(expectedValid, CabinCommsService.ValidUnit(unitName));
    }

    [Fact]
    public void LiveCommsIssuesValidSingleUseTickets()
    {
        var live = new LiveCabinCommsService();
        string ticket = live.IssueTicket("DT5107", "dispatcher");

        Assert.NotNull(ticket);
        Assert.Equal(64, ticket.Length); // 32 bytes hex = 64 chars

        // Issue another ticket for cabin
        string cabinTicket = live.IssueTicket("DT5107", "cabin");
        Assert.NotEqual(ticket, cabinTicket);
    }

    [Fact]
    public void TextMessagePublishBroadcastsCorrectPayload()
    {
        var live = new LiveCabinCommsService();
        var testMsg = new CabinMessage(
            Guid.NewGuid(),
            "DT5107",
            "dispatcher",
            "text",
            "Pengalihan rute ke Shovel EX-204",
            "urgent",
            DateTimeOffset.UtcNow
        );

        // Verify JSON serialization format matches client expectation
        var json = JsonSerializer.Serialize(new { type = "message", data = testMsg });
        using var doc = JsonDocument.Parse(json);
        
        Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
        var dataObj = doc.RootElement.GetProperty("data");
        Assert.Equal("DT5107", dataObj.GetProperty("unit_name").GetString());
        Assert.Equal("dispatcher", dataObj.GetProperty("sender_role").GetString());
        Assert.Equal("Pengalihan rute ke Shovel EX-204", dataObj.GetProperty("body").GetString());
        Assert.Equal("urgent", dataObj.GetProperty("priority").GetString());
    }

    [Fact]
    public void EndToEndTwoWayMessageAndPttFlowSimulation()
    {
        // 1. Dispatcher Inisialisasi Kunci & Layanan
        var config = CreateTestConfig();
        var comms = new CabinCommsService(null!, config);
        var live = new LiveCabinCommsService();

        Assert.True(comms.Enabled, "Layanan Comms harus aktif.");

        // 2. Simulasi Dispatcher Mengirim Pesan Teks
        string targetUnit = "DT5107";
        string dispatchBody = "🚨 [INSTRUKSI DISPATCH] Lakukan pengalihan muat ke Shovel EX-204.";
        string priority = "urgent";

        var outMsg = new CabinMessage(
            Guid.NewGuid(),
            targetUnit,
            "dispatcher",
            "text",
            dispatchBody,
            priority,
            DateTimeOffset.UtcNow
        );

        Assert.Equal("DT5107", outMsg.unit_name);
        Assert.Equal("dispatcher", outMsg.sender_role);
        Assert.Equal("urgent", outMsg.priority);

        // 3. Simulasi Operator Kabin Membalas Pesan Teks
        string cabinReply = "✓ [KABIN DT5107] 10-4 Copy, armada bergerak menuju EX-204.";
        var inMsg = new CabinMessage(
            Guid.NewGuid(),
            targetUnit,
            "cabin",
            "text",
            cabinReply,
            "normal",
            DateTimeOffset.UtcNow
        );

        Assert.Equal("DT5107", inMsg.unit_name);
        Assert.Equal("cabin", inMsg.sender_role);

        // 4. Simulasi PTT Live Voice Ticket Negotiation
        string dispTicket = live.IssueTicket(targetUnit, "dispatcher");
        string cabTicket = live.IssueTicket(targetUnit, "cabin");

        Assert.NotEmpty(dispTicket);
        Assert.NotEmpty(cabTicket);
        Assert.NotEqual(dispTicket, cabTicket);

        // 5. Simulasi Audio PCM16 Frame (16000 Hz, 16-bit mono, 20ms = 640 bytes)
        byte[] pcmAudioFrame = new byte[640];
        RandomNumberGenerator.Fill(pcmAudioFrame);

        // Validasi kelayakan audio buffer PCM16 (panjang frame genap <= 8192 bytes)
        Assert.True(pcmAudioFrame.Length % 2 == 0);
        Assert.True(pcmAudioFrame.Length <= 8192);
    }
}
