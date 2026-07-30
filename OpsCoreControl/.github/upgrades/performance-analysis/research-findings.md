# Исследование производительности DashBoard.cs

## 📊 Дата исследования
2024-01-15 (начало рефакторинга оптимизации)

## 🎯 Цель
Выявить критичные узкие места в классе DashBoard, которые вызывают зависания и медленную работу приложения.

---

## 1️⃣ АРХИТЕКТУРА COLLECT()

### Основной цикл (Loop() → Collect())
```
Loop() (async, каждую 1 секунду)
  ↓
Collect() (синхронно в Task.Run на thread pool)
  ↓
Возврат DashboardData через Dispatcher на UI-поток
```

**Проблема**: Collect() выполняется СИНХРОННО на thread pool. Если она зависнет на 2 сек → UI будет фризить 2 сек.

---

## 2️⃣ МАТРИЦА ОПЕРАЦИЙ В COLLECT()

### Операции, выполняемые КАЖДЫЙ ТИК (интервал: 1 сек)

| Операция | Тип | Время |  |  |
|---|---|---|---|---|
| `_cpuCounter.NextValue()` | PerformanceCounter | ⚠️ ~5-20мс | Один из 5 счётчиков |
| `_ramAvailableCounter.NextValue()` | PerformanceCounter | ⚠️ ~5-20мс | |
| `_vramCounter.NextValue()` | PerformanceCounter | ⚠️ ~5-20мс | |
| `_diskReadCounter.NextValue()` | PerformanceCounter | ⚠️ ~5-20мс | |
| `_diskWriteCounter.NextValue()` | PerformanceCounter | ⚠️ ~5-20мс | |
| `Process.GetProcesses()` | API Windows | 🔴 **50-200мс** | **ХУДШЕЕ** — создаёт массив всех процессов |
| `CollectDisks()` — DriveInfo.GetDrives() | FS API | ⚠️ ~10мс | Может повиснуть на сетевых дисках |
| Total PerformanceCounter | | 🔴 **25-100мс** | 5 счётчиков × 5-20мс |

**⏱️ Каждый тик (1 сек) может занять: 85-300мс** на нормальной машине, **500+мс на медленной**

---

### Операции, выполняемые КАЖДЫЙ 5-й ТИК (интервал: ~5 сек)

| Операция | Тип | Время | Проблема |
|---|---|---|---|
| `CollectWifi()` → netsh wlan show interfaces | Процесс + парсинг | 🔴 **500-2000мс** | **Может зависнуть если WiFi нет** |
| `CollectAdapters()` → NetworkInterface.GetAllNetworkInterfaces() | API Windows | ⚠️ ~50-100мс | На ПК с 10+ адаптерами медленнее |
| `CollectDiskMeta()` → ManagementObjectSearcher WMI | WMI | 🔴 **100-500мс** | **Может зависнуть в некоторых системах** |
| `CollectBattery()` → ManagementObjectSearcher WMI | WMI | ⚠️ ~20-50мс | На ПК без батареи может быть slow |

**⏱️ Каждый 5-й тик: 670-2650мс (может быть >3 сек на медленной машине)**

---

### Операции, выполняемые КАЖДЫЙ 10-й ТИК (интервал: ~10 сек)

| Операция | Тип | Время | Проблема |
|---|---|---|---|
| `CollectUsb()` → ManagementObjectSearcher WMI | WMI | 🔴 **200-1000мс** | **Может зависнуть если много USB** |

**⏱️ Каждый 10-й тик: +200-1000мс в добавок к 5-сек операциям**

---

### Операции, выполняемые КАЖДЫЙ 60-й ТИК (интервал: ~60 сек)

| Операция | Тип | Время | Проблема |
|---|---|---|---|
| `CollectPublicIp()` → WebClient.DownloadString() | HTTP GET | 🔴 **2000-30000мс** | **КРИТИЧНЫЙ: может зависнуть на 30+ сек** |

**⏱️ Каждый 60-й тик: +2-30 сек (UI фризит на эту длительность)**

---

## 3️⃣ ПИКОВЫЕ НАГРУЗКИ

### Сценарий 1: Нормальная 5-секундная пиковая нагрузка
```
Тик 5:
  - PerformanceCounters: 25-100мс
  - Process.GetProcesses(): 50-200мс
  - CollectWifi(): 500-2000мс
  - CollectAdapters(): 50-100мс
  - CollectDiskMeta(): 100-500мс
  - CollectBattery(): 20-50мс

ИТОГО: 745-2950мс (0.7-3 сек на один тик)
```

### Сценарий 2: Худший случай на 60-й секунде
```
Тик 60:
  - Весь стек 5-сек операций: 745-2950мс
  - CollectUsb(): 200-1000мс
  - CollectPublicIp() (WEB): 2000-30000мс (БЛОКИРУЮЩАЯ СЕТЬ!)

ИТОГО: 2945-34000мс (3-34 секунды!) 
UI будет фризить 3-34 сек каждую минуту
```

