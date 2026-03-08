# ⚡ EV Charging Simulator (V2G)

전기차(EV)와 충전기(EVSE) 간의 **ISO 15118 및 DIN 70121 통신 표준**을 모사하는 C# 기반 충전 시뮬레이터입니다. 
Whitebeet PLC 모뎀을 SPI 통신으로 제어하며, 비동기 스레드 아키텍처를 도입하여 안정적인 하드웨어 제어와 UI 상태 모니터링을 지원합니다.

## 🚀 Key Features (핵심 기능)

- **표준 충전 상태 머신 (State Machine):** - `IDLE` -> `SLAC` -> `SESSION_INIT` -> `PRE_CHARGE` -> `CHARGING`으로 이어지는 V2G 시퀀스를 구현.
  - `IsValidTransition`을 통한 상태 전이(Transition) 유효성 검증 및 예외 처리.
- **물리적 배터리 시뮬레이션 (Physics Model):**
  - 충전기 인가 전압과 배터리 내부 전압을 비교하여 전압 동기화(Pre-Charge) 로직 수행.
  - $E = P \times t$ 물리 법칙을 적용한 에너지 적산 및 실시간 SOC(%) 계산.
- **안전한 다중 스레드 통신 (Async TX/RX Architecture):**
  - **송신(SendingWorker):** UI 프리징을 방지하기 위해 Command Queue를 도입하여 비동기 순차 명령 처리.
  - **수신(PollingWorker):** 별도 스레드에서 하드웨어 버퍼를 지속 감시 및 이벤트 기반 데이터 전달.
  - **스레드 동기화:** `Mutex` 및 `lock`을 활용한 Thread-Safe한 하드웨어(SPI) 접근 제어.
- 2025.10.13 - 2025.01.30까지 진행함.

## 🛠️ Tech Stack
- **Language:** C#
- **Framework:** Windows Forms (WinForms)
- **Hardware Interface:** SPI Communication (via CH341A USB-to-SPI)
- **Protocol:** ISO 15118, DIN 70121, SLAC, V2G

## 📐 System Architecture

프로젝트의 핵심 구조는 데이터 연산(Core Logic), 비동기 워커(Async Threads), 하드웨어 인터페이스(HW Layer)로 분리되어 있습니다.

```mermaid
flowchart TB
    classDef blueForm fill:#0000FF,stroke:#000,stroke-width:2px,color:#FFF
    classDef greenControl fill:#66FF66,stroke:#333,stroke-width:1px,color:#000
    classDef cyanLogic fill:#00FFFF,stroke:#333,stroke-width:1px,color:#000
    classDef yellowWorker fill:#FFFF00,stroke:#333,stroke-width:1px,color:#000
    classDef redQueue fill:#FFCCCC,stroke:#333,stroke-width:2px,color:#000,stroke-dasharray: 5 5

    subgraph UI_Layer["Presentation Layer"]
        direction TB
        Form1["Form1 (Main)"]:::blueForm
        LogCtrl["LogControl"]:::greenControl
        BattCtrl["BatteryControl"]:::greenControl
    end

    subgraph Logic_Layer["Core Logic"]
        direction TB
        EvLogic["Ev.cs (State Machine)"]:::cyanLogic
        Battery["Battery.cs (Physics)"]:::cyanLogic
    end

    subgraph Worker_Layer["Async Workers"]
        direction TB
        Queue[("Command Queue")]:::redQueue
        Sender["SendingWorker (TX)"]:::yellowWorker
        Poller["PollingWorker (RX)"]:::yellowWorker
    end

    subgraph HW_Layer["Hardware (Thread-Safe)"]
        direction TB
        Whitebeet["RealWhitebeet.cs (Mutex)"]:::yellowWorker
    end

    Form1 --> LogCtrl & BattCtrl
    BattCtrl -. Monitor .-> Battery
    EvLogic -- Update --> Battery

    EvLogic -- "1. Enqueue" --> Queue
    Queue -- "2. Dequeue" --> Sender
    Sender -- "3. Write" --> Whitebeet
    Poller -. "4. Read" .-> Whitebeet
    Whitebeet -- "5. Event" --> Poller
    Poller -- "6. OnDataReceived" --> EvLogic
