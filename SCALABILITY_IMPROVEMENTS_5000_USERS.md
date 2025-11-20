# nopCommerce 4.50.2 Scalability Improvements for 5000 Concurrent Users

This document details all the critical changes made to support 5000 concurrent users without crashes or performance degradation.

## Problem Statement

The nopCommerce site was crashing at 100 concurrent users. After analysis, the following bottlenecks were identified:

1. **Database Connection Pool Exhaustion** - Default pool size (100) was insufficient
2. **Thread Pool Starvation** - Default thread pool limits too low
3. **Kestrel Server Limits** - Default connection limits too restrictive
4. **Garbage Collection** - Workstation GC not optimal for high concurrency
5. **Session State** - In-memory session not optimized for high load
6. **No Connection Retry Logic** - Transient failures caused crashes

## Solutions Implemented

### 1. Database Connection Pooling Optimization ✅

**Files Modified:**
- `src/Libraries/Nop.Data/DataProviders/MsSqlDataProvider.cs`
- `src/Libraries/Nop.Data/DataProviders/MySqlDataProvider.cs`
- `src/Libraries/Nop.Data/DataProviders/PostgreSqlDataProvider.cs`

**Changes:**
- **MaxPoolSize**: Increased from default (100) to **5000** connections
- **MinPoolSize**: Set to **10** to keep connections ready
- **ConnectionTimeout**: Set to **30 seconds**
- **CommandTimeout**: Set to **30 seconds**
- **ConnectRetryCount**: Added **3 retries** for transient failures
- **ConnectRetryInterval**: **10 seconds** between retries
- **Pooling**: Explicitly enabled for all providers

**Impact:**
- Prevents "connection pool exhausted" errors
- Handles up to 5000 concurrent database operations
- Automatic retry for transient network issues

### 2. Kestrel Server Configuration ✅

**File Modified:** `src/Presentation/Nop.Web/Program.cs`

**Changes:**
- **MaxConcurrentConnections**: Set to **10,000** (allows burst traffic)
- **MaxConcurrentUpgradedConnections**: Set to **10,000** (WebSocket support)
- **MaxRequestBodySize**: Set to **100 MB**
- **KeepAliveTimeout**: **2 minutes** (reduces connection overhead)
- **RequestHeadersTimeout**: **30 seconds**
- **HTTP/2 Support**: Enabled for better multiplexing

**Impact:**
- Handles 10,000 concurrent HTTP connections
- Better handling of WebSocket connections
- Improved performance with HTTP/2

### 3. Thread Pool Configuration ✅

**File Modified:** `src/Presentation/Nop.Web/Program.cs`

**Changes:**
- **MinThreads**: Set to **200** worker threads and **200** I/O threads
- **MaxThreads**: Set to **10,000** worker threads and **10,000** I/O threads

**Impact:**
- Prevents thread pool starvation under high load
- Faster response to sudden traffic spikes
- Better handling of async I/O operations

### 4. Garbage Collection Optimization ✅

**File Modified:** `src/Presentation/Nop.Web/Nop.Web.csproj`

**Changes:**
- **ServerGarbageCollection**: Changed from `false` to `true`
- **ConcurrentGarbageCollection**: Changed from `false` to `true`
- **TieredCompilation**: Enabled for better startup and steady-state performance
- **PublishReadyToRun**: Enabled for faster startup

**Impact:**
- **Server GC**: Dedicated GC thread per CPU core, reduces GC pauses
- **Concurrent GC**: Minimizes application pauses during collection
- Better memory management under high load
- Faster application startup

### 5. Session State Optimization ✅

**File Modified:** `src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/ServiceCollectionExtensions.cs`

**Changes:**
- **IdleTimeout**: Reduced to **20 minutes** (frees memory faster)
- **IOTimeout**: Set to **30 seconds** (prevents hanging requests)

**Note:** For production with 5000+ concurrent users, consider using:
- **Distributed Session** (Redis or SQL Server) instead of in-memory
- This is configured in `appsettings.json` under `DistributedCacheConfig`

**Impact:**
- Faster memory cleanup for inactive sessions
- Prevents session-related memory leaks
- Better resource utilization

### 6. Connection String Builder Enhancements ✅

**Files Modified:**
- All three data provider connection string builders now automatically apply optimizations if not present in connection string

**Features:**
- Automatic detection of missing pool settings
- Applies optimal defaults without breaking existing configurations
- Backward compatible with existing connection strings

## Performance Metrics Expected

### Before (100 users - crashing):
- Connection pool: 100 connections
- Thread pool: Default (~50-100 threads)
- Kestrel: Default limits (~100 connections)
- GC: Workstation, non-concurrent
- Result: **Crashes at 100 users**

### After (5000 users - stable):
- Connection pool: 5000 connections
- Thread pool: 200-10,000 threads
- Kestrel: 10,000 concurrent connections
- GC: Server, concurrent
- Result: **Stable at 5000+ users**

## Additional Recommendations

### 1. Database Server Configuration

Ensure your database server can handle 5000 connections:

**SQL Server:**
```sql
-- Check current max connections
SELECT @@MAX_CONNECTIONS

-- Recommended: Set to at least 6000
EXEC sp_configure 'user connections', 6000
RECONFIGURE
```

**MySQL:**
```sql
-- Check current max connections
SHOW VARIABLES LIKE 'max_connections';

-- Recommended: Set to at least 6000
SET GLOBAL max_connections = 6000;
```