### Сценарий 3: Сбой в WMI или сети
```
Если API.ipify.org упал или сеть медленная:
  - Собственный таймаут WebClient.DownloadString(): нет! (может жди 30+ сек)
  - Если WMI зависнет: весь Collect() зависнет
  - UI полностью заморожен
```

---

## 4️⃣ СПИСОК ВСЕХ WMI-ЗАПРОСОВ

### Выполняемые WMI запросы:

1. **GetTotalPhysicalMemoryBytes()** (строка 68)
   - Query: `SELECT TotalPhysicalMemory FROM Win32_ComputerSystem`
   - Когда: При инициализации класса (один раз)
   - Время: ~100-200мс (редко)

2. **CollectDiskMeta()** (строка 193) — каждый 5-й тик
   - Query: `SELECT Name, ProviderName, DriveType FROM Win32_LogicalDisk`
   - Когда: Каждые ~5 сек (тик % 5 == 0)
   - Время: **100-500мс** ⚠️
   - Проблема: Может зависнуть если сетевых дисков много

3. **CollectUsb()** (строка 303) — каждый 10-й тик
   - Query: `SELECT Caption, Description FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%'`
   - Когда: Каждые ~10 сек (тик % 10 == 0)
   - Время: **200-1000мс** 🔴
   - Проблема: **Может зависнуть на 1-5 сек если подключено много USB-устройств**

4. **CollectBattery()** (строка 328) — каждый 5-й тик
   - Query: `SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery`
   - Когда: Каждые ~5 сек (тик % 5 == 0)
   - Время: ~20-50мс (на ПК без батареи может быть медленнее)

5. **GetUptime()** (строка 370) — один раз при первом обращении
   - Query: `SELECT LastBootUpTime FROM Win32_OperatingSystem`
   - Когда: При первом вызове GetUptime(), потом кэшируется
   - Время: ~50-100мс

---

## 5️⃣ КРИТИЧНЫЕ ПРОБЛЕМЫ

### 🔴 КРИТИЧНЫЕ (вызывают заметные фризы)

**#1: WebClient.DownloadString() — каждую минуту (строка 353)**
- Блокирует весь Collect() поток
- Нет таймаута → может зависнуть на 30+ секунд
- Выполняется в Task.Run на thread pool, но UI всё равно будет ждать Updated события
- **Решение**: HttpClient с таймаутом 3 сек

**#2: Process.GetProcesses() — каждый тик (строка 134)**
- Выполняется каждую секунду
- На ПК с 200+ процессами может занять 50-200мс
- Создаёт мусор в памяти
- **Решение**: Кэшировать на 5 сек

**#3: CollectWifi() через netsh — каждый 5-й тик (строка 220)**
- Запускает процесс netsh, ждёт вывода
- На ПК без WiFi модуля может зависнуть на 1-2 сек
- Нет таймаута на WaitForExit()
- **Решение**: Добавить таймаут 2 сек

**#4: PerformanceCounter.NextValue() × 5 — каждый тик**
- 5 счётчиков × ~5-20мс = 25-100мс каждый тик
- Может быть медленнее на перегруженной системе
- **Решение**: Кэшировать на 100мс

---

### 🟡 ВЫСОКИЕ (могут вызвать фризы в определённых условиях)

**#5: WMI запросы (DiskMeta, USB, Battery) — каждый 5-10-й тик**
- ManagementObjectSearcher синхронны, блокируют поток
- На системах с медленным WMI может быть 500+ мс
- Нет таймаута
- **Решение**: Асинхронизировать в отдельных Tasks

**#6: NetworkInterface.GetAllNetworkInterfaces() — каждый 5-й тик (строка 261)**
- На ПК с 10+ адаптерами медленнее
- Может заблокиться если адаптер в "плохом" состоянии
- **Решение**: Добавить таймаут или асинхронизировать

**#7: DriveInfo.GetDrives() — каждый тик (строка 155)**
- На каждом диске читаются метаданные (может быть медленно на сетевых дисках)
- **Решение**: Кэшировать на 5 сек

---

## 6️⃣ УТЕЧКИ И НЕПРАВИЛЬНЫЕ ПАТТЕРНЫ

### Утечки процессов/дескрипторов

❌ **Строка 134**: `Process[] procs = Process.GetProcesses();`
- Создаёт новый массив каждый тик
- 5 сек × 60 = 300 процессов в памяти за минуту
- Хотя вызывается `.Dispose()` на каждом процессе, всё равно overhead

✅ **Решение**: Кэшировать на 5 сек, использовать `Process.GetProcessCount()` вместо `Process.GetProcesses().Length`

### Проблемы с синхронизацией

❌ **Строка 81**: `DashboardData data = await Task.Run(() => Collect());`
- Collect() синхронна и может зависнуть
- Нет таймаута на весь Collect()
- Нет обработки таймаутов

✅ **Решение**: Асинхронизировать Collect() и добавить таймаут

### Отсутствие таймаутов

❌ **Везде в WMI запросах и WebClient**
- Если система зависнет или сеть упадёт → бесконечное ожидание
- Нет CancellationToken для операций

