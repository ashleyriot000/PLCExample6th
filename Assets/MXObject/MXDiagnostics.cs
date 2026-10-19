using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

// 워커 전용 집계입니다. Unity API나 PLC 통신을 호출하지 않습니다.
internal sealed class MXDiagnostics
{
    private struct Timing
    {
        internal long Count, Failures, Total, Min, Max;
        internal int? Code;
        internal void Add(long ticks, int? code = 0)
        {
            if (Count++ == 0) Min = ticks;
            Total += ticks;
            Min = Math.Min(Min, ticks);
            Max = Math.Max(Max, ticks);
            if (code != 0) Failures++;
            Code = code;
        }
        internal string Duration()
        {
            return Count == 0 ? "평균=- | 최소=- | 최대=-" : Format(
                "평균={0:F3}ms | 최소={1:F3}ms | 최대={2:F3}ms", Ms(Total) / Count, Ms(Min), Ms(Max));
        }
        internal string Calls(string name)
        {
            string result = Count == 0 ? "-" : Code.HasValue ? "0x" + Code.Value.ToString("X8") : "예외";
            return Format("{0}: 호출={1}회(실패={2}회) | {3} | 마지막결과={4}", name, Count, Failures, Duration(), result);
        }
    }

    internal readonly int Revision;
    internal long Started { get; private set; }
    internal bool HasActivity { get { return _calls[0].Count + _calls[1].Count + _calls[2].Count > 0; } }
    private readonly Timing[] _calls = new Timing[3];
    private Timing _gaps, _waits, _cycles;
    private long _lastRead, _sent, _sentOk, _sentFailed;
    private int _userAddresses, _allAddresses;
    private string _scanAddress = "";
    private bool _haveScan;
    private uint _previousScan;
    private ulong _advanced, _skipped, _repeated, _backwards;

    internal MXDiagnostics(int revision, long now) { Revision = revision; Started = now; }

    internal void RecordCall(int operation, int userAddresses, int totalAddresses, long elapsed, int? code, long finished)
    {
        _calls[operation].Add(elapsed, code);
        if (operation == 0)
        {
            _userAddresses = userAddresses;
            _allAddresses = totalAddresses;
            if (code == 0)
            {
                if (_lastRead != 0) _gaps.Add(finished - _lastRead);
                _lastRead = finished;
            }
        }
        else if (operation == 1)
        {
            _sent += totalAddresses;
            if (code == 0) _sentOk += totalAddresses;
            else _sentFailed += totalAddresses;
        }
    }

    internal void RecordWait(long ticks) { _waits.Add(ticks); }
    internal void RecordCycle(long ticks) { _cycles.Add(ticks); }

    internal void SetScanAddress(string address)
    {
        address = address ?? "";
        if (_scanAddress == address) return;
        _scanAddress = address;
        _haveScan = false;
        _advanced = _skipped = _repeated = _backwards = 0;
    }

    internal void RecordScan(string address, uint value)
    {
        SetScanAddress(address);
        if (_haveScan)
        {
            uint difference = unchecked(value - _previousScan);
            if (difference >= 0x80000000u) _backwards++; // 역행/초기화 추정. 큰 누락으로 세지 않습니다.
            else if (difference == 0) _repeated++;
            else { _advanced += difference; _skipped += difference - 1u; }
        }
        _previousScan = value;
        _haveScan = true;
    }

    // 구간 통계만 비우고 직전 성공 읽기/스캔 기준은 보존하여 구간 경계의 누락도 측정합니다.
    internal string Report(long now, long accepted, int pending, int results)
    {
        var text = new StringBuilder(1000);
        text.AppendLine(Format("[MX Summary] 구간={0:F1}ms | 쓰기 접수누적={1}건 | 미전송={2} | 결과대기={3}", Ms(now - Started), accepted, pending, results));
        text.AppendLine(Format("읽기 주소(최종): 업무={0} | 진단추가={1} | 전체={2}", _userAddresses, _allAddresses - _userAddresses, _allAddresses));
        text.AppendLine(_calls[0].Calls("읽기"));
        text.AppendLine(Format("쓰기: 전송={0}건 | 성공={1}건 | 실패={2}건", _sent, _sentOk, _sentFailed));
        text.AppendLine(_calls[1].Calls("쓰기 시간"));
        text.AppendLine(_calls[2].Calls("초기화"));
        text.AppendLine(Format("쓰기 대기: 표본={0}건 | {1}", _waits.Count, _waits.Duration()));
        text.AppendLine(Format("읽기 완료 간격: 표본={0}회 | {1}", _gaps.Count, _gaps.Duration()));
        text.AppendLine(Format("전체 작업 회차: 표본={0}회 | {1}", _cycles.Count, _cycles.Duration()));
        text.Append(_scanAddress.Length == 0 ? "스캔: 미설정" : !_haveScan ? "스캔 " + _scanAddress + ": 수신 대기" : Format(
            "스캔 {0}: 현재={1} | 진행={2} | 미관측={3} | 동일={4} | 역행/초기화추정={5}",
            _scanAddress, _previousScan, _advanced, _skipped, _repeated, _backwards));
        Array.Clear(_calls, 0, _calls.Length);
        _gaps = _waits = _cycles = default(Timing);
        _sent = _sentOk = _sentFailed = 0;
        _advanced = _skipped = _repeated = _backwards = 0;
        Started = now;
        return text.ToString();
    }

    private static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
    private static string Format(string format, params object[] args) { return string.Format(CultureInfo.InvariantCulture, format, args); }
}
