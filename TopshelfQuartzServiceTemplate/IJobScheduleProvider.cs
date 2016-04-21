using System;
using System.Collections.Generic;

namespace TopshelfQuartzServiceTemplate
{
    public interface IJobScheduleProvider
    {
        IEnumerable<IJobSchedule> GetSchedule(DateTime? lastUpdated = null);

        IJobSchedule GetSchedule(string identifier, DateTime? lastUpdated = null);
    }
}
