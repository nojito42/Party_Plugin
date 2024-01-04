

using ExileCore;
using ExileCore.PoEMemory.Components;
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

                    // Broadcast the message to all connected clients
                    BroadcastMessage(receivedMessage);
                }
            }
        }
        catch (Exception ex)
        {
            I.LogMsg($"Error handling client: {ex.ToString()}");
        }
        finally
        {
            handler.Shutdown(SocketShutdown.Both);
            connectedClients.Remove(handler); // Remove the disconnected client from the list
        }
    }

    public void BroadcastMessage(string message)
    {
        byte[] messageBytes = Encoding.ASCII.GetBytes(message + "<EOF>");

        foreach (var client in connectedClients)
        {
            try
            {
                client.Send(messageBytes);
            }
            catch (Exception ex)
            {
                I.LogMsg($"Error broadcasting message to client: {ex.ToString()}");
            }
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

                    byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()} <EOF>");
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
    public async Task SendMessageToServer(string message)
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

                byte[] msg = Encoding.ASCII.GetBytes($"{message}<EOF>");
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
