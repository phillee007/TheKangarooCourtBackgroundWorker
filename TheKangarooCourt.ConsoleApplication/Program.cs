using log4net;
using log4net.Config;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TheKangarooCourt.ConsoleApplication
{
    class WorkerOptions
    {
        /// <summary>
        /// The url to poll
        /// </summary>
        public string Url { get; set; }
        public int PollInterval { get; set; }
        public string PostValues { get; set; }
        public string HttpMethod { get; set; }
        public string Name { get; set; }
    }

    class Program
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
        private static readonly ISchedulerFactory SchedulerFactory;
        private static readonly IScheduler Scheduler;

        static Program()
        {
            XmlConfigurator.Configure();
            // Create a regular old Quartz scheduler
            SchedulerFactory = new StdSchedulerFactory();
            Scheduler = SchedulerFactory.GetScheduler();

        }

        static void Main(string[] args)
        {

            // Now let's start our scheduler; you could perform
            // any processing or bootstrapping code here before
            // you start it but it must be started to schedule
            // any jobs
            Scheduler.Start();

            //Easy way to stop the program if needed (for example when doing updates to the web app)
            var runJobs = bool.Parse(ConfigurationManager.AppSettings["RunJobs"]);
            if (!runJobs)
            {
                return;
            }

            //https://stackoverflow.com/questions/5420656/unable-to-read-data-from-the-transport-connection-an-existing-connection-was-f
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;


            var jobList = ReadJobListFromConfig();
            foreach (var job in jobList)
            {
                // Let's generate our polling job detail now
                var jobDetail = CreateJob(job);
                
                // And finally, schedule the job
                ScheduleJob(jobDetail, job);
            }

            // Run immediately?

           // Scheduler.TriggerJob(new JobKey("StartPolling"));
        }

        private static IJobDetail CreateJob(WorkerOptions options)
        {
            // The job builder uses a fluent interface to
            // make it easier to build and generate an
            // IJobDetail object
            var pollingJobDetail = JobBuilder.Create<PollWebsiteJob>()
                .WithIdentity(options.Name)   // Here we can assign a friendly name to our job        
                .Build();                       // And now we build the job detail

            // Put options into data map
            pollingJobDetail.JobDataMap.Put("JobName", options.Name);
            pollingJobDetail.JobDataMap.Put("Url", options.Url);
            pollingJobDetail.JobDataMap.Put("HttpMethod", options.HttpMethod);
            if (!String.IsNullOrEmpty(options.PostValues))
            {
                pollingJobDetail.JobDataMap.Put("PostValues", options.PostValues);
            }

            return pollingJobDetail;
        }

        private static void ScheduleJob(IJobDetail jobDetail, WorkerOptions options)
        {

            Logger.DebugFormat("Scheduling job - {0} - to run every {1} seconds", options.Name, options.PollInterval);

            // Let's create a trigger
            ITrigger trigger = TriggerBuilder.Create()

                // A description helps other people understand what you want
                .WithDescription(String.Format("Every {0} seconds", options.PollInterval))

                // A daily time schedule gives you a
                // DailyTimeIntervalScheduleBuilder which provides
                // a fluent interface to build a schedule
                .WithSimpleSchedule(x => x
                    // Here we specify the interval
                    .WithIntervalInSeconds(options.PollInterval)
                    .RepeatForever()
                )
                .StartNow()
                .Build();

            // Ask the scheduler to schedule our EmailJob
            Scheduler.ScheduleJob(jobDetail, trigger);
        }

        private static IList<WorkerOptions> ReadJobListFromConfig()
        {
            // Job List is just a JSON serialized array of job entries
            var jobs = ConfigurationManager.AppSettings["AllJobs"];
            return JsonConvert.DeserializeObject<List<WorkerOptions>>(jobs);
        }
    }

    /// <summary>
    /// Our email job, yet to be implemented
    /// </summary>
    public class PollWebsiteJob : IJob
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(PollWebsiteJob));

        public void Execute(IJobExecutionContext context)
        {
            var name = context.MergedJobDataMap["JobName"] as string;
            var url = context.MergedJobDataMap["Url"] as string;
            var httpMethod = context.MergedJobDataMap["HttpMethod"] as string;
            string postValues = context.MergedJobDataMap["PostValues"] as string;

            Logger.Debug("Executing job: " + name);

            using (WebClient wc = new WebClient())
            {
                try
                {

                    if (httpMethod == "POST")
                    {
                        wc.Headers[HttpRequestHeader.Accept] = "application/json";
                        wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                        string result = wc.UploadString(url, postValues);
                        Logger.Debug("Executed job: " + name);
                    }
                    else
                    {
                        var result = wc.DownloadString(url);
                        Logger.Debug("Executed job: " + name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex.ToString(), ex);
                    Logger.Debug("Job failed: " + name);
                }
            }            


        }
    }
}