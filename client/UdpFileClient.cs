using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;


public class UdpFileClient
{


    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Usage: mono UdpFileClient.exe <hostname> <port> <file-list>");
            return;
        }
        //first we should look at the new UDPClient() this part means that create a new UdpClient
        //then the program use the using method means that after running the program , the program automatically dispose the client
        //using method it is different from the using System.Net.Sockets:that is import the sockets library
        //the origianl code like UdpClient Client= new UdpClient();
        //try{       } finally{   client.dispose() }
        //the content in the () is empty so the OS will allocate a local port randomly for the UdpClien socket
        //the random port means that tell the server the socket is from which device(client) and which program


        string host = args[0];
        int port = int.Parse(args[1]);
        string filelist = args[2];

        //read the files.txt
        string[] files = File.ReadAllLines(filelist);
        foreach (string line in files)
        {
            string filename = line.Trim();

            if (filename.Length == 0)
            {
                continue;
            }

            DownloadOneFile(host, port, filename);
        }
    }

    static void DownloadOneFile(string host, int port, string filename)
    {
        Console.WriteLine(filename);
        using (UdpClient client = new UdpClient())
        {


            string messagerequest = "DOWNLOAD " + filename;
            byte[] data = Encoding.ASCII.GetBytes(messagerequest);

            client.Send(data, data.Length, host, port);

            IPEndPoint server = new IPEndPoint(IPAddress.Any, 0);
            byte[] replyData = client.Receive(ref server);
            string reply = Encoding.ASCII.GetString(replyData).Trim();





            //do the reply from the server
            string[] parts = reply.Split(' ');

            //case1 ERR
            if (parts[0] == "ERR")
            {
                Console.WriteLine(reply);
                return;
            }

            // case2
            long size = long.Parse(parts[3]);
            int dataPort = int.Parse(parts[5]);
            Console.WriteLine(filename + " 0%");


            using (UdpClient dataSocket = new UdpClient())
            using (FileStream output = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                IPEndPoint dataServer = new IPEndPoint(server.Address, dataPort);

                long start = 0;


                while (start < size)
                {
                    long end = Math.Min(start + 999, size - 1);

                    string getMessage = "FILE " + filename + " GET START " + start + " END " + end;

                    byte[] getData = Encoding.ASCII.GetBytes(getMessage);

                    bool success = false;
                    string response = "";
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);

                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        while (dataSocket.Available > 0)
                        {
                            IPEndPoint junk =
                                new IPEndPoint(IPAddress.Any, 0);

                            dataSocket.Receive(ref junk);
                        }
                        try
                        {
                            dataSocket.Client.ReceiveTimeout = attempt * 1000;

                            dataSocket.Send(getData, getData.Length, dataServer);
                            while (true)
                            {
                                byte[] responseData = dataSocket.Receive(ref remote);

                                response = Encoding.ASCII.GetString(responseData).Trim();

                                string[] responseParts = response.Split(' ');

                                if (responseParts.Length >= 9 &&
                                    responseParts[0] == "FILE" &&
                                    responseParts[1] == filename &&
                                    responseParts[2] == "OK" &&
                                    responseParts[3] == "START" &&
                                    responseParts[5] == "END" &&
                                    responseParts[7] == "DATA" &&
                                    long.Parse(responseParts[4]) == start &&
                                    long.Parse(responseParts[6]) == end)
                                {
                                    success = true;
                                    break;
                                }

                            }

                            if (success)
                            {
                                break;
                            }
                        }
                        catch (SocketException)
                        {

                        }
                    }


                    if (!success)
                    {
                        Console.WriteLine("ERROR " + filename + " timeout");
                        return;
                    }






                    dataServer = remote;





                    string[] finalParts = response.Split(' ');

                    if (finalParts.Length >= 9 &&
                        finalParts[0] == "FILE" &&
                        finalParts[1] == filename &&
                        finalParts[2] == "OK")
                    {
                        string encodedData = finalParts[8];

                        byte[] chunk = Convert.FromBase64String(encodedData);
                        output.Write(chunk, 0, chunk.Length);

                        start = end + 1;

                        int percent = (int)(start * 100 / size);
                        Console.WriteLine(filename + " " + percent + "%");
                    }
                }


                string closeMessage = "FILE " + filename + " CLOSE";
                byte[] closeData = Encoding.ASCII.GetBytes(closeMessage);

                dataSocket.Send(closeData, closeData.Length, dataServer);

                IPEndPoint closeRemote = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    dataSocket.Client.ReceiveTimeout = 5000;

                    byte[] closeReplyData =
                        dataSocket.Receive(ref closeRemote);

                    string closeReply =
                        Encoding.ASCII.GetString(closeReplyData).Trim();

                    if (closeReply ==
                        "FILE " + filename + " CLOSE_OK")
                    {
                        Console.WriteLine("OK " + filename);
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLine(
                        "ERROR " + filename + " close_timeout");
                }
            }

        }



    }

    
}
