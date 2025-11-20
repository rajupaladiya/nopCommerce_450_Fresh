# Additional Optimizations Required for 5000 Concurrent Users

This document lists **additional changes and configurations** required beyond the core scalability improvements.

## Critical Code Fixes ✅

### 1. Fixed Blocking Operation in MiniProfiler ✅

**File:** `src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/ServiceCollectionExtensions.cs`

**Issue:** Using `.Result` on async method causes thread pool starvation under high load.

**Fix Applied:** Added proper error handling and ConfigureAwait(false) to minimize deadlock risk.

**Recommendation:** **Disable MiniProfiler in production** for 5000+ concurrent users:
```json
{
  "CommonConfig": {
    "MiniProfilerEnabled": false
  }
}
```

### 2. Memory Cache Size Limits ✅

**File:** `src/Presentation/Nop.Web.Framework/Infrastructure/NopStartup.cs`

**Changes Applied:**
- **SizeLimit**: Set to 10,000 cache entries
- **CompactionPercentage**: 25% (removes 25% of entries when limit reached)

**Impact:** Prevents unbounded memory growth under high load.

### 3. HTTP Client Connection Limits ✅

**File:** `src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/ServiceCollectionExtensions.cs`

**Changes Applied:**
- **DefaultHttpClient**: 100 connections per server, 30s timeout
- **StoreHttpClient**: 50 connections per server, 30s timeout
- **NopHttpClient**: 20 connections per server, 10s timeout
- **CaptchaHttpClient**: 20 connections per server, 10s timeout

**Impact:** Prevents connection exhaustion when making external HTTP calls.

## Required Configuration Changes

### 1. appsettings.json Configuration

Add/update these settings in `appsettings.json`:

```json
{
  "DistributedCacheConfig": {
    "Enabled": true,
    "DistributedCacheType": "Redis",
    "ConnectionString": "your-redis-connection-string-here"
  },
  "CommonConfig": {
    "MiniProfilerEnabled": false,
    "UseSessionStateTempDataProvider": false
  },
  "CacheConfig": {
    "DefaultCacheTime": 60,
    "ShortTermCacheTime": 3,
    "BundledFilesCacheTime": 120
  }
}
```

**Critical:** For 5000+ users, **MUST use Redis or SQL Server distributed cache**, not in-memory cache.

### 2. IIS/web.config Configuration (if using IIS)

If hosting behind IIS, update `web.config`:

```xml
<system.webServer>
  <aspNetCore 
    processPath="dotnet" 
    arguments=".\Nop.Web.dll" 
    stdoutLogEnabled="false" 
    stdoutLogFile=".\logs\stdout"
    hostingModel="InProcess"
    requestTimeout="00:20:00">
    <environmentVariables>
      <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    </environmentVariables>
  </aspNetCore>
  
  <!-- Increase request limits -->
  <security>
    <requestFiltering>
      <requestLimits maxAllowedContentLength="104857600" /> <!-- 100 MB -->
    </requestFiltering>
  </security>
</system.webServer>
```

### 3. Reverse Proxy Configuration (Nginx)

If using Nginx as reverse proxy:

```nginx
upstream nopcommerce {
    least_conn;  # Use least connections load balancing
    server 127.0.0.1:5000 max_fails=3 fail_timeout=30s;
    # Add more servers for load balancing
    # server 127.0.0.1:5001 max_fails=3 fail_timeout=30s;
}

server {
    listen 80;
    server_name your-domain.com;

    # Increase timeouts
    proxy_connect_timeout 60s;
    proxy_send_timeout 60s;
    proxy_read_timeout 60s;
    
    # Increase buffer sizes
    proxy_buffer_size 4k;
    proxy_buffers 8 4k;
    proxy_busy_buffers_size 8k;
    
    # Increase body size
    client_max_body_size 100M;
    
    location / {
        proxy_pass http://nopcommerce;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 4. Redis Configuration

If using Redis for distributed cache:

**Redis Server Settings:**
```conf
# redis.conf
maxmemory 2gb
maxmemory-policy allkeys-lru
tcp-backlog 511
timeout 0
tcp-keepalive 300
```

**Connection String:**
```
your-redis-server:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000,defaultDatabase=0
```

### 5. Database Server Optimizations

**SQL Server:**
```sql
-- Set max server memory (adjust based on total RAM)
EXEC sp_configure 'max server memory (MB)', 8192; -- 8GB example
RECONFIGURE;

-- Enable optimize for ad hoc workloads
EXEC sp_configure 'optimize for ad hoc workloads', 1;
RECONFIGURE;

-- Set max degree of parallelism (usually CPU cores / 2)
EXEC sp_configure 'max degree of parallelism', 4;
RECONFIGURE;

-- Set cost threshold for parallelism
EXEC sp_configure 'cost threshold for parallelism', 50;
RECONFIGURE;
```

**MySQL:**
```sql
-- Increase buffer pool size (adjust based on RAM)
SET GLOBAL innodb_buffer_pool_size = 4294967296; -- 4GB example

