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
[method: JsonConstructor]
public class Message(MessageType messageType, string message)
{
    [JsonProperty]
    public MessageType messageType = messageType;
    [JsonProperty]
    public string message = message;
}

public class Client
{
    public string Name;
    public Socket Socket;
}
public class MyServer : IPartyPluginInstance, IDisposable
{
    public bool isServerRunning = false;
    private Socket listener;
    public HashSet<MyClient> connectedClients = new HashSet<MyClient>();
    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

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
                    IPEndPoint localEndPoint = new(ipAddress, port);

                    listener.Bind(localEndPoint);
                    listener.Listen(10);

                    while (true)
                    {
                        I.LogMsg($"Server is listening on {localEndPoint} - co {connectedClients.Count}");

                        Socket handler = await listener.AcceptAsync();
                        if (!connectedClients.Any(c => c.Socket == handler))
                        {
                            connectedClients.Add(new MyClient { Socket = handler });
                        }

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

                receivedMessage = receivedMessage[..receivedMessage.IndexOf("<EOF>")];
                var serializedMessage = JsonConvert.DeserializeObject<Message>(receivedMessage);
                I.LogMsg($"Text received: {serializedMessage}");

                BroadcastMessage(serializedMessage);
            }
        }
        catch (Exception ex)
        {
            I.LogMsg($"Error handling client: {ex}");
        }
        finally
        {
            //connectedClients.RemoveWhere(c => c.Socket == null || !c.Socket.Connected);
        }
    }

    public void BroadcastMessage(Message message)
    {
        string serializedMessage = JsonConvert.SerializeObject(message);
        byte[] messageBytes = Encoding.ASCII.GetBytes(serializedMessage);
        connectedClients.ToList().ForEach(c =>
        {
            try { c.Socket.Send(messageBytes); }
            catch (Exception ex) { I.LogMsg($"Error broadcasting message: {ex}"); }
        });
    }

    public void Dispose()
    {
        listener.Dispose();
    }
}
public class MyClient : IPartyPluginInstance, IDisposable
{
    public bool IsClientRunning;
    private Client client;
    public Socket Socket;
    public HashSet<MyClient> connectedClients = new HashSet<MyClient>();

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
            if(client == null)
            {
                client = new Client();
                client.Name = I.GameController.Player.GetComponent<Player>().PlayerName;
            }
            using (client.Socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                try
                {
                    await client.Socket.ConnectAsync(remoteEP);
                    I.LogMsg($"Socket connected to {client.Socket.RemoteEndPoint}");

                    byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()} <EOF>");
                    int bytesSent = await client.Socket.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);

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

    private async Task ListenForMessages(Client client)
    {
        try
        {
            while (true)
            {
                byte[] bytes = new byte[1024];
                int bytesRec = await client.Socket.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
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

            using (client.Socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);
                await client.Socket.ConnectAsync(remoteEP);
                I.LogMsg($"Socket connected to {client.Socket.RemoteEndPoint}");

                byte[] msg = Encoding.ASCII.GetBytes($"{message}<EOF>");
                int bytesSent = await client.Socket.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);
            }
        }
        catch (Exception e)
        {
            I.LogMsg($"Error sending message to server: {e}");
        }
    }

    public void Dispose()
    {
        client.Socket.Dispose();
    }
}