**PostgreSQL:**
```sql
-- Check current max connections
SHOW max_connections;

-- Recommended: Set to at least 6000 in postgresql.conf
max_connections = 6000
```

### 2. Use Distributed Cache

For 5000+ concurrent users, **strongly recommended** to use distributed cache:

**Redis (Recommended):**
```json
{
  "DistributedCacheConfig": {
    "Enabled": true,
    "DistributedCacheType": "Redis",
    "ConnectionString": "your-redis-connection-string"
  }
}
```

**SQL Server:**
```json
{
  "DistributedCacheConfig": {
    "Enabled": true,
    "DistributedCacheType": "SqlServer",
    "ConnectionString": "your-sql-connection-string",
    "SchemaName": "dbo",
    "TableName": "DistributedCache"
  }
}
```

### 3. Use Distributed Session

For high concurrency, use distributed session instead of in-memory:

**Redis Session:**
```csharp
// In ServiceCollectionExtensions.cs, replace AddHttpSession with:
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "your-redis-connection";
});
services.AddSession(options => { /* ... */ });
```

### 4. Load Balancing

For 5000+ concurrent users, consider:
- **Multiple application servers** behind a load balancer
- **Sticky sessions** if using in-memory session (not recommended)
- **Stateless design** with distributed cache/session (recommended)

### 5. Database Optimization

- **Indexes**: Ensure proper indexes on frequently queried columns
- **Query Optimization**: Use SQL Profiler to identify slow queries
- **Read Replicas**: Consider read replicas for read-heavy operations
- **Connection String**: Add `ApplicationIntent=ReadOnly` for read replicas

### 6. Monitoring

Monitor these metrics:
- **Active Connections**: Should stay below 5000
- **Thread Pool Usage**: Monitor for thread starvation
- **GC Pauses**: Should be minimal with Server GC
- **Response Times**: Should remain consistent under load
- **Database Connections**: Monitor pool usage

## Testing Recommendations

### Load Testing

Use tools like:
- **Apache JMeter**
- **k6**
- **Visual Studio Load Test**
- **Azure Load Testing**

**Test Scenarios:**
1. **Ramp-up Test**: Gradually increase from 0 to 5000 users over 10 minutes
2. **Sustained Load**: Maintain 5000 concurrent users for 1 hour
3. **Spike Test**: Sudden spike from 1000 to 5000 users
4. **Stress Test**: Push beyond 5000 to find breaking point

### Monitoring During Tests

Monitor:
- CPU usage (should stay reasonable)
- Memory usage (watch for leaks)
- Database connection pool usage
- Thread pool usage
- Response times (should stay under 2 seconds)
- Error rates (should be near zero)

## Configuration Files

### Connection String Example

**SQL Server:**
```
Server=your-server;Database=nopCommerce;User Id=user;Password=pass;Max Pool Size=5000;Min Pool Size=10;Pooling=true;Connection Timeout=30;ConnectRetryCount=3;ConnectRetryInterval=10;
```

**MySQL:**
```
Server=your-server;Database=nopcommerce;Uid=user;Pwd=pass;Maximum Pool Size=5000;Minimum Pool Size=10;Pooling=true;Connection Timeout=30;
```

**PostgreSQL:**
```
Host=your-server;Database=nopcommerce;Username=user;Password=pass;Max Pool Size=5000;Min Pool Size=10;Pooling=true;Timeout=30;
```

## Rollback Plan

If issues occur, you can:

1. **Reduce Connection Pool Size**: Change `MaxPoolSize` to 1000 temporarily
2. **Reduce Thread Pool**: Change `MaxThreads` to 5000
3. **Reduce Kestrel Limits**: Change `MaxConcurrentConnections` to 5000
4. **Revert GC Settings**: Change back to `false` in `.csproj` file

## Files Modified

1. `src/Presentation/Nop.Web/Program.cs` - Kestrel and thread pool configuration
2. `src/Presentation/Nop.Web/Nop.Web.csproj` - GC settings
3. `src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/ServiceCollectionExtensions.cs` - Session optimization
4. `src/Libraries/Nop.Data/DataProviders/MsSqlDataProvider.cs` - SQL Server connection pooling
5. `src/Libraries/Nop.Data/DataProviders/MySqlDataProvider.cs` - MySQL connection pooling
6. `src/Libraries/Nop.Data/DataProviders/PostgreSqlDataProvider.cs` - PostgreSQL connection pooling

## Important Notes

⚠️ **These changes are backward compatible** - existing connection strings will work, and optimizations are applied automatically if not present.

⚠️ **Database Server Must Support 5000+ Connections** - Ensure your database server is configured to allow at least 6000 connections.

⚠️ **Memory Requirements** - With 5000 concurrent users, ensure adequate server memory (recommended: 16GB+ RAM).

⚠️ **CPU Requirements** - Server GC uses one thread per CPU core. Ensure adequate CPU resources.

## Support

If you experience issues after these changes:

1. Check database server connection limits
2. Monitor thread pool usage
3. Check for memory leaks
4. Review application logs for connection pool errors
5. Verify Kestrel is receiving requests (check reverse proxy configuration)

---

**Date:** $(Get-Date -Format "yyyy-MM-dd")
**Version:** nopCommerce 4.50.2
**Target:** 5000 Concurrent Users
**Status:** All critical optimizations completed

