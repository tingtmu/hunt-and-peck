using System;
using System.Collections.Generic;
using HuntAndPeck.Services.Interfaces;

namespace HuntAndPeck.Tests.Services
{
    /// <summary>
    /// In-memory <see cref="IRegistryRunKey"/>; set <see cref="WriteError"/> to make every write throw
    /// </summary>
    internal sealed class FakeRegistryRunKey : IRegistryRunKey
    {
        public Dictionary<string, string> Run { get; } = new Dictionary<string, string>();
        public Dictionary<string, byte[]> StartupApproved { get; } = new Dictionary<string, byte[]>();
        public Exception WriteError { get; set; }
        public int RunWrites { get; private set; }

        public string GetRunValue(string name) => Run.TryGetValue(name, out var data) ? data : null;

        public void SetRunValue(string name, string data)
        {
            ThrowIfFailing();
            RunWrites++;
            Run[name] = data;
        }

        public void DeleteRunValue(string name)
        {
            ThrowIfFailing();
            Run.Remove(name);
        }

        public byte[] GetStartupApprovedValue(string name) => StartupApproved.TryGetValue(name, out var data) ? data : null;

        public void SetStartupApprovedValue(string name, byte[] data)
        {
            ThrowIfFailing();
            StartupApproved[name] = data;
        }

        public void DeleteStartupApprovedValue(string name)
        {
            ThrowIfFailing();
            StartupApproved.Remove(name);
        }

        private void ThrowIfFailing()
        {
            if (WriteError != null)
            {
                throw WriteError;
            }
        }
    }
}
