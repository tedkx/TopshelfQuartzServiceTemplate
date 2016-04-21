using Quartz;
using Quartz.Spi;
using Quartz.Impl;
using System;
using Topshelf;
using Topshelf.HostConfigurators;
using Topshelf.Quartz;
using Topshelf.ServiceConfigurators;
using System.Configuration;

namespace TopshelfQuartzServiceTemplate
{
    static class Program
    {
        static void Main()
        {
            //Can also use Topshelf.Ninject for this
            IJobScheduleProvider provider = new TopshelfQuartzServiceTemplate.Impl.SampleJobScheduleProviderImpl();

            HostFactory.Run(x =>
            {
                x.Service(factory => new QuartzService(provider));
                x.SetDescription(Constants.ServiceName);
                x.SetDisplayName(Constants.ServiceName);
                x.SetServiceName(Constants.ServiceName);
                x.UseLog4Net(Constants.ConfigFilepath);

                x.RunAsPrompt();
                x.StartAutomaticallyDelayed();
            });
        }
    }
}
