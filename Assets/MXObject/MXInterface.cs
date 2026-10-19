using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ActUtlType64Lib;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>COM은 단일 STA 워커가 소유하고, 결과는 Unity 메인 스레드에 전달합니다.</summary>
public sealed class MXInterface : IDisposable
{
    public struct SetDeviceRequest
    {
        public string deviceAddress;
        public short writeValue;
        public Action<bool> callback;
        public bool result; // 기존 OnReceivedSetDevice 호출부 호환용
        internal long queuedAt; // Unity 프레임 수집부터 실제 전송까지의 대기 측정
    }

    public struct SetRandomDeviceRequest
    {
        public string deviceAddresses;
        public int count;
        public short[] writeValues;
        public Action<bool>[] callbacks;

        public SetRandomDeviceRequest(string addresses, int count, short[] values, Action<bool>[] callbacks)
        {
            deviceAddresses = addresses;
            this.count = count;
            writeValues = values;
            this.callbacks = callbacks;
        }
    }

    public struct DeviceValue
    {
        public readonly string Address;
        public readonly short Value;
        public DeviceValue(string address, short value) { Address = address; Value = value; }
    }

    // 요청을 큐/현재 회차가 함께 참조하여, 취소와 COM 반환 중 한쪽만 완료시킵니다.
    private sealed class WriteRequest
    {
        internal SetDeviceRequest Data;
        internal bool Done;
    }

    // 주소 구성은 교체만 하고, 수신 배열은 워커만 수정합니다.
    private sealed class ReadRequest
    {
        internal string[] Addresses;
        internal string Text;
        internal short[] Buffer, Previous;
        internal bool Received;
        internal int BusinessCount, CounterLow = -1, CounterHigh = -1;
        internal string ScanAddress;
    }

    // 연결별 상태를 묶어 전역 연결 세대 번호와 여러 개의 실행 플래그를 대신합니다.
    private sealed class Connection
    {
        internal volatile bool Stop;
        internal bool Restart, Ready;
        internal readonly List<WriteRequest> Active = new List<WriteRequest>();
        internal readonly HashSet<string> Seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal MXDiagnostics Diagnostics;
        internal readonly string[] Addresses;
        internal readonly short[] Values;
        internal Connection(int batchSize) { Addresses = new string[batchSize]; Values = new short[batchSize]; }
    }

