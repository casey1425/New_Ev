using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;





#region 어셈블리 Sharp341, Version=1.0.1.0, Culture=neutral, PublicKeyToken=null
// C:\Users\cs.kwon\Downloads\CH341_com_csharp-master\CH341_com_csharp-master\CH341_com_Program\bin\Debug\Sharp341.dll
// Decompiled with ICSharpCode.Decompiler 8.2.0.7535
#endregion

namespace CH341;

public class CH341A
{
    public enum IC_VER : uint
    {
        UNKNOWN = 0u,
        CH341A = 32u,
        CH341A3 = 48u
    }

    public enum EEPROM_TYPE : uint
    {
        ID_24C01,
        ID_24C02,
        ID_24C04,
        ID_24C08,
        ID_24C16,
        ID_24C32,
        ID_24C64,
        ID_24C128,
        ID_24C256,
        ID_24C512,
        ID_24C1024,
        ID_24C2048,
        ID_24C4096
    }

    private uint _Index;

    private bool _IsOpen = false;

    private string MessageRequiredToOpenDevice = "It is required to open device using the method OpenDevice()";

    public const uint mCH341_PACKET_LENGTH = 32u;

    public const uint mCH341_PKT_LEN_SHORT = 8u;

    public const uint mCH341_MAX_NUMBER = 16u;

    public const uint mMAX_BUFFER_LENGTH = 4096u;

    public const uint mDEFAULT_BUFFER_LEN = 1024u;

    public const uint mCH341_ENDP_INTER_UP = 129u;

    public const uint mCH341_ENDP_INTER_DOWN = 1u;

    public const uint mCH341_ENDP_DATA_UP = 130u;

    public const uint mCH341_ENDP_DATA_DOWN = 2u;

    public const uint mPipeDeviceCtrl = 4u;

    public const uint mPipeInterUp = 5u;

    public const uint mPipeDataUp = 6u;

    public const uint mPipeDataDown = 7u;

    public const uint mFuncNoOperation = 0u;

    public const uint mFuncGetVersion = 1u;

    public const uint mFuncGetConfig = 2u;

    public const uint mFuncSetTimeout = 9u;

    public const uint mFuncSetExclusive = 11u;

    public const uint mFuncResetDevice = 12u;

    public const uint mFuncResetPipe = 13u;

    public const uint mFuncAbortPipe = 14u;

    public const uint mFuncSetParaMode = 15u;

    public const uint mFuncReadData0 = 16u;

    public const uint mFuncReadData1 = 17u;

    public const uint mFuncWriteData0 = 18u;

    public const uint mFuncWriteData1 = 19u;

    public const uint mFuncWriteRead = 20u;

    public const uint mFuncBufferMode = 32u;

    public const uint mFuncBufferModeDn = 33u;

    public const uint mUSB_CLR_FEATURE = 1u;

    public const uint mUSB_SET_FEATURE = 3u;

    public const uint mUSB_GET_STATUS = 0u;

    public const uint mUSB_SET_ADDRESS = 5u;

    public const uint mUSB_GET_DESCR = 6u;

    public const uint mUSB_SET_DESCR = 7u;

    public const uint mUSB_GET_CONFIG = 8u;

    public const uint mUSB_SET_CONFIG = 9u;

    public const uint mUSB_GET_INTERF = 10u;

    public const uint mUSB_SET_INTERF = 11u;

    public const uint mUSB_SYNC_FRAME = 12u;

    public const uint mCH341_VENDOR_READ = 192u;

    public const uint mCH341_VENDOR_WRITE = 64u;

    public const byte mCH341_PARA_INIT = 177;

    public const byte mCH341_I2C_STATUS = 82;

    public const byte mCH341_I2C_COMMAND = 83;

    public const byte mCH341_PARA_CMD_R0 = 172;

    public const byte mCH341_PARA_CMD_R1 = 173;

    public const byte mCH341_PARA_CMD_W0 = 166;

    public const byte mCH341_PARA_CMD_W1 = 167;

    public const byte mCH341_PARA_CMD_STS = 160;

    public const byte mCH341A_CMD_SET_OUTPUT = 161;

    public const byte mCH341A_CMD_IO_ADDR = 162;

    public const byte mCH341A_CMD_PRINT_OUT = 163;

    public const byte mCH341A_CMD_PWM_OUT = 164;

    public const byte mCH341A_CMD_SHORT_PKT = 165;

