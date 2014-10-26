using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Persistence;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    class Program
    {
        /// <summary>
        /// Canton jobs that can run many instances.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine(".exe <config path>");
                Environment.Exit(1);
            }

            CantonUtilities.Init();

            Config config = new Config(args[0]);

            CloudStorageAccount account = CloudStorageAccount.Parse(config.GetProperty("StorageConnectionString"));

            Queue<CantonJob> jobs = new Queue<CantonJob>();

            // process gallery pages and nupkgs into catalog pages
            jobs.Enqueue(new CatalogPageJob(config, new AzureStorage(account, config.GetProperty("tmp")), CantonConstants.CatalogPageQueue));

            Stopwatch timer = new Stopwatch();

            // avoid flooding storage
            TimeSpan minWait = TimeSpan.FromMinutes(2);

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
