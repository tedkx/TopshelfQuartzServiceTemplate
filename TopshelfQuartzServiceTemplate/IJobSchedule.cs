using System;
using System.Collections.Generic;

namespace TopshelfQuartzServiceTemplate
{
    public interface IJobSchedule : IComparable<IJobSchedule>
    {
        string Identifier { get; }
        string Name { get; }
        Type Type { get; }
        IEnumerable<ServiceTrigger> Triggers { get; }
        DateTime LastUpdated { get; }
    }
}
