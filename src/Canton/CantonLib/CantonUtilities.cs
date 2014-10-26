using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Canton
{
    public static class CantonUtilities
    {
        public static void Init()
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 1024;
        }

        public static void RunJobs(Queue<CantonJob> jobs)
        {
            try
            {
                foreach (var job in jobs)
                {
                    job.Run();
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString(), "canton-job-exceptions.txt");
            }
        }

        public static void Log(string message, string file)
        {
            using (var writer = new StreamWriter(file))
            {
                writer.WriteLine(String.Format(CultureInfo.InvariantCulture, "[{0}] {1}", DateTime.UtcNow.ToString("O"), message));
            }

            Console.WriteLine(message);
        }
    }
}
