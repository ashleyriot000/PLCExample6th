using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Unity 메인 스레드에서 구독을 관리하고, 프레임별 쓰기와 통신 결과를 전달합니다.</summary>
public class MXRequester : MonoBehaviour
{
    [Serializable]
    public class DeviceSubscriber
    {
        public string address;
        public bool isBitDevice;
        public Action<short> callbacks;
        private short _value;
        public bool HasReadValue { get; private set; }
        public short ReadValue
        {
            get { return _value; }
            set
            {
                if (HasReadValue && _value == value) return;
                HasReadValue = true;
                _value = value;
                Notify(callbacks, value);
            }
        }
        public DeviceSubscriber(string address) { this.address = address; isBitDevice = IsBit(address); }
    }

    // 기존 씬의 Inspector 설정 이름을 유지합니다. interval은 API 호환용입니다.
    private int _interval = 50;
    [Min(1)][SerializeField] private int _capacity = 100;
    [SerializeField] private int _stationNumber = 1;
    [SerializeField] private string _password = null;
    [SerializeField] private bool _useAutoConnect = false;
    [Min(1)][SerializeField] private int _maxWriteBatchSize = 64;
    [Min(1)][SerializeField] private int _maxWritesPerCycle = 1024;
    [Min(1)][SerializeField] private int _maxResultsPerFrame = 128;

    // 디버그가 켜지면 구간 통계를 출력합니다. 주기 0은 작업 회차마다 출력합니다.
    [SerializeField] private bool _debugCommunication = false;
    [Min(0)][SerializeField] private int _debugLogIntervalMs = 1000;

    // 32비트 스캔 번호의 하위 워드 주소. D100 입력 시 D101도 자동으로 읽습니다. 빈 값이면 비활성입니다.
    [SerializeField] private string _scanCounterAddress = "";

    private static MXRequester _current;
    private MXInterface _link;
    private readonly Dictionary<string, DeviceSubscriber> _subscribers = new Dictionary<string, DeviceSubscriber>();
    private List<MXInterface.SetDeviceRequest> _frame = new List<MXInterface.SetDeviceRequest>(64);
    private readonly List<MXInterface.SetDeviceRequest> _ordered = new List<MXInterface.SetDeviceRequest>(64);
    private readonly List<(DeviceSubscriber Target, short Value)> _notifications = new List<(DeviceSubscriber, short)>(128);
    private bool _dirty, _dead, _writesEnabled = true;

    public static MXRequester Get { get { return _current; } }
    public bool IsConnected { get { return _link != null && _link.IsConnected; } }
    public bool IsClosed { get { return _link == null || _link.IsClosed; } }
    public int PendingResetAddressCount { get { return _link == null ? 0 : _link.PendingResetAddressCount; } }

    private void Awake()
    {
        if (_current != null && _current != this)
        {
            _dead = true;
            Debug.LogError("[MXRequester] 중복 인스턴스는 사용하지 않습니다.");
            return;
        }
        _current = this;
        _link = new MXInterface(_interval, Math.Max(1, _capacity), _stationNumber, _password,
            Math.Max(1, _maxWriteBatchSize), Math.Max(1, _maxWritesPerCycle));
        _link.SetDebugLogging(_debugCommunication, _debugLogIntervalMs);
        ApplyReading();
        _link.DeviceValuesChanged += Receive;
        if (_useAutoConnect) Open();
    }

    private void OnValidate()
    {
        _link?.SetDebugLogging(_debugCommunication, _debugLogIntervalMs);
        _dirty = true;
    }

    public void Open()
    {
        if (_dead) return;
        _link?.Open();
        _writesEnabled = true;
    }

    public void Close()
    {
        _writesEnabled = false;
        // 먼저 목록을 분리하고 인터페이스를 닫아, 취소 콜백의 Open/Close 재호출에 대비합니다.
        var cancelled = _frame;
        if (cancelled.Count > 0) _frame = new List<MXInterface.SetDeviceRequest>(64);
        _link?.Close();
        foreach (var request in cancelled) Notify(request.callback, false);
    }

    private void OnApplicationQuit() { Close(); }

    private void OnDestroy()
    {
        _dead = true;
        Close();
        if (_link != null)
        {
            _link.DeviceValuesChanged -= Receive;
            _link.Dispose();
            _link.UpdateMainThread(int.MaxValue);
        }
        if (_current == this) _current = null;
    }