    public const byte mCH341A_CMD_SPI_STREAM = 168;

    public const byte mCH341A_CMD_I2C_STREAM = 170;

    public const byte mCH341A_CMD_UIO_STREAM = 171;

    public const byte mCH341A_CMD_PIO_STREAM = 174;

    public const byte mCH341A_BUF_CLEAR = 178;

    public const byte mCH341A_I2C_CMD_X = 84;

    public const byte mCH341A_DELAY_MS = 94;

    public const byte mCH341A_GET_VER = 95;

    public const byte mCH341A_EPP_IO_MAX = byte.MaxValue;

    public const byte mCH341A_CMD_IO_ADDR_W = 0;

    public const byte mCH341A_CMD_IO_ADDR_R = 128;

    public const byte mCH341A_CMD_I2C_STM_STA = 116;

    public const byte mCH341A_CMD_I2C_STM_STO = 117;

    public const byte mCH341A_CMD_I2C_STM_OUT = 128;

    public const byte mCH341A_CMD_I2C_STM_IN = 192;

    public const byte mCH341A_CMD_I2C_STM_SET = 96;

    public const byte mCH341A_CMD_I2C_STM_US = 64;

    public const byte mCH341A_CMD_I2C_STM_MS = 80;

    public const byte mCH341A_CMD_I2C_STM_DLY = 15;

    public const byte mCH341A_CMD_I2C_STM_END = 0;

    public const uint mCH341A_CMD_UIO_STM_IN = 0u;

    public const uint mCH341A_CMD_UIO_STM_DIR = 64u;

    public const uint mCH341A_CMD_UIO_STM_OUT = 128u;

    public const uint mCH341A_CMD_UIO_STM_US = 192u;

    public const uint mCH341A_CMD_UIO_STM_END = 32u;

    public const uint mCH341_PARA_MODE_EPP = 0u;

    public const uint mCH341_PARA_MODE_EPP17 = 0u;

    public const uint mCH341_PARA_MODE_EPP19 = 1u;

    public const uint mCH341_PARA_MODE_MEM = 2u;

    public const uint mCH341_PARA_MODE_ECP = 3u;

    public const uint mStateBitERR = 256u;

    public const uint mStateBitPEMP = 512u;

    public const uint mStateBitINT = 1024u;

    public const uint mStateBitSLCT = 2048u;

    public const uint mStateBitWAIT = 8192u;

    public const uint mStateBitDATAS = 16384u;

    public const uint mStateBitADDRS = 32768u;

    public const uint mStateBitRESET = 65536u;

    public const uint mStateBitWRITE = 131072u;

    public const uint mStateBitSCL = 4194304u;

    public const uint mStateBitSDA = 8388608u;

    public const uint MAX_DEVICE_PATH_SIZE = 128u;

    public const uint MAX_DEVICE_ID_SIZE = 64u;

    private const string DllName = "CH341DLL.DLL";

    public static uint mCH341_EPP_IO_MAX => 31u;

    public static uint mCH341A_CMD_I2C_STM_MAX => 32u;

    public CH341A()
    {
        _Index = 0u;
    }

    public CH341A(uint iIndex)
    {
        _Index = iIndex;
    }

    public bool OpenDevice()
    {
        IntPtr intPtr = CH341OpenDevice(_Index);
        if ((int)intPtr == -1)
        {
            _IsOpen = false;
        }
        else
        {
            _IsOpen = true;
        }

        return _IsOpen;
    }

    public void CloseDevice()
    {
        CH341CloseDevice(_Index);
        _IsOpen = false;
    }

    public uint GetVersion()
    {
        return CH341GetVersion();
    }

