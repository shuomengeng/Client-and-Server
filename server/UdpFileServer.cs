using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

// Provided — keep for building; do not submit or replace. Receives DOWNLOAD and starts FileTransferWorker per request.
public class UdpFileServer
{
    //all the server's files are stored in the files
    public const string FileDirectory = "files"; // spec: server/files/<name>

    //one thread send the control reply each time
    private static readonly object controlSendLock = new object();
    //listen the DOWNLOAD <filename>
    //and responsible for sending OK ERR
    private static UdpClient listener;


    //combine the name of folders and the name of the files to form filepath
    //returnlike files/dog.jpeg
    public static string FilePath(string filename)
    {
        return Path.Combine(FileDirectory, filename);
    }

    // PORT in OK is the bound transfer socket port.
    //cause the worker need to tell the client the number of the port used in the OK message
    public static int PublicPort(UdpClient socket)
    {
        return ((IPEndPoint)socket.Client.LocalEndPoint).Port;
    }

    public static void Main(string[] args)
    {
        
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: mono UdpFileServer.exe <port>");
            return;
        }

        int listenPort;
        if (!Int32.TryParse(args[0], out listenPort))
        {
            Console.Error.WriteLine("Invalid port: " + args[0]);
            return;
        }
        

        //Os create a new listener for server that listens the specific port from any IP Address
        listener = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
        Console.WriteLine("Server listening on UDP port " + listenPort);

        while (true)
        {
            //IPENDPoint=IP + port means new client
            IPEndPoint client = new IPEndPoint(IPAddress.Any, 0);
            //cause the messaage has been transfer into bytes to transport,so when the listener listen the datagram should be transfer into string
            //also the request form is like DOWNLOAD files/dog.jpeg
            string request = Encoding.ASCII.GetString(listener.Receive(ref client)).Trim();

            //if the request isn't the "DOWNLOAD ",continue means start from whil loop that accept a new UDP
            if (!request.StartsWith("DOWNLOAD "))
            {
                continue;
            }

            //the last code means that the request is "DOWNLOAD ".
            //extract the filename and check whether the client forget writting the filename
            //if the client forget writting the filename so the start from while loop to accept a new client
            string filename = request.Substring("DOWNLOAD ".Length).Trim();
            if (filename.Length == 0)
            {
                continue;
            }


            //if the request form is right ,so do the rest codes
            //print the request+" from "+IP Address and port(client)
            Console.WriteLine("Accepted DOWNLOAD for " + filename + " from " + client);

            //create the transferSocket(new port for the transfer socket)
            UdpClient transferSocket = new UdpClient(0); // one data socket per transfer

            //create a new thread worker for transfering socket
            //so the listener just listen the request and check whether the request form is right
            //the content of the()isn't the FileTransferWorker.Run() means that start the FileTransferWorker(another class is a thread to do the request
            Thread worker = new Thread(FileTransferWorker.Run);
            //run the thread background
            worker.IsBackground = true;
            worker.Start(new FileTransferWorker.Job(filename, client, transferSocket));
        }
    }

    // OK and ERR go out on the control (listener) port.
    public static void SendControlReply(IPEndPoint client, string message)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(message);
        lock (controlSendLock)
        {
            listener.Send(bytes, bytes.Length, client);
        }
    }
}