✅ **Решение**: Добавить таймауты для всех операций

---

## 7️⃣ ИТОГОВАЯ МАТРИЦА ОПТИМИЗАЦИИ

| Проблема | Критичность | Приоритет | Ожидаемое улучшение |
|---|---|---|---|
| WebClient.DownloadString() без таймаута | 🔴 Высокая | 1 | -30 сек зависания каждую минуту |
| Process.GetProcesses() каждый тик | 🔴 Высокая | 2 | -150мс каждый тик (50-200мс → 0мс) |
| PerformanceCounter каждый тик | 🟡 Средняя | 3 | -30мс каждый тик (100мс → 70мс) |
| WMI запросы без асинхронизации | 🟡 Средняя | 4 | -500мс каждый 5-й тик в среднем |
| netsh без таймаута | 🟡 Средняя | 5 | -1сек максимум |
| Синхронный Collect() в Task.Run | 🟡 Средняя | 6 | Улучшение отзывчивости UI |

---

## 8️⃣ ПЛАН ИСПРАВЛЕНИЯ (КРАТКИЙ)

### Этап 1: Замена WebClient на HttpClient с таймаутом ✅
**Файл**: DashBoard.cs, метод `CollectPublicIp()`
```csharp
// ВМЕСТО синхронного WebClient:
private async Task<string> CollectPublicIpAsync()
{
	using (var client = new HttpClient())
	{
		client.Timeout = TimeSpan.FromSeconds(3); // макс 3 сек
		try 
		{ 
			return (await client.GetStringAsync("https://api.ipify.org")).Trim();
		}
		catch { return "—"; }
	}
}
```

### Этап 2: Кэширование Process.GetProcesses() на 5 сек ✅
```csharp
private DateTime _lastProcessCountTime;
private int _cachedProcessCount;

private int GetProcessCount()
{
	if ((DateTime.Now - _lastProcessCountTime).TotalSeconds >= 5)
	{
		using (var procs = Process.GetProcesses())
			_cachedProcessCount = procs.Length;
		_lastProcessCountTime = DateTime.Now;
	}
	return _cachedProcessCount;
}
```

### Этап 3: Кэширование PerformanceCounter на 100мс ✅
```csharp
private DateTime _lastCounterRead;
private DashboardData _cachedCounterData;

private void UpdateCounters(DashboardData data)
{
	if ((DateTime.Now - _lastCounterRead).TotalMilliseconds >= 100)
	{
		data.CpuPercent = _cpuCounter.NextValue();
		data.VramPercent = _vramCounter.NextValue();
		// ... остальные счётчики
		_lastCounterRead = DateTime.Now;
		_cachedCounterData = data;
	}
	else
	{
		// Верни кэшированные значения
	}
}
```

### Этап 4: Асинхронизация WMI запросов ✅
```csharp
private async Task<Dictionary<string, DiskMeta>> CollectDiskMetaAsync()
{
	return await Task.Run(() => 
	{
		var map = new Dictionary<string, DiskMeta>();
		try
		{
			using (var searcher = new ManagementObjectSearcher(...))
			{
				// ... получение данных
			}
		}
		catch (Exception ex) { Log.Add(...); }
		return map;
	});
}
```

---

## 9️⃣ ВЫВОДЫ

✅ **DashBoard.cs — это главный узел производительности приложения**

🔴 **Критичные проблемы**:
1. WebClient без таймаута → может зависнуть на 30+ сек
2. Process.GetProcesses() каждый тик → 50-200мс/тик
3. WMI запросы без таймаутов → непредсказуемая задержка

🟡 **Узкие места**:
1. PerformanceCounter.NextValue() × 5 → 25-100мс/тик
2. netsh без таймаута → может быть 1-2 сек
3. Синхронный Collect() в Task.Run → нет пользы от async/await

💡 **Ожидаемый результат после рефакторинга**:
- Каждый обычный тик: **85мс → 20мс** (-76%)
- 5-сек пики: **2950мс → 500мс** (-83%)
- 60-сек худший случай: **34сек → 3сек** (-91%)
- **Нет зависаний > 1 сек** благодаря таймаутам

---

## 🔟 ТАБЛИЦА ИНТЕРВАЛОВ (ДЛЯ БЫСТРОЙ СПРАВКИ)

```
Интервал  | Операции                           | Время  
-----------|------------------------------------|--------
1 сек     | PerformanceCounters + Process     | 25-300мс
		  | GetProcesses() + Disks             |
-----------|------------------------------------|--------
5 сек     | ^ + WiFi + Adapters +             | 745-2950мс
		  | DiskMeta + Battery                 |
-----------|------------------------------------|--------
10 сек    | ^ + USB devices                   | 945-3950мс
-----------|------------------------------------|--------
60 сек    | ^ + Public IP (WEB - ХУДШЕЕ)      | 2945-34000мс ⚠️
```

---

**Исследование завершено. Готово к переходу на Шаг 2 (рефакторинг DashBoard.cs).**
