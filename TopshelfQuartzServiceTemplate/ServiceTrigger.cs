using System;

namespace TopshelfQuartzServiceTemplate
{
    public class ServiceTrigger
    {
        public ScheduleType Type { get; set; }
        public int? Hours { get; set; }
        public int? Minutes { get; set; }
        public int? Seconds { get; set; }
        //only for ScheduleType.Cron
        public string Cron { get; set; }
        //only for ScheduleType.DependentOnOther
        public Type DependentOnType { get; set; }
    }
}
