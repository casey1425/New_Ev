using System;
using System.Threading;

namespace New_Ev
{
    public class WhitebeetEventArgs : EventArgs
    {
        public int StatusId { get; set; }     // 메시지 ID
        public byte[] Payload { get; set; }   // 실제 데이터 내용
        public string Message { get; set; }   // 로그 메시지
        public bool IsError { get; set; }     // 에러 여부
    }

    public class PollingWorker
    {
        private RealWhitebeet _device;
        private Thread _workerThread; // thread 선언
        private volatile bool _shouldStop = false;
        public event EventHandler<WhitebeetEventArgs> OnDataReceived;

        public PollingWorker(RealWhitebeet device)
        {
            _device = device;
        }

        // 스레드 시작
        public void Start()
        {
            if (_workerThread != null && _workerThread.IsAlive) return;

            _shouldStop = false;
            _workerThread = new Thread(DoWork);
            _workerThread.IsBackground = true; // 메인 프로그램 종료 시 스레드도 자동 종료
            _workerThread.Name = "WhiteBeetPollingThread";
            _workerThread.Start();
        }

        // 스레드 중지
        public void Stop()
        {
            _shouldStop = true;
            if (_workerThread != null && _workerThread.IsAlive)
            {
                // 스레드가 하던 일을 마칠 때까지 최대 0.5초 기다림
                _workerThread.Join(500);
            }
        }

        // 실제 백그라운드 작업 (무한 루프)
        private void DoWork()
        {
            while (!_shouldStop)
            {
                try
                {
                    // 1. 하드웨어에 데이터가 있는지 확인 (Blocking 방식이 아님)
                    var result = _device.V2gEvReceiveRequest();

                    // 2. 데이터가 정상적으로 반환되면 이벤트 발생 (Form에게 알림)
                    OnDataReceived?.Invoke(this, new WhitebeetEventArgs
                    {
                        StatusId = result.Item1,
                        Payload = result.Item2,
                        Message = $"수신됨: ID=0x{result.Item1:X2}, Len={result.Item2.Length}",
                        IsError = false
                    });
                }
                catch (TimeoutException)
                {
                    // 데이터가 없어서 타임아웃 난 것은 정상이므로 무시하고 계속 돔
                }
                catch (Exception ex)
                {
                    // 진짜 에러가 난 경우 로그용으로 이벤트 발생
                    OnDataReceived?.Invoke(this, new WhitebeetEventArgs
                    {
                        IsError = true,
                        Message = $"[Worker Error] {ex.Message}"
                    });
                }
                Thread.Sleep(50);
            }
        }
    }
}