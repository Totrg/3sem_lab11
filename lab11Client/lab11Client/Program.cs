using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace lab11
{
    internal class Program
    {
        static void Main()
        {
            Console.Write("Введите тикер акции: ");
            string userTicker = Console.ReadLine();

            try
            {
                using (TcpClient tcpClient = new TcpClient("127.0.0.1", 1234))
                {
                    NetworkStream networkStream = tcpClient.GetStream();

                    // Отправляем тикер акции серверу
                    byte[] requestBytes = Encoding.UTF8.GetBytes(userTicker);
                    networkStream.Write(requestBytes, 0, requestBytes.Length);

                    // Читаем ответ от сервера
                    byte[] buffer = new byte[4096];
                    int bytesRead = networkStream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    Console.WriteLine($"Цена акции: {response}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }

            Console.ReadLine();
        }
    }
}