    private readonly object _sync = new object();
    private readonly AutoResetEvent _signal = new AutoResetEvent(false);
    private readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
    private readonly Queue<WriteRequest> _pending = new Queue<WriteRequest>();
    private readonly HashSet<string> _outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int Code, long Time)> _errors = new Dictionary<string, (int, long)>();
    private readonly int _bufferSize, _station, _batchSize, _cycleLimit;
    private readonly string _secret;
    private Connection _connection;
    private ReadRequest _reading;
    private bool _disposed, _acceptWrites = true;
    private long _readTicks, _writeTicks;
    private volatile bool _debugLogging;
    private int _debugPeriodMs = 1000, _debugRevision;
    private string _scanAddress = "";
    private string[] _baseAddresses = new string[0];
    private long _acceptedWrites;

    public bool IsConnected { get { lock (_sync) return _connection != null && _connection.Ready && !_connection.Stop; } }
    public bool IsClosed { get { lock (_sync) return _connection == null; } }
    public bool IsClosing { get { lock (_sync) return _connection != null && _connection.Stop; } }
    public int PendingResetAddressCount { get { lock (_sync) return _outputs.Count; } }
    public int PendingWriteCount { get { lock (_sync) return _pending.Count; } }
    public int PendingMainThreadResults { get { return _main.Count; } }
    public double LastReadMilliseconds { get { return Interlocked.Read(ref _readTicks) * 1000.0 / Stopwatch.Frequency; } }
    public double LastWriteMilliseconds { get { return Interlocked.Read(ref _writeTicks) * 1000.0 / Stopwatch.Frequency; } }
    public event Action<DeviceValue[]> DeviceValuesChanged;

    // interval은 기존 API 호환용입니다. 정상 통신은 간격 없이 연속 실행합니다.
    public MXInterface(int interval, int capacity, int stationNumber, string password = null,
        int maxBatchSize = 64, int maxWritesPerCycle = 1024)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (maxBatchSize < 1) throw new ArgumentOutOfRangeException(nameof(maxBatchSize));
        if (maxWritesPerCycle < 1) throw new ArgumentOutOfRangeException(nameof(maxWritesPerCycle));
        _bufferSize = capacity;
        _station = stationNumber;
        _secret = password;
        _batchSize = maxBatchSize;
        _cycleLimit = maxWritesPerCycle;
        SetAutoReadDevice(new string[0]);
    }

    // 구간 내 모든 호출을 집계합니다. 주기 0은 작업 회차마다 출력합니다.
    public void SetDebugLogging(bool enabled, int logIntervalMilliseconds = 1000)
    {
        lock (_sync)
        {
            int period = Math.Max(0, logIntervalMilliseconds);
            if (_debugLogging == enabled && _debugPeriodMs == period) return;
            Volatile.Write(ref _debugPeriodMs, period);
            Interlocked.Increment(ref _debugRevision);
            _debugLogging = enabled;
        }
    }

    public void Open()
    {
        lock (_sync)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MXInterface));
            if (_connection == null) Start();
            else if (_connection.Stop) _connection.Restart = true;
        }
    }

    private void Start() // _sync를 보유한 상태에서 호출
    {
        var connection = new Connection(_batchSize);
        var thread = new Thread(() => Run(connection)) { IsBackground = true, Name = "MX Component STA" };
        _connection = connection;
        _acceptWrites = true;
        try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); }
        catch { _connection = null; throw; }
    }

    public void Close() { Stop(false); }
    public void Dispose() { Stop(true); }

    // 종료 요청만 하고 반환합니다. 0 초기화와 COM 해제는 워커의 finally에서 실행합니다.
    private void Stop(bool dispose)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = dispose;
            _acceptWrites = false;
            Complete(_pending.ToArray(), false);
            _pending.Clear();
            if (_connection != null)
            {
                _connection.Stop = true;
                _connection.Restart = false;
                Complete(_connection.Active, false);
            }
            _signal.Set();
            if (_disposed && _connection == null) _signal.Dispose();
        }
    }

    public void UpdateMainThread() { UpdateMainThread(128); }

    public void UpdateMainThread(int maxResults)
    {
        int count = Math.Min(Math.Max(0, maxResults), _main.Count);
        for (int i = 0; i < count && _main.TryDequeue(out Action action); i++)
            try { action(); } catch (Exception error) { Debug.LogException(error); }
    }

    public void AddSetDeviceRequest(SetDeviceRequest request) { AddSetDeviceRequests(new[] { request }); }

    public void AddSetDeviceRequests(IList<SetDeviceRequest> requests)
    {
        if (requests == null) throw new ArgumentNullException(nameof(requests));
        lock (_sync)
        {
            for (int i = 0; i < requests.Count; i++)
            {
                SetDeviceRequest data = requests[i];
                data.deviceAddress = (data.deviceAddress ?? "").Trim().ToUpperInvariant();
                if (data.queuedAt == 0) data.queuedAt = Stopwatch.GetTimestamp();
                var request = new WriteRequest { Data = data };
                if (_disposed || !_acceptWrites || data.deviceAddress.Length < 2 || (data.deviceAddress.IndexOf('\r') >= 0 || data.deviceAddress.IndexOf('\n') >= 0))
                    Complete(new[] { request }, false);
                else { _pending.Enqueue(request); _acceptedWrites++; }
            }
            if (!_disposed) _signal.Set();
        }
    }

    public void AddSetRandomDeviceRequest(SetRandomDeviceRequest request)
    {
        string[] addresses = (request.deviceAddresses ?? "").Split('\n');
        if (request.count < 1 || addresses.Length != request.count || request.writeValues == null ||
            request.writeValues.Length < request.count || (request.callbacks != null && request.callbacks.Length != request.count))
            throw new ArgumentException("랜덤 쓰기 주소/값/콜백 개수가 일치하지 않습니다.");
        var writes = new SetDeviceRequest[request.count];
        for (int i = 0; i < writes.Length; i++)
            writes[i] = new SetDeviceRequest
            {
                deviceAddress = addresses[i],
                writeValue = request.writeValues[i],
                callback = request.callbacks == null ? null : request.callbacks[i]
            };
        AddSetDeviceRequests(writes);
    }

    public void SetAutoReadDevice(IEnumerable<string> addresses)
    {
        if (addresses == null) throw new ArgumentNullException(nameof(addresses));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>();
        foreach (string address in addresses)
            if (!string.IsNullOrWhiteSpace(address))
            {
                string normalized = address.Trim().ToUpperInvariant();
                if (seen.Add(normalized)) list.Add(normalized);
            }
        lock (_sync)
        {
            if (_disposed) return;
            _baseAddresses = list.ToArray();
            _reading = CreateReading();
            _signal.Set();
        }
    }

    // D100을 지정하면 D100(하위)과 D101(상위)을 기존 랜덤 읽기에 포함합니다.
    public void SetScanCounterAddress(string address)
    {
        address = (address ?? "").Trim().ToUpperInvariant();
        if (address.Length > 0)
        {
            if (address.Length < 2 || address[0] != 'D' || !int.TryParse(address.Substring(1),
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number) || number == int.MaxValue)
                throw new ArgumentException("스캔 카운터는 연속된 D 두 워드의 시작 주소(D100 등)로 지정하세요.");
            address = "D" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        lock (_sync)
        {
            if (_disposed || _scanAddress == address) return;
            _scanAddress = address;
            _reading = CreateReading();
            _signal.Set();
        }
    }

    private ReadRequest CreateReading() // _sync를 보유한 상태에서 호출
    {
        var addresses = new List<string>(_baseAddresses);
        string high = null;
        if (_scanAddress.Length > 0)
        {
            int number = int.Parse(_scanAddress.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
            high = "D" + (number + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!addresses.Contains(_scanAddress)) addresses.Add(_scanAddress);
            if (!addresses.Contains(high)) addresses.Add(high);
        }
        return new ReadRequest
        {
            Addresses = addresses.ToArray(),
            Text = string.Join("\n", addresses),
            Buffer = new short[Math.Max(_bufferSize, addresses.Count)],
            Previous = new short[_baseAddresses.Length],
            BusinessCount = _baseAddresses.Length,
            ScanAddress = _scanAddress,
            CounterLow = addresses.IndexOf(_scanAddress),
            CounterHigh = high == null ? -1 : addresses.IndexOf(high)
        };
    }

    private void Run(Connection connection)
    {
        ActUtlType64 plc = null;
        bool opened = false, startupFailed = false;
        var changes = new List<DeviceValue>();
        ReadRequest current = null;
        try
        {
            plc = new ActUtlType64();
            plc.ActLogicalStationNumber = _station;
            if (!string.IsNullOrEmpty(_secret)) plc.ActPassword = _secret;
            if (connection.Stop) return;
            int code = plc.Open();
            if (code != 0) { Report("연결", code); return; }
            opened = true;
            _errors.Clear();
            GetDiagnostics(connection);
            if (!Reset(connection, plc)) { startupFailed = true; return; }
            lock (_sync) connection.Ready = !connection.Stop;
            if (!connection.Stop) _main.Enqueue(() => Debug.Log("[MXInterface] 연결되었습니다."));
            while (!connection.Stop)
            {
                long cycleStart = Stopwatch.GetTimestamp();
                GetDiagnostics(connection);
                // 이번 회차만 확보해 쓰기가 계속 들어와도 읽기 기회를 보장합니다.
                lock (_sync)
                {
                    connection.Active.Clear();
                    int count = Math.Min(_pending.Count, _cycleLimit);
                    for (int i = 0; i < count; i++) connection.Active.Add(_pending.Dequeue());
                }
                bool success = Transfer(connection, plc, connection.Active, false);
                Complete(connection.Active, false); // 종료로 미전송된 항목만 완료
                if (connection.Stop) break;
                ReadRequest reading;
                lock (_sync) reading = _reading;
                if (current != reading) { reading.Received = false; current = reading; }
                GetDiagnostics(connection)?.SetScanAddress(reading.ScanAddress);
                if (reading.Addresses.Length > 0)
                {
                    long begin = Stopwatch.GetTimestamp();
                    int? result = null;
                    try { result = code = plc.ReadDeviceRandom2(reading.Text, reading.Addresses.Length, out reading.Buffer[0]); }
                    finally
                    {
                        long finished = Stopwatch.GetTimestamp();
                        long elapsed = finished - begin;
                        Interlocked.Exchange(ref _readTicks, elapsed);
                        GetDiagnostics(connection)?.RecordCall(0, reading.BusinessCount, reading.Addresses.Length, elapsed, result, finished);
                    }
                    Report("읽기", code);
                    if (code == 0)
                    {
                        if (reading.CounterLow >= 0 && reading.CounterHigh >= 0)
                        {
                            uint scan = (uint)(ushort)reading.Buffer[reading.CounterLow] | ((uint)(ushort)reading.Buffer[reading.CounterHigh] << 16);
                            GetDiagnostics(connection)?.RecordScan(reading.ScanAddress, scan);
                        }
                        Publish(connection, reading, changes);
                    }
                    else success = false;
                }
                if (reading.Addresses.Length > 0 || connection.Active.Count > 0)
                    GetDiagnostics(connection)?.RecordCycle(Stopwatch.GetTimestamp() - cycleStart);
                LogSummary(connection);
                if (!success) Thread.Sleep(100);
                else if (reading.Addresses.Length == 0)
                {
                    bool idle;
                    lock (_sync) idle = _pending.Count == 0 && !connection.Stop;
                    if (idle)
                    {
                        var diagnostics = GetDiagnostics(connection);
                        int wait = diagnostics != null && diagnostics.HasActivity
                            ? Math.Max(1, Volatile.Read(ref _debugPeriodMs)) : Timeout.Infinite;
                        _signal.WaitOne(wait);
                    }
                }
            }
        }
        catch (Exception error) { Report(error); }
        finally
        {
            lock (_sync)
            {
                connection.Stop = true;
                _acceptWrites = false;
                Complete(_pending.ToArray(), false);
                _pending.Clear();
                Complete(connection.Active, false);
            }
            try { if (opened && !startupFailed) Reset(connection, plc); }
            catch (Exception error) { Report(error); }
            LogSummary(connection, true);
            try { if (opened) Report("연결 해제", plc.Close()); }
            catch (Exception error) { Report(error); }
            finally
            {
                try { if (plc != null && Marshal.IsComObject(plc)) Marshal.FinalReleaseComObject(plc); }
                catch (Exception error) { Report(error); }
                lock (_sync)
                {
                    _connection = null;
                    if (_disposed) _signal.Dispose();
                    else if (connection.Restart) Start();
                }
            }
        }
    }

    // 일반 쓰기와 0 초기화가 같은 배치 규칙/버퍼/COM 호출 코드를 사용합니다.
    private bool Transfer(Connection connection, ActUtlType64 plc, IList<WriteRequest> requests, bool reset)
    {
        bool success = true;
        for (int start = 0; start < requests.Count;)
        {
            if (!reset && connection.Stop) break;
            connection.Seen.Clear();
            int count = 0;
            while (start + count < requests.Count && count < _batchSize)
            {
                SetDeviceRequest data = requests[start + count].Data;
                bool single = data.deviceAddress.IndexOf("\\G", StringComparison.OrdinalIgnoreCase) >= 0;
                if (count > 0 && (single || !connection.Seen.Add(data.deviceAddress))) break;
                connection.Seen.Add(data.deviceAddress);
                connection.Addresses[count] = data.deviceAddress;
                connection.Values[count++] = reset ? (short)0 : data.writeValue;
                if (single) break;
            }
            lock (_sync)
            {
                if (!reset && connection.Stop) break;
                if (!reset)
                    for (int i = 0; i < count; i++) _outputs.Add(connection.Addresses[i]);
            }
            int? code = null;
            string addresses = count > 1 ? string.Join("\n", connection.Addresses, 0, count) : null;
            var diagnostics = GetDiagnostics(connection);
            long begin = Stopwatch.GetTimestamp();
            if (!reset && diagnostics != null)
                for (int i = 0; i < count; i++) diagnostics.RecordWait(Math.Max(0, begin - requests[start + i].Data.queuedAt));
            begin = Stopwatch.GetTimestamp();
            try
            {
                code = count == 1 ? plc.SetDevice2(connection.Addresses[0], connection.Values[0])
                    : plc.WriteDeviceRandom2(addresses, count, ref connection.Values[0]);
            }
            catch (Exception error) when (reset) { Report(error); }
            finally
            {
                long finished = Stopwatch.GetTimestamp();
                long elapsed = finished - begin;
                Interlocked.Exchange(ref _writeTicks, elapsed);
                GetDiagnostics(connection)?.RecordCall(reset ? 2 : 1, count, count, elapsed, code, finished);
            }
            if (code.HasValue && (!reset || code != 0)) Report(reset ? "주소 초기화" : "쓰기", code.Value);
            if (reset && code == 0)
            {
                lock (_sync)
                    for (int i = 0; i < count; i++) _outputs.Remove(connection.Addresses[i]);
            }
            // 결과를 묶어서 전달하고, 이미 취소 완료된 요청은 건너뜁니다.
            if (!reset) Complete(requests, code == 0, start, count);
            success &= code == 0;
            start += count;
        }
        return success;
    }

    private bool Reset(Connection connection, ActUtlType64 plc)
    {
        var requests = new List<WriteRequest>();
        lock (_sync)
            foreach (string address in _outputs)
                requests.Add(new WriteRequest { Data = new SetDeviceRequest { deviceAddress = address } });
        requests.Sort((a, b) => StringComparer.Ordinal.Compare(a.Data.deviceAddress, b.Data.deviceAddress));
        bool success = Transfer(connection, plc, requests, true);
        if (success) Report("주소 초기화", 0);
        return success;
    }

    private void Publish(Connection connection, ReadRequest reading, List<DeviceValue> changes)
    {
        changes.Clear();
        for (int i = 0; i < reading.BusinessCount; i++)
            if (!reading.Received || reading.Previous[i] != reading.Buffer[i])
            {
                reading.Previous[i] = reading.Buffer[i];
                changes.Add(new DeviceValue(reading.Addresses[i], reading.Buffer[i]));
            }
        reading.Received = true;
        if (changes.Count == 0) return;
        DeviceValue[] values = changes.ToArray();
        _main.Enqueue(() =>
        {
            lock (_sync)
                if (_connection != connection || connection.Stop || _reading != reading) return;
            var handlers = DeviceValuesChanged;
            if (handlers == null) return;
            foreach (Action<DeviceValue[]> handler in handlers.GetInvocationList())
                try { handler(values); } catch (Exception error) { Debug.LogException(error); }
        });
    }

    // Done는 _sync 안에서만 변경합니다. 취소/실제 완료가 겹쳐도 콜백은 한 번입니다.
    private void Complete(IList<WriteRequest> requests, bool success, int start = 0, int count = -1)
    {
        List<Action<bool>> callbacks = null;
        lock (_sync)
        {
            int end = count < 0 ? requests.Count : Math.Min(requests.Count, start + count);
            for (int i = start; i < end; i++)
            {
                WriteRequest request = requests[i];
                if (request.Done) continue;
                request.Done = true;
                if (request.Data.callback == null) continue;
                if (callbacks == null) callbacks = new List<Action<bool>>();
                callbacks.Add(request.Data.callback);
            }
            if (callbacks == null) return;
            _main.Enqueue(() =>
            {
                foreach (Action<bool> callback in callbacks)
                    try { callback(success); } catch (Exception error) { Debug.LogException(error); }
            });
        }
    }

    private MXDiagnostics GetDiagnostics(Connection connection)
    {
        if (!_debugLogging) { connection.Diagnostics = null; return null; }
        int revision = Volatile.Read(ref _debugRevision);
        if (connection.Diagnostics == null || connection.Diagnostics.Revision != revision)
            connection.Diagnostics = new MXDiagnostics(revision, Stopwatch.GetTimestamp());
        return connection.Diagnostics;
    }

    private void LogSummary(Connection connection, bool force = false)
    {
        var diagnostics = GetDiagnostics(connection);
        if (diagnostics == null || !diagnostics.HasActivity) return;
        long now = Stopwatch.GetTimestamp();
        double period = Volatile.Read(ref _debugPeriodMs) * (Stopwatch.Frequency / 1000.0);
        if (!force && now - diagnostics.Started < period) return;
        long accepted;
        int pending;
        lock (_sync) { accepted = _acceptedWrites; pending = _pending.Count; }
        string message = diagnostics.Report(now, accepted, pending, _main.Count);
        int revision = diagnostics.Revision;
        _main.Enqueue(() => { if (_debugLogging && Volatile.Read(ref _debugRevision) == revision) Debug.Log(message); });
    }

    private void Report(string operation, int code)
    {
        if (code == 0)
        {
            if (_errors.Remove(operation)) _main.Enqueue(() => Debug.Log("[MXInterface] " + operation + " 복구"));
            return;
        }
        long now = Stopwatch.GetTimestamp();
        if (_errors.TryGetValue(operation, out var previous) && previous.Code == code && now - previous.Time < Stopwatch.Frequency * 5L) return;
        _errors[operation] = (code, now);
        _main.Enqueue(() => Debug.LogError("[MXInterface] " + operation + " 실패: 0x" + code.ToString("X8")));
    }

    private void Report(Exception error) { _main.Enqueue(() => Debug.LogException(error)); }
}
