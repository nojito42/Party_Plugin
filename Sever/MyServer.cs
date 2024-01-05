using ExileCore;
using ExileCore.PoEMemory.Components;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Party_Plugin.Myserver;
public enum MessageType
{
    Hidout,
    Map,
    Act,
    Pause,
    None
}
public class Message
{
    [JsonProperty("messageType")]
    public MessageType MessageType { get; set; }

    [JsonProperty("messageText")]
    public string MessageText { get; set; }
    [JsonProperty("sender")]
    public Client Sender { get; set; }


    [JsonConstructor]

    public Message(MessageType messageType, string messageText, Client sender)
    {
        MessageType = messageType;
        MessageText = messageText;
        Sender = sender;
    }
}
public class Client
{
    public string Name { get; set; }
    public Socket Socket { get; set; }
}
public class MyServer : IDisposable
{
    public bool IsServerRunning { get; set; }
    private Socket listener;
    public List<Client> ConnectedClients { get; } = new List<Client>();
    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public async Task StartServer()
    {
        if (IsServerRunning)
        {
            I.LogMsg("Server is already running.");
            return;
        }

        IsServerRunning = true;

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
                    Client client = new Client { Socket = handler };
                    ConnectedClients.Add(client);

                    _ = HandleClientAsync(client);
                }
            }
        }
        catch (Exception e)
        {
            I.LogMsg($"Server error: {e.ToString()}");
        }
    }

    private async Task HandleClientAsync(Client client)
    {
        byte[] bytes = new byte[1024];

        try
        {
            while (true)
            {
                int bytesRec = await client.Socket.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);

                if (bytesRec == 0)
                {
                    ConnectedClients.Remove(client);
                    client.Socket.Close();
                    break;
                }

                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                // Check if this is a join message
                if (receivedMessage.StartsWith("JOIN::"))
                {
                    // Extract the client's name from the join message
                    client.Name = receivedMessage.Substring("JOIN::".Length);
                    I.LogMsg($"Client {client.Name} joined the server.");
                }
                else
                {
                    // Process the regular message
                    I.LogMsg($"{client.Name} says: {receivedMessage}");

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
            // Include the sender's name in the message
            //string fullMessage = $"{message.Sender?.Name ?? I.GameController.Player.GetComponent<Player>().PlayerName} says: {message.MessageText}";
            //Message updatedMessage = new Message(message.MessageType, fullMessage,message.Sender);

            string serializedMessage = JsonConvert.SerializeObject(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(serializedMessage);

            foreach (var client in ConnectedClients)
            {
                try
                {
                    client.Socket.Send(messageBytes);
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
public class MyClient : IDisposable
{
    public Client ClientInstance { get; set; }
    public bool IsClientRunning { get; set; }
    public string ClientName { get; set; }  // Add a property for the client name
    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public async Task StartClient(Player p)
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
            ClientInstance = new Client { Name = p.PlayerName, Socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp) };
            ClientName = p.PlayerName;  // Assign the client name

            using (ClientInstance.Socket)
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                try
                {
                    await ClientInstance.Socket.ConnectAsync(remoteEP);
                    I.LogMsg($"Socket connected to {ClientInstance.Socket.RemoteEndPoint}");

                    // Send the join message to the server
                    byte[] joinMsg = Encoding.UTF8.GetBytes($"JOIN::{ClientName}");
                    int joinBytesSent = await ClientInstance.Socket.SendAsync(new ArraySegment<byte>(joinMsg), SocketFlags.None);

                    // Send the regular message to the server
                    byte[] msg = Encoding.UTF8.GetBytes($"{ClientName} said: {p.PlayerName} - {p.Owner.PosNum}");
                    int bytesSent = await ClientInstance.Socket.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);

                    await Task.Run(() => ListenForMessages());
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

    private async Task ListenForMessages()
    {
        try
        {
            while (true)
            {
                byte[] bytes = new byte[1024];
                int bytesRec = await ClientInstance.Socket.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);

                if (bytesRec == 0)
                {
                    break;
                }

                string receivedMessage = Encoding.UTF8.GetString(bytes, 0, bytesRec);

                try
                {
                    Message myMessage = JsonConvert.DeserializeObject<Message>(receivedMessage);

                    // Log the deserialized message
                    I.LogMsg($"Deserialized message - Type: {myMessage.MessageType}, Text: {myMessage.MessageText}");
                }
                catch (JsonReaderException ex)
                {
                    I.LogMsg($"JsonReaderException in ListenForMessages: {ex.LineNumber}, {ex.LinePosition}, {ex.Message}");
                }
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
            byte[] msg = Encoding.UTF8.GetBytes(serializedMessage);

            int bytesSent = await ClientInstance.Socket.SendAsync(new ArraySegment<byte>(msg), SocketFlags.None);
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
