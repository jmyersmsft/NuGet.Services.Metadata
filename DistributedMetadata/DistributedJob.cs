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

        public DistributedJob()
        {
            _runTime = new Stopwatch();
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
    }
}
