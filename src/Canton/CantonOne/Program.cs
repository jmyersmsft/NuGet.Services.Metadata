using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Canton;

namespace NuGet.Canton.One
{
    class Program
    {
        /// <summary>
        /// Canton jobs that can only run as single instances.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine(".exe <config path>");
                Environment.Exit(1);
            }

            Config config = new Config(args[0]);

            Queue<CantonJob> jobs = new Queue<CantonJob>();
            jobs.Enqueue(new InitStorageJob(config));

            Stopwatch timer = new Stopwatch();
            // avoid flooding the gallery
            TimeSpan minWait = TimeSpan.FromMinutes(10);

            while (true)
            {
                timer.Restart();
                CantonUtilities.RunJobs(jobs);

                TimeSpan waitTime = minWait.Subtract(timer.Elapsed);

                Console.WriteLine("Completed jobs in: " + timer.Elapsed);

                if (waitTime.TotalMilliseconds > 0)
                {
                    Console.WriteLine("Sleeping: " + waitTime.TotalSeconds + "s");
                    Thread.Sleep(waitTime);
                }
            }
        }
    }
}
