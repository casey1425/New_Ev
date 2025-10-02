using System;

namespace New_Ev // 네임스페이스를 프로젝트 이름과 동일하게 설정
{
    public class Battery
    {
        // ... (내용은 이전과 동일) ...
        private long _last_calc_time = 0;
        public int timestep = 50;
        public bool is_charging = false;
        public bool is_full = false;
        private double _capacity = 50000;
        private double _level = 32000;
        public int full_soc = 100;
        public int bulk_soc = 80;
        private int _soc = 0;
        public double in_voltage = 0;
        public double in_current = 0;
        public double max_current = 100;
        public double max_power = 12000;
        public double max_voltage = 300;
        public double target_current = 50;
        public double target_voltage = 200;

        public Battery()
        {
            setLevel(this._level);
        }

        public double Capacity
        {
            get { return _capacity; }
            set { _capacity = value; setLevel(_level); }
        }
        public double Level
        {
            get { return _level; }
            set { setLevel(value); }
        }
        public int SOC
        {
            get { return _soc; }
            set { setSOC(value); }
        }
        public void SetInitialState(double initialSocPercentage)
        {
            _level = (_capacity * initialSocPercentage) / 100.0;
            _soc = (int)initialSocPercentage;
            is_full = false;
            is_charging = true;
            _last_calc_time = 0;
        }
        private void setLevel(double batteryLevel)
        {
            _level = batteryLevel;
            _soc = (int)((_level / _capacity) * 100);
        }
        private void setSOC(int soc)
        {
            _soc = soc;
            _level = (double)soc / 100.0 * _capacity;
        }
        public double SocAsDouble
        {
            get { return (_level / _capacity) * 100.0; }
        }
        public void TickSimulation()
        {
            long present = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if ((present - _last_calc_time) > timestep)
            {
                _last_calc_time = present;
                if (is_charging == true && _soc < full_soc)
                {
                    double energy = in_voltage * in_current;
                    energy *= (double)timestep / 1000.0 / 3600.0;
                    _level += energy;
                    _soc = (int)((_level / _capacity) * 100);
                    if (_level > _capacity)
                    {
                        _level = _capacity;
                        _soc = 100;
                        is_full = true;
                        is_charging = false;
                    }
                }
            }
        }
    }
}