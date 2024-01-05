using ExileCore;
using ExileCore.PoEMemory.Components;
using Newtonsoft.Json;
using Party_Plugin;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Party_Plugin.Myserver;
// MessageType Enum
public enum MessageType
{
    Hidout,
    Map,
    Act,
    Pause,
    None
}

// Message Class
[method: JsonConstructor]
public class Message
{
    [JsonProperty("messageType")]
    public MessageType MessageType { get; set; }

    [JsonProperty("messageText")]
    public string MessageText { get; set; }

    public Message(MessageType messageType, string messageText)
    {
        MessageType = messageType;
        MessageText = messageText;
    }
}



// Client Class
public class Client
{
    public string Name { get; set; }
    public Socket Socket { get; set; }
}

// MyServer Class
public class MyServer : IDisposable
{
    public bool IsServerRunning { get; set; }
    private Socket listener;
    public List<Socket> ConnectedClients { get; } = new List<Socket>();
    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public void StartServer()
    {
        if (IsServerRunning)
        {
            I.LogMsg("Server is already running.");
            return;
        }

        IsServerRunning = true;

        Task.Run(async () =>
        {
            try
            {
                IPAddress ipAddress = IPAddress.Any;
                int port = 11000;

                using (listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

                    listener.Bind(localEndPoint);
                    listener.Listen(10);

                    while (true)
                    {
                        I.LogMsg($"Server is listening on {localEndPoint} - co {ConnectedClients.Count}");

                        Socket handler = await listener.AcceptAsync();
                        ConnectedClients.Add(handler);

                        _ = HandleClientAsync(handler);
                    }
                }
            }
            catch (Exception e)
            {
                I.LogMsg($"Server error: {e.ToString()}");
            }
        });
    }

    private async Task HandleClientAsync(Socket handler)
    {
        byte[] bytes = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRec = await handler.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);

                if (bytesRec == 0)
                {
                    ConnectedClients.Remove(handler);
                    handler.Close();
                    break;
                }

                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                I.LogMsg($"Received message: {receivedMessage}");

                try
                {
                    Message myMessage = JsonConvert.DeserializeObject<Message>(receivedMessage);
                    BroadcastMessage(myMessage);
                }
                catch (JsonReaderException ex)
                {
                    I.LogMsg($"JsonReaderException in HandleClientAsync: {ex.LineNumber}, {ex.LinePosition}, {ex.Message}");
                }
            }
        }
        catch (SocketException ex)
        {
            I.LogMsg($"SocketException in HandleClientAsync: {ex.SocketErrorCode}, {ex.Message}");
        }
        catch (Exception ex)
        {
            I.LogMsg($"Unexpected error in HandleClientAsync: {ex.ToString()}");
        }
    }

    public void BroadcastMessage(Message message)
    {
        try
        {
            string serializedMessage = JsonConvert.SerializeObject(message);
            byte[] messageBytes = Encoding.ASCII.GetBytes(serializedMessage);

            foreach (var client in ConnectedClients)
            {
                try
                {
                    client.Send(messageBytes);
                }
                catch (SocketException ex)
                {
                    I.LogMsg($"Error broadcasting message to client: {ex.SocketErrorCode}, {ex.Message}");
                }
                catch (Exception ex)
                {
                    I.LogMsg($"Unexpected error broadcasting message to client: {ex.ToString()}");
                }
            }
        }
        catch (Exception ex)
        {
            I.LogMsg($"Unexpected error broadcasting message: {ex.ToString()}");
        }
    }

    public void Dispose()
    {
        listener.Dispose();
    }
}

// MyClient Class
public class MyClient : IDisposable
{
    public Socket Client;

    public bool IsClientRunning { get; set; }
    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public async void StartClient()
    {
        if (IsClientRunning)
        {
            I.LogMsg("Client is already running.");
            return;
        }

        IsClientRunning = true;

        try
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.1.114");
            int port = 11000;

            using ( Client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                try
                {
                    await Client.ConnectAsync(remoteEP);
                    I.LogMsg($"Socket connected to {Client.RemoteEndPoint}");

                    byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()}");
                    int bytesSent = await Client.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);

                    await Task.Run(() => ListenForMessages(Client));
                }
                catch (Exception e)
                {
                    I.LogMsg($"Client connection error: {e.ToString()}");
                }
            }
        }
        catch (Exception e)
        {
            I.LogMsg($"Client error: {e.ToString()}");
        }
    }

    private async Task ListenForMessages(Socket client)
    {
        try
        {
            while (true)
            {
                byte[] bytes = new byte[1024];
                int bytesRec = await client.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);

                if (bytesRec == 0)
                {
                    break;
                }

                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                I.LogMessage($"ListenForMessages broadcasted message: {receivedMessage}", 1, Color.Green);
            }
        }
        catch (Exception ex)
        {
            I.LogMsg($"Error while listening for messages: {ex.ToString()}");
            IsClientRunning = false;
        }
    }

    public async Task SendMessageToServer(Message myMessage)
    {
        try
        {
            string serializedMessage = JsonConvert.SerializeObject(myMessage);
            byte[] msg = Encoding.ASCII.GetBytes(serializedMessage);

            int bytesSent = await Client.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);
        }
        catch (Exception e)
        {
            I.LogMsg($"Error sending message to server: {e.ToString()}");
        }
    }

    public void Dispose()
    {
        // Dispose of any resources if needed
    }
}

