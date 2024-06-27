using System;
using System.IO.Ports;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BarcodeReaderApp
{
    class Program
    {
        private static ClientWebSocket ws;
        private static SerialPort serialPort;

        static async Task Main(string[] args)
        {
            serialPort = new SerialPort
            {
                PortName = "COM4", // Replace with your COM port name
                BaudRate = 9600, // Set your baud rate
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

            try
            {
                serialPort.Open();
                Console.WriteLine("Listening for barcode data on " + serialPort.PortName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error opening serial port: " + ex.Message);
                return;
            }

            while (true)
            {
                await ConnectWebSocket();

                // Wait before attempting to reconnect
                await Task.Delay(5000);
            }
        }

        private static async Task ConnectWebSocket()
        {
            ws = new ClientWebSocket();
            var uri = new Uri("ws://54.85.62.96:8000/ws");

            try
            {
                await ws.ConnectAsync(uri, CancellationToken.None);
                Console.WriteLine("WebSocket connection established");

                await ReceiveMessages();
            }
            catch (Exception ex)
            {
                Console.WriteLine("WebSocket connection error: " + ex.Message);
            }
        }

        private static async Task ReceiveMessages()
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    else
                    {
                        Console.WriteLine("Message from server: " + Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("WebSocket receive error: " + ex.Message);
            }
        }

        private static async void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();
            Console.WriteLine("Data Received:");
            Console.WriteLine(indata);

            // Send data to WebSocket server
            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    var buffer = Encoding.UTF8.GetBytes(indata);
                    var segment = new ArraySegment<byte>(buffer);
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine("Data sent to WebSocket server");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending data to WebSocket server: " + ex.Message);
                }
            }
            else
            {
                Console.WriteLine("WebSocket connection is not open");
            }
        }
    }
}
