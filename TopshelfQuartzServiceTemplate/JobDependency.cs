using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TopshelfQuartzServiceTemplate
{
    public class JobDependencyManager : Dictionary<JobKey, JobDependencySatifaction>
    {
        public void Add(JobKey key, IEnumerable<JobKey> dependingOnJobKeys)
        {
            if (this.ContainsKey(key))
            {
                foreach (var newKey in dependingOnJobKeys.Where(jk => !this.ContainsKey(jk)))
                    this[key].Add(newKey, false);
            }
            else
            {
                this[key] = new JobDependencySatifaction(dependingOnJobKeys);
            }
        }

        public void AddDependencyOn(IJobDetail job, params JobKey[] dependentJobKeys)
        {
            foreach (var depKey in dependentJobKeys)
            {
                if (this.ContainsKey(depKey) && !this[depKey].ContainsKey(job.Key))
                    this[depKey].Add(job.Key, false);
                else
                    this[depKey] = new JobDependencySatifaction(job.Key);
            }
        }

        public void RemoveDependencyOn(IJobDetail job)
        {
            RemoveDependencyOn(job.Key);
        }

        public List<JobKey> RemoveDependencyOn(JobKey key)
        {
            var dependentKeys = new List<JobKey>();
            foreach (var pair in this.Where(p => p.Value.ContainsKey(key)))
            {
                pair.Value.Remove(key);
                dependentKeys.Add(pair.Key);
            }
            return dependentKeys;
        }

        public bool HasDependentJobs(JobKey jobKey)
        {
            return this.Any(jds => jds.Value.ContainsKey(jobKey));
        }

        public List<JobKey> SatisfyAndGetDependenciesToTrigger(IJobDetail job)
        {
            var lst = new List<JobKey>();
            foreach (var dep in this.Where(jds => jds.Value.DependsOnJob(job)))
            {
                if (this[dep.Key].Satisfy(job))
                    lst.Add(dep.Key);
            }
            return lst;
        }
    }

    public class JobDependencySatifaction : Dictionary<JobKey, bool>
    {
        public JobDependencySatifaction(IEnumerable<JobKey> dependentOnJobs)
        {
            if (dependentOnJobs != null)
            {
                foreach (var doj in dependentOnJobs) this[doj] = false;
            }
        }

        public JobDependencySatifaction(params JobKey[] dependentOnJobs)
        {
            if (dependentOnJobs != null)
            {
                foreach (var doj in dependentOnJobs) this[doj] = false;
            }
        }

        /// <summary>
        /// Checks if other jobs depend on the supplied job to start
        /// </summary>
        /// <param name="job"></param>
        /// <returns>True if other jobs depend on this job, false otherwise</returns>
        public bool DependsOnJob(IJobDetail job)
        {
            return this.ContainsKey(job.Key);
        }

        /// <summary>
        /// Mark a job dependency as satisfied
        /// </summary>
        /// <param name="job">The completed job</param>
        /// <returns>True if all job criteria satisfied, false otherwise</returns>
        public bool Satisfy(IJobDetail job)
        {
            this[job.Key] = true;
            if (this.All(d => d.Value == true))
            {
                try
                {
                    foreach (var key in this.Keys.ToArray()) this[key] = false;
                    return true;
                }
                catch (Exception ex)
                {
                    Helper.Log.Error(string.Format("error while satisfying {0}\n{1}", job.Key), ex);
                    return false;
                }
            }
            return false;
        }
    }

    public class DependentJobListener : IJobListener
    {
        QuartzService _service;
        public DependentJobListener(QuartzService service)
        {
            _service = service;
        }

        public string Name
        {
            get { return "DependentJobListener"; }
        }

        public void JobExecutionVetoed(IJobExecutionContext context)
        { }

        public void JobToBeExecuted(IJobExecutionContext context)
        {
            Console.WriteLine("{0}: Executing {1}", DateTime.Now, context.JobDetail.Key.Group);
        }

        public void JobWasExecuted(IJobExecutionContext context, JobExecutionException jobException)
        {
            if (jobException != null)
            {
                var msg = string.Format("Unhandled {0} exception", context.JobDetail.Key.Group);
                Helper.Log.Error(msg, jobException);
                Console.WriteLine(msg + ", Message is: {0}, see log for more", jobException.Message);
            }

            if (jobException == null && _service.HasDependentJobs(context.JobDetail.Key))
                _service.SatisfyDependency(context.JobDetail);

            var fn = context.MergedJobDataMap["AfterExecution"] as Action<IJobExecutionContext>;
            if (fn != null) fn.Invoke(context);
        }
    }

    public class DependentJobMatcher : IMatcher<JobKey>
    {
        public bool IsMatch(JobKey key)
        {
            return true;
        }
    }
}
