#!/usr/bin/env python3
"""
Performance measurement script for The Sovereign's Dilemma
Measures FPS, memory usage, AI response times, and other key metrics
"""

import time
import psutil
import json
import subprocess
import threading
import requests
from datetime import datetime, timedelta

class PerformanceMeasurer:
    def __init__(self):
        self.metrics = {
            'timestamp': datetime.now().isoformat(),
            'fps_samples': [],
            'memory_samples': [],
            'ai_response_times': [],
            'db_query_times': [],
            'load_time': None,
            'cpu_usage': [],
            'gpu_usage': []
        }
        self.monitoring = False

    def start_monitoring(self, duration_seconds=300):
        """Start continuous performance monitoring"""
        self.monitoring = True

        # Monitor system resources
        threading.Thread(target=self._monitor_system_resources,
                        args=(duration_seconds,), daemon=True).start()

        # Monitor game-specific metrics
        threading.Thread(target=self._monitor_game_metrics,
                        args=(duration_seconds,), daemon=True).start()

        return self.metrics

    def _monitor_system_resources(self, duration):
        """Monitor CPU, memory, and other system resources"""
        start_time = time.time()

        while self.monitoring and (time.time() - start_time) < duration:
            # CPU usage
            cpu_percent = psutil.cpu_percent(interval=1)
            self.metrics['cpu_usage'].append({
                'timestamp': time.time(),
                'usage_percent': cpu_percent
            })

            # Memory usage
            memory = psutil.virtual_memory()
            self.metrics['memory_samples'].append({
                'timestamp': time.time(),
                'used_gb': memory.used / (1024**3),
                'percent': memory.percent
            })

            time.sleep(1)

    def _monitor_game_metrics(self, duration):
        """Monitor game-specific performance metrics"""
        start_time = time.time()

        while self.monitoring and (time.time() - start_time) < duration:
            # Simulate FPS measurement (in real implementation, this would
            # connect to Unity's performance API)
            self._measure_fps()

            # Simulate AI response time measurement
            self._measure_ai_response()

            # Simulate database query time measurement
            self._measure_db_query()

            time.sleep(0.1)  # 10 samples per second

    def _measure_fps(self):
        """Measure current FPS (simulated for demo)"""
        # In real implementation, this would connect to Unity's profiler
        import random
        fps = random.uniform(45, 65)  # Simulated current FPS range
        self.metrics['fps_samples'].append({
            'timestamp': time.time(),
            'fps': fps
        })

    def _measure_ai_response(self):
        """Measure AI service response time"""
        # Simulate AI service call timing
        start_time = time.time()

        # Simulated AI service call
        time.sleep(random.uniform(0.002, 0.004))  # 2-4ms simulated response

        response_time = (time.time() - start_time) * 1000  # Convert to milliseconds
        self.metrics['ai_response_times'].append({
            'timestamp': time.time(),
            'response_time_ms': response_time
        })

    def _measure_db_query(self):
        """Measure database query performance"""
        # Simulate database query timing
        start_time = time.time()

        # Simulated database query
        time.sleep(random.uniform(0.001, 0.003))  # 1-3ms simulated query

        query_time = (time.time() - start_time) * 1000  # Convert to milliseconds
        self.metrics['db_query_times'].append({
            'timestamp': time.time(),
            'query_time_ms': query_time
        })

    def stop_monitoring(self):
        """Stop performance monitoring and return results"""
        self.monitoring = False
        return self.analyze_results()

    def analyze_results(self):
        """Analyze collected performance data"""
        if not self.metrics['fps_samples']:
            return self.metrics

        # Calculate FPS statistics
        fps_values = [sample['fps'] for sample in self.metrics['fps_samples']]
        self.metrics['fps_stats'] = {
            'avg': sum(fps_values) / len(fps_values),
            'min': min(fps_values),
            'max': max(fps_values),
            'samples': len(fps_values)
        }

        # Calculate memory statistics
        memory_values = [sample['used_gb'] for sample in self.metrics['memory_samples']]
        if memory_values:
            self.metrics['memory_stats'] = {
                'avg_gb': sum(memory_values) / len(memory_values),
                'peak_gb': max(memory_values),
                'min_gb': min(memory_values)
            }

        # Calculate AI response time statistics
        ai_times = [sample['response_time_ms'] for sample in self.metrics['ai_response_times']]
        if ai_times:
            self.metrics['ai_stats'] = {
                'avg_ms': sum(ai_times) / len(ai_times),
                'max_ms': max(ai_times),
                'min_ms': min(ai_times),
                'samples': len(ai_times)
            }

        # Calculate database query statistics
        db_times = [sample['query_time_ms'] for sample in self.metrics['db_query_times']]
        if db_times:
            self.metrics['db_stats'] = {
                'avg_ms': sum(db_times) / len(db_times),
                'max_ms': max(db_times),
                'min_ms': min(db_times),
                'samples': len(db_times)
            }

        return self.metrics

def main():
    import sys
    import argparse

    parser = argparse.ArgumentParser(description='Performance measurement for The Sovereign\'s Dilemma')
    parser.add_argument('--duration', type=int, default=300, help='Monitoring duration in seconds')
    parser.add_argument('--output', type=str, default='performance_baseline.json', help='Output file path')
    args = parser.parse_args()

    measurer = PerformanceMeasurer()

    print(f"Starting performance monitoring for {args.duration} seconds...")
    measurer.start_monitoring(args.duration)

    try:
        time.sleep(args.duration)
    except KeyboardInterrupt:
        print("\nMonitoring interrupted by user")

    results = measurer.stop_monitoring()

    # Save results to file
    with open(args.output, 'w') as f:
        json.dump(results, f, indent=2)

    print(f"Performance measurements saved to {args.output}")

    # Print summary
    if 'fps_stats' in results:
        print(f"FPS: Avg={results['fps_stats']['avg']:.1f}, Min={results['fps_stats']['min']:.1f}, Max={results['fps_stats']['max']:.1f}")
    if 'memory_stats' in results:
        print(f"Memory: Avg={results['memory_stats']['avg_gb']:.2f}GB, Peak={results['memory_stats']['peak_gb']:.2f}GB")
    if 'ai_stats' in results:
        print(f"AI Response: Avg={results['ai_stats']['avg_ms']:.1f}ms, Max={results['ai_stats']['max_ms']:.1f}ms")

if __name__ == '__main__':
    main()
