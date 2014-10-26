using Microsoft.WindowsAzure.Storage;
using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public abstract class CantonJob
    {
        private readonly Stopwatch _runTime;
        private CloudStorageAccount _account;
        private Config _config;
        private readonly string _host;

        public CantonJob(Config config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _config = config;

            _runTime = new Stopwatch();
            _host = Dns.GetHostName();
        }

        public void Run()
        {
            _runTime.Start();

            Task.Run(async () => await RunCore()).Wait();

            _runTime.Stop();
        }

        public virtual async Task RunCore()
        {
            throw new NotImplementedException();
        }

        public Stopwatch RunTime
        {
            get
            {
                return _runTime;
            }
        }

        public virtual Config Config
        {
            get
            {
                return _config;
            }
        }

        protected void Log(string message)
        {
            lock (this)
            {
                CantonUtilities.Log(message, "canton-log.txt");
            }
        }

        protected void LogError(string message)
        {
            lock (this)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                CantonUtilities.Log(message, "canton-error.txt");
                Console.ResetColor();
            }
        }

        protected void Log(Exception ex)
        {
            LogError(ex.ToString());
        }

        protected CloudStorageAccount Account
        {
            get
            {
                if(_account == null)
                {
                    _account = CloudStorageAccount.Parse(Config.GetProperty("StorageConnectionString"));
                }

                return _account;
            }
        }

        /// <summary>
        /// Host name of this machine
        /// </summary>
        protected string Host
        {
            get
            {
                return _host;
            }
        }
    }
}
