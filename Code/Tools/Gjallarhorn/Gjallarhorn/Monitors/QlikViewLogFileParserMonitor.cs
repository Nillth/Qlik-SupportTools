﻿using Eir.Common.IO;
using Eir.Common.Logging;
using Gjallarhorn.Monitors.QmsApi;
using Gjallarhorn.Notifiers;
using Gjallarhorn.QvLogReading;
using Gjallarhorn.SenseLogReading;
using Gjallarhorn.SenseLogReading.FileMiners;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Eir.Common.Common;
using QMS_API.QMSBackend;
using Exception = System.Exception;

namespace Gjallarhorn.Monitors
{
    public class QlikViewLogFileParserMonitor : BaseMonitor, IGjallarhornMonitor
    {
        //private string _installationId;
        //private string _licenseSerialNr;s
        private static int FAKERUNCOUNT = 0;
        private readonly LicenceHelper _licenceHelper = new LicenceHelper();
        public QlikViewLogFileParserMonitor(Func<string, IEnumerable<INotifyerDaemon>> notifyerDaemons) : base(notifyerDaemons, "QlikViewLogFileParserMonitor") { }


        public void Execute()
        {
            try
            {
                var qmsAddress = Settings.GetSetting($"{MonitorName}.QmsAddress", "(undefined)");
                DirectorySetting archivedLogsLocation;
                if (qmsAddress.Equals("(undefined)", StringComparison.InvariantCultureIgnoreCase))
                {
                    qmsAddress = $"http://{(Dns.GetHostEntry(Dns.GetHostName()).HostName).ToLower()}:4799/QMS/Service";
                }

                string installationId;
                License licence = null;
                using (var qmsApiService = new QMS_API.AgentsQmsApiService(qmsAddress))
                {
                    if (!qmsApiService.TestConnection())
                    {
                        Log.To.Main.Add($"Could not connect to QMS API {qmsAddress} in {MonitorName}");
                        return;
                    }
                    List<ServiceInfo> qvsServices = qmsApiService.GetServices(ServiceTypes.QlikViewServer);
                    QVSSettings qvsSettings = qmsApiService.GetQvsSettings(qvsServices[0].ID, QVSSettingsScope.Logging);
                   
                    var folder = qvsSettings.Logging.Folder;
                    if (!Directory.Exists(folder))
                    {
                        Log.To.Main.Add($"The folder does not exist or we don't have access to the folder:'{folder}' will not read logs.");
                        return;
                    }
                    archivedLogsLocation = new DirectorySetting(folder);
                    var services = qmsApiService.GetServices(ServiceTypes.QlikViewServer | ServiceTypes.QlikViewDistributionService);
                    var qvServers = services.Where(p => p.Type == ServiceTypes.QlikViewServer).ToList();
                    installationId = qvServers.OrderBy(p => p.ID).First().ID.ToString();

                    qvServers.ForEach(p =>
                    {
                        licence = qmsApiService.GetLicense(p.Type == ServiceTypes.QlikViewServer ? LicenseType.QlikViewServer : LicenseType.Publisher, p.ID);
                         
                    });
                }

                var logMinerData = new FileMinerDto();
                var data = new StatisticsDto { LogFileMinerData = logMinerData, CollectionDateUtc = logMinerData.CollectionDateUtc };
                var settings = new LogFileDirectorSettings
                {
                    StartDateForLogs = DateTime.Now.AddDays(-2).Date,
                    StopDateForLogs = DateTime.Now.AddDays(-1).Date.AddMilliseconds(-1),
                };

                //settings.StartDateForLogs = DateTime.Parse("2019-04-30 00:00:00").AddDays(FAKERUNCOUNT);
                //settings.StopDateForLogs = DateTime.Parse("2019-04-30 23:59:59").AddDays(FAKERUNCOUNT);
                //archivedLogsLocation = new DirectorySetting(@"C:\ProgramData\QlikTech\QlikViewServer");
                var logFileDirector = new QvLogDirector();
                data.InstallationId = $"{licence?.Serial ?? "(unknown)"}_{installationId} ";
                data.QlikViewLicence = _licenceHelper.AnalyzeLicense(licence);
                data.LogFileMinerData.LicenseSerialNo = licence?.Serial ?? "(unknown qv)";
                Log.To.Main.Add($"Starting log director on {archivedLogsLocation.Path}");
                logFileDirector.LoadAndRead(archivedLogsLocation, settings, logMinerData);
                Notify($"{MonitorName} has analyzed the following system", new List<string> { JsonConvert.SerializeObject(data, Formatting.Indented) }, "-1");
                FAKERUNCOUNT++;
            }
            catch (Exception ex)
            {
                Log.To.Main.AddException($"Failed executing {MonitorName}", ex);
            }
        }
    }
}
