﻿/*
Technitium DNS Server
Copyright (C) 2021  Shreyas Zare (shreyas@technitium.com)

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace Failover
{
    class AddressMonitoring : IDisposable
    {
        #region variables

        readonly HealthMonitoringService _service;
        readonly IPAddress _address;

        readonly ConcurrentDictionary<string, HealthMonitor> _healthMonitors = new ConcurrentDictionary<string, HealthMonitor>(1, 1);

        #endregion

        #region constructor

        public AddressMonitoring(HealthMonitoringService service, IPAddress address, string healthCheck, Uri healthCheckUrl)
        {
            _service = service;
            _address = address;

            if (_service.HealthChecks.TryGetValue(healthCheck, out HealthCheck existingHealthCheck))
                _healthMonitors.TryAdd(GetHealthMonitorKey(healthCheck, healthCheckUrl), new HealthMonitor(_service.DnsServer, _address, existingHealthCheck, healthCheckUrl));
        }

        #endregion

        #region IDisposable

        bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                foreach (KeyValuePair<string, HealthMonitor> healthMonitor in _healthMonitors)
                    healthMonitor.Value.Dispose();

                _healthMonitors.Clear();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

        #region private

        private static string GetHealthMonitorKey(string healthCheck, Uri healthCheckUrl)
        {
            string healthMonitorKey = healthCheck;

            if (healthCheckUrl is not null)
                healthMonitorKey += "|" + healthCheckUrl.AbsoluteUri;

            return healthMonitorKey;
        }

        #endregion

        #region public

        public HealthCheckStatus QueryStatus(string healthCheck, Uri healthCheckUrl)
        {
            string healthMonitorKey = GetHealthMonitorKey(healthCheck, healthCheckUrl);

            if (_healthMonitors.TryGetValue(healthMonitorKey, out HealthMonitor monitor))
                return monitor.HealthCheckStatus;

            if (_service.HealthChecks.TryGetValue(healthCheck, out HealthCheck existingHealthCheck))
                _healthMonitors.TryAdd(healthMonitorKey, new HealthMonitor(_service.DnsServer, _address, existingHealthCheck, healthCheckUrl));

            return null;
        }

        public void RemoveHealthMonitor(string healthCheck)
        {
            foreach (KeyValuePair<string, HealthMonitor> healthMonitor in _healthMonitors)
            {
                if (healthMonitor.Key.StartsWith(healthCheck + "|"))
                {
                    if (_healthMonitors.TryRemove(healthMonitor.Key, out HealthMonitor removedMonitor))
                        removedMonitor.Dispose();
                }
            }
        }

        public bool IsExpired()
        {
            foreach (KeyValuePair<string, HealthMonitor> healthMonitor in _healthMonitors)
            {
                if (healthMonitor.Value.IsExpired())
                {
                    if (_healthMonitors.TryRemove(healthMonitor.Key, out HealthMonitor removedMonitor))
                        removedMonitor.Dispose();
                }
            }

            return _healthMonitors.IsEmpty;
        }

        #endregion

        #region property

        public IPAddress Address
        { get { return _address; } }

        #endregion
    }
}
