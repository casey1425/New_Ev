using System;

namespace New_Ev
{
    public class Battery
    {
        private double _level = 0;
        private double _capacity = 50000;
        private int _soc = 0;
        private long _last_calc_time = 0;

        public int timestep = 50;
        public double time_multiplier = 1000;
        public bool is_charging = false;
        public bool is_full = false;
        public int full_soc = 100;
        public int bulk_soc = 80;
        public double in_voltage = 0;
        public double in_current = 0;
        public double max_power = 12000;
        public double max_voltage = 300;
        public double target_voltage = 200;

        public Battery()
        {
        }

        public double Capacity { get => _capacity; set { _capacity = value; setLevel(_level); } }
        public double Level { get => _level; set { setLevel(value); } }
        public int SOC { get => _soc; } // set 접근자를 제거하여 외부에서 직접 수정을 막습니다.
        public double SocAsDouble { get => (_level / _capacity) * 100.0; }

        public void SetInitialState(double initialSocPercentage)
        {
            initialSocPercentage = Math.Clamp(initialSocPercentage, 0, 100);

            _level = (_capacity * initialSocPercentage) / 100.0;
            _soc = (int)initialSocPercentage;
            is_full = (_soc >= 100);
            is_charging = false; // 초기 상태는 항상 충전 중이 아님
            _last_calc_time = 0;
        }

        private void setLevel(double batteryLevel)
        {
            _level = batteryLevel;
            _soc = (int)((_level / _capacity) * 100);
        }

        public void TickSimulation()
        {
            long present = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if ((present - _last_calc_time) > timestep)
            {
                _last_calc_time = present;
                if (is_charging && !is_full)
                {
                    double energy = in_voltage * in_current;
                    energy *= (double)timestep / 1000.0 / 3600.0;
                    energy *= time_multiplier;
                    _level += energy;

                    if (_level >= _capacity)
                    {
                        _level = _capacity;
                        is_full = true;
                        is_charging = false;
                    }
                    _soc = (int)((_level / _capacity) * 100);
                }
            }
        }
    }
}