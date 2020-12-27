using Fleck;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace websocket_text
{
    class Program
    {
        static List<IWebSocketConnection> allSockets = new List<IWebSocketConnection>();

        static BackgroundWorker backgroundWorkerReceiveData = new BackgroundWorker();
        static BackgroundWorker backgroundWorkerSendData = new BackgroundWorker();

        static Random ra = new Random();

        static UdpClient udpClient = new UdpClient(8888);
        static int channelNo = 0;
        static bool requestData = false;
        static int max_cont = 20;

        static void TimerTimeout(object sender, System.Timers.ElapsedEventArgs e)
        {
            allSockets.ToList().ForEach(s => s.Send("Echo: " + ra.NextDouble()));
        }

        static void Main(string[] args)
        {
            FleckLog.Level = LogLevel.Warn;// LogLevel.Debug;
            //var allSockets = new List<IWebSocketConnection>();
            var server = new WebSocketServer("ws://192.168.100.153:9003");

            System.Timers.Timer timer = new System.Timers.Timer(30);
            timer.Elapsed += new ElapsedEventHandler(TimerTimeout);

            backgroundWorkerReceiveData.WorkerSupportsCancellation = true;
            backgroundWorkerReceiveData.DoWork += BackgroundWorkerReceiveRawData_DoWork;
            backgroundWorkerSendData.WorkerSupportsCancellation = true;
            backgroundWorkerSendData.DoWork += BackgroundWorkerReceiveData_DoWork;

            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    Console.WriteLine("Open!");
                    allSockets.Add(socket);
                };
                socket.OnClose = () =>
                {
                    Console.WriteLine("Close!");
                    allSockets.Remove(socket);
                    requestData = false;
                };
                socket.OnMessage = message =>
                {
                    Console.WriteLine(message);
                    string[] msg = message.Split(',');
                    if (msg.Length ==2)
                    {
                        channelNo = int.Parse(msg[0]);
                        max_cont = int.Parse(msg[1])>1? int.Parse(msg[1]) : 1;
                        if (channelNo < 8 && channelNo > -1)
                        {
                            requestData = true;
                        }
                    }
                    else
                    {
                        requestData = false;
                    }
                    
                    

                    //if (message == "start")
                    //{
                    //    timer.Start();
                    //}
                    //else
                    //{
                    //    timer.Stop();
                    //}
                };
            });

            backgroundWorkerReceiveData.RunWorkerAsync();

            var input = Console.ReadLine();
            while (input != "exit")
            {
                foreach (var socket in allSockets.ToList())
                {
                    socket.Send(input);
                }
                input = Console.ReadLine();
            }

            timer.Stop();
            udpClient.Close();
            backgroundWorkerReceiveData.CancelAsync();
        }

        static float ACT1228ExtractChannel(byte higher, byte lower)
        {
            int data = (higher & 0x7f) * 256 + lower;

            if ((higher & 0x80) == 0x80)
            {
                data = -data;
            }

            float channel = (float)(data / 10000.0);
            //float channel = (float)(data);
            return channel;
        }

        static void BackgroundWorkerReceiveRawData_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgWorker = sender as BackgroundWorker;
            int count = 0;
            while (true)
            {
                try
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);
                    string record = System.Text.Encoding.Default.GetString(receiveBytes);
                    string[] values = record.Split(',');

                    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    if (requestData)
                    {
                        count++;
                        //if (count > max_cont)
                        {
                            count = 0;
                            allSockets.ToList().ForEach(s => s.Send(values[channelNo]));
                            //Console.WriteLine(values[5]);
                        }
                    }
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    //log.Error(Tag, ex);
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                if (bgWorker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
            }
        }

        static void BackgroundWorkerReceiveData_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgWorker = sender as BackgroundWorker;
            int count = 0;
            while (true)
            {
                try
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);

                    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    float[] data;
                    data = new float[8];

                    StringBuilder sb = new StringBuilder(1024);
                    //sb.Append(stamp + ",");

                    for (int i = 0; i < 8; i++)
                    {
                        data[i] = ACT1228ExtractChannel(receiveBytes[i * 2 + 4], receiveBytes[i * 2 + 5]);
                        if (requestData)
                        {
                            count++;
                            if (count > max_cont)
                            {
                                count = 0;
                                allSockets.ToList().ForEach(s => s.Send(data[channelNo].ToString()));
                            }
                        }
                    }
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    //log.Error(Tag, ex);
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                if (bgWorker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
            }
        }

        static void BackgroundWorkerSendData_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker bgWorker = sender as BackgroundWorker;
            int count = 0;
            while (true)
            {
                try
                {
                    //IPEndPoint object will allow us to read datagrams sent from any source.
                    IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = udpClient.Receive(ref RemoteIpEndPoint);

                    string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    float[] data;
                    data = new float[8];

                    StringBuilder sb = new StringBuilder(1024);
                    //sb.Append(stamp + ",");

                    for (int i = 0; i < 8; i++)
                    {
                        data[i] = ACT1228ExtractChannel(receiveBytes[i * 2 + 4], receiveBytes[i * 2 + 5]);
                        if (requestData)
                        {
                            count++;
                            if (count > max_cont)
                            {
                                count = 0;
                                allSockets.ToList().ForEach(s => s.Send(data[channelNo].ToString()));
                            }
                        }
                    }
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    //log.Error(Tag, ex);
                    if (bgWorker.CancellationPending == true)
                    {
                        e.Cancel = true;
                        break;
                    }
                }
                if (bgWorker.CancellationPending == true)
                {
                    e.Cancel = true;
                    break;
                }
            }
        }

    }
}
