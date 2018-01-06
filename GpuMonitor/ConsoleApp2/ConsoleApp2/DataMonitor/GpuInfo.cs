using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using OpenHardwareMonitor.Hardware;
using System.Threading;

namespace ConsoleApp2.DataMonitor
{
    public class GpuInfo
    {
        public IHardware Gpu;
        public List<ISensor> TemperatureSensors;
        public Stopwatch TemperatureMaxWatch = new Stopwatch();
        public Stopwatch TemperatureMinWatch = new Stopwatch();
        private int TemperatureMax = 70;
        private int TemperatureMaxInterval = 5000; //ms
        private int TemperatureMin = 40;
        private int TemperatureMinInterval = 5000;//ms
        private static bool LastCheckOverMax = false;
        private static bool LastCheckUnderMin = false;
        private bool Display = false;
        public List<SensorAverage> SensorAverages = new List<SensorAverage>();

        public GpuInfo(IHardware gpu, int temperatureMax, int temperatureMaxInterval, int temperatureMin, int temperatureMinInterval, bool display)
        {
            Gpu = gpu;
            TemperatureMax = temperatureMax;
            TemperatureMaxInterval = temperatureMaxInterval;
            TemperatureMin = temperatureMin;
            TemperatureMinInterval = temperatureMinInterval;
            Display = display;

            //filter out the temperature sensors
            TemperatureSensors = Gpu.Sensors
                    .Where(s => s.SensorType == SensorType.Temperature)
                    .ToList();

            foreach (var sensor in gpu.Sensors)
            {
                switch (sensor.SensorType)
                {
                    case SensorType.Voltage:
                        SensorAverages.Add(new SensorAverage(Gpu, sensor, "Voltage"));
                        break;

                    case SensorType.Power:
                        SensorAverages.Add(new SensorAverage(Gpu, sensor, "Power"));
                        break;

                    case SensorType.Temperature:
                        SensorAverages.Add(new SensorAverage(Gpu, sensor, "Temperature"));
                        break;
                }
            }
        }

        public TemperatureWarning CheckTemperatures()
        {
            Gpu.Update();
            foreach (var temperatureSensor in TemperatureSensors)
            {
                var temperature = temperatureSensor.Value.GetValueOrDefault();

                if (Display)
                {
                    Console.WriteLine("GPU Name: " + Gpu.Name + ", Temperature Sensor Name : " + temperatureSensor.Name + ", Temperature : " + temperatureSensor.Value.GetValueOrDefault());
                }

                if (temperature > TemperatureMax && LastCheckOverMax && TemperatureMaxWatch.ElapsedMilliseconds > TemperatureMaxInterval)
                {
                    return TemperatureWarning.MaxReached;
                }

                LastCheckOverMax = temperature > TemperatureMax;
                if (LastCheckOverMax && !TemperatureMaxWatch.IsRunning)
                {
                    TemperatureMaxWatch.Start();
                }

                if (!LastCheckOverMax && TemperatureMaxWatch.IsRunning)
                {
                    TemperatureMaxWatch.Reset();
                }

                if (temperature <= TemperatureMin && LastCheckUnderMin && TemperatureMinWatch.ElapsedMilliseconds > TemperatureMinInterval)
                {
                    return TemperatureWarning.MinReached;
                }

                LastCheckUnderMin = temperature <= TemperatureMin;
                if (LastCheckUnderMin && !TemperatureMinWatch.IsRunning)
                {
                    TemperatureMinWatch.Start();
                }

                if (!LastCheckUnderMin && TemperatureMinWatch.IsRunning)
                {
                    TemperatureMinWatch.Reset();
                }
            }
            return TemperatureWarning.None;
        }
    }
}
