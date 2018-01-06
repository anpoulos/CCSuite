using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenHardwareMonitor.Hardware;

namespace ConsoleApp2.DataMonitor
{
    public class SensorAverage
    {
        private ISensor Sensor;
        private IHardware Gpu;
        private long Total;
        private int TotalReadings;
        public decimal Average;
        public string AverageName;

        public SensorAverage(IHardware gpu, ISensor sensor, string averageName)
        {
            Sensor = sensor;
            AverageName = averageName;
            Average = 0.0m;
            Gpu = gpu;
            TotalReadings = 0;

            var averagesTimer = new Timer(CalculateAverage);
            averagesTimer.Change(0, 60000);
        }

        public void CalculateAverage(Object stateInfo)
        {
            Gpu.Update();
            var value = Convert.ToInt64(Sensor.Value.GetValueOrDefault());
            try
            {
                Total = checked(Total + value);
                TotalReadings += 1;
            }
            catch(OverflowException e)
            {
                Total = value;
                TotalReadings = 1;
            }
            Average = Total / TotalReadings;
        }
    }
}
