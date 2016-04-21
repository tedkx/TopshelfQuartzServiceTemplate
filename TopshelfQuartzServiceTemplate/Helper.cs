using log4net;
using Newtonsoft.Json;
using Quartz;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace TopshelfQuartzServiceTemplate
{
    public static class Helper
    {
        private static ILog _log;
        public static ILog Log
        {
            get
            {
                if (_log == null) _log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
                return _log;
            }
        }

        public static string GetJobGroupName(IJobSchedule schedule)
        {
            return schedule.Type.FullName;
        }

        public static string GetJobGroupName(Type type)
        {
            return type.FullName;
        }

        public static JobBuilder GetJobBuilder(IJobSchedule schedule, bool durable = true)
        {
            return JobBuilder.Create(schedule.Type)
                .WithIdentity(schedule.Identifier, Helper.GetJobGroupName(schedule))
                .StoreDurably(durability: durable)
                .UsingJobData(new JobDataMap(new Dictionary<string, object>
                {
                    { "ConnectionString", ConfigurationManager.ConnectionStrings["connectionString"] }
                } as IDictionary<string, object>));
        }

        public static IJobDetail CreateJob(IJobSchedule schedule, bool durable = true)
        {
            return GetJobBuilder(schedule, durable: durable).Build();
        }

        public static Quartz.Collection.ISet<ITrigger> BuildTriggers(IJobSchedule schedule)
        {
            if (schedule == null || schedule.Triggers == null) return null;

            var errors = new List<string>();
            var set = new Quartz.Collection.HashSet<ITrigger>(schedule.Triggers.Select(t =>
            {
                try
                {
                    return ParseTrigger(t);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    return null;
                }
            }).Where(t => t != null));
            if (errors.Count > 0) Log.Error("Trigger Creation Errors: \n" + string.Join("\n", errors));
            return set;
        }

        public static ITrigger ParseTrigger(ServiceTrigger trigger)
        {
            var builder = TriggerBuilder.Create();
            switch (trigger.Type)
            {
                case ScheduleType.Continuous:
                    return builder.WithSimpleSchedule(b => b.RepeatForever().WithInterval(TimeSpan.FromMilliseconds(Constants.CONTINUOUS_EXECUTION_INTERVAL))).Build();
                case ScheduleType.Interval:
                    var span = trigger.Hours.HasValue ? TimeSpan.FromHours(trigger.Hours.Value)
                        : trigger.Minutes.HasValue ? TimeSpan.FromMinutes(trigger.Minutes.Value)
                        : trigger.Seconds.HasValue ? TimeSpan.FromSeconds(trigger.Seconds.Value)
                        : null as TimeSpan?;
                    if (!span.HasValue)
                        throw new ArgumentException("Invalid interval specified by trigger {0}", JsonConvert.SerializeObject(trigger));
                    return builder.WithSimpleSchedule(b => b.RepeatForever().WithInterval(span.Value)).Build();
                case ScheduleType.Cron:
                    return builder.WithCronSchedule(trigger.Cron).Build();
                case ScheduleType.Daily:
                    if (!trigger.Hours.HasValue || trigger.Hours.Value < 0 || trigger.Hours.Value > 24)
                        throw new ArgumentException("Invalid hours on trigger {0}", JsonConvert.SerializeObject(trigger));
                    if (!trigger.Minutes.HasValue || trigger.Minutes.Value < 0 || trigger.Minutes.Value > 60)
                        throw new ArgumentException("Invalid minutes on trigger {0}", JsonConvert.SerializeObject(trigger));
                    if (trigger.Seconds.HasValue && (trigger.Seconds.Value < 0 || trigger.Seconds.Value > 60))
                        throw new ArgumentException("Invalid seconds on trigger {0}", JsonConvert.SerializeObject(trigger));
                    var timeOfDay = trigger.Seconds.HasValue
                        ? TimeOfDay.HourMinuteAndSecondOfDay(trigger.Hours.Value, trigger.Minutes.Value, trigger.Seconds.Value)
                        : TimeOfDay.HourAndMinuteOfDay(trigger.Hours.Value, trigger.Minutes.Value);
                    return builder.WithDailyTimeIntervalSchedule(b => b.WithIntervalInHours(24).OnEveryDay().StartingDailyAt(timeOfDay)).Build();
                default:
                    throw new ArgumentException(string.Format("Invalid trigger {0}", JsonConvert.SerializeObject(trigger)));
            }
        }

        public static bool TryParseKey(ConsoleKeyInfo key, out int output, params int[] validOptions)
        {
            return TryParseKey(key.KeyChar.ToString(), out output, validOptions);
        }
        public static bool TryParseKey(string str, out int output, params int[] validOptions)
        {
            output = 0;
            try
            {
                output = Int32.Parse(str);
                return validOptions == null || validOptions.Contains(output);
            }
            catch
            {
                return false;
            }
        }
    }
}