    public uint GetDrvVersion()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341GetDrvVersion();
    }

    public string GetDeviceName()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr ptr = CH341GetDeviceName(_Index);
        return Marshal.PtrToStringAnsi(ptr);
    }

    public IC_VER GetVerIC()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return (IC_VER)CH341GetVerIC(_Index);
    }

    public bool SetExclusive(uint iExclusive)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetExclusive(_Index, iExclusive);
    }

    public bool SetStream(uint iMode)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetStream(_Index, iMode);
    }

    public bool SetDelaymS(uint iDelay)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetDelaymS(_Index, iDelay);
    }

    //public uint CH341DriverCommand(ref ControlTransferCommand ioCommand)
    //{
    //    if (!_IsOpen)
    //    {
    //        throw new InvalidOperationException(MessageRequiredToOpenDevice);
    //    }

    //    IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf((object)ioCommand));
    //    Marshal.StructureToPtr((object)ioCommand, intPtr, fDeleteOld: false);
    //    uint result = CH341DriverCommand(_Index, intPtr);
    //    ioCommand = (ControlTransferCommand)Marshal.PtrToStructure(intPtr, typeof(ControlTransferCommand));
    //    return result;
    //}

    public bool SetTimeout(uint iWriteTimeout, uint iReadTimeout)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetTimeout(_Index, iWriteTimeout, iReadTimeout);
    }

    public bool ReadData(out byte[] oBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal(4096);
        uint ioLength = 4096u;
        bool flag = CH341ReadData(_Index, intPtr, out ioLength);
        if (flag)
        {
            oBuffer = new byte[ioLength];
            Marshal.Copy(intPtr, oBuffer, 0, (int)ioLength);
        }
        else
        {
            oBuffer = null;
        }

        return flag;
    }

    public bool WriteData(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341WriteData(_Index, intPtr, out ioLength);
    }

    public bool WriteRead(byte[] iWriteBuffer, uint iReadStep, uint iReadTimes, out byte[] oReadBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint num = (uint)iWriteBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)num);
        Marshal.Copy(iWriteBuffer, 0, intPtr, (int)num);
        uint oReadLength = 1024u;
        IntPtr intPtr2 = Marshal.AllocHGlobal((int)oReadLength);
        bool flag = CH341WriteRead(_Index, num, intPtr, iReadStep, iReadTimes, out oReadLength, intPtr2);
        if (flag)
        {
            oReadBuffer = new byte[oReadLength];
            Marshal.Copy(intPtr2, oReadBuffer, 0, (int)oReadLength);
        }
        else
        {
            oReadBuffer = null;
        }

        return flag;
    }

    public bool FlushBuffer()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341FlushBuffer(_Index);
    }

    public bool ResetDevice()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341ResetDevice(_Index);
    }

    public bool ResetRead()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341ResetRead(_Index);
    }

    public bool ResetWrite()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341ResetWrite(_Index);
    }

    public bool GetDeviceDescr(out byte[] oBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal(4096);
        uint ioLength = 4096u;
        bool flag = CH341GetDeviceDescr(_Index, intPtr, out ioLength);
        if (flag)
        {
            oBuffer = new byte[ioLength];
            Marshal.Copy(intPtr, oBuffer, 0, (int)ioLength);
        }
        else
        {
            oBuffer = null;
        }

        return flag;
    }

    public bool GetConfigDescr(out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal(4096);
        uint ioLength = 4096u;
        bool flag = CH341GetConfigDescr(_Index, intPtr, out ioLength);
        if (flag)
        {
            obuf = new byte[ioLength];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool SetBufUpload(uint iEnableOrClear)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetBufUpload(_Index, iEnableOrClear);
    }

    public int QueryBufUpload()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341QueryBufUpload(_Index);
    }

    public bool SetBufDownload(uint iEnableOrClear)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetBufDownload(_Index, iEnableOrClear);
    }

    public int QueryBufDownload()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341QueryBufDownload(_Index);
    }

    public bool ReadInter(out uint iStatus)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341ReadInter(_Index, out iStatus);
    }

    public bool AbortInter()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341AbortInter(_Index);
    }

    public bool ResetInter()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341ResetInter(_Index);
    }

    public bool InitParallel(uint iMode)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341InitParallel(_Index, iMode);
    }

    public bool SetParaMode(uint iMode)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetParaMode(_Index, iMode);
    }

    public bool GetStatus(out uint iStatus)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341GetStatus(_Index, out iStatus);
    }

    public bool AbortRead()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341AbortRead(_Index);
    }

    public bool AbortWrite()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341AbortWrite(_Index);
    }

    public bool ReadData0(uint ioLength, out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        uint ioLength2 = ioLength;
        bool flag = CH341ReadData0(_Index, intPtr, ref ioLength2);
        if (flag)
        {
            obuf = new byte[ioLength2];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength2);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool ReadData1(uint ioLength, out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        uint ioLength2 = ioLength;
        bool flag = CH341ReadData1(_Index, intPtr, ref ioLength2);
        if (flag)
        {
            obuf = new byte[ioLength2];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength2);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool WriteData0(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341WriteData0(_Index, intPtr, out ioLength);
    }

    public bool WriteData1(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341WriteData1(_Index, intPtr, out ioLength);
    }

    public bool EppReadData(uint ioLength, out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        uint ioLength2 = ioLength;
        bool flag = CH341EppReadData(_Index, intPtr, out ioLength2);
        if (flag)
        {
            obuf = new byte[ioLength2];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength2);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool EppReadAddr(uint ioLength, out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal(4096);
        uint ioLength2 = 4096u;
        bool flag = CH341EppReadAddr(_Index, intPtr, out ioLength2);
        if (flag)
        {
            obuf = new byte[ioLength2];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength2);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool EppWriteData(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341EppWriteData(_Index, intPtr, out ioLength);
    }

    public bool EppWriteAddr(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341EppWriteAddr(_Index, intPtr, out ioLength);
    }

    public bool EppSetAddr(byte iAddr)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341EppSetAddr(_Index, iAddr);
    }

    public bool MemReadAddr0(uint ioLength, out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        uint ioLength2 = ioLength;
        bool flag = CH341MemReadAddr0(_Index, intPtr, out ioLength2);
        if (flag)
        {
            obuf = new byte[ioLength2];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength2);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool MemReadAddr1(uint ioLength, out byte[] obuf)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        uint ioLength2 = ioLength;
        bool flag = CH341MemReadAddr1(_Index, intPtr, out ioLength2);
        if (flag)
        {
            obuf = new byte[ioLength2];
            Marshal.Copy(intPtr, obuf, 0, (int)ioLength2);
        }
        else
        {
            obuf = null;
        }

        return flag;
    }

    public bool MemWriteAddr0(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341MemWriteAddr0(_Index, intPtr, out ioLength);
    }

    public bool MemWriteAddr1(byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint ioLength = (uint)iBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)ioLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)ioLength);
        return CH341MemWriteAddr1(_Index, intPtr, out ioLength);
    }

    public bool GetInput(out uint iStatus)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341GetInput(_Index, out iStatus);
    }

    public bool SetOutput(uint iEnable, uint iSetDirOut, uint iSetDataOut)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetOutput(_Index, iEnable, iSetDirOut, iSetDataOut);
    }

    public bool Set_D5_D0(uint iSetDirOut, uint iSetDataOut)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341Set_D5_D0(_Index, iSetDirOut, iSetDataOut);
    }

    public bool StreamSPI3(uint iChipSelect, ref byte[] ioBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint num = (uint)ioBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)num);
        Marshal.Copy(ioBuffer, 0, intPtr, (int)num);
        bool flag = CH341StreamSPI3(_Index, iChipSelect, num, intPtr);
        if (flag)
        {
            Marshal.Copy(intPtr, ioBuffer, 0, (int)num);
        }

        return flag;
    }

    public bool StreamSPI4(uint iChipSelect, ref byte[] ioBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint num = (uint)ioBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)num);
        Marshal.Copy(ioBuffer, 0, intPtr, (int)num);
        bool flag = CH341StreamSPI4(_Index, iChipSelect, num, intPtr);
        if (flag)
        {
            Marshal.Copy(intPtr, ioBuffer, 0, (int)num);
        }

        return flag;
    }

    public bool StreamSPI5(uint iChipSelect, uint iLength, ref byte[] ioBuffer, ref byte[] ioBuffer2)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        if (iLength > ioBuffer.Length)
        {
            throw new ArgumentOutOfRangeException("iLength < ioBuffer.Length");
        }

        if (iLength > ioBuffer2.Length)
        {
            throw new ArgumentOutOfRangeException("iLength < ioBuffer2.Length");
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)iLength);
        Marshal.Copy(ioBuffer, 0, intPtr, (int)iLength);
        IntPtr intPtr2 = Marshal.AllocHGlobal((int)iLength);
        Marshal.Copy(ioBuffer2, 0, intPtr2, (int)iLength);
        bool flag = CH341StreamSPI5(_Index, iChipSelect, iLength, intPtr, intPtr2);
        if (flag)
        {
            Marshal.Copy(intPtr, ioBuffer, 0, (int)iLength);
            Marshal.Copy(intPtr2, ioBuffer2, 0, (int)iLength);
        }

        return flag;
    }

    public bool BitStreamSPI(uint iLength, ref byte[] ioBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal(ioBuffer.Length);
        Marshal.Copy(ioBuffer, 0, intPtr, ioBuffer.Length);
        bool flag = CH341BitStreamSPI(_Index, iLength, intPtr);
        if (flag)
        {
            Marshal.Copy(intPtr, ioBuffer, 0, ioBuffer.Length);
        }

        return flag;
    }

    public bool ReadEEPROM(EEPROM_TYPE iEepromID, uint iAddr, uint iLength, out byte[] oBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)iLength);
        bool flag = CH341ReadEEPROM(_Index, (uint)iEepromID, iAddr, iLength, intPtr);
        if (flag)
        {
            oBuffer = new byte[iLength];
            Marshal.Copy(intPtr, oBuffer, 0, (int)iLength);
        }
        else
        {
            oBuffer = null;
        }

        return flag;
    }

    public bool WriteEEPROM(EEPROM_TYPE iEepromID, uint iAddr, uint iLength, byte[] iBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        IntPtr intPtr = Marshal.AllocHGlobal((int)iLength);
        Marshal.Copy(iBuffer, 0, intPtr, (int)iLength);
        return CH341WriteEEPROM(_Index, (uint)iEepromID, iAddr, iLength, intPtr);
    }

    public bool ReadI2C(byte iDevice, byte iAddr, out byte oByte)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341ReadI2C(_Index, iDevice, iAddr, out oByte);
    }

    public bool WriteI2C(byte iDevice, byte iAddr, byte iByte)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341WriteI2C(_Index, iDevice, iAddr, iByte);
    }

    public bool StreamI2C(byte[] iWriteBuffer, uint iReadLength, out byte[] oReadBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        uint num = (uint)iWriteBuffer.Length;
        IntPtr intPtr = Marshal.AllocHGlobal((int)num);
        Marshal.Copy(iWriteBuffer, 0, intPtr, (int)num);
        IntPtr intPtr2 = Marshal.AllocHGlobal((int)iReadLength);
        bool flag = CH341StreamI2C(_Index, num, intPtr, iReadLength, intPtr2);
        if (flag)
        {
            oReadBuffer = new byte[iReadLength];
            Marshal.Copy(intPtr2, oReadBuffer, 0, (int)iReadLength);
        }
        else
        {
            oReadBuffer = null;
        }

        return flag;
    }

    public bool IIC_IssueStart()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        byte[] iBuffer = new byte[3] { 170, 116, 0 };
        return WriteData(iBuffer);
    }

    public bool IIC_IssueStop()
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        byte[] iBuffer = new byte[3] { 170, 117, 0 };
        return WriteData(iBuffer);
    }

    public bool IIC_OutBlockSkipAck(byte[] iOutBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        byte[] array = new byte[2 + iOutBuffer.Length + 1];
        array[0] = 170;
        array[1] = (byte)(0x80u | (uint)iOutBuffer.Length);
        Array.Copy(iOutBuffer, 0, array, 2, iOutBuffer.Length);
        array[iOutBuffer.Length + 2] = 170;
        return WriteData(array);
    }

    public bool IIC_OutByteCheckAck(byte iOutByte)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        byte[] iWriteBuffer = new byte[4] { 170, 128, iOutByte, 0 };
        if (WriteRead(iWriteBuffer, mCH341A_CMD_I2C_STM_MAX, 1u, out var oReadBuffer) && oReadBuffer.Length != 0 && (oReadBuffer[oReadBuffer.Length - 1] & 0x80) == 0)
        {
            return true;
        }

        return false;
    }

    public bool IIC_InBlockByAck(uint iInLength, out byte[] iOutBuffer)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        if (iInLength == 0)
        {
            throw new ArgumentOutOfRangeException("iInLength == 0");
        }

        if (iInLength > mCH341A_CMD_I2C_STM_MAX)
        {
            throw new ArgumentOutOfRangeException("iInLength > mCH341A_CMD_I2C_STM_MAX");
        }

        byte[] iWriteBuffer = new byte[3]
        {
            170,
            (byte)(0xC0u | iInLength),
            0
        };
        if (WriteRead(iWriteBuffer, mCH341A_CMD_I2C_STM_MAX, 1u, out iOutBuffer) && iOutBuffer.Length == iInLength)
        {
            return true;
        }

        return false;
    }

    public bool IIC_InByteNoAck(out byte oInByte)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        byte[] iWriteBuffer = new byte[3] { 170, 192, 0 };
        if (WriteRead(iWriteBuffer, mCH341A_CMD_I2C_STM_MAX, 1u, out var oReadBuffer) && oReadBuffer.Length != 0)
        {
            oInByte = oReadBuffer[oReadBuffer.Length - 1];
            return true;
        }

        oInByte = 0;
        return false;
    }

    public bool SetupSerial(uint iParityMode, uint iBaudRate)
    {
        if (!_IsOpen)
        {
            throw new InvalidOperationException(MessageRequiredToOpenDevice);
        }

        return CH341SetupSerial(_Index, iParityMode, iBaudRate);
    }

    [DllImport("CH341DLL.DLL")]
    private static extern IntPtr CH341OpenDevice(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern void CH341CloseDevice(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern uint CH341GetVersion();

    [DllImport("CH341DLL.DLL")]
    private static extern uint CH341DriverCommand(uint iIndex, IntPtr ioCommand);

    [DllImport("CH341DLL.DLL")]
    private static extern uint CH341GetDrvVersion();

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ResetDevice(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341GetDeviceDescr(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341GetConfigDescr(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ReadInter(uint iIndex, out uint iStatus);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341AbortInter(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetParaMode(uint iIndex, uint iMode);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341InitParallel(uint iIndex, uint iMode);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ReadData0(uint iIndex, IntPtr oBuffer, ref uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ReadData1(uint iIndex, IntPtr oBuffer, ref uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341AbortRead(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341WriteData0(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341WriteData1(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341AbortWrite(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341GetStatus(uint iIndex, out uint iStatus);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ReadI2C(uint iIndex, byte iDevice, byte iAddr, out byte oByte);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341WriteI2C(uint iIndex, byte iDevice, byte iAddr, byte iByte);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341EppReadData(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341EppReadAddr(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341EppWriteData(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341EppWriteAddr(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341EppSetAddr(uint iIndex, byte iAddr);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341MemReadAddr0(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341MemReadAddr1(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341MemWriteAddr0(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341MemWriteAddr1(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetExclusive(uint iIndex, uint iExclusive);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetTimeout(uint iIndex, uint iWriteTimeout, uint iReadTimeout);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ReadData(uint iIndex, IntPtr oBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341WriteData(uint iIndex, IntPtr iBuffer, out uint ioLength);

    [DllImport("CH341DLL.DLL")]
    private static extern IntPtr CH341GetDeviceName(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern uint CH341GetVerIC(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341FlushBuffer(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341WriteRead(uint iIndex, uint iWriteLength, IntPtr iWriteBuffer, uint iReadStep, uint iReadTimes, out uint oReadLength, IntPtr oReadBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetStream(uint iIndex, uint iMode);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetDelaymS(uint iIndex, uint iDelay);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341StreamI2C(uint iIndex, uint iWriteLength, IntPtr iWriteBuffer, uint iReadLength, IntPtr oReadBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ReadEEPROM(uint iIndex, uint iEepromID, uint iAddr, uint iLength, IntPtr oBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341WriteEEPROM(uint iIndex, uint iEepromID, uint iAddr, uint iLength, IntPtr iBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341GetInput(uint iIndex, out uint iStatus);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetOutput(uint iIndex, uint iEnable, uint iSetDirOut, uint iSetDataOut);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341Set_D5_D0(uint iIndex, uint iSetDirOut, uint iSetDataOut);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341StreamSPI3(uint iIndex, uint iChipSelect, uint iLength, IntPtr ioBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341StreamSPI4(uint iIndex, uint iChipSelect, uint iLength, IntPtr ioBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341StreamSPI5(uint iIndex, uint iChipSelect, uint iLength, IntPtr ioBuffer, IntPtr ioBuffer2);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341BitStreamSPI(uint iIndex, uint iLength, IntPtr ioBuffer);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetBufUpload(uint iIndex, uint iEnableOrClear);

    [DllImport("CH341DLL.DLL")]
    private static extern int CH341QueryBufUpload(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetBufDownload(uint iIndex, uint iEnableOrClear);

    [DllImport("CH341DLL.DLL")]
    private static extern int CH341QueryBufDownload(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ResetInter(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ResetRead(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341ResetWrite(uint iIndex);

    [DllImport("CH341DLL.DLL")]
    private static extern bool CH341SetupSerial(uint iIndex, uint iParityMode, uint iBaudRate);
}
#if false // 디컴파일 로그
캐시의 '13'개 항목
------------------
확인: 'mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
단일 어셈블리를 찾았습니다. 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
WARN: 버전이 일치하지 않습니다. 예상: '2.0.0.0', 실제: '4.0.0.0'
로드 위치: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll'
#endif

