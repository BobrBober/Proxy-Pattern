using System;
using System.Collections.Generic;
using System.Threading;

public enum UserRole
{
    Admin,
    User,
    Guest
}

public class User
{
    public string Name { get; }
    public UserRole Role { get; }

    public User(string name, UserRole role)
    {
        Name = name;
        Role = role;
    }
}

public interface ISubject
{
    void Request(string request);
}

public class RealSubject : ISubject
{
    public void Request(string request)
    {
        // Основная логика обработки запроса
        Console.WriteLine($"RealSubject: Обрабатываю запрос \"{request}\"...");
    }
}

public class Proxy : ISubject
{
    private readonly RealSubject _realSubject = new RealSubject();
    private readonly Dictionary<string, (string Response, DateTime CacheTime)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(10);
    private readonly Timer _cacheCleanupTimer;

    public User CurrentUser { get; set; }

    public Proxy(User user)
    {
        CurrentUser = user;
        // Инициализация таймера для периодической очистки кэша
        _cacheCleanupTimer = new Timer(CleanupCache, null, _cacheDuration, _cacheDuration);
    }

    public void Request(string request)
    {
        // Проверка прав доступа
        if (!CheckAccess())
        {
            Console.WriteLine($"Proxy: Доступ запрещен для пользователя {CurrentUser.Name} с ролью {CurrentUser.Role}.");
            return;
        }

        // Проверка наличия запроса в кэше
        if (_cache.TryGetValue(request, out var cacheEntry) && (DateTime.Now - cacheEntry.CacheTime) < _cacheDuration)
        {
            Console.WriteLine($"Proxy: Результат из кэша для запроса \"{request}\": {cacheEntry.Response}");
            return;
        }

        // Если в кэше нет записи или она устарела, передаем запрос RealSubject
        Console.WriteLine("Proxy: Кэш отсутствует или устарел. Передаю запрос RealSubject...");
        _realSubject.Request(request);

        // Сохраняем результат в кэше
        _cache[request] = ($"Результат для запроса \"{request}\"", DateTime.Now);
    }

    private bool CheckAccess()
    {
        // Логика проверки прав доступа на основе роли пользователя
        Console.WriteLine($"Proxy: Проверяю доступ для пользователя {CurrentUser.Name} с ролью {CurrentUser.Role}...");
        return CurrentUser.Role == UserRole.Admin || CurrentUser.Role == UserRole.User;
    }

    private void CleanupCache(object state)
    {
        var expiredKeys = new List<string>();
        foreach (var entry in _cache)
        {
            if ((DateTime.Now - entry.Value.CacheTime) >= _cacheDuration)
            {
                expiredKeys.Add(entry.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            _cache.Remove(key);
        }

        Console.WriteLine($"Proxy: Очищено {expiredKeys.Count} устаревших записей из кэша.");
    }
}

class Program
{
    static void Main()
    {
        // Создаем пользователей с разными ролями
        var adminUser = new User("Админ", UserRole.Admin);
        var regularUser = new User("Пользователь", UserRole.User);
        var guestUser = new User("Гость", UserRole.Guest);

        // Создаем прокси для каждого пользователя
        var adminProxy = new Proxy(adminUser);
        var userProxy = new Proxy(regularUser);
        var guestProxy = new Proxy(guestUser);

        // Администратор делает запрос
        adminProxy.Request("Запрос1"); // Доступ разрешен

        // Обычный пользователь делает запрос
        userProxy.Request("Запрос2"); // Доступ разрешен

        // Гость пытается сделать запрос
        guestProxy.Request("Запрос3"); // Доступ запрещен

        // Повторный запрос от администратора, который будет обработан через кэш
        adminProxy.Request("Запрос1"); // Использует кэш

        // Ожидание для устаревания кэша
        Console.WriteLine("Ожидание 11 секунд...");
        Thread.Sleep(11000);

        // Повторный запрос после устаревания кэша
        adminProxy.Request("Запрос1"); // Передается в RealSubject
    }
}
