#!/usr/bin/env python3
"""
AI System Performance Optimizer for The Sovereign's Dilemma
Implements request batching, caching, and circuit breaker patterns
"""

import asyncio
import time
import json
import logging
from typing import List, Dict, Any, Optional
from dataclasses import dataclass
from collections import deque
import aiohttp
import hashlib

@dataclass
class AIRequest:
    id: str
    prompt: str
    context: Dict[str, Any]
    timestamp: float
    priority: int = 0

@dataclass
class AIResponse:
    request_id: str
    response: str
    processing_time: float
    from_cache: bool = False

class RequestCache:
    def __init__(self, max_size: int = 10000, ttl_seconds: int = 3600):
        self.cache = {}
        self.timestamps = {}
        self.max_size = max_size
        self.ttl_seconds = ttl_seconds
        self.hit_count = 0
        self.miss_count = 0

    def _generate_key(self, request: AIRequest) -> str:
        """Generate cache key from request"""
        content = f"{request.prompt}:{json.dumps(request.context, sort_keys=True)}"
        return hashlib.sha256(content.encode()).hexdigest()

    def get(self, request: AIRequest) -> Optional[str]:
        """Get cached response if available and not expired"""
        key = self._generate_key(request)

        if key in self.cache:
            # Check if expired
            if time.time() - self.timestamps[key] < self.ttl_seconds:
                self.hit_count += 1
                return self.cache[key]
            else:
                # Remove expired entry
                del self.cache[key]
                del self.timestamps[key]

        self.miss_count += 1
        return None

    def put(self, request: AIRequest, response: str):
        """Store response in cache"""
        key = self._generate_key(request)

        # Evict oldest entries if cache is full
        if len(self.cache) >= self.max_size:
            oldest_key = min(self.timestamps.keys(), key=lambda k: self.timestamps[k])
            del self.cache[oldest_key]
            del self.timestamps[oldest_key]

        self.cache[key] = response
        self.timestamps[key] = time.time()

    def get_hit_rate(self) -> float:
        """Calculate cache hit rate"""
        total = self.hit_count + self.miss_count
        return self.hit_count / total if total > 0 else 0.0

class CircuitBreaker:
    def __init__(self, failure_threshold: int = 5, recovery_timeout: int = 30):
        self.failure_threshold = failure_threshold
        self.recovery_timeout = recovery_timeout
        self.failure_count = 0
        self.last_failure_time = 0
        self.state = "CLOSED"  # CLOSED, OPEN, HALF_OPEN

    def can_proceed(self) -> bool:
        """Check if requests can proceed"""
        if self.state == "CLOSED":
            return True
        elif self.state == "OPEN":
            if time.time() - self.last_failure_time > self.recovery_timeout:
                self.state = "HALF_OPEN"
                return True
            return False
        else:  # HALF_OPEN
            return True

    def record_success(self):
        """Record successful request"""
        if self.state == "HALF_OPEN":
            self.state = "CLOSED"
        self.failure_count = 0

    def record_failure(self):
        """Record failed request"""
        self.failure_count += 1
        self.last_failure_time = time.time()

        if self.failure_count >= self.failure_threshold:
            self.state = "OPEN"

