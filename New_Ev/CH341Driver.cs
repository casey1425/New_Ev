using System;
using System.Runtime.InteropServices;

public class CH341Driver
{
    // CH341DLL.DLL 경로 (실행 파일 옆에 두세요)
    private const string DLL_NAME = "CH341DLL.DLL";

    // 1. 장치 열기
    [DllImport(DLL_NAME)]
    private static extern int CH341OpenDevice(int iIndex);

    // 2. 장치 닫기
    [DllImport(DLL_NAME)]
    private static extern void CH341CloseDevice(int iIndex);

    // 3. SPI 모드 설정 (모드, 데이터 순서 등)
    [DllImport(DLL_NAME)]
    private static extern bool CH341SetStream(int iIndex, int iMode);

    // 4. SPI 데이터 송수신 (4선 방식)
    [DllImport(DLL_NAME)]
    private static extern bool CH341StreamSPI4(int iIndex, int iChipSelect, int iLength, byte[] ioBuffer);

    // 5. 입력 핀 상태 읽기 (GPIO 읽기용)
    // 리턴값의 비트를 확인하여 D0~D7 상태 확인
    [DllImport(DLL_NAME)]
    private static extern bool CH341GetInput(int iIndex, out int iStatus);

    private int _deviceIndex = 0; // 첫 번째 장치 사용
    private bool _isOpen = false;

    public bool Open()
    {
        if (CH341OpenDevice(_deviceIndex) != -1) // -1이면 실패
        {
            // 모드 설정: SPI 모드 (보통 0x80 또는 0x81 사용, 문서 참조 필요)
            // 여기서는 기본 SPI 모드로 가정
            CH341SetStream(_deviceIndex, 0x80);
            _isOpen = true;
            return true;
        }
        return false;
    }

    public void Close()
    {
        if (_isOpen)
        {
            CH341CloseDevice(_deviceIndex);
            _isOpen = false;
        }
    }

    // SPI 데이터 송수신
    public bool TransferSpi(byte[] writeBuffer, byte[] readBuffer)
    {
        if (!_isOpen) return false;

        // CH341은 Write와 Read를 동시에 수행하여 버퍼를 덮어쓰는 방식입니다.
        // 따라서 writeBuffer 내용을 복사해서 전달해야 합니다.
        byte[] tempBuffer = new byte[writeBuffer.Length];
        Array.Copy(writeBuffer, tempBuffer, writeBuffer.Length);

        // CS(Chip Select)는 보통 0x80(비활성) / 0x00(활성) 등으로 제어
        if (CH341StreamSPI4(_deviceIndex, 0x80, tempBuffer.Length, tempBuffer))
        {
            Array.Copy(tempBuffer, readBuffer, tempBuffer.Length);
            return true;
        }
        return false;
    }

    // 핀 상태 읽기 (TX_PENDING, RX_READY 확인용)
    public int ReadPins()
    {
        if (!_isOpen) return 0;
        int status = 0;
        if (CH341GetInput(_deviceIndex, out status))
        {
            return status;
        }
        return 0;
    }
}