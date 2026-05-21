# ProcessGuard

## [中文文档](README-zh.md)

About how it works:

> ⚠️ **Important Note: Cron vs Start Once**
> - **Start Once (Checked)**: The `Cron` expression is **disabled**. The program will only be launched exactly once when the service starts (or is added). It will never be restarted, and its lifecycle is completely ignored afterwards.
> - **Start Once (Unchecked)**: The `Cron` expression becomes active. **When a cron is triggered, the existing process tree is killed and restarted.** By default, `0 1 * * *` will restart the process every day at 1 AM.

[Subverting Vista UAC in Both 32 and 64 bit Architectures By Pero Matić](https://www.codeproject.com/Articles/35773/Subverting-Vista-UAC-in-Both-32-and-64-bit-Archite)

[Application Compatibility - Session 0 Isolation By Craig Marcho](https://techcommunity.microsoft.com/t5/ask-the-performance-team/application-compatibility-session-0-isolation/ba-p/372361)

With the ability to start a process from the Windows service, we can:

1. Start a program with an interactive interface from a Windows service and restart it after it has been closed
2. Set some programs to start automatically at boot
3. For console applications, including but not limited to `java`, `dotnet`, `node`, etc., they can be deployed on Windows systems as no window like Windows services

## ⚙Configuration Interface

> You can download the program directly from the [Release](https://github.com/lovitus/processgod/releases) page. The interface you see is just a configuration interface for configuring the processes to be guarded here. After starting the service, you can close the configuration interface

![](https://lambda.cyou/assets/img/processguard-5.PNG)

Note: The configuration can take effect only after the service is started

### CSV Batch Editing

The configuration UI now includes a CSV editor for backup-friendly batch changes. Use the CSV button in the status bar to open the editable table, export the current process configuration to CSV, import an edited CSV file, or save changes directly from the table.

CSV editing keeps the JSON configuration file as the source used by the app and service. When saving, unchanged rows are ignored, changed rows are updated, and rows with a blank or previously unknown valid `Id` are added. Rows that are missing from the CSV are not deleted. If the service is running, only the rows that need to take effect are sent through the existing service command channel; the service is not restarted.



## 📕Configuration Items

**Process Name:** The name used to identify the current configuration item, only used for interface display

**Full Path:** Full path to executable

**Parameters:** The parameters be carried when starting the application, ignore this if you do not need any parameters

**Start Once:** Only started once during the service running

**Minimize:** For programs with an interactive interface, it can make it minimized to the taskbar when it starts, instead of popping up the interface as usual

**NoWindow:** For console applications, enabling this item can make it start like a windows service, without displaying the console at all

**Cron Schedule:** Optional 5-field cron expression used to restart a guarded process on schedule. Leave it empty when **Start Once** is enabled.

**Stop Before Cron:** Controls whether the currently running process tree is stopped before a cron-triggered execution.



## Configuration Example

### An Interactive Program

![](https://lambda.cyou/assets/img/processguard-6.PNG)



### A Spring Boot Program

![](https://lambda.cyou/assets/img/processguard-7.PNG)
