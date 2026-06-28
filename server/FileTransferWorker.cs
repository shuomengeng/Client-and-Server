using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;



//after analysis the udpFile server.cs,the FileTransferWorker's job is to do the request
public class FileTransferWorker
{
    public class Job
    {
        //gobal variables
        public string Filename;
        public IPEndPoint ClientEndpoint;
        public UdpClient TransferSocket;

        public Job(string filename, IPEndPoint clientEndpoint, UdpClient transferSocket)
        {
            Filename = filename;
            ClientEndpoint = clientEndpoint;
            TransferSocket = transferSocket;
        }
    }

    // Implement Run — see assignment specification
    // (Job: Filename, ClientEndpoint, TransferSocket).
    public static void Run(object jobObject)
    {
        //ckeck heck files/<filename> and send ERR <filename> NOT_FOUND or OK <filename> SIZE
        //. PORT... on the control port;
        //cause the UdpFileServer has checked the request the files and file name and provide a method 

        if (jobObject == null)
        {
            return;
        }
        Job job = (Job)jobObject;
        string filepath = UdpFileServer.FilePath(job.Filename);
        if (!File.Exists(filepath))
        {
            //SendControlReply(IPEndPoint client, string message)
            UdpFileServer.SendControlReply(job.ClientEndpoint, "ERR " + job.Filename + " NOT_FOUND");
            return;
        }
        else
        {
            long size = new FileInfo(filepath).Length;
            int port = UdpFileServer.PublicPort(job.TransferSocket);

            UdpFileServer.SendControlReply(job.ClientEndpoint, "OK " + job.Filename +
            " SIZE " + size +
            " PORT " + port);


        }



        byte[] fileBytes = File.ReadAllBytes(filepath);


        //do the request
        while (true)
        {
            // Receive a datagram
            //IPEndPoint means IP +Port,remoteEndPoint is a variable that store the IP+Port
            //IPAddress.Any means that the server can receive from any  IP Address,0 is the port after the server receive the datagram(socket) that the number will change
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] requestdata = job.TransferSocket.Receive(ref remoteEndPoint);


            // encoding the datagram into the string
            string request = Encoding.ASCII.GetString(requestdata);

            string[] parts = request.Split(' ');
            if (parts.Length >= 7 && parts[2] == "GET")
            {
                int start = int.Parse(parts[4]);
                int end = int.Parse(parts[6]);


                //calculate how many bytes that shold be readed
                int length = end - start + 1;
                byte[] chunk = new byte[length];

                Array.Copy(fileBytes, (int)start, chunk, 0, length);

                string encodedData = Convert.ToBase64String(chunk);

                string reply = "FILE " + job.Filename + " OK START " + start + " END " + end + " DATA " + encodedData;

                byte[] replyBytes = Encoding.ASCII.GetBytes(reply);

                job.TransferSocket.Send(replyBytes, replyBytes.Length, remoteEndPoint);




            }
            if (parts.Length >= 3 && parts[2] == "CLOSE")
            {
                string reply = "FILE " + job.Filename + " CLOSE_OK";
                byte[] replyBytes = Encoding.ASCII.GetBytes(reply);
                job.TransferSocket.Send(replyBytes, replyBytes.Length, remoteEndPoint);
                return;
            }
        }
    }
}
