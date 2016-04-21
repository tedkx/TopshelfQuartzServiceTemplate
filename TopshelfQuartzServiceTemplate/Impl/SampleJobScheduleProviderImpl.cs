using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TopshelfQuartzServiceTemplate.Impl
{
    public class SampleJobScheduleProviderImpl : IJobScheduleProvider
    {
        public IEnumerable<IJobSchedule> GetSchedule(DateTime? lastUpdated = null)
        {
            return _sampleJobs.Where(s => lastUpdated.HasValue ? s.LastChanged > lastUpdated.Value : true);
        }

        public IJobSchedule GetSchedule(string identifier, DateTime? lastUpdated = null)
        {
            int id;
            if(!Int32.TryParse(identifier, out id))
                return null;
            return _sampleJobs.FirstOrDefault(s => s.JobScheduleID == id && (lastUpdated.HasValue ? s.LastChanged > lastUpdated.Value : true));
        }

        static List<SampleJobScheduleImpl> _sampleJobs = new List<SampleJobScheduleImpl>
        {
            new SampleJobScheduleImpl()
            {
                JobScheduleID = 1,
                JobName = "Specific time job",
                JobQualifiedTypeName = "TopshelfQuartzServiceTemplate.Impl.TimedSampleJob, TopshelfQuartzServiceTemplate",
                ScheduleParams = "[{ \"Type\" : 3, \"Hours\" : 17, \"Minutes\" : 50 }]",
                LastChanged = DateTime.Now.AddYears(-1),
            },
            new SampleJobScheduleImpl()
            {
                JobScheduleID = 2,
                JobName = "Interval job",
                JobQualifiedTypeName = "TopshelfQuartzServiceTemplate.Impl.IntervalSampleJob, TopshelfQuartzServiceTemplate",
                ScheduleParams = "[{ \"Type\" : 2, \"Seconds\" : 10 }]",
                LastChanged = DateTime.Now.AddHours(-1),
            },
            new SampleJobScheduleImpl()
            {
                JobScheduleID = 3,
                JobName = "Cron job",
                JobQualifiedTypeName = "TopshelfQuartzServiceTemplate.Impl.CronSampleJob, TopshelfQuartzServiceTemplate",
                ScheduleParams = "[{ \"Type\" : 4, \"Cron\" : \"0 0 1 1 1/1 ? *\" }]",
                LastChanged = DateTime.Now.AddMinutes(-1),
            },
        };
    }

    #region Sample Jobs

    public class TimedSampleJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Executing timed job");
        }
    }

    public class IntervalSampleJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Executing interval job");
        }
    }

    public class CronSampleJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            Console.WriteLine("Executing cron job");
        }
    }

    #endregion
}
