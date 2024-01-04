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
public enum MessageType
{
    Hidout,
    Map,
    act,
    pause,
    none
}
public class Message
{
    

    [JsonProperty]
    public MessageType messageType;
    [JsonProperty]
    public string message;

    [JsonConstructor]
    public Message(MessageType messageType, string message)
    {
        this.messageType = messageType;
        this.message = message;
    }
}
public class MyServer : IPartyPluginInstance, IDisposable
{
    public bool isServerRunning = false;
    private Socket listener;
    public List<Socket> connectedClients = new List<Socket>(); // Maintain a list of connected clients

    public void StartServer()
    {
        if (isServerRunning)
        {
            I.LogMsg("Server is already running.");
            return;
        }

        isServerRunning = true;
        Task.Run(async () =>
        {
            try
            {
                IPAddress ipAddress = IPAddress.Parse("192.168.1.114"); // Replace with your server's IP address
                int port = 11000;

                using (listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port);

                    listener.Bind(localEndPoint);
                    listener.Listen(10);

                    while (true)
                    {
                        I.LogMsg($"Server is listening on {localEndPoint} - co {connectedClients.Count}");

                        Socket handler = await listener.AcceptAsync();
                        connectedClients.Add(handler);

                        _ = HandleClientAsync(handler);
                    }
                }
            }
            catch (Exception e)
            {
                I.LogMsg($"Server error: {e}");
            }
        });
    }

    private async Task HandleClientAsync(Socket handler)
    {
        try
        {
            byte[] bytes = new byte[1024];
            while (true)
            {
                int bytesRec = await handler.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                if (receivedMessage.Contains("<EOF>"))
                {
                    receivedMessage = receivedMessage[..receivedMessage.IndexOf("<EOF>")];
                    I.LogMsg($"Text received: {receivedMessage}");
                    var serializedMessage = JsonConvert.DeserializeObject<Message>(receivedMessage);
                    BroadcastMessage(serializedMessage);
                }
            }
        }
        catch (Exception ex) { }
        finally
        {
            //I.LogMsg($"finally");
        }
    }

    public void BroadcastMessage(Message message)
    {
        string serializedMessage = JsonConvert.SerializeObject(message);
        byte[] messageBytes = Encoding.ASCII.GetBytes(serializedMessage + "<EOF>");
        connectedClients.ForEach(c =>
        {
            try { c.Send(messageBytes); } catch (Exception ex) { }
        });
    }

    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public void Dispose()
    {
        listener.Dispose();
    }
}
public class MyClient : IPartyPluginInstance, IDisposable
{
    public bool IsClientRunning;
    private Socket client;

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
            IPAddress ipAddress = IPAddress.Parse("192.168.1.114"); // Replace with your server's IP address
            int port = 11000;

            using (Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                try
                {
                    await client.ConnectAsync(remoteEP);
                    I.LogMsg($"Socket connected to {client.RemoteEndPoint}");

                    byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()} <EOF>");
                    int bytesSent = await client.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);

                    // Start a separate thread to listen for incoming messages
                    await Task.Run(() => ListenForMessages(client));

                    // You can add other logic or operations here if needed
                }
                catch (Exception e)
                {
                    I.LogMsg($"Client connection error: {e}");
                }
            }
        }
        catch (Exception e)
        {
            I.LogMsg($"Client error: {e}");
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
                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                I.LogMessage($"ListenForMessages broadcasted message: {receivedMessage}", 1, Color.Green);
            }
        }
        catch (Exception ex)
        {
            I.LogMsg($"Error while listening for messages: {ex}");
            IsClientRunning = false;
        }
    }
    public async Task SendMessageToServer(Message message)
    {
        try
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.1.114");
            int port = 11000;

            using (client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
                await client.ConnectAsync(remoteEP);
                I.LogMsg($"Socket connected to {client.RemoteEndPoint}");

                byte[] msg = Encoding.ASCII.GetBytes($"{message}<EOF>");
                int bytesSent = await client.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);
            }
        }
        catch (Exception e)
        {
            I.LogMsg($"Error sending message to server: {e}");
        }
    }
    public void Dispose()
    {
        client.Dispose();
    }
}
