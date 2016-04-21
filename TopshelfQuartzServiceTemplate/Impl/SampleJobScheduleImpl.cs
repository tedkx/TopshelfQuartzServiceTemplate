using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TopshelfQuartzServiceTemplate.Impl
{
    public class SampleJobScheduleImpl : IJobSchedule
    {
        List<ServiceTrigger> _triggers;
        private Type _serviceType;

        public int JobScheduleID { get; set; }
        public string JobName { get; set; }
        public string JobQualifiedTypeName { get; set; }
        public string ScheduleParams { get; set; }
        public DateTime LastChanged { get; set; }

        #region IServiceSchedule Members

        public string Identifier
        {
            get { return this.JobScheduleID.ToString(); }
        }

        public string Name
        {
            get { return this.JobName; }
        }

        public Type Type
        {
            get 
            {
                if (_serviceType == null) 
                    _serviceType = Type.GetType(this.JobQualifiedTypeName);
                return _serviceType; 
            }
        }

        public IEnumerable<ServiceTrigger> Triggers
        {
            get 
            {
                if (_triggers == null) 
                    _triggers = JsonConvert.DeserializeObject<List<ServiceTrigger>>(ScheduleParams);
                return _triggers;
            }
        }

        public DateTime LastUpdated
        {
            get { return this.LastChanged; }
        }

        #endregion

        public int CompareTo(IJobSchedule other)
        {
            if (this.Identifier == other.Identifier) return 0;

            IEnumerable<Type> thisTypes = this.Triggers.Where(t => t.Type == ScheduleType.DependentOnOther).Select(t => t.DependentOnType),
                otherTypes = other.Triggers.Where(t => t.Type == ScheduleType.DependentOnOther).Select(t => t.DependentOnType);

            if (thisTypes.Contains(other.Type))
                return 1;
            if (otherTypes.Contains(this.Type))
                return -1;
            if (otherTypes.Count() > 0)
                return -1;
            if (thisTypes.Count() > 0)
                return 1;

            return this.JobScheduleID.CompareTo(other.Identifier);
        }
    }
}
