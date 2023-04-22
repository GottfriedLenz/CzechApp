using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic.FileIO;

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Net;
using System.Timers;

namespace FunctionsBasic
{
    class Program
    {
        static Timer timer;
        static int[] TimerSettings;
        static void Main(string[] args)
        {
            string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            var MyConfig = new ConfigurationBuilder().AddJsonFile(projectDirectory + "//appsettings.json").Build();
            var TSH = Convert.ToInt32(MyConfig.GetSection("TimerSettings")["Hours"]);
            var TSM = Convert.ToInt32(MyConfig.GetSection("TimerSettings")["Minutes"]);
            var TSS = Convert.ToInt32(MyConfig.GetSection("TimerSettings")["Seconds"]);
            TimerSettings = new int[] { TSH, TSM, TSS };

            Console.WriteLine("### ПО Заполнения БД данными курса Чешской кроны ###");
            FillYearlyDatabase("2021");
            Console.WriteLine("### БД Заполнена информацией за 2021 год ###");
            ScheduleTimer(TimerSettings);
            Console.ReadLine();
        }
        public static void FillDailyDatabase()
        {
            var response = new WebClient().DownloadString("https://www.cnb.cz/en/financial_markets/foreign_exchange_market/exchange_rate_fixing/year.txt?year=2023");
            Stream sr = GenerateStreamFromString(response);
            List<Currency> CurrencyList = ParseToCurrencyList(sr);
            CurrencyList.Reverse();
            CurrencyList.RemoveRange(1, CurrencyList.Count - 1);
            WriteToYearlyTable(CurrencyList);
        }
        public static void FillYearlyDatabase(string year)
        {
            var response = new WebClient().DownloadString("https://www.cnb.cz/en/financial_markets/foreign_exchange_market/exchange_rate_fixing/year.txt?year=" + year);
            Stream sr = GenerateStreamFromString(response);
            List<Currency> CurrencyList = ParseToCurrencyList(sr);
            WriteToYearlyTable(CurrencyList);
        }
        static void ScheduleTimer(int[] TimerSettings)
        {
            Console.WriteLine("### Таймер запущен ###");
            DateTime nowTime = DateTime.Now;
            DateTime scheduledTime = new DateTime(nowTime.Year, nowTime.Month, nowTime.Day, TimerSettings[0], TimerSettings[1], TimerSettings[2], 0);
            if (nowTime > scheduledTime)
            {
                scheduledTime = scheduledTime.AddDays(1);
            }

            double tickTime = (double)(scheduledTime - DateTime.Now).TotalMilliseconds;
            timer = new Timer(tickTime);
            timer.Elapsed += new ElapsedEventHandler(TimerElapsed);
            timer.Start();
        }

        static void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("### Таймер запущен ### \n");
            timer.Stop();
            FillDailyDatabase();
            Console.WriteLine("### Текущий курс Чешской кроны внесен в БД ### \n\n");
            ScheduleTimer(TimerSettings);
        }


        public static void WriteToYearlyTable(List<Currency> CurrencyList)
        {
            using (SqlConnection connection = new SqlConnection("Server=(local); DataBase=CzechFinance;Integrated Security=true"))
            {
                string sqlcmd = "INSERT into dbo.ExchangeRatesHistory (Date,Code,Amount,Rate) VALUES (@Date,@Code,@Amount,@Rate)";

                using (SqlCommand query = new SqlCommand(sqlcmd))
                {
                    query.Connection = connection;
                    foreach (var value in CurrencyList)
                    {
                        query.Parameters.Add("@Date", SqlDbType.DateTime).Value = value.Date;
                        query.Parameters.Add("@Code", SqlDbType.NVarChar).Value = value.Code;
                        query.Parameters.Add("@Amount", SqlDbType.Int).Value = value.Amount;
                        query.Parameters.Add("@Rate", SqlDbType.Decimal).Value = value.Rate;
                        connection.Open();
                        query.ExecuteNonQuery();
                        query.Parameters.Clear();
                        connection.Close();
                    }
                }

            }
        }

        public static List<Currency> ParseToCurrencyList(Stream strm)
        {
            bool flag = false;
            List<string> headers = new List<string>();
            List<Currency> CurrencyList = new List<Currency>();

            using (TextFieldParser parser = new TextFieldParser(strm))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters("|");
                while (!parser.EndOfData)
                {
                    string[] fields = parser.ReadFields();
                    if (flag == false)
                    {
                        foreach (string field in fields)
                        {
                            headers.Add(field);
                        }
                        flag = true;
                        continue;
                    }

                    for (int i = 1; i < fields.Length; i++)
                    {
                        CurrencyList.Add(new Currency()
                        {
                            Date = Convert.ToDateTime(fields[0]),
                            Amount = Convert.ToInt32(headers[i].Split(' ')[0]),
                            Code = headers[i].Split(' ')[1],
                            Rate = Convert.ToDecimal(fields[i], CultureInfo.InvariantCulture)
                        });
                    }
                }
                return CurrencyList;
            }
        }
        public static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }
}