class AIOptimizer:
    def __init__(self, config_path: str):
        with open(config_path, 'r') as f:
            self.config = json.load(f)['aiOptimization']

        self.cache = RequestCache(
            max_size=self.config['nvidia_nim']['caching']['cacheSize'],
            ttl_seconds=self.config['nvidia_nim']['caching']['ttlSeconds']
        )

        self.circuit_breaker = CircuitBreaker(
            failure_threshold=self.config['nvidia_nim']['circuitBreaker']['failureThreshold'],
            recovery_timeout=self.config['nvidia_nim']['circuitBreaker']['recoveryTimeoutMs'] // 1000
        )

        self.request_queue = deque()
        self.batch_size = self.config['nvidia_nim']['requestBatching']['maxBatchSize']
        self.batch_interval = self.config['nvidia_nim']['requestBatching']['batchInterval'] / 1000

        self.session = None
        self.processing_stats = {
            'requests_processed': 0,
            'cache_hits': 0,
            'cache_misses': 0,
            'batch_count': 0,
            'average_response_time': 0,
            'circuit_breaker_trips': 0
        }

    async def initialize(self):
        """Initialize the AI optimizer"""
        connector = aiohttp.TCPConnector(
            limit=self.config['nvidia_nim']['connectionPooling']['maxConnections'],
            keepalive_timeout=self.config['nvidia_nim']['connectionPooling']['keepAliveMs'] / 1000
        )

        timeout = aiohttp.ClientTimeout(
            total=self.config['nvidia_nim']['connectionPooling']['connectionTimeoutMs'] / 1000
        )

        self.session = aiohttp.ClientSession(
            connector=connector,
            timeout=timeout
        )

        # Start batch processing
        asyncio.create_task(self._process_batches())

    async def process_request(self, request: AIRequest) -> AIResponse:
        """Process AI request with optimization"""
        start_time = time.time()

        # Check cache first
        cached_response = self.cache.get(request)
        if cached_response:
            self.processing_stats['cache_hits'] += 1
            return AIResponse(
                request_id=request.id,
                response=cached_response,
                processing_time=time.time() - start_time,
                from_cache=True
            )

        self.processing_stats['cache_misses'] += 1

        # Check circuit breaker
        if not self.circuit_breaker.can_proceed():
            # Use fallback response
            fallback_response = self._generate_fallback_response(request)
            return AIResponse(
                request_id=request.id,
                response=fallback_response,
                processing_time=time.time() - start_time,
                from_cache=False
            )

        # Add to batch queue
        future = asyncio.Future()
        self.request_queue.append((request, future))

        # Wait for batch processing
        try:
            response = await future
            self.circuit_breaker.record_success()

            # Cache the response
            self.cache.put(request, response)

            processing_time = time.time() - start_time
            self._update_stats(processing_time)

            return AIResponse(
                request_id=request.id,
                response=response,
                processing_time=processing_time,
                from_cache=False
            )

        except Exception as e:
            self.circuit_breaker.record_failure()
            self.processing_stats['circuit_breaker_trips'] += 1

            # Use fallback response
            fallback_response = self._generate_fallback_response(request)
            return AIResponse(
                request_id=request.id,
                response=fallback_response,
                processing_time=time.time() - start_time,
                from_cache=False
            )

    async def _process_batches(self):
        """Process requests in batches"""
        while True:
            await asyncio.sleep(self.batch_interval)

            if not self.request_queue:
                continue

            # Collect batch
            batch = []
            futures = []

            while len(batch) < self.batch_size and self.request_queue:
                request, future = self.request_queue.popleft()
                batch.append(request)
                futures.append(future)

            if batch:
                await self._process_batch(batch, futures)

    async def _process_batch(self, requests: List[AIRequest], futures: List[asyncio.Future]):
        """Process a batch of AI requests"""
        try:
            # Simulate batch API call to NVIDIA NIM
            # In real implementation, this would make actual API calls
            responses = await self._call_nvidia_nim_batch(requests)

            # Distribute responses to futures
            for i, (request, response) in enumerate(zip(requests, responses)):
                if i < len(futures):
                    futures[i].set_result(response)

            self.processing_stats['batch_count'] += 1

        except Exception as e:
            # Handle batch failure
            for future in futures:
                if not future.done():
                    future.set_exception(e)

    async def _call_nvidia_nim_batch(self, requests: List[AIRequest]) -> List[str]:
        """Make batch API call to NVIDIA NIM (simulated)"""
        # Simulate API call delay
        await asyncio.sleep(0.1)

        # Generate simulated responses
        responses = []
        for request in requests:
            response = f"AI response for: {request.prompt[:50]}..."
            responses.append(response)

        return responses

    def _generate_fallback_response(self, request: AIRequest) -> str:
        """Generate fallback response when AI service is unavailable"""
        return f"Fallback response for: {request.prompt[:50]}..."

    def _update_stats(self, processing_time: float):
        """Update processing statistics"""
        self.processing_stats['requests_processed'] += 1

        # Update rolling average
        current_avg = self.processing_stats['average_response_time']
        count = self.processing_stats['requests_processed']
        new_avg = ((current_avg * (count - 1)) + processing_time) / count
        self.processing_stats['average_response_time'] = new_avg

    def get_performance_stats(self) -> Dict[str, Any]:
        """Get current performance statistics"""
        stats = self.processing_stats.copy()
        stats['cache_hit_rate'] = self.cache.get_hit_rate()
        stats['circuit_breaker_state'] = self.circuit_breaker.state
        return stats

    async def cleanup(self):
        """Cleanup resources"""
        if self.session:
            await self.session.close()

# Example usage
async def main():
    optimizer = AIOptimizer('ai_optimization_config.json')
    await optimizer.initialize()

    # Simulate AI requests
    requests = [
        AIRequest(
            id=f"req_{i}",
            prompt=f"Generate political response for scenario {i}",
            context={"voter_type": "conservative", "issue": "economy"},
            timestamp=time.time()
        )
        for i in range(100)
    ]

    # Process requests
    start_time = time.time()
    responses = []

    for request in requests:
        response = await optimizer.process_request(request)
        responses.append(response)

    total_time = time.time() - start_time

    # Print statistics
    stats = optimizer.get_performance_stats()
    print(f"Processed {len(responses)} requests in {total_time:.2f} seconds")
    print(f"Cache hit rate: {stats['cache_hit_rate']:.2%}")
    print(f"Average response time: {stats['average_response_time']:.3f} seconds")
    print(f"Batches processed: {stats['batch_count']}")

    await optimizer.cleanup()

if __name__ == '__main__':
    asyncio.run(main())
