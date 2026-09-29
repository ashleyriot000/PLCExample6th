using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public class MXRequester : MonoBehaviour
{
    [Serializable]
    public class DeviceSubscriber
    {
        public string address;
        public bool isBitDevice;

        public short ReadValue
        {
            get => _readValue;
            set
            {
                if (_readValue == value)
                    return;

                _readValue = value;
                callbacks?.Invoke(value);
            }
        }
        private short _readValue;
        public Action<short> callbacks;

        public DeviceSubscriber(string address)
        {
            this.address = address;
            string prefix = Regex.Match(address, @"^[a-zA-Z]+").Value.ToUpper();
            this.isBitDevice = prefix == "X" || prefix == "Y" || prefix == "M";
        }
    }

    private static MXRequester _instance = null;
    public static MXRequester Get => _instance;

    private MXInterface _mxComponent;

    private ConcurrentQueue<MXInterface.SetDeviceRequest> _setCallbackEnqueue = new();
    private ConcurrentQueue<KeyValuePair<string, short>> _valueChangedQueue = new();

    [Min(30)]
    [SerializeField] private int _interval = 50;
    [SerializeField] private int _capacity = 100;
    [SerializeField] private int _stationNumber = 1;
    [SerializeField] private string _password;
    [SerializeField] private bool _useAutoConnect = false;

    private bool _updated = false;
    private bool _changed = false;

    [SerializeField] private List<DeviceSubscriber> _deviceList = new(100);
    [SerializeField] private List<string> _addressList = new(100);
    private Dictionary<string, DeviceSubscriber> _deviceDict = new();

    private struct PendingWrite
    {
        public string address;
        public short value;
        public Action<bool> callback;
        public int priority;
        public int order;
    }

    private struct PendingRead
    {
        public DeviceSubscriber subscriber;
        public short value;
        public int priority;
        public int order;
    }

    private List<PendingWrite> _pendingWrites = new List<PendingWrite>(50);
    private List<PendingRead> _pendingReads = new List<PendingRead>(50);

    private void Awake()
    {
        _instance = this;
        _deviceList = new(_capacity);
        _addressList = new(_capacity);
        _deviceDict = new(_capacity);

        _mxComponent = new MXInterface(_interval, _capacity, _stationNumber, _password);

        if (_useAutoConnect) Open();
    }

    public void Open() => _mxComponent.Open();
    public void Close() => _mxComponent.Close();
    private void OnApplicationQuit() => Close();
    private void OnDestroy() => _mxComponent?.Dispose();

    // 슬래시(/)를 역슬래시(\)로 자동 변환하여 포맷 통일
    private string NormalizeAddress(string address)
    {
        if (string.IsNullOrEmpty(address)) 
            return string.Empty;

        return address.ToUpper();
    }

    public void AddDeviceAddress(string address, Action<short> action)
    {
        address = NormalizeAddress(address);
        if (address.Length < 2) return;

        if (!_deviceDict.TryGetValue(address, out DeviceSubscriber subscriber))
        {
            subscriber = new DeviceSubscriber(address);
            _deviceList.Add(subscriber);
            _addressList.Add(address);
            _deviceDict[address] = subscriber;
        }

        if (action != null)
        {
            subscriber.callbacks += action;
            action.Invoke(subscriber.ReadValue);
        }

        _deviceList.Sort((x, y) => x.address.CompareTo(y.address));
        _addressList.Sort((x, y) => x.CompareTo(y));
        _changed = true;
    }

    public void RemoveDeviceAddress(string address, Action<short> action)
    {
        address = NormalizeAddress(address);
        if (!_deviceDict.TryGetValue(address, out DeviceSubscriber subscriber)) return;

        if (action != null) subscriber.callbacks -= action;

        if (subscriber.callbacks == null)
        {
            _deviceList.Remove(subscriber);
            _addressList.Remove(address);
            _deviceDict.Remove(address);
        }
        _changed = true;
    }

    public void AddSetDeviceRequest(string deviceAddress, short writeValue, Action<bool> callback = null)
    {
        deviceAddress = NormalizeAddress(deviceAddress);
        if (string.IsNullOrEmpty(deviceAddress)) return;

        string prefix = Regex.Match(deviceAddress, @"^[a-zA-Z]+").Value;
        bool isBit = prefix == "X" || prefix == "Y" || prefix == "M" || prefix == "B" || prefix == "L" || prefix == "F" || prefix == "V";

        _pendingWrites.Add(new PendingWrite()
        {
            address = deviceAddress,
            value = writeValue,
            callback = callback,
            priority = isBit ? 1 : 0,
            order = _pendingWrites.Count
        });

        _updated = true;
    }

    public void OnReceivedSetDevice(MXInterface.SetDeviceRequest receive) { _setCallbackEnqueue.Enqueue(receive); _updated = true; }
    public void OnDeviceValueChanged(string address, short newValue) { _valueChangedQueue.Enqueue(new KeyValuePair<string, short>(address, newValue)); _updated = true; }

    private void Update()
    {
        if (_changed)
        {
            _mxComponent.SetAutoReadDevice(_addressList);
            _changed = false;
        }

        _mxComponent.UpdateMainThread();

        if (_pendingWrites.Count > 0)
        {
            _pendingWrites.Sort((a, b) =>
            {
                int priorityCompare = a.priority.CompareTo(b.priority);
                if (priorityCompare == 0) return a.order.CompareTo(b.order);
                return priorityCompare;
            });

            List<PendingWrite> batchList = new List<PendingWrite>();

            Action FlushBatch = () =>
            {
                if (batchList.Count == 0) return;
                if (batchList.Count == 1)
                {
                    _mxComponent.AddSetDeviceRequest(new MXInterface.SetDeviceRequest()
                    {
                        deviceAddress = batchList[0].address,
                        writeValue = batchList[0].value,
                        callback = batchList[0].callback
                    });
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    short[] values = new short[batchList.Count];
                    Action<bool>[] callbacks = new Action<bool>[batchList.Count];

                    for (int i = 0; i < batchList.Count; i++)
                    {
                        if (i > 0) sb.Append("\n");
                        sb.Append(batchList[i].address);
                        values[i] = batchList[i].value;
                        callbacks[i] = batchList[i].callback;
                    }

                    _mxComponent.AddSetRandomDeviceRequest(new MXInterface.SetRandomDeviceRequest(
                        sb.ToString(), batchList.Count, values, callbacks
                    ));
                }
                batchList.Clear();
            };

            foreach (var w in _pendingWrites)
            {
                // U 디바이스(버퍼메모리)는 다중 쓰기시 에러가 날 수 있으므로 단건 전송으로 자동 분리
                if (w.address.Contains("\\G"))
                {
                    FlushBatch();
                    _mxComponent.AddSetDeviceRequest(new MXInterface.SetDeviceRequest()
                    {
                        deviceAddress = w.address,
                        writeValue = w.value,
                        callback = w.callback
                    });
                }
                else
                {
                    batchList.Add(w);
                }
            }
            FlushBatch();
            _pendingWrites.Clear();
        }

        if (!_updated) return;

        // 2. 단건 쓰기 콜백 처리
        while (_setCallbackEnqueue.TryDequeue(out MXInterface.SetDeviceRequest receive))
        {
            receive.callback?.Invoke(receive.result);
        }

        // 3. 수신 데이터 (Read) 워드/비트 정렬 후 UI 콜백 실행
        while (_valueChangedQueue.TryDequeue(out KeyValuePair<string, short> kvp))
        {
            if (_deviceDict.TryGetValue(kvp.Key, out DeviceSubscriber subscriber))
            {
                _pendingReads.Add(new PendingRead()
                {
                    subscriber = subscriber,
                    value = kvp.Value,
                    priority = subscriber.isBitDevice ? 1 : 0,
                    order = _pendingReads.Count
                });
            }
        }

        if (_pendingReads.Count > 0)
        {
            _pendingReads.Sort((a, b) =>
            {
                int priorityCompare = a.priority.CompareTo(b.priority);
                if (priorityCompare == 0) return a.order.CompareTo(b.order);
                return priorityCompare;
            });

            foreach (var read in _pendingReads)
            {
                read.subscriber.ReadValue = read.value;
            }

            _pendingReads.Clear();
        }

        _updated = false;
    }
}