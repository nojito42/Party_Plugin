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
    public List<Socket> connectedClients = new List<Socket>(); // Maintain a list of connected clients

    public void StartServer()
    {
        if (isServerRunning)
        {
            I.LogMsg("Server is already running.");
            return;
        }

        // Set isServerRunning to true to avoid starting the server multiple times
        isServerRunning = true;

        // Run the server in a separate thread using Task.Run
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

                        Socket handler = await listener.AcceptAsync(); // Use async Accept
                        connectedClients.Add(handler);

                        _ = HandleClientAsync(handler); // Handle each client asynchronously
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
                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                // Log the received message
                I.LogMsg($"Received message: {receivedMessage}");

                try
                {
                    // Deserialize the received message
                    Message myMessage = JsonConvert.DeserializeObject<Message>(receivedMessage);

                    // Broadcast the message to all connected clients
                    BroadcastMessage(myMessage);
                }
                catch (JsonReaderException ex)
                {
                    // Log the specific error and additional information
                    I.LogMsg($"JsonReaderException in HandleClientAsync: {ex.LineNumber}, {ex.LinePosition}, {ex.Message}");
                }
            }
        }
        catch (SocketException ex)
        {
            // Log the specific error and additional information
            I.LogMsg($"SocketException in HandleClientAsync: {ex.SocketErrorCode}, {ex.Message}");
        }
        catch (Exception ex)
        {
            // Log any other unexpected exceptions
            I.LogMsg($"Unexpected error in HandleClientAsync: {ex.ToString()}");
        }
    }

    // Updated BroadcastMessage method
    public void BroadcastMessage(Message message)
    {
        try
        {
            // Serialize the message to JSON
            string serializedMessage = JsonConvert.SerializeObject(message);
            byte[] messageBytes = Encoding.ASCII.GetBytes(serializedMessage);

            foreach (var client in connectedClients)
            {
                try
                {
                    client.Send(messageBytes);
                }
                catch (SocketException ex)
                {
                    // Log the specific error and additional information
                    I.LogMsg($"Error broadcasting message to client: {ex.SocketErrorCode}, {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Log any other unexpected exceptions
                    I.LogMsg($"Unexpected error broadcasting message to client: {ex.ToString()}");
                }
            }
        }
        catch (Exception ex)
        {
            // Log any other unexpected exceptions
            I.LogMsg($"Unexpected error broadcasting message: {ex.ToString()}");
        }
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

                    byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()}");
                    int bytesSent = await client.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);

                    // Start a separate thread to listen for incoming messages
                    await Task.Run(() => ListenForMessages(client));

                    // You can add other logic or operations here if needed
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

                // Check if no bytes were received (socket closed by the server)
                if (bytesRec == 0)
                {
                    // Handle socket closed by the server
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

    // New method to send a message to the server
    public async Task SendMessageToServer(Message myMessage)
    {
        try
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.1.114"); // Replace with your server's IP address
            int port = 11000;

            using (Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                await client.ConnectAsync(remoteEP);
                I.LogMsg($"Socket connected to {client.RemoteEndPoint}");

                // Serialize the message to JSON, append <EOF>, and convert to bytes
                string serializedMessage = JsonConvert.SerializeObject(myMessage);
                byte[] msg = Encoding.ASCII.GetBytes(serializedMessage);

                int bytesSent = await client.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);
            }
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

