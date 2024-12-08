# prometheus-exporter-alternative

Prometheus Exporter alternative implementation by .NET

![Windows](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/windows.png)
![Summary](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/summary.png)
![Power](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/power.png)

# Metrics

## RS-WFWATTCH2

for [RS-WFWATTCH2](https://www.ratocsystems.com/products/sensor/watt/rswfwattch2/)

```
sensor_power
sensor_current
sensor_voltage
```

## SwitchBot

for [SwitchBot Meter](https://www.switchbot.jp/products/switchbot-meter)

```
sensor_rssi
sensor_temperature
sensor_humidity
sensor_co2
```

for [SwitchBot Plug Mini](https://www.switchbot.jp/products/switchbot-plug-mini)

```
sensor_power
```

## 2JCIE-BU

for [2JCIE-BU](https://www.fa.omron.co.jp/products/family/3724/lineup.html)

![Environment](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/environment.png)

```
sensor_temperature
sensor_humidity
sensor_light
sensor_pressure
sensor_noise
sensor_discomfort
sensor_heat
sensor_tvoc
sensor_co2
sensor_seismic
```

## Libre Hardware Monitor

```
hardware_battery_charge
hardware_battery_degradation
hardware_battery_voltage
hardware_battery_current
hardware_battery_capacity
hardware_battery_rate
hardware_battery_remaining
hardware_cpu_load
hardware_cpu_clock
hardware_cpu_temperature
hardware_cpu_voltage
hardware_cpu_current
hardware_cpu_power
hardware_gpu_load
hardware_gpu_clock
hardware_gpu_fan
hardware_gpu_temperature
hardware_gpu_power
hardware_gpu_memory
hardware_gpu_throughput
hardware_io_control
hardware_io_fan
hardware_io_temperature
hardware_io_voltage
hardware_memory_used
hardware_memory_available
hardware_memory_load
hardware_storage_used
hardware_storage_bytes
hardware_storage_speed
hardware_storage_temperature
hardware_storage_life
hardware_storage_spare
hardware_storage_amplification
hardware_network_bytes
hardware_network_speed
hardware_network_load
```

## S.M.A.R.T

![Environment](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/smart.png)

```
smart_disk_byte_per_sector
smart_nvme_value
smart_generic_value
```

## Performance Counter

```
performance_* (default)
```

## Hyper-V

![Hyper-V](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/hyperv.png)

```
hyperv_vm_count
hyperv_vm_information
hyperv_vm_state
hyperv_vm_processor_load
hyperv_vm_memory_usage
hyperv_vm_uptime
```

## Ping

```
ping_result_time
```

## BLE signal strength


```
ble_rssi
```

## WiFi signal strength

```
wifi_rssi
```

# Install

## Windows

Copy files to install directory.

* PrometheusExporter.exe
* appsettings.json

```
sc create PrometheusExporter binPath=(install directory)\PrometheusExporter.exe start=auto
```

```
sc start PrometheusExporter
```

## Linux

Service file example.

```
[Unit]
Description=Prometheus Exporter

[Service]
WorkingDirectory=/opt/exporter
ExecStart=/opt/exporter/PrometheusExporter
Restart=always
RestartSec=5
KillSignal=SIGINT

[Install]
WantedBy = multi-user.target
```

## Prometheus

Add targets to prometheus.yml.

```yaml
  - job_name: 'alternative'
    scrape_interval: 10s
    static_configs:
      - targets: ['192.168.1.101:9228']
```