    public void AddDeviceAddress(string address, Action<short> action)
    {
        address = Normalize(address);
        if (_dead || !Valid(address)) return;
        if (!_subscribers.TryGetValue(address, out DeviceSubscriber subscriber))
        {
            subscriber = new DeviceSubscriber(address);
            _subscribers.Add(address, subscriber);
            _dirty = true;
        }
        subscriber.callbacks += action;
        if (subscriber.HasReadValue) Notify(action, subscriber.ReadValue);
    }

    public void RemoveDeviceAddress(string address, Action<short> action)
    {
        address = Normalize(address);
        if (!_subscribers.TryGetValue(address, out DeviceSubscriber subscriber)) return;
        subscriber.callbacks -= action;
        if (subscriber.callbacks != null) return;
        _subscribers.Remove(address);
        _dirty = true;
    }

    public void AddSetDeviceRequest(string deviceAddress, short writeValue, Action<bool> callback = null)
    {
        deviceAddress = Normalize(deviceAddress);
        if (_dead || !_writesEnabled || !Valid(deviceAddress)) { Notify(callback, false); return; }
        _frame.Add(new MXInterface.SetDeviceRequest { deviceAddress = deviceAddress, writeValue = writeValue, callback = callback, queuedAt = System.Diagnostics.Stopwatch.GetTimestamp() });
    }

    // 기존 외부 API 호환 경로입니다. 두 함수 모두 메인 스레드에서 호출합니다.
    public void OnReceivedSetDevice(MXInterface.SetDeviceRequest receive) { Notify(receive.callback, receive.result); }
    public void OnDeviceValueChanged(string address, short newValue)
    {
        if (_subscribers.TryGetValue(Normalize(address), out DeviceSubscriber subscriber)) subscriber.ReadValue = newValue;
    }

    private void Update()
    {
        if (_dead || _link == null) return;
        if (_dirty) ApplyReading();
        _link.UpdateMainThread(Math.Max(1, _maxResultsPerFrame));
    }

    private void ApplyReading()
    {
        _link.SetAutoReadDevice(_subscribers.Keys);
        try { _link.SetScanCounterAddress(_scanCounterAddress); }
        catch (ArgumentException error)
        {
            _link.SetScanCounterAddress(null);
            Debug.LogError("[MXRequester] " + error.Message);
        }
        _dirty = false;
    }

    private void LateUpdate()
    {
        if (_dead || _link == null || _frame.Count == 0) return;
        _ordered.Clear();
        // 프레임 안에서만 워드 → 비트 순서로 정리합니다. 실제 전송 배치는 워커가 만듭니다.
        for (int pass = 0; pass < 2; pass++)
            foreach (var request in _frame)
                if (IsBit(request.deviceAddress) == (pass == 1)) _ordered.Add(request);
        _frame.Clear();
        _link.AddSetDeviceRequests(_ordered);
        _ordered.Clear();
    }

    private void Receive(MXInterface.DeviceValue[] values)
    {
        if (_dead || _dirty) return;
        _notifications.Clear();
        for (int pass = 0; pass < 2; pass++)
            foreach (var value in values)
                if (_subscribers.TryGetValue(value.Address, out DeviceSubscriber target) && target.isBitDevice == (pass == 1))
                    _notifications.Add((target, value.Value));
        // 콜백 중 주소를 삭제/재등록해도 이전 결과를 새 구독 객체에 적용하지 않습니다.
        foreach (var item in _notifications)
        {
            if (_dead) break;
            if (_subscribers.TryGetValue(item.Target.address, out DeviceSubscriber target) && ReferenceEquals(target, item.Target))
                target.ReadValue = item.Value;
        }
        _notifications.Clear();
    }

    private static string Normalize(string address) { return (address ?? "").Trim().ToUpperInvariant(); }
    private static bool Valid(string address) { return address.Length >= 2 && address.IndexOf('\r') < 0 && address.IndexOf('\n') < 0; }

    private static bool IsBit(string address)
    {
        if (string.IsNullOrEmpty(address) || address.Length < 2) return false;
        char next = char.ToUpperInvariant(address[1]);
        return "XYMBLFV".IndexOf(char.ToUpperInvariant(address[0])) >= 0 &&
            (char.IsDigit(next) || (next >= 'A' && next <= 'F'));
    }

    private static void Notify<T>(Action<T> callbacks, T value)
    {
        if (callbacks == null) return;
        foreach (Action<T> callback in callbacks.GetInvocationList())
            try { callback(value); } catch (Exception error) { Debug.LogException(error); }
    }
}