-- Increase max connections
SET GLOBAL max_connections = 6000;

-- Optimize for high concurrency
SET GLOBAL innodb_thread_concurrency = 0;
SET GLOBAL innodb_read_io_threads = 8;
SET GLOBAL innodb_write_io_threads = 8;
```

**PostgreSQL:**
```conf
# postgresql.conf
max_connections = 6000
shared_buffers = 2GB
effective_cache_size = 6GB
maintenance_work_mem = 512MB
checkpoint_completion_target = 0.9
wal_buffers = 16MB
default_statistics_target = 100
random_page_cost = 1.1
effective_io_concurrency = 200
work_mem = 10MB
min_wal_size = 1GB
max_wal_size = 4GB
```

## Infrastructure Recommendations

### 1. Load Balancing

For 5000+ concurrent users, use multiple application servers:

- **Minimum:** 2 application servers
- **Recommended:** 3-5 application servers
- **Load Balancer:** Use sticky sessions only if using in-memory session (not recommended)
- **Better:** Use stateless design with distributed cache/session

### 2. CDN for Static Assets

Configure CDN for:
- Images
- CSS files
- JavaScript files
- Fonts

This reduces load on application servers significantly.

### 3. Database Read Replicas

For read-heavy operations:
- Configure read replicas
- Use connection string with `ApplicationIntent=ReadOnly` for read operations
- Distribute read queries across replicas

### 4. Monitoring and Alerting

Set up monitoring for:
- **Application Metrics:**
  - Active connections
  - Thread pool usage
  - Memory usage
  - GC pauses
  - Response times
  - Error rates

- **Database Metrics:**
  - Active connections
  - Query performance
  - Lock waits
  - Buffer pool usage

- **Infrastructure Metrics:**
  - CPU usage
  - Memory usage
  - Disk I/O
  - Network I/O

**Tools:**
- Application Insights
- Prometheus + Grafana
- New Relic
- Datadog

## Application-Level Optimizations

### 1. Disable Unnecessary Features in Production

```json
{
  "CommonConfig": {
    "MiniProfilerEnabled": false,
    "DisplayFullErrorStack": false
  }
}
```

### 2. Optimize Static File Caching

Ensure static files have proper cache headers (already configured):
```json
{
  "CommonConfig": {
    "StaticFilesCacheControl": "public,max-age=31536000"
  }
}
```

### 3. Enable Output Caching (Future Enhancement)

Consider implementing output caching for:
- Product listing pages
- Category pages
- Home page
- Static content pages

## Testing Checklist

Before going live with 5000 concurrent users:

- [ ] Database connection pool configured (5000+)
- [ ] Database server max_connections set (6000+)
- [ ] Redis/SQL Server distributed cache configured
- [ ] Thread pool limits configured
- [ ] Kestrel limits configured
- [ ] Server GC enabled
- [ ] Memory cache size limits set
- [ ] HTTP client limits configured
- [ ] MiniProfiler disabled in production
- [ ] Load testing completed (0-5000 users)
- [ ] Stress testing completed (5000+ users)
- [ ] Monitoring configured
- [ ] Alerting configured
- [ ] CDN configured (if applicable)
- [ ] Load balancer configured (if multiple servers)
- [ ] Database indexes optimized
- [ ] Slow query log reviewed

## Performance Targets

With all optimizations in place, you should achieve:

- **Response Time:** < 2 seconds for 95% of requests
- **Error Rate:** < 0.1%
- **CPU Usage:** < 70% average
- **Memory Usage:** Stable (no leaks)
- **Database Connections:** < 80% of pool
- **Thread Pool:** < 80% utilization
- **Cache Hit Rate:** > 80%

## Troubleshooting

### If Still Experiencing Issues:

1. **Connection Pool Exhausted:**
   - Check database server max_connections
   - Verify connection string has MaxPoolSize=5000
   - Check for connection leaks (not disposing connections)

2. **Thread Pool Starvation:**
   - Check for blocking operations (.Result, .Wait())
   - Verify thread pool limits are set correctly
   - Monitor thread pool usage

3. **Memory Issues:**
   - Enable distributed cache (Redis/SQL Server)
   - Check for memory leaks
   - Review cache size limits
   - Monitor GC performance

4. **Slow Response Times:**
   - Check database query performance
   - Review slow query log
   - Optimize database indexes
   - Check for N+1 query problems
   - Enable distributed cache

5. **High CPU Usage:**
   - Check for inefficient queries
   - Review application logs for errors
   - Check for tight loops
   - Monitor GC performance

## Support Resources

- nopCommerce Documentation: https://docs.nopcommerce.com
- nopCommerce Forums: https://www.nopcommerce.com/boards
- Performance Tuning Guide: See SCALABILITY_IMPROVEMENTS_5000_USERS.md

---

**Status:** Additional optimizations documented
**Last Updated:** $(Get-Date -Format "yyyy-MM-dd")

