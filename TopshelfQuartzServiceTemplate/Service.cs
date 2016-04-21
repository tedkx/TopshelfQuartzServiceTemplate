using log4net;
using Newtonsoft.Json;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

namespace TopshelfQuartzServiceTemplate
{
    public class QuartzService : ServiceControl
    {
        private const int RECHECK_SCHEDULE_INTERVAL_SECONDS = 5;

        readonly CancellationTokenSource _cancellationTokenSource;
        readonly CancellationToken _cancellationToken;
        readonly ISchedulerFactory _schedulerFactory;
        readonly IJobScheduleProvider _scheduleProvider;
        static Task _task;

        private HostControl _hostControl;
        private IScheduler _scheduler;
        private DateTime _lastTriggerDate;
        private JobDependencyManager _jobDependencyManager;

        public QuartzService(IJobScheduleProvider scheduleProvider)
        {
            _schedulerFactory = new StdSchedulerFactory();
            _scheduler = _schedulerFactory.GetScheduler();
            _scheduleProvider = scheduleProvider;
            _lastTriggerDate = (DateTime)System.Data.SqlTypes.SqlDateTime.MinValue;
            _jobDependencyManager = new JobDependencyManager();

            _scheduler.ListenerManager.AddJobListener(new DependentJobListener(this), new DependentJobMatcher());

            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
        }

        public IScheduler Scheduler { get { return _scheduler; } }
        public bool RunningInConsole { get { return _hostControl is Topshelf.Hosts.ConsoleRunHost; } }

        private void RefreshJobs()
        {
            RefreshJobs(null);
        }

        private void RefreshJobs(string jobIdentifier = null)
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                List<IJobSchedule> scheduleTriggers = null;
                if(string.IsNullOrWhiteSpace(jobIdentifier))
                {
                    scheduleTriggers = _scheduleProvider.GetSchedule(_lastTriggerDate).OrderBy(t => t).ToList();
                }
                else
                {
                    var sched = _scheduleProvider.GetSchedule(jobIdentifier, _lastTriggerDate);
                    scheduleTriggers = sched == null ? null : new List<IJobSchedule> { sched };
                }

                if (scheduleTriggers != null && scheduleTriggers.Count > 0)
                {
                    _scheduler.Standby();

                    Console.WriteLine("Scheduling jobs for " + string.Join(", ", scheduleTriggers.Select(s => s.Name)));

                    foreach (var schedule in scheduleTriggers)
                    {
                        //delete all jobs that might already exist for this type
                        List<JobKey> dependentJobKeys = null;
                        var existingJobKey = _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(Helper.GetJobGroupName(schedule))).FirstOrDefault();
                        if (existingJobKey != null)
                        {
                            if (_jobDependencyManager.ContainsKey(existingJobKey))
                                _jobDependencyManager.Remove(existingJobKey);
                            else if (_jobDependencyManager.HasDependentJobs(existingJobKey))
                                dependentJobKeys = _jobDependencyManager.RemoveDependencyOn(existingJobKey);
                            _scheduler.UnscheduleJobs(_scheduler.GetTriggersOfJob(existingJobKey).Select(t => t.Key).ToList());
                            _scheduler.DeleteJob(existingJobKey);
                        }

                        IEnumerable<Type> dependentOnJobTypes = schedule.Triggers
                            .Where(t => t.Type == ScheduleType.DependentOnOther)
                            .Select(t => t.DependentOnType);
                        var job = Helper.CreateJob(schedule);
                        //create job dependencies, triggers will be added later
                        if (dependentOnJobTypes.Count() > 0)
                        {
                            if (!schedule.Triggers.All(t => t.Type == ScheduleType.DependentOnOther))
                            {
                                Helper.Log.ErrorFormat("Schedule type DependentOnOther cannot be combined with other types on job {0} - {1}", schedule.Identifier, schedule.Name);
                                continue;
                            }
                            //AddJob() vs ScheduleJob() needed since Dependent jobs don't have any actual triggers
                            _scheduler.AddJob(job, true);

                            var dependingJobKeys = dependentOnJobTypes.Select(doj => Helper.GetJobGroupName(doj))
                                .SelectMany(g => _scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(g)));
                            _jobDependencyManager.Add(job.Key, dependingJobKeys);
                        }
                        else
                        {
                            //just build triggers
                            job = Helper.CreateJob(schedule);
                            var triggers = Helper.BuildTriggers(schedule);
                            if (triggers == null || triggers.Count == 0) continue;
                            _scheduler.ScheduleJob(job, triggers, true);
                            if (dependentJobKeys != null)
                                _jobDependencyManager.AddDependencyOn(job, dependentJobKeys.ToArray());
                        }
                    }
                    _lastTriggerDate = scheduleTriggers.OrderByDescending(t => t.LastUpdated).First().LastUpdated;

