# prometheus-exporter-alternative

Prometheus Exporter alternative implementation by .NET

![Windows](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/windows.png)

# Metrics

## RS-WFWATTCH2

for [RS-WFWATTCH2](https://www.ratocsystems.com/products/sensor/watt/rswfwattch2/)

```
sensor_power
sensor_current
sensor_voltage
```

## RS-BTWATTCH2 (Advertisement packet)

for [RS-BTWATTCH2](https://www.ratocsystems.com/products/sensor/watt/rsbtwattch2/)

```
sensor_rssi
sensor_power
sensor_current
sensor_voltage
```

## SwitchBot (Advertisement packet)

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

## 2JCIE-BU (Serial port)

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

## Linux

```
system_uptime
system_interrupt_total
system_context_switch_total
system_forks_total
system_scheduler_task
system_softirq_total
system_cpu_time_total
system_load_average
system_memory_mem gauge
system_memory_buffers gauge
system_memory_cached gauge
system_memory_swap_cached gauge
system_memory_lru gauge
system_memory_unevictable gauge
system_memory_mlocked gauge
system_memory_swap gauge
system_memory_dirty gauge
system_memory_writeback gauge
system_memory_anon_pages gauge
system_memory_mapped gauge
system_memory_shmem gauge
system_memory_k_reclaimable gauge
system_memory_slab gauge
system_memory_kernel_stack gauge
system_memory_page_tables gauge
system_memory_commit_limit gauge
system_memory_committed_as gauge
system_memory_hardware_corrupted gauge
system_virtual_page_total counter
system_virtual_swap_total counter
system_virtual_page_faults_total counter
system_virtual_steal_total counter
system_virtual_scan_total counter
system_virtual_oom_kill_total counter
system_partition_used
system_partition_total
system_partition_free
system_disk_completed_total
system_disk_merged_total
system_disk_sectors_total
system_disk_time_total
system_disk_ios_in_progress
system_disk_io_time_total
system_disk_weight_io_time_total
system_fd_allocated
system_fd_used
system_network_bytes_total
system_network_packets_total
system_network_errors_total
system_network_dropped_total
system_network_fifo_total
system_network_compressed_total
system_network_frame_total
system_network_multicast_total
system_network_collisions_total
system_network_carrier_total
system_tcp_statics
system_process_count
system_thread_count
hardware_cpu_frequency
hardware_cpu_power
hardware_battery_capacity
hardware_battery_voltage
hardware_battery_current
hardware_battery_charge
hardware_battery_charge_full
hardware_ac_online
hardware_monitor
```

### Link

* [LinuxDotNet](https://github.com/usausa/linux-dotnet)

## Raspberry Pi

```
hardware_vcio_temperature
hardware_vcio_frequency
hardware_vcio_voltage
hardware_vcio_throttled
hardware_gpio_level
```

### Link

* [RaspberryDotNet](https://github.com/usausa/raspberrypi-dotnet)

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

# Gallery

![Power](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/power.png)
![Room](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/room.png)
![Uptime](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/uptime.png)
![Summary](https://github.com/usausa/prometheus-exporter-alternative/blob/main/Document/summary.png)

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
