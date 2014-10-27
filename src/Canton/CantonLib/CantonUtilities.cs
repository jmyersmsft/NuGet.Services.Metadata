using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VDS.RDF;

namespace NuGet.Canton
{
    public static class CantonUtilities
    {
        public static void ReplaceIRI(IGraph graph, Uri oldIRI, Uri newIRI)
        {
            // replace the local IRI with the NuGet IRI
            string localUri = oldIRI.AbsoluteUri;

            var triples = graph.Triples.ToArray();

            string mainIRI = newIRI.AbsoluteUri;

            foreach (var triple in triples)
            {
                IUriNode subject = triple.Subject as IUriNode;
                IUriNode objNode = triple.Object as IUriNode;
                INode newSubject = triple.Subject;
                INode newObject = triple.Object;

                bool replace = false;

                if (subject != null && subject.Uri.AbsoluteUri.StartsWith(localUri))
                {
                    // TODO: store these mappings in a dictionary
                    Uri iri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}", mainIRI, subject.Uri.AbsoluteUri.Substring(localUri.Length)));
                    newSubject = graph.CreateUriNode(iri);
                    replace = true;
                }

                if (objNode != null && objNode.Uri.AbsoluteUri.StartsWith(localUri))
                {
                    // TODO: store these mappings in a dictionary
                    Uri iri = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}{1}", mainIRI, objNode.Uri.AbsoluteUri.Substring(localUri.Length)));
                    newObject = graph.CreateUriNode(iri);
                    replace = true;
                }

                if (replace)
                {
                    graph.Assert(newSubject, triple.Predicate, newObject);
                    graph.Retract(triple);
                }
            }
        }

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

        /// <summary>
        /// Run a job multiple times in parallel
        /// </summary>
        public static void RunManyJobs(Queue<Func<CantonJob>> jobs, int instances)
        {
            try
            {
                foreach (var getJob in jobs)
                {
                    Stack<Task> tasks = new Stack<Task>(instances);

                    for (int i = 0; i < instances; i++)
                    {
                        tasks.Push(Task.Run(() =>
                            {
                                CantonJob job = getJob();
                                job.Run();
                            }));
                    }

                    Task.WaitAll(tasks.ToArray());
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString(), "canton-job-exceptions.txt");
            }
        }

        private static readonly object _lockObj = new object();
        public static void Log(string message, string file)
        {

            lock (_lockObj)
            {
                using (var writer = new StreamWriter(file, true))
                {
                    writer.WriteLine(String.Format(CultureInfo.InvariantCulture, "[{0}] {1}", DateTime.UtcNow.ToString("O"), message));
                }

                Console.WriteLine(message);
            }
        }
    }
}
