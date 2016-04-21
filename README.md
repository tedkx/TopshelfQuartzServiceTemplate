# Topshelf Quartz Service Template

A project template for using quartz as a scheduler for jobs, running as a service with topshelf.

## Usage

* Create an implementation of **IJobSchedule** which will contain the schedule triggers.
* Create an implementation of **IJobScheduleProvider** that will provide the schedules to the service
* Pass you provider to the service constructor during initialization in the Program's **Main**().