                    _scheduler.Start();
                }

                //recheck every once in a while for updated schedules
                Thread.Sleep(RECHECK_SCHEDULE_INTERVAL_SECONDS * 1000);
            }
        }

        private void OperateFromPrompt()
        {
            int mode = -1,
                jobToRun = -1;
            var scheds = _scheduleProvider.GetSchedule().ToList();

            while (true)
            {
                Console.WriteLine("\n1. Specific job once");
                Console.WriteLine("2. Specific job with schedule");
                Console.WriteLine("3. Normal - All jobs with schedules");
                Console.WriteLine("0: Quit");
                Console.WriteLine("------------");
                Console.Write("Select Mode: ");

                if (!Helper.TryParseKey(Console.ReadLine(), out mode, 0, 1, 2, 3))
                    continue;

                if (mode == 0)
                {
                    _hostControl.Stop();
                    return;
                }
                if (mode == 3)
                {
                    RefreshJobs();
                    return;
                }

                while (true)
                {
                    List<int> validOptions = new List<int> { 0 };
                    Console.WriteLine("\n\n");
                    for (var i = 0; i < scheds.Count; i++)
                    {
                        var canRun = mode != 2 || (scheds[i].Triggers != null && scheds[i].Triggers.All(t => t.DependentOnType == null));
                        var typeName = scheds[i].Type.FullName;
                        Console.WriteLine("{0}. {1}", canRun ? (i + 1).ToString() : "-", canRun ? typeName : "(Cannot Run) " + typeName);
                        if (canRun) validOptions.Add(i + 1);
                    }
                    Console.WriteLine("0: Back");
                    Console.WriteLine("------------");
                    Console.Write("Select Job: ");

                    if (Helper.TryParseKey(Console.ReadLine(), out jobToRun, validOptions.ToArray()))
                    {
                        if (jobToRun == 0)
                        {
                            Console.Clear();
                            break;
                        }
                        Console.WriteLine();
                        var sched = scheds[jobToRun - 1];

                        if (mode == 1)
                        {
                            var job = Helper.CreateJob(sched);
                            _scheduler.AddJob(job, true);
                            bool finished = false;
                            Action<IJobExecutionContext> afterExecutionFn = (context) => { finished = true; };
                            var dic = new Dictionary<string, object>() { { "AfterExecution", afterExecutionFn } } as System.Collections.IDictionary;
                            _scheduler.TriggerJob(job.Key, new JobDataMap(dic));
                            while (!finished)
                                Thread.Sleep(200);
                        }
                        else
                        {
                            RefreshJobs(sched.Identifier);
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks JobDependencyManager to see if the current job has other jobs depending on it
        /// </summary>
        /// <param name="key">The JobKey</param>
        /// <returns></returns>
        public bool HasDependentJobs(JobKey key)
        {
            return _jobDependencyManager.HasDependentJobs(key);
        }

        /// <summary>
        /// Marks the job as completed so that other jobs that depend on it, start
        /// </summary>
        /// <param name="job">The completed job</param>
        public void SatisfyDependency(IJobDetail job)
        {
            var toTrigger = _jobDependencyManager.SatisfyAndGetDependenciesToTrigger(job);
            toTrigger.ForEach(t => _scheduler.TriggerJob(t));
        }

        #region Topshelf Methods

        public bool Start(HostControl hostControl)
        {
            _hostControl = hostControl;
            if (RunningInConsole)
            {
                Helper.Log.Debug("Starting as console");
                _task = new Task(OperateFromPrompt, _cancellationToken);
            }
            else
            {
                Helper.Log.Debug("Starting as windows service");
                _task = new Task(RefreshJobs, _cancellationToken);
            }

            _scheduler.Start();
            _task.Start();
            return true;
        }

        public bool Stop(HostControl hostControl)
        {
            _scheduler.Shutdown(true);
            _cancellationTokenSource.Cancel();

            TimeSpan timeout = TimeSpan.FromSeconds(30);
            while (!_task.Wait(timeout))
            {
                hostControl.RequestAdditionalTime(timeout);
            }

            //TODO: maybe jobs have resources to dispose?

            return true;
        }

        public bool Pause(HostControl hostControl)
        {
            _scheduler.PauseAll();
            return true;
        }

        public bool Continue(HostControl hostControl)
        {
            _scheduler.ResumeAll();
            return true;
        }

        #endregion
    }
}
