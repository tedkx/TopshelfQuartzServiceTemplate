namespace TopshelfQuartzServiceTemplate
{
    public static class Constants
    {
        public const string ConfigFilepath = "TopshelfQuartzServiceTemplate.exe.config";
        public const string ServiceName = "TopshelfQuartzServiceTemplate";
        public const double CONTINUOUS_EXECUTION_INTERVAL = 100;
    }

    public enum ScheduleType
    {
        Continuous = 1,
        Interval = 2,
        Daily = 3,
        Cron = 4,
        DependentOnOther = 5,
    }
}
