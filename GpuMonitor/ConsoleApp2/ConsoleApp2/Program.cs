using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using OpenHardwareMonitor.Hardware;
using ConsoleApp2.DataMonitor;
using System.Net.Mail;
using System.Net;
using System.Text;

namespace ConsoleApp2
{
    class Program
    {
        private static List<GpuInfo> GpuInfoList;

        private static string Email = "jimangelomine@gmail.com";
        private static Stopwatch EmailWatch = new Stopwatch();
        private static int EmailInterval = 10000; //ms
        private static SmtpClient SmtpServer;

        public static void Main(string[] args)
        {
            SmtpServer = new SmtpClient("smtp.gmail.com");
            SmtpServer.Port = 587;
            SmtpServer.EnableSsl = true;

            while (!AttemptLogin()){}

            Console.WriteLine("\nMonitors are now running, please do not close this window.");

            //defaults
            var temperatureMax = 70;
            var temperatureMaxInterval = 1000; //ms
            var temperatureMin = 35;
            var temperatureMinInterval = 60000*5;//ms
            var display = false;
            var allAveragesInterval = 60000*30; //ms

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "-email":
                        Email = args[i + 1];
                        break;

                    case "-tempMax":
                        temperatureMax = Int32.Parse(args[i + 1]);
                        break;

                    case "-tempMaxInt":
                        temperatureMaxInterval = Int32.Parse(args[i + 1]);
                        break;

                    case "-tempMin":
                        temperatureMin = Int32.Parse(args[i + 1]);
                        break;

                    case "-tempMinInt":
                        temperatureMinInterval = Int32.Parse(args[i + 1]);
                        break;

                    case "-emailInt":
                        EmailInterval = Int32.Parse(args[i + 1]);
                        break;

                    case "-display":
                        display = bool.Parse(args[i + 1]);
                        break;

                    case "-allAveragesInt":
                        allAveragesInterval = Int32.Parse(args[i + 1]);
                        break;

                }
            }

            Computer c = new Computer()
            {
                GPUEnabled = true
            };
            c.Open();

            //only monitor GPUs from the hardware list
            GpuInfoList = c.Hardware
                .Where(h => h.HardwareType == HardwareType.GpuNvidia || h.HardwareType == HardwareType.GpuAti)
                .Select(h => new GpuInfo(h,temperatureMax, temperatureMaxInterval, temperatureMin, temperatureMinInterval, display))
                .ToList();

            var temperatureTimer = new Timer(CheckTemperatures);
            temperatureTimer.Change(0, 1000);

            var averagesTimer = new Timer(EmailAverages);
            averagesTimer.Change(0, allAveragesInterval);

            Console.Read();
        }

        public static void EmailAverages(Object stateInfo)
        {
            var emailBody = new StringBuilder();
            emailBody.AppendLine("--- Overall Averages ---");

            var allSensorAverages = GpuInfoList.SelectMany(g => g.SensorAverages);
            foreach(var grouping in allSensorAverages.GroupBy(s => s.AverageName))
            {
                decimal total = 0;
                var sensorAverages = grouping.AsQueryable();
                foreach (var sensorAverage in sensorAverages)
                {
                    total += sensorAverage.Average;
                }
                var average = total / sensorAverages.Count();
                emailBody.AppendLine(grouping.Key + ": " + average);
            }

            emailBody.AppendLine("\n\n--- Individual Averages ---");
            foreach(var gpuInfo in GpuInfoList)
            {
                emailBody.AppendLine("\n--- " + gpuInfo.Gpu.Name + " ---");
                foreach(var sensorAverage in gpuInfo.SensorAverages)
                {
                    emailBody.AppendLine(" " + sensorAverage.AverageName + " : " + sensorAverage.Average);
                }
            }

            emailBody.AppendLine();

            SendEmail(emailBody.ToString(), "Reports - Averages");
        }

        public static bool AttemptLogin()
        {
            try
            {
                Console.WriteLine();
                Console.Write("Gmail username: ");
                var username = Console.ReadLine();
                Console.WriteLine();
                Console.Write("Gmail password: ");
                var password = Console.ReadLine();
                Console.WriteLine();

                SmtpServer.Credentials = new NetworkCredential(username, password);
                SendEmail("Test Email", "Test Email");
                Console.WriteLine("\nTest Email successfully sent.");
            }
            catch (Exception e)
            {
                Console.WriteLine("\nTest email failed.\n This issue is most likely caused by invalid network credentials.\n");
                return false;
            }

            return true;
        }

        private static void CheckTemperatures(Object stateInfo)
        {
            foreach(var gpuInfo in GpuInfoList)
            {
                switch (gpuInfo.CheckTemperatures())
                {
                    case TemperatureWarning.MaxReached:
                        SendEmail("Temperature Max Limit hit on GPU " + gpuInfo.Gpu.Name + " for " + gpuInfo.TemperatureMaxWatch.Elapsed.Seconds + " seconds.", "Reports - Maximum Temperature Reached");
                        break;

                    case TemperatureWarning.MinReached:
                        SendEmail("Temperature Min Limit hit on GPU " + gpuInfo.Gpu.Name + " for " + gpuInfo.TemperatureMinWatch.Elapsed.Seconds + " seconds.", "Reports - Minimum Temperature Reached");
                        break;
                }
            }
        }

        private static void SendEmail(string emailMessage, string subject)
        {
            var email = false;

            if (!EmailWatch.IsRunning)
            {
                email = true;
                EmailWatch.Start();
            }

            if(EmailWatch.ElapsedMilliseconds > EmailInterval)
            {
                email = true;
            }

            if (email)
            {
                Console.WriteLine("Emailing: \n" + emailMessage);
                EmailWatch.Restart();

                MailMessage message = new MailMessage();
                message.To.Add(Email);
                message.Subject = subject;
                message.From = new MailAddress("Reports@" + Environment.MachineName);
                message.Body = emailMessage;
                SmtpServer.Send(message);
            }
        }
    }

}
