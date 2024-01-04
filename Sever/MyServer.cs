﻿using ExileCore;
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
    private List<Socket> connectedClients = new List<Socket>(); // Maintain a list of connected clients

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
        Task.Run(() =>
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
                        byte[] bytes = new byte[1024];
                        I.LogMsg($"Server is listening on {localEndPoint} - co {connectedClients.ToString()}");

                        using (Socket handler = listener.Accept())
                        {
                            // Add the connected client to the list
                          

                            while (true)
                            {

                                if (!connectedClients.Any(e => e == handler))
                                    connectedClients.Add(handler);
                                int bytesRec = handler.Receive(bytes);
                                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                                if (receivedMessage.Contains("<EOF>"))
                                {
                                    receivedMessage = receivedMessage[..receivedMessage.IndexOf("<EOF>")];
                                    I.LogMsg($"Text received: {receivedMessage}");

                                    // Broadcast the message to all connected clients
                                    BroadcastMessage(receivedMessage);

                                   // break;
                                }
                            }

                            handler.Shutdown(SocketShutdown.Both);
                            connectedClients.Remove(handler); // Remove the disconnected client from the list
                        }
                    }
                }
            }
            catch (Exception e)
            {
                I.LogMsg($"Server error: {e.ToString()}");
            }          
        });
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



    //public void StartClient()
    //{
    //    byte[] bytes = new byte[1024];

    //    try
    //    {
    //        // Connect to a Remote server
    //        // Get Host IP Address that is used to establish a connection
    //        // In this case, we get one IP address of the server machine
    //        IPAddress ipAddress = IPAddress.Parse("192.168.1.114"); // Replace with your server's IP address
    //        IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);

    //        // Create a TCP/IP socket.
    //        using (Socket sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
    //        {
    //            // Connect the socket to the remote endpoint. Catch any errors.
    //            try
    //            {
    //                // Connect to Remote EndPoint
    //                sender.Connect(remoteEP);

    //                I.LogMsg($"Socket connected to {sender.RemoteEndPoint}");

    //                // Encode the data string into a byte array.
    //                byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()} <EOF> ");

    //                // Send the data through the socket.
    //                int bytesSent = sender.Send(msg);

    //                // Receive the response from the remote device.
    //                int bytesRec = sender.Receive(bytes);
    //                I.LogMsg($"Echoed test = {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");

    //                // Release the socket.
    //                sender.Shutdown(SocketShutdown.Both);
    //            }
    //            catch (ArgumentNullException ane)
    //            {
    //                I.LogMsg($"ArgumentNullException : {ane.ToString()}");
    //            }
    //            catch (SocketException se)
    //            {
    //                I.LogMsg($"SocketException : {se.ToString()}");
    //            }
    //            catch (Exception e)
    //            {
    //                I.LogMsg($"Unexpected exception : {e.ToString()}");
    //            }
    //        }
    //    }
    //    catch (Exception e)
    //    {
    //        I.LogMsg(e.ToString());
    //    }
    //}

    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;

    public void Dispose()
    {
        listener.Dispose();
    }
}



public class MyClient : IPartyPluginInstance, IDisposable
{
    public bool isClientRunning = false;
    private Socket client;


    public async void StartClient()
    {
        if (isClientRunning)
        {
            I.LogMsg("Client is already running.");
            return;
        }

        isClientRunning = true;

        try
        {
            IPAddress ipAddress = IPAddress.Parse("192.168.1.114"); // Replace with your server's IP address
            int port = 11000;

            using (client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                try
                {
                    await client.ConnectAsync(remoteEP);
                    I.LogMsg($"Socket connected to {client.RemoteEndPoint}");

                    byte[] msg = Encoding.ASCII.GetBytes($"{I.GameController.Player.GetComponent<Player>().PlayerName} said : {I.GameController.Player.PosNum.ToString()} <EOF>");
                    int bytesSent = await client.SendAsync(msg, SocketFlags.None);

                    // Start a separate thread to listen for incoming messages
                    await Task.Run(() => ListenForMessages());

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

    private void ListenForMessages()
    {
        try
        {
            while (true)
            {
                byte[] bytes = new byte[1024];
                int bytesRec = client.Receive(bytes);

                // Check if no bytes were received (socket closed by the server)
                if (bytesRec == 0)
                {
                  //  I.LogMsg("Server closed the connection. Exiting the loop.");
        
                }

                string receivedMessage = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                I.LogMessage($"LiestenForMessages broadcasted message: {receivedMessage}",1,Color.Green);
            }
        }
        catch (Exception ex)
        {
            I.LogMsg($"Error while listening for messages: {ex.ToString()}");
        }
    }



    public void Dispose()
    {
        client?.Dispose();
    }

    // Rest of your client code...

    public PartyPlugin I => Core.Current.pluginManager.Plugins.Find(e => e.Name == "Party_Plugin").Plugin as PartyPlugin;
}

