using System.Timers;

namespace CS_project1
{
    internal class PeriodicWorker
    {
        public PeriodicWorker() { }

        static private int count;
        static public void startWorker()
        {
            count = 0;
            var aTimer = new System.Timers.Timer(4 * 1000);
            aTimer.Elapsed += new System.Timers.ElapsedEventHandler(executeOnce);
            aTimer.Start();
        }
        static private void executeOnce(object source, ElapsedEventArgs e)
        {
            Console.WriteLine("wake up: " + DateTime.Now.ToString() + "   " + e.SignalTime + "   " + source);
            count++;
            if (count == 5)
            {
                var aTimer = (System.Timers.Timer)source; 
                aTimer.Stop();
            }
        }
    }
}
