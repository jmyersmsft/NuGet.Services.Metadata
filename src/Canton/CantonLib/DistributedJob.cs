using NuGet.Services.Metadata.Catalog.Collecting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.DistributedWork
{
    public abstract class DistributedJob
    {
        private readonly Stopwatch _runTime;
        private readonly CollectorCursor _start;
        private readonly CollectorCursor _end;

        public DistributedJob(CollectorCursor start, CollectorCursor end)
        {
            _runTime = new Stopwatch();
            _start = start;
            _end = end;
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
                return Config.Instance;
            }
        }
    }
}
