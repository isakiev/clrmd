namespace Microsoft.Diagnostics.Runtime
{
  #if _TRACING
    class TraceDataReader : IDataReader
    {
        private IDataReader _reader;
        private StreamWriter _file;

        public TraceDataReader(IDataReader reader)
        {
            _reader = reader;
            _file = File.CreateText("datareader.txt");
            _file.AutoFlush = true;
            _file.WriteLine(reader.GetType().ToString());
        }

        public void Close()
        {
            _file.WriteLine("Close");
            _reader.Close();
        }

        public void Flush()
        {
            _file.WriteLine("Flush");
            _reader.Flush();
        }

        public Architecture GetArchitecture()
        {
            var arch = _reader.GetArchitecture();
            _file.WriteLine("GetArchitecture - {0}", arch);
            return arch;
        }

        public uint GetPointerSize()
        {
            var ptrsize = _reader.GetPointerSize();
            _file.WriteLine("GetPointerSize - {0}", ptrsize);
            return ptrsize;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            var modules = _reader.EnumerateModules();

            int hash = 0;
            foreach (var module in modules)
                hash ^= module.FileName.ToLower().GetHashCode();

            _file.WriteLine("EnumerateModules - {0} {1:x}", modules.Count, hash);
            return modules;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            _reader.GetVersionInfo(baseAddress, out version);
            _file.WriteLine("GetVersionInfo - {0:x} {1}", baseAddress, version.ToString());
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bool result = _reader.ReadMemory(address, buffer, bytesRequested, out bytesRead);

            StringBuilder sb = new StringBuilder();
            int count = bytesRead > 8 ? 8 : bytesRead;
            for (int i = 0; i < count; ++i)
                sb.Append(buffer[i].ToString("x"));

            _file.WriteLine("ReadMemory {0}- {1:x} {2} {3}", result ? "" : "failed ", address, bytesRead, sb.ToString());

            return result;
        }

        public ulong GetThreadTeb(uint thread)
        {
            ulong teb = _reader.GetThreadTeb(thread);
            _file.WriteLine("GetThreadTeb - {0:x} {1:x}", thread, teb);
            return teb;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            List<uint> threads = new List<uint>(_reader.EnumerateAllThreads());

            bool first = true;
            StringBuilder sb = new StringBuilder();
            foreach (uint id in threads)
            {
                if (!first)
                    sb.Append(", ");
                first = false;
                sb.Append(id.ToString("x"));
            }

            _file.WriteLine("Threads: {0} {1}", threads.Count, sb.ToString());
            return threads;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            bool result = _reader.VirtualQuery(addr, out vq);
            _file.WriteLine("VirtualQuery {0}: {1:x} {2:x} {3}", result ? "" : "failed ", addr, vq.BaseAddress, vq.Size);
            return result;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            bool result = _reader.GetThreadContext(threadID, contextFlags, contextSize, context);
            _file.WriteLine("GetThreadContext - {0}", result);
            return result;
        }
    }
#endif

}