using System;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using ActUtlType64Lib;
using UnityEngine;

public sealed class MXInterface : IDisposable
{
    public struct SetDeviceRequest
    {
        public string deviceAddress;
        public short writeValue;
        public Action<bool> callback;
        public bool result;
    }

    public struct SetRandomDeviceRequest
    {
        public string deviceAddresses;
        public int count;
        public short[] writeValues;
        public Action<bool>[] callbacks;

        public SetRandomDeviceRequest(string addresses, int count, short[] values, Action<bool>[] callbacks)
        {
            this.deviceAddresses = addresses;
            this.count = count;
            this.writeValues = values;
            this.callbacks = callbacks;
        }
    }

    private Thread _worker;
    private readonly AutoResetEvent _resetEvent = new(false);
    private ActUtlType64 _communicator;

    private readonly int _interval;
    private readonly int _stationNumber;
    private readonly string _password;
    private bool _isRunning = false;

    private readonly ConcurrentQueue<Action> _mainThreadActions = new();
    private readonly ConcurrentQueue<SetDeviceRequest> _setRequestQueue = new();
    private readonly ConcurrentQueue<SetRandomDeviceRequest> _setRandomRequestQueue = new();

    // =========================================================
    // 상시 읽기 통신용 변수 및 재사용 버퍼 (GC 최적화)
    // =========================================================
    private string _allReadAddressesString = "";
    private int _allReadCount = 0;
    private string[] _allReadAddressArray = new string[0];
    private Dictionary<string, short> _requestedAddresses = new Dictionary<string, short>();

    // 매번 new 배열을 생성하지 않고 이 버퍼를 재사용합니다.
    private short[] _readDataBuffer;

    public MXInterface(int interval, int capacity, int stationNumber, string password = null)
    {
        _interval = interval;
        _stationNumber = stationNumber;
        _password = password;

        // 초기 용량(capacity)만큼 버퍼를 미리 할당해 둡니다.
        _readDataBuffer = new short[capacity];
    }

    ~MXInterface() { Close(); Dispose(); }

    public void Open()
    {
        if (_worker != null) return;
        _worker = new Thread(Run) { IsBackground = true };
        _worker.SetApartmentState(ApartmentState.STA);
        _worker.Start();
    }

    public void Close() { _isRunning = false; _resetEvent.Set(); }

    public void Dispose()
    {
        if (_worker != null) 
        { 
            _worker.Abort(); 
            _worker = null; 
        }
        GC.SuppressFinalize(this);
    }

    public void UpdateMainThread()
    {
        while (_mainThreadActions.TryDequeue(out Action action))
            action?.Invoke();
    }

    public void AddSetDeviceRequest(SetDeviceRequest request) { _setRequestQueue.Enqueue(request); _resetEvent.Set(); }
    public void AddSetRandomDeviceRequest(SetRandomDeviceRequest request) { _setRandomRequestQueue.Enqueue(request); _resetEvent.Set(); }

    public void SetAutoReadDevice(IEnumerable<string> deviceArray)
    {
        var validAddresses = deviceArray.Where(addr => !string.IsNullOrEmpty(addr)).ToList();

        if (validAddresses.Count == 0)
        {
            _allReadCount = 0;
            _allReadAddressesString = "";
            return;
        }

        _allReadAddressesString = string.Join("\n", validAddresses);
        _allReadCount = validAddresses.Count;
        _allReadAddressArray = validAddresses.ToArray();

        if (_allReadCount > _readDataBuffer.Length)
        {
            int newSize = Math.Max(_allReadCount, _readDataBuffer.Length * 2);
            _readDataBuffer = new short[newSize];
            Debug.Log($"<color=cyan>[MXInterface]</color> 상시 읽기 버퍼가 {newSize} 크기로 확장되었습니다.");
        }

        _requestedAddresses.Clear();
        foreach (var addr in validAddresses)
        {
            _requestedAddresses[addr] = -1;
        }
    }

    private void Run()
    {
        try
        {
            _communicator = new ActUtlType64();
            _communicator.ActLogicalStationNumber = _stationNumber;
            _communicator.ActPassword = _password;
        }
        catch (COMException e) { Debug.LogError(e.Message); return; }

        if (_communicator.Open() == 0)
            Debug.Log("<color=green>[MXInterface]</color> 시뮬레이터(또는 PLC)와 연결되었습니다.");
        else
            return;

        _isRunning = true;

        while (_isRunning)
        {
            _resetEvent.WaitOne(1);

            while (_setRequestQueue.TryDequeue(out SetDeviceRequest req))
            {
                int ret = _communicator.SetDevice2(req.deviceAddress, req.writeValue);
                if (ret == 0 && req.callback != null)
                {
                    req.result = true;
                    _mainThreadActions.Enqueue(() => req.callback(true));
                }
            }

            while (_setRandomRequestQueue.TryDequeue(out SetRandomDeviceRequest rndReq))
            {
                int ret = _communicator.WriteDeviceRandom2(rndReq.deviceAddresses, rndReq.count, ref rndReq.writeValues[0]);
                bool isSuccess = (ret == 0);
                _mainThreadActions.Enqueue(() =>
                {
                    foreach (var cb in rndReq.callbacks) cb?.Invoke(isSuccess);
                });
            }

            if (_allReadCount > 0)
            {
                int ret = _communicator.ReadDeviceRandom2(_allReadAddressesString, _allReadCount, out _readDataBuffer[0]);

                if (ret == 0)
                {
                    for (int i = 0; i < _allReadCount; i++)
                    {
                        string addr = _allReadAddressArray[i];
                        short currentVal = _readDataBuffer[i];

                        if (_requestedAddresses[addr] != currentVal)
                        {
                            _requestedAddresses[addr] = currentVal;
                            short finalVal = currentVal;
                            _mainThreadActions.Enqueue(() => MXRequester.Get.OnDeviceValueChanged(addr, finalVal));
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[MXInterface] 오리지널 랜덤 읽기 실패: 0x{ret:X8}");
                }
            }
        }
        _communicator?.Close();
    }
}