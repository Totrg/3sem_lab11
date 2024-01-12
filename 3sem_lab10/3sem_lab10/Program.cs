using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;

namespace _3sem_lab10
{
    public class DBContext : DbContext
    {
        public DBContext() : base("name=DBContext"){}

        public DbSet<Stock> Stocks { get; set; }
    }
    public class Stock
    {
        public int Id { get; set; }
        public string Ticker { get; set; }
        public decimal PriceToday { get; set; }
        public decimal PriceYesterday { get; set; }
    }
    class Program
    {
        static async Task Main()
        {
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 1234);
            tcpListener.Start();

            Console.WriteLine("Сервер запущен. Ожидание подключений...");

            while (true)
            {
                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                await ProcessClientAsync(tcpClient);
            }
        }

        static async Task ProcessClientAsync(TcpClient tcpClient)
        {
            try
            {
                NetworkStream networkStream = tcpClient.GetStream();

                byte[] buffer = new byte[4096];
                int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                string userTicker = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                double priceToday = await GetTodayPriceAsync(userTicker);

                string response = GetTodayPrice(priceToday);
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                await networkStream.WriteAsync(responseBytes, 0, responseBytes.Length);

                tcpClient.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке клиента: {ex.Message}");
            }
        }

        static async Task<double> GetTodayPriceAsync(string userTicker)
        {
            using (var dbContext = new DBContext())
            {
                var stockData = dbContext.Stocks
                    .Where(s => s.Ticker == userTicker)
                    .OrderByDescending(s => s.Id)
                    .Take(2)
                    .ToList();

                var stock = await dbContext.Stocks.FirstOrDefaultAsync(s => s.Ticker == userTicker);
                return Convert.ToDouble(stock != null ? stock.PriceToday : 0);
            }
        }

        static string GetTodayPrice(double today)
        {
            return Convert.ToString(today);
        }

        static string GetChangeStatus(double yesterday, double today)
        {
            const double epsilon = 0.0001;

            if (Math.Abs(today - yesterday) < epsilon)
            {
                return "Цена осталась неизменной";
            }
            else if (today > yesterday)
            {
                return "Цена выросла";
            }
            else
            {
                return "Цена упала";
            }
        }

        private static readonly object fileLock = new object();

        static List<string> ReadTickersFromFile(string filePath)
        {
            return File.ReadAllLines(filePath).ToList();
        }

        static double CalculateAveragePrice(string csvData)
        {
            string[] lines = csvData.Split('\n');
            double sum = 0;
            int count = 0;

            foreach (string line in lines)
            {
                if (line[0] == 'D') continue;
                else
                {
                    string[] fields = line.Split(',');
                    if (fields.Length >= 5 && fields[2] != null && fields[3] != null)
                    {
                        try
                        {
                            double high = Convert.ToDouble(fields[2].Replace('.', ','));
                            double low = Convert.ToDouble(fields[3].Replace('.', ','));
                            sum += (high + low) / 2;
                            count++;
                        }
                        catch { return 0.0; }
                    }
                    else { return 0.0; }
                }
            }

            return count > 0 ? sum / count : 0.0;
        }

        static async Task ProcessStockAsync(string ticker, DBContext dbContext)
        {
            string apiUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1={GetUnixTimestampLastYear()}&period2={GetUnixTimestampNow()}&interval=1d&events=history&includeAdjustedClose=true";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    string csvData = await client.GetStringAsync(apiUrl);
                    double averagePrice = CalculateAveragePrice(csvData);

                    Stock stock = new Stock
                    {
                        Ticker = ticker,
                        PriceToday = (decimal)averagePrice,
                        PriceYesterday = (decimal)GetYesterdayPrice(csvData)
                    };

                    lock (fileLock)
                    {
                        dbContext.Stocks.Add(stock);
                        dbContext.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {ticker}: {ex.Message}");
                }
            }
        }

        static void WriteResultToFile(string result)
        {
            File.AppendAllText("result.txt", result + Environment.NewLine);
        }

        static long GetUnixTimestampNow()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        static long GetUnixTimestampLastYear()
        {
            return DateTimeOffset.Now.AddYears(-1).ToUnixTimeSeconds();
        }

        static double GetYesterdayPrice(string csvData)
        {
            string[] lines = csvData.Split('\n');

            if (lines.Length >= 3)
            {
                string[] fields = lines[1].Split(',');

                if (fields.Length >= 5 && fields[2] != null && fields[3] != null)
                {
                    try
                    {
                        double high = Convert.ToDouble(fields[2].Replace('.', ','));
                        double low = Convert.ToDouble(fields[3].Replace('.', ','));
                        return (high + low) / 2;
                    }
                    catch
                    {
                        return 0.0;
                    }
                }
            }

            return 0.0;
        }
    }
}
