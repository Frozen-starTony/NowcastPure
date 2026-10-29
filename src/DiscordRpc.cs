using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Web.Script.Serialization;

namespace NowcastPure
{
    public class DiscordRpc : IDisposable
    {
        private const int OpHandshake = 0;
        private const int OpFrame = 1;
        private const int OpClose = 2;
        private const int OpPing = 3;
        private const int OpPong = 4;

        private NamedPipeClientStream pipe;
        private string currentClientId;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer();
        private int currentPid;

        public bool IsConnected
        {
            get { return pipe != null && pipe.IsConnected; }
        }

        public DiscordRpc()
        {
            currentPid = Process.GetCurrentProcess().Id;
        }

        public bool EnsureConnected(string clientId)
        {
            if (string.IsNullOrEmpty(clientId))
                clientId = AppSources.DefaultClientId;

            // If already connected with the same client id and pipe is active
            if (IsConnected && string.Equals(currentClientId, clientId, StringComparison.OrdinalIgnoreCase))
                return true;

            Disconnect();

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var testPipe = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut);
                    testPipe.Connect(500);
                    pipe = testPipe;
                    break;
                }
                catch
                {
                    // Try next pipe index
                }
            }

            if (pipe == null)
                return false;

            try
            {
                // Handshake
                string handshakeJson = json.Serialize(new { v = 1, client_id = clientId });
                byte[] payloadBytes = Encoding.UTF8.GetBytes(handshakeJson);

                var writer = new BinaryWriter(pipe, Encoding.UTF8, true);
                var reader = new BinaryReader(pipe, Encoding.UTF8, true);

                writer.Write(OpHandshake);
                writer.Write(payloadBytes.Length);
                writer.Write(payloadBytes);
                writer.Flush();

                int op = reader.ReadInt32();
                int len = reader.ReadInt32();
                byte[] respBytes = reader.ReadBytes(len);

                if (op == OpFrame)
                {
                    currentClientId = clientId;
                    return true;
                }

                Disconnect();
                return false;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public bool SetActivity(
            string clientId,
            string title,
            string artist,
            string album,
            string sourceName,
            string artworkUrl,
            string button1Label,
            string button1Url,
            string button2Label,
            string button2Url,
            long? startUnixSec,
            long? endUnixSec,
            bool isVideo)
        {
            if (!EnsureConnected(clientId))
                return false;

            try
            {
                var activityDict = new Dictionary<string, object>();

                // Truncate details and state to 128 chars (Discord API requirement)
                string details = (title ?? string.Empty).Trim();
                if (details.Length > 128) details = details.Substring(0, 125) + "...";
                activityDict["details"] = details;

                string state = (artist ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(album))
                {
                    if (string.IsNullOrEmpty(state)) state = album;
                    else state = state + " • " + album;
                }
                if (state.Length > 128) state = state.Substring(0, 125) + "...";
                if (!string.IsNullOrEmpty(state))
                    activityDict["state"] = state;

                // Timestamps
                if (startUnixSec.HasValue)
                {
                    var ts = new Dictionary<string, object>();
                    ts["start"] = startUnixSec.Value;
                    if (endUnixSec.HasValue && endUnixSec.Value > startUnixSec.Value)
                    {
                        ts["end"] = endUnixSec.Value;
                    }
                    activityDict["timestamps"] = ts;
                }

                // Assets
                var assets = new Dictionary<string, object>();
                if (!string.IsNullOrEmpty(artworkUrl))
                {
                    assets["large_image"] = artworkUrl;
                    assets["large_text"] = details;
                }
                if (!string.IsNullOrEmpty(sourceName))
                {
                    assets["small_text"] = sourceName;
                }
                if (assets.Count > 0)
                {
                    activityDict["assets"] = assets;
                }

                // Buttons (Max 2)
                var buttons = new List<object>();
                if (!string.IsNullOrEmpty(button1Label) && !string.IsNullOrEmpty(button1Url))
                {
                    buttons.Add(new { label = button1Label, url = button1Url });
                }
                if (!string.IsNullOrEmpty(button2Label) && !string.IsNullOrEmpty(button2Url))
                {
                    buttons.Add(new { label = button2Label, url = button2Url });
                }
                if (buttons.Count > 0)
                {
                    activityDict["buttons"] = buttons;
                }

                // Activity Type: 2 = Listening, 3 = Watching, 0 = Playing
                activityDict["type"] = isVideo ? 3 : 2;

                var payload = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = currentPid,
                        activity = activityDict
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                SendFrame(payload);
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public bool SetAfkActivity(string details, string state, long startUnixSec, string artworkUrl)
        {
            if (!EnsureConnected(AppSources.DefaultClientId))
                return false;

            try
            {
                var activityDict = new Dictionary<string, object>();
                activityDict["details"] = string.IsNullOrEmpty(details) ? "На перерыве ☕" : details;
                if (!string.IsNullOrEmpty(state))
                    activityDict["state"] = state;

                var ts = new Dictionary<string, object>();
                ts["start"] = startUnixSec;
                activityDict["timestamps"] = ts;

                var assets = new Dictionary<string, object>();
                assets["large_image"] = !string.IsNullOrEmpty(artworkUrl) ? artworkUrl : "https://cdn-icons-png.flaticon.com/512/924/924514.png";
                assets["large_text"] = "AFK / Пауза";
                activityDict["assets"] = assets;

                activityDict["type"] = 0; // Playing (e.g. В игре: На перерыве ☕)

                var payload = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = currentPid,
                        activity = activityDict
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                SendFrame(payload);
                return true;
            }
            catch
            {
                Disconnect();
                return false;
            }
        }

        public void ClearActivity()
        {
            if (!IsConnected)
                return;

            try
            {
                var payload = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = currentPid,
                        activity = (object)null
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                SendFrame(payload);
            }
            catch
            {
                Disconnect();
            }
        }

        private void SendFrame(object payloadObj)
        {
            string jsonText = json.Serialize(payloadObj);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonText);

            var writer = new BinaryWriter(pipe, Encoding.UTF8, true);
            var reader = new BinaryReader(pipe, Encoding.UTF8, true);

            writer.Write(OpFrame);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.Flush();

            // Read ACK
            int op = reader.ReadInt32();
            int len = reader.ReadInt32();
            byte[] resp = reader.ReadBytes(len);
        }

        public void Disconnect()
        {
            currentClientId = null;
            if (pipe != null)
            {
                try { pipe.Close(); } catch { }
                try { pipe.Dispose(); } catch { }
                pipe = null;
            }
        }

        public void Dispose()
        {
            ClearActivity();
            Disconnect();
        }
    }
}
