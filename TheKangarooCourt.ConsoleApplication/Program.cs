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
    }

    class Program
    {
        private static readonly ISchedulerFactory SchedulerFactory;
        private static readonly IScheduler Scheduler;
        private static IJobDetail _pollingJobDetail;
        private static WorkerOptions _options;

        static Program()
        {

            // Create a regular old Quartz scheduler
            SchedulerFactory = new StdSchedulerFactory();
            Scheduler = SchedulerFactory.GetScheduler();

        }

        static void Main(string[] args)
        {

            // Read our options from config (provided locally 
            // or via cloud host)
            ReadOptionsFromConfig();

            // Now let's start our scheduler; you could perform
            // any processing or bootstrapping code here before
            // you start it but it must be started to schedule
            // any jobs
            Scheduler.Start();

            // Let's generate our polling job detail now
            CreateJob();

            // And finally, schedule the job
            ScheduleJob();

            // Run immediately?

            Scheduler.TriggerJob(new JobKey("StartPolling"));
        }

        private static void CreateJob()
        {

            // The job builder uses a fluent interface to
            // make it easier to build and generate an
            // IJobDetail object
            _pollingJobDetail = JobBuilder.Create<PollWebsiteJob>()
                .WithIdentity("StartPolling")   // Here we can assign a friendly name to our job        
                .Build();                       // And now we build the job detail

            // Put options into data map
            _pollingJobDetail.JobDataMap.Put("Url", _options.Url);
            _pollingJobDetail.JobDataMap.Put("PostValues", _options.PostValues);
        }

        private static void ScheduleJob()
        {

            // Let's create a trigger
            ITrigger trigger = TriggerBuilder.Create()

                // A description helps other people understand what you want
                .WithDescription("Every two minutes")

                // A daily time schedule gives you a
                // DailyTimeIntervalScheduleBuilder which provides
                // a fluent interface to build a schedule
                .WithSimpleSchedule(x => x
                    // Here we specify the interval
                    .WithIntervalInSeconds(_options.PollInterval)
                    .RepeatForever()
                )
                .Build();

            // Ask the scheduler to schedule our EmailJob
            Scheduler.ScheduleJob(_pollingJobDetail, trigger);
        }

        private static void ReadOptionsFromConfig()
        {
            // Make sure we have options to change
            if (_options == null)
                _options = new WorkerOptions();

            string configUrl = ConfigurationManager.AppSettings["Url"];
            if (!String.IsNullOrEmpty(configUrl))
            {
                _options.Url = configUrl;
            }

            string pollInterval = ConfigurationManager.AppSettings["PollIntervalInSeconds"];
            if (!String.IsNullOrEmpty(pollInterval))
            {
                _options.PollInterval = int.Parse(pollInterval);
            }

            string postValues = ConfigurationManager.AppSettings["PostValues"];
            if (!String.IsNullOrEmpty(postValues))
            {
                _options.PostValues = postValues;
            }
        }
    }

    /// <summary>
    /// Our email job, yet to be implemented
    /// </summary>
    public class PollWebsiteJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("***** Executing *****");
            var url = context.MergedJobDataMap["Url"] as string;
            string postValues = context.MergedJobDataMap["PostValues"] as string;

            using (WebClient wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                string result = wc.UploadString(url, postValues);
                Console.WriteLine(result);
            }

            // Read the values from our merged (final) data map
            

            //WebClient client = new WebClient();
            //string reply = client.DownloadString(url);

           //// request.Method = "HEAD";
           // request.UserAgent = "Scheduled task poller";
           // var response = request.GetResponse();
           // using (var reader = new StreamReader(response.GetResponseStream()))
           // {
           //     string result = reader.ReadToEnd(); // do something fun...
           //     Console.WriteLine(result);
           // }

            

            Console.WriteLine("***** Executed *****");


        }
    }
}