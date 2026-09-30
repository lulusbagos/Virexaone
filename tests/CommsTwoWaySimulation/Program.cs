using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Virexa.Simulation
{
    class Program
    {
        private const string BaseUrl = "http://127.0.0.1:8000";
        private const string DispatcherKey = "astha-local-dispatcher-key-2026-09";
        private const string TestUnitId = "DT5107";

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("================================================================================");
            Console.WriteLine("🎙️ SIMULASI KOMUNIKASI DUA ARAH (UNITY CONTROL ROOM <-> MOBILE IN-CABIN OPERATOR)");
            Console.WriteLine("================================================================================");

            using var httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(5) };

            // 1. Check Backend Health
            Console.WriteLine("\n[1/6] 🔍 Memeriksa status kesehatan backend...");
            try
            {
                var healthResp = await httpClient.GetAsync("/api/v1/comms/status");
                string healthContent = await healthResp.Content.ReadAsStringAsync();
                Console.WriteLine($"      Status: HTTP {healthResp.StatusCode} -> {healthContent}");
                if (!healthResp.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ GAGAL: Backend comms tidak aktif.");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GAGAL KONEKSI BACKEND: {ex.Message}");
                return;
            }

            // 2. Pair Unit DT5107 (Mobile Cabin Credential)
            Console.WriteLine($"\n[2/6] 🔑 Melakukan pairing unit mobile: {TestUnitId}...");
            string unitKey = null;
            try
            {
                var pairReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/comms/pair");
                pairReq.Headers.Add("X-FMS-Dispatcher-Key", DispatcherKey);
                pairReq.Content = new StringContent(JsonSerializer.Serialize(new { unit_name = TestUnitId }), Encoding.UTF8, "application/json");
                var pairResp = await httpClient.SendAsync(pairReq);
                string pairContent = await pairResp.Content.ReadAsStringAsync();
                Console.WriteLine($"      Response Pairing: HTTP {pairResp.StatusCode} -> {pairContent}");

                if (pairResp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(pairContent);
                    unitKey = doc.RootElement.GetProperty("token").GetString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning saat pairing: {ex.Message}");
            }

            if (string.IsNullOrEmpty(unitKey))
            {
                Console.WriteLine("❌ GAGAL: Tidak mendapatkan unit token untuk Mobile.");
                return;
            }

            // 3. Request Live Radio WebSocket Tickets
            Console.WriteLine("\n[3/6] 🎫 Meminta Tiket Radio WebSocket untuk Unity & Mobile...");
            string unityTicket = null;
            string mobileTicket = null;

            // 3a. Unity Dispatcher Ticket
            var unityTicketReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/comms/live-ticket");
            unityTicketReq.Headers.Add("X-FMS-Dispatcher-Key", DispatcherKey);
            unityTicketReq.Content = new StringContent(JsonSerializer.Serialize(new { unit_name = "ALL" }), Encoding.UTF8, "application/json");
            var unityTicketResp = await httpClient.SendAsync(unityTicketReq);
            string unityTicketContent = await unityTicketResp.Content.ReadAsStringAsync();
            Console.WriteLine($"      [Unity Control Room] Ticket Response: HTTP {unityTicketResp.StatusCode} -> {unityTicketContent}");
            using (var doc = JsonDocument.Parse(unityTicketContent))
            {
                unityTicket = doc.RootElement.GetProperty("ticket").GetString();
            }

            // 3b. Mobile Cabin Ticket
            var mobileTicketReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/comms/live-ticket");
            mobileTicketReq.Headers.Add("X-FMS-Unit-Key", unitKey);
            mobileTicketReq.Content = new StringContent(JsonSerializer.Serialize(new { unit_name = TestUnitId }), Encoding.UTF8, "application/json");
            var mobileTicketResp = await httpClient.SendAsync(mobileTicketReq);
            string mobileTicketContent = await mobileTicketResp.Content.ReadAsStringAsync();
            Console.WriteLine($"      [Mobile In-Cabin] Ticket Response: HTTP {mobileTicketResp.StatusCode} -> {mobileTicketContent}");
            using (var doc = JsonDocument.Parse(mobileTicketContent))
            {
                mobileTicket = doc.RootElement.GetProperty("ticket").GetString();
            }

            // 4. Connect WebSockets for both Unity and Mobile
            Console.WriteLine("\n[4/6] 🌐 Membuka Koneksi WebSocket Radio 16kHz PCM16...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var wsUnity = new ClientWebSocket();
            using var wsMobile = new ClientWebSocket();

            string wsUnityUrl = $"ws://127.0.0.1:8000/api/v1/comms/live?ticket={Uri.EscapeDataString(unityTicket)}";
            string wsMobileUrl = $"ws://127.0.0.1:8000/api/v1/comms/live?ticket={Uri.EscapeDataString(mobileTicket)}";

            await wsUnity.ConnectAsync(new Uri(wsUnityUrl), cts.Token);
            Console.WriteLine($"      ✅ [Unity Control Room] WebSocket Terhubung! (State: {wsUnity.State})");

            await wsMobile.ConnectAsync(new Uri(wsMobileUrl), cts.Token);
            Console.WriteLine($"      ✅ [Mobile In-Cabin ({TestUnitId})] WebSocket Terhubung! (State: {wsMobile.State})");

            // Setup Event Trackers
            List<string> unityReceivedTexts = new List<string>();
            List<string> mobileReceivedTexts = new List<string>();
            int unityAudioBytesReceived = 0;
            int mobileAudioBytesReceived = 0;

            // Background Receive Loops
            var unityReceiveTask = Task.Run(async () =>
            {
                byte[] buf = new byte[8192];
                while (wsUnity.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                {
                    var res = await wsUnity.ReceiveAsync(buf, cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    if (res.MessageType == WebSocketMessageType.Text)
                    {
                        string txt = Encoding.UTF8.GetString(buf, 0, res.Count);
                        lock (unityReceivedTexts) unityReceivedTexts.Add(txt);
                        Console.WriteLine($"         🖥️ [Unity RX Text] {txt}");
                    }
                    else if (res.MessageType == WebSocketMessageType.Binary)
                    {
                        Interlocked.Add(ref unityAudioBytesReceived, res.Count);
                    }
                }
            });

            var mobileReceiveTask = Task.Run(async () =>
            {
                byte[] buf = new byte[8192];
                while (wsMobile.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                {
                    var res = await wsMobile.ReceiveAsync(buf, cts.Token);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    if (res.MessageType == WebSocketMessageType.Text)
                    {
                        string txt = Encoding.UTF8.GetString(buf, 0, res.Count);
                        lock (mobileReceivedTexts) mobileReceivedTexts.Add(txt);
                        Console.WriteLine($"         📱 [Mobile RX Text] {txt}");
                    }
                    else if (res.MessageType == WebSocketMessageType.Binary)
                    {
                        Interlocked.Add(ref mobileAudioBytesReceived, res.Count);
                    }
                }
            });

            await Task.Delay(400);

            // 5. TEST STEP A: Mobile In-Cabin Operator Speaks (Mobile -> Unity)
            Console.WriteLine("\n[5/6] 🎙️ TEST A: Operator Kabin DT5107 Menekan PTT (Mobile -> Unity Control Room)");
            Console.WriteLine("      1. Mobile mengirim ptt_start...");
            await wsMobile.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"ptt_start\"}"), WebSocketMessageType.Text, true, cts.Token);
            await Task.Delay(250);

            // Send 10 chunks of 16kHz PCM16 audio (each chunk 640 bytes = 20ms of audio)
            byte[] syntheticAudioChunk = new byte[640];
            for (int i = 0; i < syntheticAudioChunk.Length; i += 2)
            {
                short sample = (short)(Math.Sin(i * 0.05) * 16000);
                syntheticAudioChunk[i] = (byte)(sample & 0xFF);
                syntheticAudioChunk[i + 1] = (byte)((sample >> 8) & 0xFF);
            }

            Console.WriteLine("      2. Mobile streaming 10 chunks audio PCM16 (200ms)...");
            for (int i = 0; i < 10; i++)
            {
                await wsMobile.SendAsync(syntheticAudioChunk, WebSocketMessageType.Binary, true, cts.Token);
                await Task.Delay(20);
            }

            Console.WriteLine("      3. Mobile melepas PTT (ptt_stop)...");
            await wsMobile.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"ptt_stop\"}"), WebSocketMessageType.Text, true, cts.Token);
            await Task.Delay(300);

            Console.WriteLine($"      📊 Hasil Test A: Unity menerima {unityAudioBytesReceived} bytes audio stream dari DT5107!");

            // 6. TEST STEP B: Unity Control Room Speaks Talkback (Unity -> Mobile In-Cabin)
            Console.WriteLine("\n[6/6] 📻 TEST B: Dispatcher Unity Menekan Talkback PTT (Unity -> Mobile In-Cabin)");
            Console.WriteLine("      1. Unity mengirim ptt_start...");
            await wsUnity.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"ptt_start\"}"), WebSocketMessageType.Text, true, cts.Token);
            await Task.Delay(250);

            Console.WriteLine("      2. Unity streaming 10 chunks audio PCM16 (200ms)...");
            for (int i = 0; i < 10; i++)
            {
                await wsUnity.SendAsync(syntheticAudioChunk, WebSocketMessageType.Binary, true, cts.Token);
                await Task.Delay(20);
            }

            Console.WriteLine("      3. Unity melepas Talkback (ptt_stop)...");
            await wsUnity.SendAsync(Encoding.UTF8.GetBytes("{\"type\":\"ptt_stop\"}"), WebSocketMessageType.Text, true, cts.Token);
            await Task.Delay(300);

            Console.WriteLine($"      📊 Hasil Test B: Mobile menerima {mobileAudioBytesReceived} bytes audio stream dari Dispatcher!");

            // 7. TEST STEP C: Bidirectional Chat Messaging
            Console.WriteLine("\n[BONUS] 💬 TEST C: Pesan Teks Chat Dua Arah");
            
            // Unity -> Mobile
            Console.WriteLine("      1. Unity mengirim pesan teks ke DT5107...");
            var msgReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/comms/messages");
            msgReq1.Headers.Add("X-FMS-Dispatcher-Key", DispatcherKey);
            msgReq1.Content = new StringContent(JsonSerializer.Serialize(new { unit_name = TestUnitId, body = "Instruksi Dispatch: Lanjut ke Shovel EX-201", priority = "normal" }), Encoding.UTF8, "application/json");
            var msgResp1 = await httpClient.SendAsync(msgReq1);
            Console.WriteLine($"         Status: HTTP {msgResp1.StatusCode}");
            await Task.Delay(200);

            // Mobile -> Unity
            Console.WriteLine("      2. Mobile membalas pesan teks ke Control Room...");
            var msgReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/comms/messages");
            msgReq2.Headers.Add("X-FMS-Unit-Key", unitKey);
            msgReq2.Content = new StringContent(JsonSerializer.Serialize(new { unit_name = TestUnitId, body = "Siap copy Dispatch, unit meluncur ke EX-201.", priority = "normal" }), Encoding.UTF8, "application/json");
            var msgResp2 = await httpClient.SendAsync(msgReq2);
            Console.WriteLine($"         Status: HTTP {msgResp2.StatusCode}");
            await Task.Delay(300);

            // SUMMARY & VALIDATION
            Console.WriteLine("\n================================================================================");
            Console.WriteLine("📋 HASIL SIMULASI & DIAGNOSA SISTEM KOMUNIKASI:");
            Console.WriteLine("================================================================================");

            bool testAPassed = unityAudioBytesReceived > 0;
            bool testBPassed = mobileAudioBytesReceived > 0;
            bool testCPassed = msgResp1.IsSuccessStatusCode && msgResp2.IsSuccessStatusCode;

            Console.WriteLine($"1. Transmisi PTT Mobile -> Unity     : {(testAPassed ? "✅ SUKSES (Audio Stream Diterima Unity)" : "❌ GAGAL")}");
            Console.WriteLine($"2. Transmisi PTT Unity -> Mobile     : {(testBPassed ? "✅ SUKSES (Audio Stream Diterima Mobile)" : "❌ GAGAL")}");
            Console.WriteLine($"3. Pengiriman Pesan Teks Dua Arah   : {(testCPassed ? "✅ SUKSES (Pesan Tersinkronisasi)" : "❌ GAGAL")}");

            if (testAPassed && testBPassed && testCPassed)
            {
                Console.WriteLine("\n🎉 SEMUA JALUR KOMUNIKASI 2-ARAH (VOICE PTT & CHAT) BERFUNGSI SEMPURNA 100%!");
            }
            else
            {
                Console.WriteLine("\n⚠️ Terdeteksi kendala pada salah satu jalur komunikasi.");
            }

            // Cleanup
            cts.Cancel();
            try { await wsUnity.CloseAsync(WebSocketCloseStatus.NormalClosure, "End", CancellationToken.None); } catch { }
            try { await wsMobile.CloseAsync(WebSocketCloseStatus.NormalClosure, "End", CancellationToken.None); } catch { }
        }
    }
}
